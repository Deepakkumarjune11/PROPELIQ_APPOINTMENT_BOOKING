using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using PatientAccess.Application.DTOs;
using PatientAccess.Application.Services;
using PatientAccess.Data.Entities;
using PatientAccess.Domain.Enums;

namespace PatientAccess.Data.Services;

/// <summary>
/// Authenticates Staff and Admin principals against <see cref="PropelIQDbContext"/>,
/// issues 15-minute JWTs (TR-010), and manages token blacklisting via Redis (FR-017).
/// All failure paths return the same <c>null</c> result to prevent user enumeration (OWASP A07).
/// </summary>
public sealed class AuthService(
    PropelIQDbContext db,
    IConfiguration config,
    IDistributedCache cache,
    IPasswordHasher<Staff> staffHasher,
    IPasswordHasher<Admin> adminHasher,
    IPasswordHasher<Patient> patientHasher,
    ILogger<AuthService> logger) : IAuthService
{
    private static readonly JwtSecurityTokenHandler TokenHandler = new();

    /// <inheritdoc/>
    public async Task<AuthResponse?> LoginAsync(LoginRequest req, CancellationToken ct)
    {
        var (userId, role, passwordHash) = await ResolveCredentialAsync(req.Email, ct);

        if (userId == Guid.Empty)
        {
            // OWASP A07 — do not distinguish user-not-found from wrong-password in the response.
            // Audit with System actor because the user identity is unknown.
            await WriteAuditAsync(AuditActionType.UserLogin, Guid.Empty, AuditActorType.System,
                Guid.Empty, "LoginFailed", ct);
            return null;
        }

        var verifyResult = VerifyPasswordHash(role, passwordHash, req.Password);
        if (verifyResult == PasswordVerificationResult.Failed)
        {
            await WriteAuditAsync(AuditActionType.UserLogin, userId, ActorTypeFromRole(role),
                userId, "LoginFailed", ct);
            return null;
        }

        var token = GenerateJwt(userId, role, req.Email);
        await WriteAuditAsync(AuditActionType.UserLogin, userId, ActorTypeFromRole(role),
            userId, "LoginSuccess", ct);

        return new AuthResponse(
            token,
            userId,
            req.Email,
            role,
            DateTimeOffset.UtcNow.AddMinutes(15).ToUnixTimeMilliseconds());
    }

    /// <inheritdoc/>
    public async Task<AuthResponse?> RefreshAsync(string currentToken, CancellationToken ct)
    {
        var principal = ValidateToken(currentToken, allowExpiredWithinGrace: true);
        if (principal is null) return null;

        var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var role   = principal.FindFirstValue(ClaimTypes.Role)!;
        var email  = principal.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

        // Atomic blacklist — prevents token reuse on concurrent refresh calls (SET NX semantics).
        var remaining = GetTokenRemainingSeconds(currentToken);
        await cache.SetStringAsync(
            $"blacklist:{currentToken}",
            "1",
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(Math.Max(remaining, 60))
            },
            ct);

        var newToken = GenerateJwt(userId, role, email);
        return new AuthResponse(
            newToken,
            userId,
            email,
            role,
            DateTimeOffset.UtcNow.AddMinutes(15).ToUnixTimeMilliseconds());
    }

    /// <inheritdoc/>
    public async Task LogoutAsync(string token, CancellationToken ct)
    {
        var principal = ValidateToken(token, allowExpiredWithinGrace: false);
        var userId    = principal is not null
            ? Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!)
            : Guid.Empty;
        var role = principal?.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

        try
        {
            var remaining = GetTokenRemainingSeconds(token);
            if (remaining > 0)
                await cache.SetStringAsync(
                    $"blacklist:{token}",
                    "1",
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(remaining)
                    },
                    ct);
        }
        catch (Exception ex)
        {
            // Fail-open for availability: Redis unavailable must not block logout.
            // Security is maintained by the 15-minute JWT TTL (NFR-005, FR-017).
            logger.LogWarning(ex, "Token blacklist failed — Redis unavailable");
        }

        if (userId != Guid.Empty)
            await WriteAuditAsync(AuditActionType.UserLogout, userId, ActorTypeFromRole(role),
                userId, null, ct);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Resolves credentials by querying Staff (by Username), Admin (by Username), then Patient (by Email).
    /// Returns <c>(Guid.Empty, "", "")</c> when no matching active principal is found.
    /// </summary>
    private async Task<(Guid UserId, string Role, string PasswordHash)> ResolveCredentialAsync(
        string identifier, CancellationToken ct)
    {
        // 1. Staff — identifier maps to Username
        var staff = await db.Staff
            .FirstOrDefaultAsync(s => s.Username == identifier && s.IsActive, ct);
        if (staff is not null)
            return (staff.Id, staff.Role.ToString(), staff.AuthCredentials);

        // 2. Admin — identifier maps to Username
        var admin = await db.Admins
            .FirstOrDefaultAsync(a => a.Username == identifier && a.IsActive, ct);
        if (admin is not null)
            return (admin.Id, "Admin", admin.AuthCredentials);

        // 3. Patient — identifier maps to Email; must have a password set
        var patient = await db.Patients
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Email == identifier && !p.IsDeleted && p.AuthCredentials != null, ct);
        if (patient is not null)
            return (patient.Id, "Patient", patient.AuthCredentials!);

        return (Guid.Empty, string.Empty, string.Empty);
    }

    private PasswordVerificationResult VerifyPasswordHash(
        string role, string hash, string providedPassword)
    {
        if (role == "Admin")
            return adminHasher.VerifyHashedPassword(new Admin(), hash, providedPassword);

        if (role == "Patient")
            return patientHasher.VerifyHashedPassword(new Patient(), hash, providedPassword);

        // All StaffRole enum values (FrontDesk, CallCenter, ClinicalReviewer) share one hasher.
        return staffHasher.VerifyHashedPassword(new Staff(), hash, providedPassword);
    }

    private string GenerateJwt(Guid userId, string role, string email)
    {
        var keyBytes = Encoding.UTF8.GetBytes(config["Jwt:Key"]!);
        var creds    = new SigningCredentials(
            new SymmetricSecurityKey(keyBytes),
            SecurityAlgorithms.HmacSha256);

        // Normalize to coarse-grained claim: "Admin" stays "Admin"; "Patient" stays "Patient";
        // FrontDesk / CallCenter / ClinicalReviewer all become "Staff"
        // so [Authorize(Roles = "Staff")] on StaffController passes correctly.
        var roleClaim = role switch
        {
            "Admin"   => "Admin",
            "Patient" => "Patient",
            _         => "Staff",
        };

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role,           roleClaim),
            new Claim("staff_role",              role),   // fine-grained role for future use
            new Claim(ClaimTypes.Email,          email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer:             config["Jwt:Issuer"],
            audience:           config["Jwt:Audience"],
            claims:             claims,
            notBefore:          DateTime.UtcNow,
            expires:            DateTime.UtcNow.AddMinutes(15),
            signingCredentials: creds);

        return TokenHandler.WriteToken(token);
    }

    private ClaimsPrincipal? ValidateToken(string token, bool allowExpiredWithinGrace)
    {
        var keyBytes  = Encoding.UTF8.GetBytes(config["Jwt:Key"]!);
        var clockSkew = allowExpiredWithinGrace ? TimeSpan.FromSeconds(30) : TimeSpan.Zero;

        var parameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new SymmetricSecurityKey(keyBytes),
            ValidateIssuer           = true,
            ValidIssuer              = config["Jwt:Issuer"],
            ValidateAudience         = true,
            ValidAudience            = config["Jwt:Audience"],
            ValidateLifetime         = true,
            ClockSkew                = clockSkew,
        };

        try
        {
            return TokenHandler.ValidateToken(token, parameters, out _);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Token validation failed");
            return null;
        }
    }

    private static int GetTokenRemainingSeconds(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(token)) return 0;
        var jwt       = handler.ReadJwtToken(token);
        var remaining = (int)(jwt.ValidTo - DateTime.UtcNow).TotalSeconds;
        return remaining > 0 ? remaining : 0;
    }

    private async Task WriteAuditAsync(
        AuditActionType actionType,
        Guid actorId,
        AuditActorType actorType,
        Guid targetId,
        string? details,
        CancellationToken ct)
    {
        // Details column is jsonb — wrap plain strings as a JSON object so Postgres accepts them.
        var jsonDetails = details is null
            ? null
            : System.Text.Json.JsonSerializer.Serialize(new { action = details });

        db.AuditLogs.Add(new AuditLog
        {
            Id             = Guid.NewGuid(),
            ActorId        = actorId,
            ActorType      = actorType,
            ActionType     = actionType,
            TargetEntityId = targetId,
            Details        = jsonDetails,
            OccurredAt     = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
    }

    private static AuditActorType ActorTypeFromRole(string role) => role switch
    {
        "Admin"   => AuditActorType.Admin,
        "Patient" => AuditActorType.Patient,
        _         => AuditActorType.Staff,
    };
}

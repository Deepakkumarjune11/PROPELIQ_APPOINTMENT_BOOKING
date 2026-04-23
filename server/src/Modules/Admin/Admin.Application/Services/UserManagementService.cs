using System.Security.Cryptography;
using System.Text;
using Admin.Application.DTOs;
using Admin.Application.Exceptions;
using Admin.Application.Validators;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PatientAccess.Data;
using PatientAccess.Data.Entities;
using PatientAccess.Domain.Enums;
using AdminEntity = PatientAccess.Data.Entities.Admin;
using PatientEntity = PatientAccess.Data.Entities.Patient;

namespace Admin.Application.Services;

/// <summary>
/// Staff and Admin lifecycle management.
/// Uses <see cref="PropelIQDbContext"/> directly since Admin has no separate data layer —
/// Staff and Admin entities are owned by the PatientAccess bounded context (DR-009, DR-010).
/// Passwords are never returned in any DTO (OWASP A02).
/// </summary>
public sealed class UserManagementService(
    PropelIQDbContext                 db,
    IPasswordHasher<Staff>            staffHasher,
    IPasswordHasher<AdminEntity>      adminHasher,
    IPasswordHasher<PatientEntity>    patientHasher,
    ISessionInvalidator               sessionInvalidator,
    ILogger<UserManagementService>    logger) : IUserManagementService
{
    // ── Public API ───────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<AdminUserDto>> GetAllUsersAsync(CancellationToken ct)
    {
        var staff = await db.Staff
            .AsNoTracking()
            .Select(s => new AdminUserDto(s.Id, s.Username, s.Username, s.Role.ToString(),
                s.IsActive, s.PermissionsBitfield, null))
            .ToListAsync(ct);

        var admins = await db.Admins
            .AsNoTracking()
            .Select(a => new AdminUserDto(a.Id, a.Username, a.Username, "Admin",
                a.IsActive, a.AccessPrivileges, null))
            .ToListAsync(ct);

        return [.. staff, .. admins];
    }

    public async Task<AdminUserDto> CreateUserAsync(
        CreateUserRequest req, Guid actorId, CancellationToken ct)
    {
        if (req.Role == "Staff")
        {
            var staffRole = req.StaffRole
                ?? throw new ArgumentException("StaffRole is required when Role is 'Staff'", nameof(req));

            var conflict = PermissionValidator.Validate(staffRole, req.PermissionsBitfield);
            if (conflict is not null)
                throw new PermissionConflictException(conflict);

            var staff = new Staff
            {
                Id                  = Guid.NewGuid(),
                Username            = req.Email,
                Role                = staffRole,
                PermissionsBitfield = req.PermissionsBitfield,
                CreatedAt           = DateTime.UtcNow,
                IsActive            = true,
            };
            staff.AuthCredentials = staffHasher.HashPassword(staff, req.Password);
            db.Staff.Add(staff);
            await db.SaveChangesAsync(ct);

            await WriteAuditAsync(actorId, AuditActionType.AdminAction, staff.Id,
                $"UserCreated|role=Staff|email={req.Email}", ct);

            logger.LogInformation("Admin {ActorId} created Staff user {UserId}", actorId, staff.Id);
            return MapStaff(staff);
        }
        else if (req.Role == "Patient")
        {
            var patient = new PatientEntity
            {
                Id        = Guid.NewGuid(),
                Name      = req.Name.Trim(),
                Email     = req.Email.Trim(),
                Phone     = string.Empty,
                Dob       = DateOnly.MinValue,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            patient.AuthCredentials = patientHasher.HashPassword(patient, req.Password);
            if (req.Department is not null)
                patient.SetDepartment(req.Department);
            db.Patients.Add(patient);
            await db.SaveChangesAsync(ct);

            await WriteAuditAsync(actorId, AuditActionType.AdminAction, patient.Id,
                $"UserCreated|role=Patient|email={req.Email}", ct);

            logger.LogInformation("Admin {ActorId} created Patient user {UserId}", actorId, patient.Id);
            return MapPatient(patient);
        }
        else
        {
            var admin = new AdminEntity
            {
                Id               = Guid.NewGuid(),
                Username         = req.Email,
                AccessPrivileges = req.PermissionsBitfield,
                CreatedAt        = DateTime.UtcNow,
                IsActive         = true,
            };
            admin.AuthCredentials = adminHasher.HashPassword(admin, req.Password);
            db.Admins.Add(admin);
            await db.SaveChangesAsync(ct);

            await WriteAuditAsync(actorId, AuditActionType.AdminAction, admin.Id,
                $"UserCreated|role=Admin|email={req.Email}", ct);

            logger.LogInformation("Admin {ActorId} created Admin user {UserId}", actorId, admin.Id);
            return MapAdmin(admin);
        }
    }

    public async Task<AdminUserDto> UpdateUserAsync(
        Guid userId, UpdateUserRequest req, Guid actorId, CancellationToken ct)
    {
        var staff = await db.Staff.FindAsync([userId], ct);
        if (staff is not null)
        {
            staff.Username = req.Name;
            if (!string.IsNullOrWhiteSpace(req.Password))
                staff.AuthCredentials = staffHasher.HashPassword(staff, req.Password);
            await db.SaveChangesAsync(ct);
            await WriteAuditAsync(actorId, AuditActionType.AdminAction, userId,
                "UserUpdated|type=Staff", ct);
            return MapStaff(staff);
        }

        var admin = await db.Admins.FindAsync([userId], ct)
            ?? throw new UserNotFoundException(userId);

        admin.Username = req.Name;
        if (!string.IsNullOrWhiteSpace(req.Password))
            admin.AuthCredentials = adminHasher.HashPassword(admin, req.Password);
        await db.SaveChangesAsync(ct);
        await WriteAuditAsync(actorId, AuditActionType.AdminAction, userId, "UserUpdated|type=Admin", ct);
        return MapAdmin(admin);
    }

    public async Task ResetPasswordAsync(
        Guid userId, ResetPasswordRequest req, Guid actorId, CancellationToken ct)
    {
        var staff = await db.Staff.FindAsync([userId], ct);
        if (staff is not null)
        {
            staff.AuthCredentials = staffHasher.HashPassword(staff, req.NewPassword);
            await db.SaveChangesAsync(ct);
            await sessionInvalidator.InvalidateSessionAsync(userId, ct);
            await WriteAuditAsync(actorId, AuditActionType.AdminAction, userId, "PasswordReset|type=Staff", ct);
            logger.LogInformation("Admin {ActorId} reset password for Staff {UserId}", actorId, userId);
            return;
        }

        var admin = await db.Admins.FindAsync([userId], ct)
            ?? throw new UserNotFoundException(userId);

        admin.AuthCredentials = adminHasher.HashPassword(admin, req.NewPassword);
        await db.SaveChangesAsync(ct);
        await sessionInvalidator.InvalidateSessionAsync(userId, ct);
        await WriteAuditAsync(actorId, AuditActionType.AdminAction, userId, "PasswordReset|type=Admin", ct);
        logger.LogInformation("Admin {ActorId} reset password for Admin {UserId}", actorId, userId);
    }

    public async Task AssignRoleAsync(
        Guid userId, AssignRoleRequest req, Guid actorId, CancellationToken ct)
    {
        var staff = await db.Staff.FindAsync([userId], ct);
        if (staff is not null)
        {
            var conflict = PermissionValidator.Validate(req.StaffRole, req.PermissionsBitfield);
            if (conflict is not null)
                throw new PermissionConflictException(conflict);

            staff.Role                = req.StaffRole;
            staff.PermissionsBitfield = req.PermissionsBitfield;
            await db.SaveChangesAsync(ct);

            // Force re-login so the user's JWT reflects the updated role claim (UC-006 AC-3 / 4a)
            await sessionInvalidator.InvalidateSessionAsync(userId, ct);
            await WriteAuditAsync(actorId, AuditActionType.AdminAction, userId,
                $"RoleAssigned|newRole={req.StaffRole}|bits={req.PermissionsBitfield}", ct);
            logger.LogInformation("Admin {ActorId} assigned role {Role} to {UserId}", actorId, req.StaffRole, userId);
            return;
        }

        // Admin users do not have a StaffRole sub-role; only update AccessPrivileges.
        var admin = await db.Admins.FindAsync([userId], ct)
            ?? throw new UserNotFoundException(userId);

        admin.AccessPrivileges = req.PermissionsBitfield;
        await db.SaveChangesAsync(ct);

        await sessionInvalidator.InvalidateSessionAsync(userId, ct);
        await WriteAuditAsync(actorId, AuditActionType.AdminAction, userId,
            $"PermissionsUpdated|bits={req.PermissionsBitfield}", ct);
        logger.LogInformation("Admin {ActorId} updated permissions for Admin user {UserId}", actorId, userId);
    }

    public async Task DisableUserAsync(Guid userId, Guid actorId, CancellationToken ct)
    {
        var staff = await db.Staff.FindAsync([userId], ct);
        if (staff is not null)
        {
            staff.IsActive = false;
            await db.SaveChangesAsync(ct);
        }
        else
        {
            var admin = await db.Admins.FindAsync([userId], ct)
                ?? throw new UserNotFoundException(userId);
            admin.IsActive = false;
            await db.SaveChangesAsync(ct);
        }

        // Terminate active sessions immediately (UC-006 AC-4 / 5a)
        await sessionInvalidator.InvalidateSessionAsync(userId, ct);
        await WriteAuditAsync(actorId, AuditActionType.UserLogout, userId, "UserDisabled", ct);
        logger.LogInformation("Admin {ActorId} disabled user {UserId}", actorId, userId);
    }

    public async Task EnableUserAsync(Guid userId, Guid actorId, CancellationToken ct)
    {
        var staff = await db.Staff.FindAsync([userId], ct);
        if (staff is not null)
        {
            staff.IsActive = true;
            await db.SaveChangesAsync(ct);
        }
        else
        {
            var admin = await db.Admins.FindAsync([userId], ct)
                ?? throw new UserNotFoundException(userId);
            admin.IsActive = true;
            await db.SaveChangesAsync(ct);
        }

        await WriteAuditAsync(actorId, AuditActionType.AdminAction, userId, "UserEnabled", ct);
        logger.LogInformation("Admin {ActorId} enabled user {UserId}", actorId, userId);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static AdminUserDto MapStaff(Staff s) =>
        new(s.Id, s.Username, s.Username, s.Role.ToString(), s.IsActive, s.PermissionsBitfield);

    private static AdminUserDto MapAdmin(AdminEntity a) =>
        new(a.Id, a.Username, a.Username, "Admin", a.IsActive, a.AccessPrivileges);

    private static AdminUserDto MapPatient(PatientEntity p) =>
        new(p.Id, p.Name, p.Email, "Patient", !p.IsDeleted, 0, p.Department);

    /// <summary>
    /// Generates a cryptographically random 16-character temporary password.
    /// The password is hashed before storage — it is never returned to the caller.
    /// </summary>
    private static string GenerateSecureTemporaryPassword()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$";
        var bytes = RandomNumberGenerator.GetBytes(16);
        var sb    = new StringBuilder(16);
        foreach (var b in bytes)
            sb.Append(chars[b % chars.Length]);
        return sb.ToString();
    }

    private async Task WriteAuditAsync(
        Guid actorId, AuditActionType actionType, Guid targetId, string? details, CancellationToken ct)
    {
        // Details column is jsonb — wrap plain strings as a JSON object so Postgres accepts them.
        var jsonDetails = details is null
            ? null
            : System.Text.Json.JsonSerializer.Serialize(new { action = details });

        db.AuditLogs.Add(new AuditLog
        {
            Id             = Guid.NewGuid(),
            ActorId        = actorId,
            ActorType      = AuditActorType.Admin,
            ActionType     = actionType,
            TargetEntityId = targetId,
            Details        = jsonDetails,
            OccurredAt     = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
    }
}

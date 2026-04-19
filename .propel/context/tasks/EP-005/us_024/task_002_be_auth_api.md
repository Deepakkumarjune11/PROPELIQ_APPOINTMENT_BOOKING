# Task - task_002_be_auth_api

## Requirement Reference

- **User Story**: US_024 — Role-Based Authentication & Session Management
- **Story Location**: `.propel/context/tasks/EP-005/us_024/us_024.md`
- **Acceptance Criteria**:
  - AC-1: `POST /api/v1/auth/login` authenticates user against existing Staff, Admin, or Patient entities using `IPasswordHasher<T>`; returns JWT with role claim and `expiresAt` (15 minutes from now) per TR-010.
  - AC-2: JWT is validated on every protected endpoint via `[Authorize]`; requests without a valid token return 401; requests with a valid token but wrong role return 403 per NFR-004.
  - AC-3: `POST /api/v1/auth/refresh` validates the current token (not yet expired, or within a 30-second grace window); issues a new 15-minute JWT; returns 401 if token is invalid/expired beyond grace window per NFR-005.
  - AC-4: `POST /api/v1/auth/logout` blacklists the current token in Redis with TTL equal to its remaining lifetime; subsequent requests with that token return 401 per FR-017.
  - AC-5: Login success/failure and logout are written to `AuditLog` with `operation`, `userId`, no PHI in payload per FR-016.
- **Edge Cases**:
  - Login with non-existent email → 401 with generic message "Invalid credentials" (do NOT distinguish user-not-found from wrong-password per OWASP A07).
  - Concurrent refresh calls with the same token → Redis atomic `SET NX` pattern ensures only one new token is issued; second call returns 401.
  - Token blacklist Redis unavailable → log warning and allow logout to succeed without blacklist (fail-open for availability; login security maintained by 15-min TTL).

---

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | No |
| **Figma URL** | N/A |
| **Wireframe Status** | N/A |
| **Wireframe Path/URL** | N/A |
| **Screen Spec** | N/A |
| **UXR Requirements** | N/A |
| **Design Tokens** | N/A |

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Backend | .NET | 8 LTS |
| API Framework | ASP.NET Core Web API | 8.0 |
| Auth Library | ASP.NET Core Identity (password hasher only) | 8.0 |
| Token Format | JWT Bearer | — |
| Cache | Upstash Redis (IDistributedCache) | — |
| ORM | Entity Framework Core | 8.0 |
| Database | PostgreSQL | 15.x |
| Architecture | Modular — PatientAccess bounded context | — |
| Language | C# | 12 |

> **Architecture note**: The project does NOT use `AddIdentity()` full framework. Instead it registers `IPasswordHasher<Staff>` and `IPasswordHasher<Admin>` directly (see `Program.cs` line 71–72). Patient password hasher follows the same pattern. `AuthService` queries Staff/Admin/Patient tables directly via `PropelIQDbContext`.

---

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |
| **AIR Requirements** | N/A |
| **AI Pattern** | N/A |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | N/A |
| **Model Provider** | N/A |

---

## Mobile References (Mobile Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **Mobile Impact** | No |
| **Platform Target** | N/A |
| **Min OS Version** | N/A |
| **Mobile Framework** | N/A |

---

## Task Overview

Add JWT Bearer authentication middleware to the existing `Program.cs`, create `AuthController` with login / logout / refresh endpoints, and implement `IAuthService` that validates credentials against Staff/Admin/Patient entities and generates role-scoped JWTs.

### Part A — JWT Configuration in `appsettings.json`

```json
"Jwt": {
  "Key": "<min-32-char-secret-from-environment>",
  "Issuer": "propeliq-api",
  "Audience": "propeliq-client",
  "ExpiryMinutes": 15
}
```

> **Security (OWASP A02)**: `Jwt:Key` MUST NOT be committed to source control. In `appsettings.Development.json` use a placeholder. In production, supply via environment variable or Azure Key Vault. Add key to `.gitignore` exclusion.

### Part B — `Program.cs` (MODIFY — add auth middleware)

```csharp
// JWT Bearer authentication (TR-010) — add BEFORE builder.Services.AddControllers()
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is required");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,          // strict 15-min TTL per NFR-005
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPasswordHasher<Patient>, PasswordHasher<Patient>>();
// (Staff and Admin hashers already registered)
```

Add to pipeline — **MUST** be before `UseAuthorization()`:
```csharp
app.UseAuthentication();    // ← add this line
app.UseAuthorization();     // ← already present
```

Add Bearer security definition to Swagger:
```csharp
options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme {
    Name = "Authorization", Type = SecuritySchemeType.Http,
    Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header
});
options.AddSecurityRequirement(new OpenApiSecurityRequirement {{ bearerScheme, Array.Empty<string>() }});
```

### Part C — `IAuthService` + `AuthService`

```csharp
public interface IAuthService
{
    Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<AuthResponse?> RefreshAsync(string currentToken, CancellationToken ct);
    Task LogoutAsync(string token, CancellationToken ct);
}

public sealed class AuthService(
    PropelIQDbContext db,
    IConfiguration config,
    IDistributedCache cache,
    IPasswordHasher<Staff> staffHasher,
    IPasswordHasher<Admin> adminHasher,
    IPasswordHasher<Patient> patientHasher,
    IAuditLogger auditLogger,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<AuthResponse?> LoginAsync(LoginRequest req, CancellationToken ct)
    {
        // Try Staff → Admin → Patient (order matters; first match wins)
        var (userId, role, passwordHash) = await ResolveCredentialAsync(req.Email, ct);
        if (userId == Guid.Empty) { /* audit failed attempt */ return null; }

        var hasher = GetHasher(role);
        if (hasher.VerifyHashedPassword(null!, passwordHash, req.Password)
                == PasswordVerificationResult.Failed)
        {
            await auditLogger.LogAsync("LoginFailed", userId, new { email = req.Email, role }, ct);
            return null;
        }

        var token = GenerateJwt(userId, role);
        await auditLogger.LogAsync("UserLoggedIn", userId, new { role }, ct);
        return new AuthResponse(token, userId, role, DateTimeOffset.UtcNow.AddMinutes(15).ToUnixTimeMilliseconds());
    }

    public async Task<AuthResponse?> RefreshAsync(string currentToken, CancellationToken ct)
    {
        // Validate existing token (allow up to 30s grace past expiry)
        var principal = ValidateToken(currentToken, allowExpiredWithinGrace: true);
        if (principal is null) return null;

        var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var role = principal.FindFirstValue(ClaimTypes.Role)!;

        // Blacklist old token atomically (prevent token reuse)
        var remaining = GetTokenRemainingSeconds(currentToken);
        await cache.SetStringAsync($"blacklist:{currentToken}", "1",
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(Math.Max(remaining, 60)) }, ct);

        var newToken = GenerateJwt(userId, role);
        return new AuthResponse(newToken, userId, role, DateTimeOffset.UtcNow.AddMinutes(15).ToUnixTimeMilliseconds());
    }

    public async Task LogoutAsync(string token, CancellationToken ct)
    {
        var principal = ValidateToken(token, allowExpiredWithinGrace: false);
        var userId = principal is not null
            ? Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!)
            : Guid.Empty;
        try
        {
            var remaining = GetTokenRemainingSeconds(token);
            if (remaining > 0)
                await cache.SetStringAsync($"blacklist:{token}", "1",
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(remaining) }, ct);
        }
        catch (Exception ex) { logger.LogWarning(ex, "Token blacklist failed — Redis unavailable"); }

        if (userId != Guid.Empty)
            await auditLogger.LogAsync("UserLoggedOut", userId, new { }, ct);
    }
}
```

**Token blacklist validation** — Add to JWT validation via `OnTokenValidated` event:
```csharp
options.Events = new JwtBearerEvents
{
    OnTokenValidated = async ctx =>
    {
        var cache = ctx.HttpContext.RequestServices.GetRequiredService<IDistributedCache>();
        var token = ctx.HttpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
        var blacklisted = await cache.GetStringAsync($"blacklist:{token}");
        if (blacklisted is not null)
            ctx.Fail("Token has been revoked");
    }
};
```

### Part D — `AuthController`

```csharp
[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await authService.LoginAsync(request, ct);
        if (result is null)
            return Unauthorized(new { message = "Invalid credentials" });
        return Ok(result);
    }

    [HttpPost("refresh")]
    [Authorize]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var token = Request.Headers.Authorization.ToString().Replace("Bearer ", "");
        var result = await authService.RefreshAsync(token, ct);
        if (result is null) return Unauthorized(new { message = "Token refresh failed" });
        return Ok(result);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var token = Request.Headers.Authorization.ToString().Replace("Bearer ", "");
        await authService.LogoutAsync(token, ct);
        return NoContent();
    }
}
```

### Part E — DTOs

```csharp
public record LoginRequest(string Email, string Password, bool RememberMe = false);
public record AuthResponse(string Token, Guid UserId, string Role, long ExpiresAt);
```

---

## Dependent Tasks

- **task_001_fe_login_session_guards.md** (US_024) — Consumes `POST /api/v1/auth/login` and `POST /api/v1/auth/refresh`.
- **Existing**: `IAuditLogger` already registered in DI (US_020/task_002); `IDistributedCache` already registered in `Program.cs` via Redis; `PropelIQDbContext` with Staff/Admin/Patient entities already in PatientAccess.Data.

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | `server/src/PropelIQ.Api/Program.cs` | Add `AddAuthentication(JwtBearer)` + `AddJwtBearer` + `AddAuthorization` + `IAuthService` DI; add `UseAuthentication()`; Swagger bearer security definition; `IPasswordHasher<Patient>` registration |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Presentation/Controllers/AuthController.cs` | `POST /api/v1/auth/login` (AllowAnonymous), `POST /api/v1/auth/refresh` (Authorize), `POST /api/v1/auth/logout` (Authorize) |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Services/IAuthService.cs` | Interface: `LoginAsync`, `RefreshAsync`, `LogoutAsync` |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Services/AuthService.cs` | Credential validation against Staff/Admin/Patient; JWT generation; Redis blacklist |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/DTOs/LoginRequest.cs` | `record LoginRequest(string Email, string Password, bool RememberMe)` |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/DTOs/AuthResponse.cs` | `record AuthResponse(string Token, Guid UserId, string Role, long ExpiresAt)` |
| MODIFY | `server/src/PropelIQ.Api/appsettings.json` | Add `Jwt` section (`Issuer`, `Audience`, `ExpiryMinutes`); add placeholder `Key` with comment to supply via environment |
| MODIFY | `server/src/PropelIQ.Api/ConfigurationValidator.cs` | Add `"Jwt:Key"` to required keys list |

---

## Implementation Plan

1. **Add `Jwt:*` config** to `appsettings.json` (no key value) and `appsettings.Development.json` (dev-only random key). Register `"Jwt:Key"` in `ConfigurationValidator`.

2. **Modify `Program.cs`**: Register JWT Bearer auth, `IAuthService`, `IPasswordHasher<Patient>`, and add `UseAuthentication()` before `UseAuthorization()`. Extend Swagger options with bearer security definition. Add `OnTokenValidated` event for Redis blacklist check.

3. **Create DTOs** (`LoginRequest`, `AuthResponse`) in `PatientAccess.Application/DTOs/`.

4. **Create `IAuthService`** + **`AuthService`**:
   - `ResolveCredentialAsync`: query `Staff` first, then `Admin`, then `Patient` by `Email` → return `(Guid userId, string role, string passwordHash)`. If not found, return default tuple (Guid.Empty).
   - `GenerateJwt`: `JwtSecurityToken` with `NameIdentifier`, `Role`, `Email` claims; signed with `SymmetricSecurityKey(UTF8 key)`; `Expires = UtcNow + 15min`; `ClockSkew = Zero`.
   - `GetTokenRemainingSeconds`: validate without lifetime; read `exp` claim → compute remaining TTL.
   - `ValidateToken(allowExpiredWithinGrace)`: `TokenValidationParameters` with `ClockSkew = 30s` when grace allowed, else `ClockSkew = Zero`.

5. **Create `AuthController`** in `PatientAccess.Presentation/Controllers/`. Annotate `[ApiController]` + `[Route("api/v1/auth")]`. Three endpoints as above.

6. **Security hardening** (OWASP A07 — Identification and Authentication Failures):
   - Constant-time password verification via `IPasswordHasher.VerifyHashedPassword` (already constant-time in .NET).
   - Generic 401 message regardless of whether user was not found or password was wrong.
   - Rate limiting: add `app.UseRateLimiter()` with fixed window of 5 login attempts per IP per 60 seconds (ASP.NET Core 8 built-in `RateLimiterOptions`).

---

## Current Project State

```
server/src/
  PropelIQ.Api/
    Program.cs                                          ← MODIFY — auth middleware + UseAuthentication
    appsettings.json                                    ← MODIFY — add Jwt section
    ConfigurationValidator.cs                           ← MODIFY — add Jwt:Key to required keys
  Modules/
    PatientAccess/
      PatientAccess.Application/
        Services/
          IAuthService.cs                               ← THIS TASK (create)
          AuthService.cs                                ← THIS TASK (create)
        DTOs/
          LoginRequest.cs                               ← THIS TASK (create)
          AuthResponse.cs                               ← THIS TASK (create)
      PatientAccess.Presentation/
        Controllers/
          AuthController.cs                             ← THIS TASK (create)
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | `server/src/PropelIQ.Api/Program.cs` | JWT Bearer auth; `AddAuthentication`; `UseAuthentication()`; `IAuthService` DI; Swagger bearer; `OnTokenValidated` Redis blacklist check; login rate limiter |
| MODIFY | `server/src/PropelIQ.Api/appsettings.json` | Add `"Jwt": { "Issuer", "Audience", "ExpiryMinutes": 15 }` |
| MODIFY | `server/src/PropelIQ.Api/ConfigurationValidator.cs` | Add `"Jwt:Key"` to required keys |
| CREATE | `server/.../PatientAccess.Application/Services/IAuthService.cs` | `LoginAsync` / `RefreshAsync` / `LogoutAsync` interface |
| CREATE | `server/.../PatientAccess.Application/Services/AuthService.cs` | Credential lookup (Staff → Admin → Patient); JWT generation; Redis blacklist; AuditLog |
| CREATE | `server/.../PatientAccess.Application/DTOs/LoginRequest.cs` | `record LoginRequest(string Email, string Password, bool RememberMe)` |
| CREATE | `server/.../PatientAccess.Application/DTOs/AuthResponse.cs` | `record AuthResponse(string Token, Guid UserId, string Role, long ExpiresAt)` |
| CREATE | `server/.../PatientAccess.Presentation/Controllers/AuthController.cs` | `/api/v1/auth/login`, `/refresh`, `/logout` |

---

## External References

- [ASP.NET Core 8 — JWT Bearer authentication configuration](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/jwt-authn)
- [ASP.NET Core 8 — `ClockSkew = TimeSpan.Zero` for strict token expiry](https://learn.microsoft.com/en-us/dotnet/api/microsoft.identitymodel.tokens.tokenvalidationparameters.clockskew)
- [ASP.NET Core 8 — Rate Limiting middleware (`AddRateLimiter`)](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit)
- [ASP.NET Core Identity — `IPasswordHasher<T>.VerifyHashedPassword`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.ipasswordhasher-1.verifyhashedpassword)
- [IDistributedCache — `SetStringAsync` with `AbsoluteExpirationRelativeToNow`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.caching.distributed.idistributedcache.setstringasync)
- [JWT `SecurityTokenDescriptor` + `JwtSecurityTokenHandler`](https://learn.microsoft.com/en-us/dotnet/api/system.identitymodel.tokens.jwt.jwtsecuritytokenhandler)
- [OWASP A07 — Identification and Authentication Failures](https://owasp.org/Top10/A07_2021-Identification_and_Authentication_Failures/)
- [TR-010 — ASP.NET Core Identity for authentication and RBAC](../.propel/context/docs/design.md)
- [NFR-004 — Role-based access control: patient/staff/admin separation](../.propel/context/docs/design.md)
- [NFR-005 — 15-minute session termination](../.propel/context/docs/design.md)
- [FR-016 — Immutable audit log for all access actions](../.propel/context/docs/spec.md)
- [FR-017 — Secure session handling and inactivity timeout](../.propel/context/docs/spec.md)

---

## Build Commands

```bash
cd server
dotnet restore PropelIQ.slnx
dotnet build PropelIQ.slnx --configuration Debug
```

---

## Implementation Validation Strategy

- [ ] Unit test: `AuthService.LoginAsync` with valid staff credentials → returns `AuthResponse` with `role = "staff"` and `expiresAt = UtcNow + 15min (±5s)`
- [ ] Unit test: `AuthService.LoginAsync` with wrong password → returns `null`; `AuditLog` contains `"LoginFailed"` entry
- [ ] Unit test: `AuthService.LoginAsync` with unknown email → returns `null` with same generic response (OWASP A07 — no user enumeration)
- [ ] Unit test: `AuthService.LogoutAsync` calls `cache.SetStringAsync("blacklist:{token}", "1", TTL)` with TTL equal to token remaining seconds
- [ ] Unit test: `AuthController.Login` returns 401 when `IAuthService.LoginAsync` returns `null`
- [ ] Integration test: `POST /api/v1/auth/login` with valid credentials → 200 with `token`, `userId`, `role`, `expiresAt`
- [ ] Integration test: blacklisted token → `OnTokenValidated` rejects with 401
- [ ] Rate limit: 6th login attempt from same IP within 60s → 429 Too Many Requests

---

## Implementation Checklist

- [ ] Modify `Program.cs`: add `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...)` with `ClockSkew = TimeSpan.Zero`; add `OnTokenValidated` event that checks Redis `blacklist:{token}` key; add `UseAuthentication()` before `UseAuthorization()`; register `IAuthService`; add `AddRateLimiter` fixed-window policy (5 req/60s per IP) on `/api/v1/auth/login`
- [ ] Modify `appsettings.json`: add `"Jwt"` section with `Issuer`, `Audience`, `ExpiryMinutes: 15`; add `"Jwt:Key"` comment referencing environment variable `PROPELIQ_JWT_KEY`
- [ ] Modify `ConfigurationValidator.cs`: add `"Jwt:Key"` to required keys list so missing key throws at startup
- [ ] Create `IAuthService` + `AuthService`: `ResolveCredentialAsync` queries Staff → Admin → Patient by email; `GenerateJwt` creates 15-min signed JWT with `NameIdentifier`, `Role`, `Email` claims; `LogoutAsync` blacklists token in Redis with remaining TTL (catch Redis failure → log warning, continue); `LoginAsync` audit-logs success/failure; `RefreshAsync` validates current token + atomic Redis blacklist old + issues new JWT
- [ ] Create `AuthController`: `[AllowAnonymous]` on Login; `[Authorize]` on Refresh and Logout; extract token from `Authorization` header for Refresh/Logout; return 401 with `{ message = "Invalid credentials" }` (never distinguish not-found from wrong-password per OWASP A07)
- [ ] Create `LoginRequest` + `AuthResponse` records in `PatientAccess.Application/DTOs/`
- [ ] Add Swagger Bearer security definition and `AddSecurityRequirement` in `SwaggerGen` options
- [ ] Verify `[Authorize(Roles="Staff")]` on existing staff endpoints returns 403 for patient-role JWT (no code change needed — built-in middleware behaviour)

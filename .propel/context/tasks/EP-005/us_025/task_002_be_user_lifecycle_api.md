# Task - task_002_be_user_lifecycle_api

## Requirement Reference

- **User Story**: US_025 — Admin User Lifecycle Management
- **Story Location**: `.propel/context/tasks/EP-005/us_025/us_025.md`
- **Acceptance Criteria**:
  - AC-1: `GET /api/v1/admin/users` returns all Staff and Admin records (name, email, role, `is_active`, `permissionsBitfield`) as `AdminUserDto[]` per FR-015; `[Authorize(Roles = "Admin")]` enforced.
  - AC-2: `POST /api/v1/admin/users` creates a Staff or Admin entity with a hashed initial password, persists to DB, and writes `AuditLog "UserCreated"` with `{actorId, targetId, role}` per UC-006 and FR-016. No PHI in log.
  - AC-3: `PATCH /api/v1/admin/users/{id}/role` validates the requested `role` + `permissionsBitfield` combination via `PermissionValidator.Validate`; on success updates the entity, blacklists the user's cached session in Redis (forcing re-login per UC-006 extension 4a), and writes `AuditLog "RoleAssigned"`.
  - AC-4: `PATCH /api/v1/admin/users/{id}/disable` sets `IsActive = false`; blacklists all active sessions for the user in Redis; blocks future logins (existing `AuthService.LoginAsync` already checks `IsActive`); writes `AuditLog "UserDisabled"` per FR-015 and UC-006 extension 5a.
  - AC-5: Admin attempting `PATCH .../disable` on their own `Id` (extracted from JWT `NameIdentifier` claim) → 400 with `{ error_code: "self_disable_forbidden" }` per edge case.
- **Edge Cases**:
  - Role combination `FrontDesk + VerifyClinicalData permission` is an invalid conflict → `PermissionValidator` returns 422 `{ error_code: "permission_conflict", message: "FrontDesk role cannot hold VerifyClinicalData permission" }`.
  - `POST /api/v1/admin/users` with a duplicate email → 409 `{ error_code: "email_already_exists" }` (unique index `uix_staff_username` already exists).
  - Re-enabling a disabled user (`PATCH .../enable`) → sets `IsActive = true`; no session invalidation needed; AuditLog `"UserEnabled"`.
  - `PUT /api/v1/admin/users/{id}` (update name/email/department) → updates fields, skips password (no password field in update payload); AuditLog `"UserUpdated"`.

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
| ORM | Entity Framework Core | 8.0 |
| Database | PostgreSQL | 15.x |
| Cache | Upstash Redis (IDistributedCache) | — |
| Architecture | Modular monolith — Admin bounded context | — |
| Language | C# | 12 |

> The Admin module (`Admin.Application`, `Admin.Domain`, `Admin.Presentation`) currently contains only class stubs. This task implements the first real logic in that module. Staff/Admin entities live in `PatientAccess.Data` and are accessed via `PropelIQDbContext` (already shared across modules). `IPasswordHasher<Staff>` and `IPasswordHasher<Admin>` are already registered in `Program.cs`.

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

Implement the Admin user lifecycle API inside the `Admin` bounded context. The module already has `AddAdminModule()` registered in `Program.cs`. This task populates the application and presentation layers.

### Part A — `PermissionValidator` (Admin.Application)

Defines valid role-permission combinations. Invalid combinations return a descriptive error:

```csharp
public static class PermissionValidator
{
    // Bit positions matching client-side constants
    public const int ViewPatientCharts   = 1 << 0;
    public const int VerifyClinicalData  = 1 << 1;
    public const int ManageAppointments  = 1 << 2;
    public const int UploadDocuments     = 1 << 3;
    public const int ViewMetrics         = 1 << 4;

    // StaffRole.FrontDesk may NOT hold VerifyClinicalData (clinical review is only for ClinicalReviewer)
    // StaffRole.CallCenter may NOT hold ViewPatientCharts or ManageAppointments simultaneously
    private static readonly IReadOnlyList<(StaffRole role, int forbiddenBit, string message)> Conflicts =
    [
        (StaffRole.FrontDesk,   VerifyClinicalData,  "FrontDesk role cannot hold VerifyClinicalData permission"),
        (StaffRole.CallCenter,  ViewPatientCharts,   "CallCenter role cannot hold ViewPatientCharts permission"),
    ];

    public static string? Validate(StaffRole role, int permissionsBitfield)
    {
        foreach (var (r, bit, msg) in Conflicts)
            if (r == role && (permissionsBitfield & bit) != 0)
                return msg;
        return null;
    }
}
```

### Part B — `IUserManagementService` + `UserManagementService`

```csharp
public interface IUserManagementService
{
    Task<IReadOnlyList<AdminUserDto>> GetAllUsersAsync(CancellationToken ct);
    Task<AdminUserDto> CreateUserAsync(CreateUserRequest request, Guid actorId, CancellationToken ct);
    Task<AdminUserDto> UpdateUserAsync(Guid userId, UpdateUserRequest request, Guid actorId, CancellationToken ct);
    Task AssignRoleAsync(Guid userId, AssignRoleRequest request, Guid actorId, CancellationToken ct);
    Task DisableUserAsync(Guid userId, Guid actorId, CancellationToken ct);
    Task EnableUserAsync(Guid userId, Guid actorId, CancellationToken ct);
}
```

**`CreateUserAsync`**:
```csharp
public async Task<AdminUserDto> CreateUserAsync(CreateUserRequest req, Guid actorId, CancellationToken ct)
{
    // OWASP A07: validate role before creating any record
    if (req.Role == "Staff")
    {
        var conflict = PermissionValidator.Validate(req.StaffRole!.Value, req.PermissionsBitfield);
        if (conflict is not null)
            throw new PermissionConflictException(conflict);
    }

    var initialPassword = GenerateSecureTemporaryPassword(); // crypto-random 16 chars
    string passwordHash;
    if (req.Role == "Staff")
    {
        var staff = new Staff { Id = Guid.NewGuid(), Username = req.Email,
            Role = req.StaffRole!.Value, PermissionsBitfield = req.PermissionsBitfield,
            CreatedAt = DateTime.UtcNow, IsActive = true };
        staff.AuthCredentials = _staffHasher.HashPassword(staff, initialPassword);
        _db.Staff.Add(staff);
        await _db.SaveChangesAsync(ct);
        await _auditLogger.LogAsync("UserCreated", actorId,
            new { targetId = staff.Id, role = req.Role, email = req.Email }, ct);
        return MapToDto(staff);
    }
    else
    {
        var admin = new Admin { Id = Guid.NewGuid(), Username = req.Email,
            AccessPrivileges = req.PermissionsBitfield, CreatedAt = DateTime.UtcNow, IsActive = true };
        admin.AuthCredentials = _adminHasher.HashPassword(admin, initialPassword);
        _db.Admins.Add(admin);
        await _db.SaveChangesAsync(ct);
        await _auditLogger.LogAsync("UserCreated", actorId,
            new { targetId = admin.Id, role = req.Role, email = req.Email }, ct);
        return MapToDto(admin);
    }
}
```

**`AssignRoleAsync`** — role update + Redis session blacklist:
```csharp
public async Task AssignRoleAsync(Guid userId, AssignRoleRequest req, Guid actorId, CancellationToken ct)
{
    var staff = await _db.Staff.FindAsync([userId], ct);
    if (staff is null) throw new UserNotFoundException(userId);

    var conflict = PermissionValidator.Validate(req.StaffRole, req.PermissionsBitfield);
    if (conflict is not null) throw new PermissionConflictException(conflict);

    staff.Role = req.StaffRole;
    staff.PermissionsBitfield = req.PermissionsBitfield;
    await _db.SaveChangesAsync(ct);

    // Invalidate user session — user must re-login to receive updated role claim (UC-006 4a)
    await _sessionInvalidator.InvalidateSessionAsync(userId, ct);
    await _auditLogger.LogAsync("RoleAssigned", actorId,
        new { targetId = userId, newRole = req.StaffRole.ToString(), permissionsBitfield = req.PermissionsBitfield }, ct);
}
```

**`DisableUserAsync`**:
```csharp
public async Task DisableUserAsync(Guid userId, Guid actorId, CancellationToken ct)
{
    // Try Staff first, then Admin
    var staff = await _db.Staff.FindAsync([userId], ct);
    if (staff is not null)
    {
        staff.IsActive = false;
        await _db.SaveChangesAsync(ct);
    }
    else
    {
        var admin = await _db.Admins.FindAsync([userId], ct)
            ?? throw new UserNotFoundException(userId);
        admin.IsActive = false;
        await _db.SaveChangesAsync(ct);
    }

    await _sessionInvalidator.InvalidateSessionAsync(userId, ct);
    await _auditLogger.LogAsync("UserDisabled", actorId, new { targetId = userId }, ct);
}
```

### Part C — `ISessionInvalidator` + `RedisSessionInvalidator`

Uses a per-user token key in Redis to mark all tokens for that user as blacklisted. The `OnTokenValidated` event in `Program.cs` (US_024/task_002) already checks `blacklist:{token}`. This service adds a second check on a user-scoped key:

```csharp
public interface ISessionInvalidator
{
    Task InvalidateSessionAsync(Guid userId, CancellationToken ct);
    Task<bool> IsUserInvalidatedAsync(Guid userId, CancellationToken ct);
}

public sealed class RedisSessionInvalidator(IDistributedCache cache) : ISessionInvalidator
{
    private static string UserKey(Guid id) => $"session_invalidated:{id}";

    public async Task InvalidateSessionAsync(Guid userId, CancellationToken ct) =>
        await cache.SetStringAsync(UserKey(userId), "1",
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15) }, ct);

    public async Task<bool> IsUserInvalidatedAsync(Guid userId, CancellationToken ct) =>
        await cache.GetStringAsync(UserKey(userId), ct) is not null;
}
```

**Extend `OnTokenValidated`** in `Program.cs` (MODIFY — delta):
```csharp
// Add AFTER existing token blacklist check:
var userIdClaim = ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
if (Guid.TryParse(userIdClaim, out var uid))
{
    var invalidator = ctx.HttpContext.RequestServices.GetRequiredService<ISessionInvalidator>();
    if (await invalidator.IsUserInvalidatedAsync(uid, ct))
        ctx.Fail("User session has been invalidated");
}
```

Additionally, update `AuthService.LoginAsync` to check `IsActive` before issuing a token:
```csharp
// In ResolveCredentialAsync — after locating user:
if (!isActive) return (Guid.Empty, string.Empty, string.Empty); // return as not-found
```

### Part D — `AdminController`

```csharp
[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "Admin")]
public sealed class AdminController(IUserManagementService userService) : ControllerBase
{
    private Guid ActorId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(CancellationToken ct) =>
        Ok(await userService.GetAllUsersAsync(ct));

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest req, CancellationToken ct)
    {
        try { return Ok(await userService.CreateUserAsync(req, ActorId, ct)); }
        catch (PermissionConflictException ex) => UnprocessableEntity(new { error_code = "permission_conflict", message = ex.Message });
    }

    [HttpPut("users/{id:guid}")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest req, CancellationToken ct)
    {
        try { return Ok(await userService.UpdateUserAsync(id, req, ActorId, ct)); }
        catch (UserNotFoundException) => NotFound();
    }

    [HttpPatch("users/{id:guid}/role")]
    public async Task<IActionResult> AssignRole(Guid id, [FromBody] AssignRoleRequest req, CancellationToken ct)
    {
        try { await userService.AssignRoleAsync(id, req, ActorId, ct); return NoContent(); }
        catch (PermissionConflictException ex) => UnprocessableEntity(new { error_code = "permission_conflict", message = ex.Message });
        catch (UserNotFoundException) => NotFound();
    }

    [HttpPatch("users/{id:guid}/disable")]
    public async Task<IActionResult> DisableUser(Guid id, CancellationToken ct)
    {
        if (id == ActorId)
            return BadRequest(new { error_code = "self_disable_forbidden" });
        try { await userService.DisableUserAsync(id, ActorId, ct); return NoContent(); }
        catch (UserNotFoundException) => NotFound();
    }

    [HttpPatch("users/{id:guid}/enable")]
    public async Task<IActionResult> EnableUser(Guid id, CancellationToken ct)
    {
        try { await userService.EnableUserAsync(id, ActorId, ct); return NoContent(); }
        catch (UserNotFoundException) => NotFound();
    }
}
```

### Part E — `Admin.Presentation/ServiceCollectionExtensions.cs` (MODIFY)

```csharp
public static IServiceCollection AddAdminModule(this IServiceCollection services)
{
    services.AddScoped<IUserManagementService, UserManagementService>();
    services.AddScoped<ISessionInvalidator, RedisSessionInvalidator>();
    return services;
}
```

---

## Dependent Tasks

- **task_002_be_auth_api.md** (US_024) — `PropelIQDbContext`, `IDistributedCache`, `IAuditLogger`, `IPasswordHasher<Staff>`, `IPasswordHasher<Admin>` all available in DI; `OnTokenValidated` event extended here.
- **Existing**: `Staff` entity with `IsActive` + `PermissionsBitfield`, `Admin` entity — both in `PatientAccess.Data`.

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/Admin/Admin.Application/Services/IUserManagementService.cs` | Interface: GetAll, Create, Update, AssignRole, Disable, Enable |
| CREATE | `server/src/Modules/Admin/Admin.Application/Services/UserManagementService.cs` | CRUD + permission validation + session invalidation + audit logging |
| CREATE | `server/src/Modules/Admin/Admin.Application/Services/ISessionInvalidator.cs` | Interface: `InvalidateSessionAsync`, `IsUserInvalidatedAsync` |
| CREATE | `server/src/Modules/Admin/Admin.Application/Services/RedisSessionInvalidator.cs` | Redis `session_invalidated:{userId}` key with 15-min TTL |
| CREATE | `server/src/Modules/Admin/Admin.Application/Validators/PermissionValidator.cs` | Static validator: role × permission conflict rules |
| CREATE | `server/src/Modules/Admin/Admin.Application/DTOs/AdminUserDto.cs` | `record AdminUserDto(Guid Id, string Name, string Email, string Role, bool IsActive, int PermissionsBitfield)` |
| CREATE | `server/src/Modules/Admin/Admin.Application/DTOs/CreateUserRequest.cs` | `record CreateUserRequest(string Name, string Email, string Role, StaffRole? StaffRole, int PermissionsBitfield)` |
| CREATE | `server/src/Modules/Admin/Admin.Application/DTOs/UpdateUserRequest.cs` | `record UpdateUserRequest(string Name, string? Department)` |
| CREATE | `server/src/Modules/Admin/Admin.Application/DTOs/AssignRoleRequest.cs` | `record AssignRoleRequest(StaffRole StaffRole, int PermissionsBitfield)` |
| CREATE | `server/src/Modules/Admin/Admin.Application/Exceptions/PermissionConflictException.cs` | `sealed class PermissionConflictException(string message) : Exception(message)` |
| CREATE | `server/src/Modules/Admin/Admin.Application/Exceptions/UserNotFoundException.cs` | `sealed class UserNotFoundException(Guid userId) : Exception(...)` |
| CREATE | `server/src/Modules/Admin/Admin.Presentation/Controllers/AdminController.cs` | REST endpoints `[Authorize(Roles="Admin")]` |
| MODIFY | `server/src/Modules/Admin/Admin.Presentation/ServiceCollectionExtensions.cs` | Register `IUserManagementService` + `ISessionInvalidator` |
| MODIFY | `server/src/PropelIQ.Api/Program.cs` | Extend `OnTokenValidated` with user-level invalidation check |

---

## Current Project State

```
server/src/
  Modules/
    Admin/
      Admin.Application/
        Class1.cs                                       ← stub (superseded by new files)
        Services/
          IUserManagementService.cs                     ← THIS TASK (create)
          UserManagementService.cs                      ← THIS TASK (create)
          ISessionInvalidator.cs                        ← THIS TASK (create)
          RedisSessionInvalidator.cs                    ← THIS TASK (create)
        Validators/
          PermissionValidator.cs                        ← THIS TASK (create)
        DTOs/
          AdminUserDto.cs                               ← THIS TASK (create)
          CreateUserRequest.cs                          ← THIS TASK (create)
          UpdateUserRequest.cs                          ← THIS TASK (create)
          AssignRoleRequest.cs                          ← THIS TASK (create)
        Exceptions/
          PermissionConflictException.cs                ← THIS TASK (create)
          UserNotFoundException.cs                      ← THIS TASK (create)
      Admin.Domain/
        Class1.cs                                       ← stub (no domain entities needed — entities in PatientAccess.Data)
      Admin.Presentation/
        Controllers/
          AdminController.cs                            ← THIS TASK (create)
        ServiceCollectionExtensions.cs                  ← MODIFY — register services
  PropelIQ.Api/
    Program.cs                                          ← MODIFY — extend OnTokenValidated with user-level check
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/.../Admin.Application/Services/IUserManagementService.cs` | Full CRUD + lifecycle interface |
| CREATE | `server/.../Admin.Application/Services/UserManagementService.cs` | Staff/Admin CRUD; password hashing; session invalidation; audit logging |
| CREATE | `server/.../Admin.Application/Services/ISessionInvalidator.cs` | User-scoped session invalidation interface |
| CREATE | `server/.../Admin.Application/Services/RedisSessionInvalidator.cs` | Redis `session_invalidated:{userId}` key; 15-min TTL |
| CREATE | `server/.../Admin.Application/Validators/PermissionValidator.cs` | Conflict rules table: FrontDesk × VerifyClinicalData; CallCenter × ViewPatientCharts |
| CREATE | `server/.../Admin.Application/DTOs/` (4 files) | `AdminUserDto`, `CreateUserRequest`, `UpdateUserRequest`, `AssignRoleRequest` |
| CREATE | `server/.../Admin.Application/Exceptions/` (2 files) | `PermissionConflictException`, `UserNotFoundException` |
| CREATE | `server/.../Admin.Presentation/Controllers/AdminController.cs` | 6 endpoints; `[Authorize(Roles="Admin")]` on controller; self-disable guard |
| MODIFY | `server/.../Admin.Presentation/ServiceCollectionExtensions.cs` | Register `IUserManagementService` + `ISessionInvalidator` |
| MODIFY | `server/src/PropelIQ.Api/Program.cs` | Extend `OnTokenValidated`: check `IsUserInvalidatedAsync` after token blacklist check |

---

## External References

- [ASP.NET Core 8 — `[Authorize(Roles = "Admin")]` attribute](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/roles)
- [ASP.NET Core Identity — `IPasswordHasher<T>.HashPassword`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.ipasswordhasher-1.hashpassword)
- [IDistributedCache — `SetStringAsync` / `GetStringAsync`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.caching.distributed.idistributedcache)
- [EF Core 8 — `DbContext.FindAsync` for PK lookup](https://learn.microsoft.com/en-us/dotnet/api/microsoft.entityframeworkcore.dbcontext.findasync)
- [OWASP A01 — Broken Access Control (self-disable guard, role enforcement)](https://owasp.org/Top10/A01_2021-Broken_Access_Control/)
- [OWASP A02 — Cryptographic Failures (password hashing via IPasswordHasher)](https://owasp.org/Top10/A02_2021-Cryptographic_Failures/)
- [DR-009 — Staff entity with role enum, permissions bitfield, auth credentials](../.propel/context/docs/design.md)
- [DR-010 — Admin entity with access control privileges](../.propel/context/docs/design.md)
- [FR-015 — Admin create/update/disable/role-assign accounts](../.propel/context/docs/spec.md)
- [FR-016 — Immutable audit log for all admin actions](../.propel/context/docs/spec.md)
- [UC-006 — Admin manages users: extension 2a (conflict), 4a (re-auth), 5a (block logins)](../.propel/context/docs/spec.md)

---

## Build Commands

```bash
cd server
dotnet restore PropelIQ.slnx
dotnet build PropelIQ.slnx --configuration Debug
```

---

## Implementation Validation Strategy

- [ ] Unit test: `PermissionValidator.Validate(StaffRole.FrontDesk, VerifyClinicalData)` → returns non-null conflict message
- [ ] Unit test: `PermissionValidator.Validate(StaffRole.ClinicalReviewer, VerifyClinicalData)` → returns `null` (valid)
- [ ] Unit test: `UserManagementService.DisableUserAsync` calls `InvalidateSessionAsync(userId)` and writes `AuditLog "UserDisabled"`
- [ ] Unit test: `AdminController.DisableUser` with `id == ActorId` → 400 `{ error_code: "self_disable_forbidden" }`
- [ ] Unit test: `AdminController.CreateUser` with permission conflict → `UserManagementService` throws `PermissionConflictException` → 422 response
- [ ] Integration test: `GET /api/v1/admin/users` with Staff JWT → 403 Forbidden
- [ ] Integration test: `GET /api/v1/admin/users` with Admin JWT → 200 with `AdminUserDto[]`
- [ ] Integration test: disabled user token → `IsUserInvalidatedAsync` returns true → `OnTokenValidated` rejects with 401
- [ ] Security test: `POST /api/v1/admin/users` password never returned in response; `AuthCredentials` column not mapped to any DTO

---

## Implementation Checklist

- [ ] Create `PermissionValidator.cs` with conflict rules table (FrontDesk × VerifyClinicalData, CallCenter × ViewPatientCharts) and static `Validate(StaffRole, int)` method
- [ ] Create `ISessionInvalidator` + `RedisSessionInvalidator`: `InvalidateSessionAsync` writes `session_invalidated:{userId}` to Redis with 15-min TTL; `IsUserInvalidatedAsync` reads and returns non-null check
- [ ] Create `IUserManagementService` + `UserManagementService`: `CreateUserAsync` hashes initial password via `IPasswordHasher`; validates role/permission conflict before DB write; `AssignRoleAsync` validates conflict + `InvalidateSessionAsync` + AuditLog; `DisableUserAsync` sets `IsActive=false` + `InvalidateSessionAsync` + AuditLog; `UpdateUserAsync` skips password field; `GetAllUsersAsync` projects Staff + Admin to `AdminUserDto[]`
- [ ] Create `AdminController` with 6 endpoints all under `[Authorize(Roles="Admin")]`; `DisableUser` adds self-disable guard (`id == ActorId → 400`); catch `PermissionConflictException` → 422; catch `UserNotFoundException` → 404
- [ ] Create DTOs: `AdminUserDto` (no `AuthCredentials` field), `CreateUserRequest`, `UpdateUserRequest`, `AssignRoleRequest`; create `PermissionConflictException` + `UserNotFoundException`
- [ ] Modify `ServiceCollectionExtensions.cs`: register `IUserManagementService` + `ISessionInvalidator`
- [ ] Modify `Program.cs` `OnTokenValidated`: after token blacklist check, call `IsUserInvalidatedAsync` using `NameIdentifier` claim; fail context if true
- [ ] Security: verify no DTO exposes `AuthCredentials`; AuditLog payloads contain no PHI (no passwords, no raw permission details beyond role name)
- [ ] Verify `AuthService.LoginAsync` (US_024/task_002) checks `IsActive` on resolved entity before issuing JWT (self-disabling detection at login boundary)

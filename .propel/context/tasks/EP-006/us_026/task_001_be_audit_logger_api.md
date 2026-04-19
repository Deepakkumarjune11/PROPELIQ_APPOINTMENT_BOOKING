# Task - task_001_be_audit_logger_api

## Requirement Reference

- **User Story**: US_026 — Immutable Audit Log & Compliance Logging
- **Story Location**: `.propel/context/tasks/EP-006/us_026/us_026.md`
- **Acceptance Criteria**:
  - AC-1: Every create/read/update/delete operation writes an `audit_log` entry containing `user_id` (`ActorId`), `action` (`ActionType`), `entity_type` (`TargetEntityType`), `entity_id` (`TargetEntityId`), `old_values` (jsonb), `new_values` (jsonb), `ip_address` (varchar 45), and `timestamp` (`CreatedAt`) per FR-016 and DR-012.
  - AC-3: `GET /api/v1/audit-logs` with date-range, actor, entity-type, and action-type filters returns paginated results; query optimised to run within 2s on 1M rows per NFR-013.
  - AC-4: Each persisted `AuditLog` entry carries a `ChainHash` (SHA-256 of the entry's core fields + predecessor's `ChainHash`) and a `PreviousHash` (nullable — `null` for the genesis entry), enabling tamper detection per TR-018.
  - AC-5: The `IAuditLogger.LogAsync` call stages an `AuditLog` entity on the `DbContext` change tracker **without** calling `SaveChangesAsync`; the calling service's existing `SaveChangesAsync` commits the main entity and the audit entry atomically, so a write failure rolls back both per AC-5.
- **Edge Cases**:
  - First audit entry has `PreviousHash = null`; `ChainHash` is computed using the sentinel `"GENESIS"` as the predecessor hash.
  - `ActorId = Guid.Empty` for system-initiated actions (background jobs, Hangfire workers) — `AuditLogger` must accept `Guid.Empty` without throwing.
  - If `IHttpContextAccessor.HttpContext` is `null` (background job context), `IpAddress` must be stored as `"system"` rather than throwing.

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
| Language | C# | 12 |

> **Current state of `AuditLog` entity** (from ModelSnapshot — authoritative):
> `Id` (uuid), `ActorId` (uuid), `ActorType` (varchar 20), `ActionType` (varchar 50), `TargetEntityId` (uuid), `TargetEntityType` (varchar 100), `Payload` (jsonb, required), `CreatedAt` (timestamptz, DEFAULT NOW()).
> Existing indexes: `ix_audit_log_actor_id`, `ix_audit_log_created_at`, `ix_audit_log_target (TargetEntityType, TargetEntityId)`.
> This task adds: `IpAddress`, `OldValues`, `NewValues`, `PreviousHash`, `ChainHash` to the entity and configuration. The DB migration is handled in `task_002_db_audit_schema_migration.md`.

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

Implement the `IAuditLogger` service and the compliance query endpoint for US_026. The audit subsystem already has an `AuditLog` entity, an EF Core `SaveChangesInterceptor` blocking in-process mutations, and three read indexes. This task:

1. **Extends `AuditLog.cs`** with the five missing compliance fields (`IpAddress`, `OldValues`, `NewValues`, `PreviousHash`, `ChainHash`).
2. **Extends `AuditLogConfiguration.cs`** with EF Core column mappings for the new properties.
3. **Creates `IAuditLogger`** — a concise interface used by all service layers to record compliance events.
4. **Creates `AuditLogger`** — computes a SHA-256 hash chain, captures the caller's IP via `IHttpContextAccessor`, and stages the entry on the shared `PropelIQDbContext` for atomic commit with the main operation (AC-5).
5. **Creates `AuditLogController`** — a read-only compliance endpoint (`GET /api/v1/audit-logs`) with date-range, actor, entity-type, and action-type filters, pagination, and `[Authorize(Roles="Admin")]` enforcement.
6. **Registers** `IAuditLogger` and `IHttpContextAccessor` in `PatientAccess.Presentation/ServiceCollectionExtensions.cs`.

---

## Dependent Tasks

- **task_002_db_audit_schema_migration.md** (US_026) — must run the EF Core migration that adds the five new columns before the application can save `AuditLog` entries with hash chain data. The entity extension in this task is the C# model change; the migration file is the complementary DB artefact.
- **Existing**: `PropelIQDbContext.AuditLogs`, `AuditLogImmutabilityInterceptor` (singleton, already registered in `Program.cs`), `AuditActorType` + `AuditActionType` enums — all already available.

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Data/Entities/AuditLog.cs` | Add `IpAddress`, `OldValues`, `NewValues`, `PreviousHash`, `ChainHash` properties |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Data/Configurations/AuditLogConfiguration.cs` | Column configs for five new properties |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Infrastructure/IAuditLogger.cs` | `Task LogAsync(AuditActorType, Guid actorId, AuditActionType, string targetEntityType, Guid targetEntityId, object? oldValues, object? newValues, CancellationToken)` |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Infrastructure/AuditLogger.cs` | SHA-256 hash chain + IP capture + `_db.AuditLogs.Add()` (no SaveChanges) |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Presentation/Controllers/AuditLogController.cs` | `GET /api/v1/audit-logs`; `[Authorize(Roles="Admin")]`; paginated + filtered |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/DTOs/AuditLogDto.cs` | `record AuditLogDto(Guid Id, string ActorType, Guid ActorId, string ActionType, string TargetEntityType, Guid TargetEntityId, string? IpAddress, string? OldValues, string? NewValues, DateTime CreatedAt, string ChainHash)` |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/DTOs/AuditLogPagedResult.cs` | `record AuditLogPagedResult(IReadOnlyList<AuditLogDto> Items, int TotalCount, int PageNumber, int PageSize, int TotalPages)` |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Presentation/ServiceCollectionExtensions.cs` | Register `IAuditLogger` + `IHttpContextAccessor` |

---

## Implementation Plan

### Part A — Extend `AuditLog` Entity

Add five new auto-properties to `AuditLog.cs`. No constructor change — EF Core uses object initializers:

```csharp
/// <summary>
/// Client IP address of the actor. Set to "system" for background-job actions.
/// varchar(45) accommodates IPv4 (15) and IPv6 (39) plus mapped IPv4 (45).
/// </summary>
public string? IpAddress { get; set; }

/// <summary>
/// JSONB snapshot of entity state before the operation. Null for CREATE actions.
/// Must not contain PHI beyond what is already in the audit record (NFR-007).
/// </summary>
public string? OldValues { get; set; }

/// <summary>
/// JSONB snapshot of entity state after the operation. Null for DELETE actions.
/// </summary>
public string? NewValues { get; set; }

/// <summary>
/// ChainHash of the immediately preceding AuditLog row.
/// Null only for the genesis entry (first row ever inserted).
/// </summary>
public string? PreviousHash { get; set; }

/// <summary>
/// SHA-256 of "{Id}|{ActorId}|{ActionType}|{TargetEntityType}|{TargetEntityId}|{CreatedAt:O}|{PreviousHash ?? "GENESIS"}".
/// Enables tamper detection per TR-018.
/// </summary>
public string ChainHash { get; set; } = string.Empty;
```

### Part B — Extend `AuditLogConfiguration`

```csharp
builder.Property(a => a.IpAddress)
    .HasColumnName("ip_address")
    .HasMaxLength(45);                          // IPv6 max 39 + 6 prefix = 45

builder.Property(a => a.OldValues)
    .HasColumnName("old_values")
    .HasColumnType("jsonb");

builder.Property(a => a.NewValues)
    .HasColumnName("new_values")
    .HasColumnType("jsonb");

builder.Property(a => a.PreviousHash)
    .HasColumnName("previous_hash")
    .HasColumnType("text");

builder.Property(a => a.ChainHash)
    .HasColumnName("chain_hash")
    .HasColumnType("text")
    .IsRequired();
```

### Part C — `IAuditLogger`

```csharp
namespace PatientAccess.Application.Infrastructure;

/// <summary>
/// Stages an immutable audit entry on the current DbContext change tracker.
/// The caller's SaveChangesAsync commits the entry atomically with the main operation (AC-5).
/// </summary>
public interface IAuditLogger
{
    /// <param name="actorId">Staff/Admin Guid. Pass Guid.Empty for system actions.</param>
    /// <param name="oldValues">Pre-operation snapshot. Pass null for CREATE. Must not contain AuthCredentials.</param>
    /// <param name="newValues">Post-operation snapshot. Pass null for DELETE. Must not contain AuthCredentials.</param>
    Task LogAsync(
        AuditActorType actorType,
        Guid actorId,
        AuditActionType actionType,
        string targetEntityType,
        Guid targetEntityId,
        object? oldValues,
        object? newValues,
        CancellationToken ct = default);
}
```

### Part D — `AuditLogger` Implementation

```csharp
public sealed class AuditLogger(
    PropelIQDbContext db,
    IHttpContextAccessor httpContextAccessor) : IAuditLogger
{
    public async Task LogAsync(
        AuditActorType actorType,
        Guid actorId,
        AuditActionType actionType,
        string targetEntityType,
        Guid targetEntityId,
        object? oldValues,
        object? newValues,
        CancellationToken ct = default)
    {
        // Get predecessor hash for chain linkage (AC-4)
        var previousHash = await db.AuditLogs
            .AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => a.ChainHash)
            .FirstOrDefaultAsync(ct);   // null → genesis entry

        var id = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;
        var ipAddress = httpContextAccessor.HttpContext?
            .Connection.RemoteIpAddress?.ToString() ?? "system";    // background job fallback

        // Compute SHA-256 hash chain (TR-018)
        var chainInput = $"{id}|{actorId}|{actionType}|{targetEntityType}|{targetEntityId}|{createdAt:O}|{previousHash ?? "GENESIS"}";
        var chainHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(chainInput))).ToLowerInvariant();

        var entry = new AuditLog
        {
            Id                = id,
            ActorType         = actorType,
            ActorId           = actorId,
            ActionType        = actionType,
            TargetEntityType  = targetEntityType,
            TargetEntityId    = targetEntityId,
            IpAddress         = ipAddress,
            OldValues         = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValues         = newValues is null ? null : JsonSerializer.Serialize(newValues),
            Payload           = "{}",                     // backward-compat; structured data is in OldValues/NewValues
            PreviousHash      = previousHash,
            ChainHash         = chainHash,
            CreatedAt         = createdAt,
        };

        // Stage on change tracker — caller's SaveChangesAsync commits atomically (AC-5)
        db.AuditLogs.Add(entry);
    }
}
```

> **Security note**: `OldValues` and `NewValues` must **never** contain `AuthCredentials` or raw passwords. Callers are responsible for filtering sensitive fields before passing to `LogAsync`. Validate in code review.

### Part E — `AuditLogController`

```csharp
[ApiController]
[Route("api/v1/audit-logs")]
[Authorize(Roles = "Admin")]
public sealed class AuditLogController(PropelIQDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetLogs(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] Guid? actorId,
        [FromQuery] string? actionType,
        [FromQuery] string? entityType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        // Clamp page size to prevent abuse (OWASP A01 — resource exhaustion)
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(page, 1);

        var query = db.AuditLogs.AsNoTracking();

        if (dateFrom.HasValue)
            query = query.Where(a => a.CreatedAt >= dateFrom.Value);
        if (dateTo.HasValue)
            query = query.Where(a => a.CreatedAt <= dateTo.Value);
        if (actorId.HasValue)
            query = query.Where(a => a.ActorId == actorId.Value);
        if (!string.IsNullOrWhiteSpace(actionType))
            query = query.Where(a => a.ActionType == actionType);
        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(a => a.TargetEntityType == entityType);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditLogDto(
                a.Id, a.ActorType.ToString(), a.ActorId,
                a.ActionType.ToString(), a.TargetEntityType, a.TargetEntityId,
                a.IpAddress, a.OldValues, a.NewValues, a.CreatedAt, a.ChainHash))
            .ToListAsync(ct);

        return Ok(new AuditLogPagedResult(
            items, totalCount, page, pageSize,
            (int)Math.Ceiling(totalCount / (double)pageSize)));
    }
}
```

> **Performance (AC-3)**: The `ix_audit_log_created_at_actor` composite index and `ix_audit_log_entity_type_created_at` index (both added in `task_002`) allow PostgreSQL to satisfy date-range + actor/entity-type predicate filters without sequential scans on 1M rows. `AsNoTracking()` avoids change-tracker overhead for read-only queries.

### Part F — Register in `ServiceCollectionExtensions.cs`

```csharp
public static IServiceCollection AddPatientAccessModule(this IServiceCollection services)
{
    services.AddHttpContextAccessor();                          // IHttpContextAccessor for IP capture
    services.AddScoped<IAuditLogger, AuditLogger>();
    return services;
}
```

> `AddHttpContextAccessor()` is idempotent — safe to call even if already registered elsewhere.

---

## Current Project State

```
server/src/
  Modules/
    PatientAccess/
      PatientAccess.Data/
        Entities/
          AuditLog.cs                   ← MODIFY — add 5 new properties
        Configurations/
          AuditLogConfiguration.cs      ← MODIFY — add 5 column configs
        Interceptors/
          AuditLogImmutabilityInterceptor.cs   ← no change (already blocks Update/Delete at EF level)
        Migrations/
          [latest migration]            ← task_002 adds new migration on top
      PatientAccess.Application/
        Infrastructure/
          IAuditLogger.cs               ← THIS TASK (create)
          AuditLogger.cs                ← THIS TASK (create)
        DTOs/
          AuditLogDto.cs                ← THIS TASK (create)
          AuditLogPagedResult.cs        ← THIS TASK (create)
      PatientAccess.Presentation/
        Controllers/
          AuditLogController.cs         ← THIS TASK (create)
        ServiceCollectionExtensions.cs  ← MODIFY — register IAuditLogger + IHttpContextAccessor
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | `server/.../PatientAccess.Data/Entities/AuditLog.cs` | Add `IpAddress`, `OldValues`, `NewValues`, `PreviousHash`, `ChainHash` properties |
| MODIFY | `server/.../PatientAccess.Data/Configurations/AuditLogConfiguration.cs` | EF Core column configs for 5 new properties; column names snake_case |
| CREATE | `server/.../PatientAccess.Application/Infrastructure/IAuditLogger.cs` | Audit staging interface (no SaveChanges — atomic with caller) |
| CREATE | `server/.../PatientAccess.Application/Infrastructure/AuditLogger.cs` | SHA-256 hash chain; IP from `IHttpContextAccessor`; `"system"` fallback; `db.AuditLogs.Add()` |
| CREATE | `server/.../PatientAccess.Application/DTOs/AuditLogDto.cs` | Read projection; excludes `Payload` (legacy column) and `PreviousHash` (internal) |
| CREATE | `server/.../PatientAccess.Application/DTOs/AuditLogPagedResult.cs` | Wraps `AuditLogDto[]` with pagination metadata |
| CREATE | `server/.../PatientAccess.Presentation/Controllers/AuditLogController.cs` | `GET /api/v1/audit-logs`; `[Authorize(Roles="Admin")]`; filter + paginate; `AsNoTracking()` |
| MODIFY | `server/.../PatientAccess.Presentation/ServiceCollectionExtensions.cs` | `AddHttpContextAccessor()` + `AddScoped<IAuditLogger, AuditLogger>()` |

---

## External References

- [SHA256.HashData — .NET 8 cryptography](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.sha256.hashdata)
- [IHttpContextAccessor — remote IP address](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.http.ihttpcontextaccessor)
- [EF Core AsNoTracking — read-only queries](https://learn.microsoft.com/en-us/ef/core/querying/tracking#no-tracking-queries)
- [EF Core SaveChangesInterceptor — DR-008 immutability](https://learn.microsoft.com/en-us/ef/core/logging-events-diagnostics/interceptors#savechanges-interception)
- [OWASP A01 — page-size clamp prevents resource exhaustion](https://owasp.org/Top10/A01_2021-Broken_Access_Control/)
- [OWASP A02 — no passwords/AuthCredentials in OldValues/NewValues](https://owasp.org/Top10/A02_2021-Cryptographic_Failures/)
- [DR-008 — AuditLog immutable append-only table](../.propel/context/docs/design.md)
- [DR-012 — AuditLog indefinite retention for HIPAA](../.propel/context/docs/design.md)
- [NFR-007 — Immutable audit entries for all patient data access](../.propel/context/docs/design.md)
- [TR-018 — Serilog structured logging + hash chain for tamper detection](../.propel/context/docs/design.md)

---

## Build Commands

```bash
cd server
dotnet restore PropelIQ.slnx
dotnet build PropelIQ.slnx --configuration Debug
```

---

## Implementation Validation Strategy

- [ ] Unit test: `AuditLogger.LogAsync` genesis entry — `PreviousHash` is `null`; `ChainHash` computed from `"GENESIS"` sentinel
- [ ] Unit test: `AuditLogger.LogAsync` subsequent entry — `PreviousHash` equals `ChainHash` of prior row; `ChainHash` recomputed correctly
- [ ] Unit test: `AuditLogger.LogAsync` in background-job context (`IHttpContextAccessor.HttpContext == null`) → `IpAddress == "system"` (no exception)
- [ ] Unit test: Mutating a staged `AuditLog` entry on the same `DbContext` → `AuditLogImmutabilityInterceptor` throws `InvalidOperationException` on `SaveChanges`
- [ ] Unit test: `AuditLogController.GetLogs` with Admin JWT + date filters → returns `AuditLogPagedResult` with correct `TotalPages` calculation
- [ ] Integration test: `GET /api/v1/audit-logs` with Staff JWT → 403 Forbidden
- [ ] Integration test: Service calls `IAuditLogger.LogAsync` then `SaveChangesAsync` → both main entity and `AuditLog` committed in one transaction; failure aborts both (AC-5)
- [ ] Security test: `AuditLogDto` response body contains no `AuthCredentials` or raw password data

---

## Implementation Checklist

- [ ] Add `IpAddress`, `OldValues`, `NewValues`, `PreviousHash`, `ChainHash` properties to `AuditLog.cs`; add `TargetEntityType` property if absent from the entity file (authoritative source: ModelSnapshot)
- [ ] Add EF Core column configs in `AuditLogConfiguration.cs` for all five new properties with correct `HasColumnName`, `HasColumnType`, and nullability; `chain_hash` is `IsRequired()`
- [ ] Create `IAuditLogger` interface with single `LogAsync` method matching the signature in Part C; place in `PatientAccess.Application.Infrastructure`
- [ ] Create `AuditLogger`: query last `ChainHash` with `AsNoTracking().OrderByDescending(CreatedAt).Select(ChainHash).FirstOrDefaultAsync()`; compute SHA-256 via `SHA256.HashData`; capture IP from `IHttpContextAccessor` with `"system"` fallback; call `db.AuditLogs.Add()` — no `SaveChangesAsync`
- [ ] Create `AuditLogController` with `GET /api/v1/audit-logs` using `[Authorize(Roles="Admin")]`; clamp `pageSize` to `[1, 100]`; chain nullable `Where` predicates; use `AsNoTracking()` + `OrderByDescending(CreatedAt)` + `Skip/Take`
- [ ] Create `AuditLogDto` record and `AuditLogPagedResult` record in `PatientAccess.Application/DTOs/`
- [ ] Modify `ServiceCollectionExtensions.cs`: call `services.AddHttpContextAccessor()` + `services.AddScoped<IAuditLogger, AuditLogger>()`
- [ ] Security review: verify no `AuthCredentials`, raw passwords, or encryption keys appear in `OldValues`/`NewValues`/`Payload` columns across all `IAuditLogger.LogAsync` call sites added in prior tasks (US_024/task_002 — `AuthService`, US_025/task_002 — `UserManagementService`)

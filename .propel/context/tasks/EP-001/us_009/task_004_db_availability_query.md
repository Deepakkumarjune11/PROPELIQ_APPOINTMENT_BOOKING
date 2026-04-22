# Task - task_004_db_availability_query

## Requirement Reference

- **User Story**: US_009 — Appointment Availability Search
- **Story Location**: `.propel/context/tasks/EP-001/us_009/us_009.md`
- **Acceptance Criteria**:
  - AC-1: System returns available slots within 2 seconds at p95 — requires an index on `SlotDatetime` + `Status` to avoid full table scans.
  - AC-3: Cache-aside falls through to DB on cache expiry — the underlying query must be efficient.
- **Edge Cases**:
  - Soft-deleted appointments (`IsDeleted = true`) must be excluded (DR-017).
  - Only `AppointmentStatus.Available` rows represent bookable slots; statuses `Booked`, `Arrived`, `Completed`, `Cancelled`, and `NoShow` must be excluded.

---

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | No |
| **Figma URL** | N/A |
| **Wireframe Status** | N/A |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | N/A |
| **Screen Spec** | N/A |
| **UXR Requirements** | N/A |
| **Design Tokens** | N/A |

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| ORM | Entity Framework Core | 8.0 |
| Database | PostgreSQL | 15.x |
| Language | C# | 12 (.NET 8) |
| Migration tooling | EF Core CLI (`dotnet-ef`) | 8.0 |
| AI/ML | N/A | N/A |
| Mobile | N/A | N/A |

> All code and libraries MUST be compatible with versions above.

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

Establish the **data access layer** for availability queries within the `PatientAccess` bounded context:

1. Add `AppointmentStatus.Available` value to the `AppointmentStatus` enum so the domain correctly represents an unclaimed (bookable) slot.
2. Define the `IAvailabilityRepository` interface in `PatientAccess.Application` (dependency inversion).
3. Implement `AvailabilityRepository` in `PatientAccess.Data` using EF Core, applying the soft-delete filter and status filter.
4. Add a composite PostgreSQL index on `(slot_datetime, status)` — filtered to `is_deleted = false` — so the availability range query executes with an Index Scan instead of a Sequential Scan, satisfying the p95 ≤ 2s NFR-001 requirement.
5. Generate and apply the EF Core migration adding the index.

---

## Dependent Tasks

- None — this is a foundational data task. `task_003_be_availability_api.md` depends on this task.

---

## Impacted Components

| Action | Module | Description |
|--------|--------|-------------|
| MODIFY | `PatientAccess.Domain` | `Enums/AppointmentStatus.cs` — add `Available = 0` value |
| CREATE | `PatientAccess.Application` | `Repositories/IAvailabilityRepository.cs` — query interface |
| CREATE | `PatientAccess.Data` | `Repositories/AvailabilityRepository.cs` — EF Core implementation |
| MODIFY | `PatientAccess.Data` | `Configurations/AppointmentConfiguration.cs` — add composite index |
| CREATE | `PatientAccess.Data` | `Migrations/<timestamp>_AddAvailabilityIndex.cs` — EF Core migration |

---

## Implementation Plan

1. **Extend `AppointmentStatus` enum** — Add `Available = 0` as the default/initial state for a pre-allocated time slot that has not yet been claimed. Existing values (`Booked`, `Arrived`, `Completed`, `Cancelled`, `NoShow`) remain unchanged. Confirm the enum is in `PatientAccess.Domain/Enums/AppointmentStatus.cs`.

2. **`IAvailabilityRepository` interface** (`PatientAccess.Application/Repositories/IAvailabilityRepository.cs`):
   ```csharp
   public interface IAvailabilityRepository
   {
       Task<IReadOnlyList<Appointment>> GetAvailableSlotsAsync(
           DateOnly startDate,
           DateOnly endDate,
           CancellationToken ct = default);
   }
   ```
   Lives in `PatientAccess.Application` so the handler (`task_003`) depends only on the interface, not the EF Core implementation (SOLID — Dependency Inversion).

3. **`AvailabilityRepository` implementation** (`PatientAccess.Data/Repositories/AvailabilityRepository.cs`):
   ```csharp
   public sealed class AvailabilityRepository : IAvailabilityRepository
   {
       private readonly PropelIQDbContext _db;
   
       public AvailabilityRepository(PropelIQDbContext db) => _db = db;
   
       public async Task<IReadOnlyList<Appointment>> GetAvailableSlotsAsync(
           DateOnly startDate, DateOnly endDate, CancellationToken ct = default)
       {
           var from = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
           var to   = endDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
   
           return await _db.Appointments
               .AsNoTracking()
               .Where(a => !a.IsDeleted                          // DR-017 soft-delete filter
                        && a.Status == AppointmentStatus.Available
                        && a.SlotDatetime >= from
                        && a.SlotDatetime <= to)
               .OrderBy(a => a.SlotDatetime)
               .ToListAsync(ct);
       }
   }
   ```
   - `AsNoTracking()` — read-only query; avoids change-tracking overhead (performance).
   - `OrderBy(SlotDatetime)` — deterministic result ordering for predictable UI rendering.
   - `ToListAsync(ct)` — honors cancellation token (request abort).

4. **Composite index in `AppointmentConfiguration`** — Add to `AppointmentConfiguration.Configure()`:
   ```csharp
   builder.HasIndex(a => new { a.SlotDatetime, a.Status })
          .HasFilter("is_deleted = false")
          .HasDatabaseName("ix_appointments_slot_datetime_status_active");
   ```
   - Composite on `(slot_datetime, status)` supports the range + equality predicates in the query.
   - Partial index (`WHERE is_deleted = false`) reduces index size by excluding soft-deleted rows.
   - `HasDatabaseName` ensures a deterministic, descriptive index name in PostgreSQL.

5. **EF Core migration** — Run:
   ```bash
   dotnet ef migrations add AddAvailabilityIndex \
     --project src/Modules/PatientAccess/PatientAccess.Data \
     --startup-project src/PropelIQ.Api
   ```
   Review the generated migration: confirm `CreateIndex` adds `ix_appointments_slot_datetime_status_active` with the partial filter. Apply with `dotnet ef database update`.

6. **Zero-downtime consideration** (DR-014) — The `CREATE INDEX` operation in PostgreSQL can use `CONCURRENTLY` to avoid locking the `appointments` table. EF Core migrations do not generate `CONCURRENTLY` automatically; if this migration is applied to a live database with data, update the generated migration file's `Up` method to use raw SQL:
   ```csharp
   migrationBuilder.Sql(
       "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_appointments_slot_datetime_status_active " +
       "ON appointments (slot_datetime, status) WHERE is_deleted = false;",
       suppressTransaction: true);  // CONCURRENTLY cannot run inside a transaction
   ```
   Set `suppressTransaction: true` for the migration (DR-014 zero-downtime).

---

## Current Project State

```
server/src/Modules/PatientAccess/
  PatientAccess.Domain/
    (Enums folder may not exist yet — create)
  PatientAccess.Application/
    Infrastructure/
      ICacheService.cs          ← available
    Class1.cs                   ← placeholder
  PatientAccess.Data/
    PropelIQDbContext.cs         ← DbSet<Appointment> available
    Entities/
      Appointment.cs             ← Status, SlotDatetime, IsDeleted, NoShowRiskScore
    Configurations/
      AppointmentConfiguration.cs ← MODIFY: add composite index
    Migrations/                  ← existing migrations
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Domain/Enums/AppointmentStatus.cs` | Add `Available = 0` value |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Repositories/IAvailabilityRepository.cs` | Repository interface in Application layer |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Data/Repositories/AvailabilityRepository.cs` | EF Core implementation with soft-delete + status filter |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Data/Configurations/AppointmentConfiguration.cs` | Add composite partial index on (slot_datetime, status) WHERE is_deleted = false |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Data/Migrations/<timestamp>_AddAvailabilityIndex.cs` | EF Core migration adding the composite index |

---

## External References

- [EF Core 8 — HasIndex, HasFilter (partial indexes)](https://learn.microsoft.com/en-us/ef/core/modeling/indexes)
- [PostgreSQL — Partial Indexes](https://www.postgresql.org/docs/15/indexes-partial.html)
- [PostgreSQL — CREATE INDEX CONCURRENTLY](https://www.postgresql.org/docs/15/sql-createindex.html#SQL-CREATEINDEX-CONCURRENTLY)
- [EF Core 8 — AsNoTracking for read-only queries](https://learn.microsoft.com/en-us/ef/core/querying/tracking)
- [EF Core CLI — dotnet ef migrations add](https://learn.microsoft.com/en-us/ef/core/cli/dotnet#dotnet-ef-migrations-add)
- [EF Core — suppressTransaction for raw SQL migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/operations#using-migrationbuilder-sql)

---

## Build Commands

```bash
# From server/ — restore packages
dotnet restore

# Build to catch compile errors after enum change
dotnet build PropelIQ.slnx

# Add EF Core migration
dotnet ef migrations add AddAvailabilityIndex \
  --project src/Modules/PatientAccess/PatientAccess.Data \
  --startup-project src/PropelIQ.Api

# Apply migration (development)
dotnet ef database update \
  --project src/Modules/PatientAccess/PatientAccess.Data \
  --startup-project src/PropelIQ.Api

# Verify index in PostgreSQL (psql)
# \d appointments
# Should show: ix_appointments_slot_datetime_status_active on (slot_datetime, status) WHERE (is_deleted = false)
```

---

## Implementation Validation Strategy

- [ ] Unit tests pass — `AvailabilityRepository.GetAvailableSlotsAsync` returns only `Status = Available` rows in the date range
- [ ] Unit tests pass — soft-deleted appointments (`IsDeleted = true`) are excluded from results
- [ ] Unit tests pass — results are ordered by `SlotDatetime` ascending
- [ ] Integration test (Testcontainers) — query against seeded data returns correct slots within range
- [ ] Migration file generated — `CreateIndex` targets `(slot_datetime, status)` with `WHERE is_deleted = false`
- [ ] Migration uses `suppressTransaction: true` with `CREATE INDEX CONCURRENTLY IF NOT EXISTS` (DR-014)
- [ ] `dotnet build` passes with zero errors after `AppointmentStatus.Available` is added
- [ ] `AppointmentConfiguration` includes `HasFilter("is_deleted = false")` on the composite index
- [ ] `IAvailabilityRepository` is in `PatientAccess.Application` (not `PatientAccess.Data`) — dependency inversion confirmed

---

## Implementation Checklist

- [X] Locate `PatientAccess.Domain/Enums/AppointmentStatus.cs` (or create `Enums/` folder) and add `Available = 0` value
- [X] Create `PatientAccess.Application/Repositories/IAvailabilityRepository.cs` — interface with `GetAvailableSlotsAsync(DateOnly, DateOnly, CancellationToken)`
- [X] Create `PatientAccess.Data/Repositories/AvailabilityRepository.cs` — EF Core implementation with `AsNoTracking()`, `!IsDeleted`, `Status == Available`, date range filter, `OrderBy(SlotDatetime)`
- [X] Modify `AppointmentConfiguration.cs` — add `HasIndex(slot_datetime, status).HasFilter("is_deleted = false").HasDatabaseName("ix_appointments_slot_datetime_status_active")`
- [X] Run `dotnet ef migrations add AddAvailabilityIndex` — verify generated migration is correct
- [X] Update generated migration `Up()` to use `CREATE INDEX CONCURRENTLY IF NOT EXISTS` with `suppressTransaction: true` (DR-014)
- [X] Run `dotnet ef database update` in development environment
- [X] Verify index exists in PostgreSQL via `\d appointments` or `pg_indexes` system view
- [X] Register `AvailabilityRepository` → `IAvailabilityRepository` in `PatientAccess.Presentation/ServiceCollectionExtensions.cs` (used by task_003)

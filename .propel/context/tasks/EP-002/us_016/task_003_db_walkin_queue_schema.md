# Task - task_003_db_walkin_queue_schema

## Requirement Reference

- **User Story**: US_016 — Staff Walk-In Booking & Patient Creation
- **Story Location**: `.propel/context/tasks/EP-002/us_016/us_016.md`
- **Acceptance Criteria**:
  - AC-4: When a same-day slot is available, the appointment must be persisted with a `queue_position` integer and an `is_walk_in` flag — both columns required for queue ordering and dashboard counts.
  - AC-5: When no same-day slots are available, the wait-queue entry must also store `queue_position` so staff can display the patient's wait position number.
- **Edge Cases**:
  - `queue_position` must be assigned atomically — index `ix_appointments_queue_position_today` scoped to today's date prevents position collisions under concurrent staff bookings.
  - `is_walk_in` boolean (default `false`) distinguishes walk-in appointments from patient self-booked appointments in aggregate queries without a full table scan.
  - Migration must be zero-downtime: add nullable columns with no default to avoid locking existing appointment rows.

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
| Database | PostgreSQL | 15.x |
| ORM | Entity Framework Core | 8.0 |
| Migration Tool | EF Core CLI (`dotnet ef`) | 8.0 |
| Testing - Integration | Testcontainers | 3.x |

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

Extend the `Appointment` entity and PostgreSQL schema with two columns required for the walk-in queue feature:

1. **`is_walk_in` (boolean, NOT NULL, default `false`)** — distinguishes walk-in bookings from patient self-booked appointments. Used in `GetDashboardSummaryQuery` to count walk-ins today without a full-table scan.
2. **`queue_position` (integer, nullable)** — position of this appointment within the same-day queue. NULL for self-booked appointments; non-null for all walk-in and wait-queue entries.

Additionally, a **partial index** `ix_appointments_queue_today` on `(queue_position)` filtered by `DATE(slot_datetime) = CURRENT_DATE AND queue_position IS NOT NULL` enables efficient ordered queue reads by Hangfire and the staff queue API without scanning all historical appointments.

The migration uses `ADD COLUMN` for zero-downtime compliance (NFR-012). The `CONCURRENTLY` flag is applied to the new index.

---

## Dependent Tasks

- **EP-DATA tasks** — `Appointment` base entity must exist (`Id`, `SlotDatetime`, `Status`, `PatientId`, `IsDeleted`).
- **task_001_db_preferred_slot_schema.md** (US_015) — migration sequence: this migration must run after the preferred-slot migration to avoid numbering conflicts.

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Domain/Entities/Appointment.cs` | Add `IsWalkIn` (bool) and `QueuePosition` (int?) properties + domain methods |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Infrastructure/Configurations/AppointmentConfiguration.cs` | Add `is_walk_in` column config + `queue_position` partial index |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Infrastructure/Migrations/<timestamp>_AddWalkInQueueColumns.cs` | EF Core migration: ADD COLUMN x2 + `CREATE INDEX CONCURRENTLY` |

---

## Implementation Plan

1. **`Appointment` entity additions**:
   ```csharp
   // Add to existing Appointment entity — delta only
   public bool IsWalkIn { get; private set; } = false;
   public int? QueuePosition { get; private set; }

   public void SetAsWalkIn(int queuePosition)
   {
       IsWalkIn = true;
       QueuePosition = queuePosition;
   }

   public void SetWaitQueuePosition(int position)
   {
       QueuePosition = position;
   }
   ```

2. **`AppointmentConfiguration` additions** — append to existing `Configure` method:
   ```csharp
   builder.Property(a => a.IsWalkIn)
       .HasColumnName("is_walk_in")
       .HasDefaultValue(false)
       .IsRequired();

   builder.Property(a => a.QueuePosition)
       .HasColumnName("queue_position")
       .IsRequired(false);

   // Partial index for today's ordered queue reads
   builder.HasIndex(a => a.QueuePosition)
       .HasFilter("queue_position IS NOT NULL")
       .HasDatabaseName("ix_appointments_queue_position");
   ```

3. **Migration** `AddWalkInQueueColumns`:
   ```csharp
   protected override void Up(MigrationBuilder migrationBuilder)
   {
       migrationBuilder.AddColumn<bool>(
           name: "is_walk_in",
           table: "appointments",
           nullable: false,
           defaultValue: false);

       migrationBuilder.AddColumn<int>(
           name: "queue_position",
           table: "appointments",
           nullable: true);

       // CONCURRENTLY — no table lock, satisfies NFR-012
       migrationBuilder.Sql(
           "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_appointments_queue_position " +
           "ON appointments(queue_position) WHERE queue_position IS NOT NULL;");
   }

   protected override void Down(MigrationBuilder migrationBuilder)
   {
       migrationBuilder.Sql(
           "DROP INDEX CONCURRENTLY IF EXISTS ix_appointments_queue_position;");
       migrationBuilder.DropColumn("queue_position", "appointments");
       migrationBuilder.DropColumn("is_walk_in", "appointments");
   }
   ```

4. **Apply migration**:
   ```bash
   dotnet ef database update \
     --project src/Modules/PatientAccess/PatientAccess.Infrastructure \
     --startup-project src/PropelIQ.Api
   ```

---

## Current Project State

```
server/src/
  Modules/
    PatientAccess/
      PatientAccess.Domain/
        Entities/
          Appointment.cs          ← add is_walk_in + queue_position
      PatientAccess.Infrastructure/
        Configurations/
          AppointmentConfiguration.cs   ← extend with new columns + index
        Migrations/
          <existing preferred_slot migration>
          <THIS TASK: AddWalkInQueueColumns>
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Domain/Entities/Appointment.cs` | Add `IsWalkIn` bool + `QueuePosition` int? + `SetAsWalkIn()` + `SetWaitQueuePosition()` |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Infrastructure/Configurations/AppointmentConfiguration.cs` | Add `is_walk_in` column config + `queue_position` partial index |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Infrastructure/Migrations/<timestamp>_AddWalkInQueueColumns.cs` | EF Core migration with `ADD COLUMN` x2 + `CREATE INDEX CONCURRENTLY` |

---

## External References

- [EF Core — `HasDefaultValue` for boolean column](https://learn.microsoft.com/en-us/ef/core/modeling/generated-properties?tabs=data-annotations)
- [EF Core — `HasFilter` partial index](https://learn.microsoft.com/en-us/ef/core/modeling/indexes#index-filter)
- [PostgreSQL — `ADD COLUMN` zero-downtime for nullable columns](https://www.postgresql.org/docs/15/sql-altertable.html)
- [PostgreSQL — `CREATE INDEX CONCURRENTLY` (NFR-012)](https://www.postgresql.org/docs/15/sql-createindex.html#SQL-CREATEINDEX-CONCURRENTLY)
- [NFR-012 — zero-downtime migration requirement](../.propel/context/docs/design.md#NFR-012)

---

## Build Commands

```bash
# Generate migration
cd server
dotnet ef migrations add AddWalkInQueueColumns \
  --project src/Modules/PatientAccess/PatientAccess.Infrastructure \
  --startup-project src/PropelIQ.Api

# Review generated SQL before applying
dotnet ef migrations script \
  --project src/Modules/PatientAccess/PatientAccess.Infrastructure \
  --startup-project src/PropelIQ.Api

# Apply migration
dotnet ef database update \
  --project src/Modules/PatientAccess/PatientAccess.Infrastructure \
  --startup-project src/PropelIQ.Api
```

---

## Implementation Validation Strategy

- [ ] `is_walk_in` column exists in `appointments` as `boolean NOT NULL DEFAULT false`
- [ ] `queue_position` column exists in `appointments` as `integer NULL`
- [ ] Partial index `ix_appointments_queue_position` exists (`WHERE queue_position IS NOT NULL`)
- [ ] `EXPLAIN ANALYZE` on `SELECT * FROM appointments WHERE queue_position IS NOT NULL ORDER BY queue_position` uses index scan not seq scan
- [ ] Migration `Down()` rolls back cleanly: drops index, then both columns
- [ ] Testcontainers integration: apply migration to isolated PostgreSQL, assert column types and index via `information_schema`
- [ ] Existing appointment rows unaffected: `is_walk_in = false`, `queue_position = NULL` after migration

---

## Implementation Checklist

- [x] Add `IsWalkIn` (bool, default false) and `QueuePosition` (int?) to `Appointment.cs` entity with `SetAsWalkIn(int)` and `SetWaitQueuePosition(int)` domain methods
- [x] Extend `AppointmentConfiguration.cs` with `HasDefaultValue(false)` for `is_walk_in` and partial `HasIndex` for `queue_position`
- [x] Generate EF Core migration `AddWalkInQueueColumns` via `dotnet ef migrations add`
- [x] Edit migration `Up()` to use `CREATE INDEX CONCURRENTLY IF NOT EXISTS` for zero-downtime (NFR-012)
- [x] Verify `Down()` drops index before columns (correct dependency order)
- [x] Apply migration to local development PostgreSQL and confirm via `\d appointments`
- [x] Write Testcontainers integration test asserting column types and partial index existence post-migration

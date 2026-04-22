# Task - task_004_db_preferred_slot_schema

## Requirement Reference

- **User Story**: US_015 — Preferred Slot Swap Watchlist
- **Story Location**: `.propel/context/tasks/EP-001/us_015/us_015.md`
- **Acceptance Criteria**:
  - AC-2: When the patient confirms a preferred slot, `preferred_slot_id` is updated in the Appointment record — column must exist with correct FK constraint.
  - AC-3: Atomic swap requires efficient watchlist polling — a partial index on `preferred_slot_id WHERE preferred_slot_id IS NOT NULL` enables O(watchlist-size) queries, not O(all-appointments).
  - AC-4: After swap executes, `preferred_slot_id` is set to `NULL` — column must be nullable.
- **Edge Cases**:
  - `preferred_slot_id` must be a nullable self-referencing FK to `Appointment.Id` — points to the target slot row (or a slot-record row, consistent with existing data model).
  - Migration must be zero-downtime: add nullable column with no default (no table lock on existing rows in PostgreSQL).
  - If EP-DATA tasks already created `preferred_slot_id` (DR-002 calls for it), this task only adds the missing index and verifies the EF Core navigation property — do NOT re-create what exists.

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

Establish the database schema changes required by the swap watchlist feature. DR-002 specifies the `Appointment` entity includes an "optional preferred-slot foreign key for swap watchlist"; this task verifies and completes the EF Core entity configuration and generates the migration.

Two deliverables:
1. **EF Core entity configuration** — self-referencing optional FK (`preferred_slot_id`) on `Appointment`, with correct navigation properties used by `SlotSwapService` and `GetPatientAppointmentsQuery`.
2. **PostgreSQL partial index** — `CREATE INDEX CONCURRENTLY ix_appointments_preferred_slot_id ON appointments(preferred_slot_id) WHERE preferred_slot_id IS NOT NULL` — enables the Hangfire job to retrieve only watchlist entries in milliseconds, regardless of total appointment count.

If EP-DATA already added `preferred_slot_id` to `Appointments`, this task operates in **delta mode**: verifies the column exists, adds the partial index (if absent), and ensures the EF Core navigation property is configured correctly.

---

## Dependent Tasks

- **EP-DATA tasks** — `Appointment` entity base must exist with `Id`, `SlotDatetime`, `Status`, `PatientId`, `ProviderId`, `IsDeleted`.

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Domain/Entities/Appointment.cs` | Add `PreferredSlotId` (Guid?) property and `PreferredSlot` navigation property (if absent) |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Infrastructure/Configurations/AppointmentConfiguration.cs` | EF Core `IEntityTypeConfiguration<Appointment>` — self-referencing optional FK + partial index |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Infrastructure/Migrations/<timestamp>_AddPreferredSlotWatchlist.cs` | EF Core migration: ADD COLUMN (if absent) + CREATE INDEX CONCURRENTLY |

---

## Implementation Plan

1. **`Appointment` entity** — delta-safe additions:
   ```csharp
   // Only add if not already present from EP-DATA task
   public Guid? PreferredSlotId { get; private set; }
   public Appointment? PreferredSlot { get; private set; }   // navigation property

   // Domain method (encapsulate mutation within entity)
   public void SetPreferredSlot(Guid? preferredSlotId)
   {
       PreferredSlotId = preferredSlotId;
   }
   ```

2. **`AppointmentConfiguration`** — EF Core Fluent API:
   ```csharp
   public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
   {
       public void Configure(EntityTypeBuilder<Appointment> builder)
       {
           // Self-referencing optional FK
           builder.HasOne(a => a.PreferredSlot)
               .WithMany()
               .HasForeignKey(a => a.PreferredSlotId)
               .IsRequired(false)
               .OnDelete(DeleteBehavior.SetNull);   // if preferred slot record deleted → set null

           // Partial index for O(watchlist-size) watchlist polling
           builder.HasIndex(a => a.PreferredSlotId)
               .HasFilter("preferred_slot_id IS NOT NULL")
               .HasDatabaseName("ix_appointments_preferred_slot_id");
       }
   }
   ```

3. **Migration generation**:
   ```bash
   cd server
   dotnet ef migrations add AddPreferredSlotWatchlist \
     --project src/Modules/PatientAccess/PatientAccess.Infrastructure \
     --startup-project src/PropelIQ.Api
   ```
   The generated migration `Up()` method MUST use `CONCURRENTLY` for index creation to satisfy NFR-012 (zero-downtime):
   ```csharp
   protected override void Up(MigrationBuilder migrationBuilder)
   {
       // Only if column does not already exist (idempotent guard)
       migrationBuilder.AddColumn<Guid>(
           name: "preferred_slot_id",
           table: "appointments",
           nullable: true,
           defaultValue: null);

       migrationBuilder.AddForeignKey(
           name: "fk_appointments_preferred_slot_id",
           table: "appointments",
           column: "preferred_slot_id",
           principalTable: "appointments",
           principalColumn: "id",
           onDelete: ReferentialAction.SetNull);

       // CONCURRENTLY — no table lock, satisfies NFR-012
       migrationBuilder.Sql(
           "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_appointments_preferred_slot_id " +
           "ON appointments(preferred_slot_id) WHERE preferred_slot_id IS NOT NULL;");
   }

   protected override void Down(MigrationBuilder migrationBuilder)
   {
       migrationBuilder.Sql(
           "DROP INDEX CONCURRENTLY IF EXISTS ix_appointments_preferred_slot_id;");

       migrationBuilder.DropForeignKey("fk_appointments_preferred_slot_id", "appointments");

       migrationBuilder.DropColumn("preferred_slot_id", "appointments");
   }
   ```

4. **Verify referential integrity** (DR-011): `ON DELETE SET NULL` ensures that if a referenced appointment row is soft-deleted or actually deleted, the FK is set to NULL — no orphaned watchlist entries pointing to deleted slots.

5. **Apply migration** (development):
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
          Appointment.cs       ← verify preferred_slot_id presence
      PatientAccess.Infrastructure/
        Configurations/        ← add AppointmentConfiguration.cs
        Migrations/            ← generate AddPreferredSlotWatchlist migration
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Domain/Entities/Appointment.cs` | Add `PreferredSlotId` (Guid?) + `PreferredSlot` nav property + `SetPreferredSlot()` domain method if absent |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Infrastructure/Configurations/AppointmentConfiguration.cs` | EF Core Fluent API: optional self-referencing FK + partial index with `HasFilter` |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Infrastructure/Migrations/<timestamp>_AddPreferredSlotWatchlist.cs` | EF Core migration: nullable column + FK + `CREATE INDEX CONCURRENTLY` |

---

## External References

- [EF Core — self-referencing relationships](https://learn.microsoft.com/en-us/ef/core/modeling/relationships/self-referencing)
- [EF Core — `HasFilter` for partial indexes](https://learn.microsoft.com/en-us/ef/core/modeling/indexes#index-filter)
- [PostgreSQL — `CREATE INDEX CONCURRENTLY` (zero-downtime)](https://www.postgresql.org/docs/15/sql-createindex.html#SQL-CREATEINDEX-CONCURRENTLY)
- [EF Core migrations — `migrationBuilder.Sql` for raw SQL](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/operations)
- [DR-002 — Appointment entity preferred-slot FK specification](../.propel/context/docs/design.md#DR-002)
- [NFR-012 — Zero-downtime database migration requirement](../.propel/context/docs/design.md#NFR-012)

---

## Build Commands

```bash
# Generate migration
cd server
dotnet ef migrations add AddPreferredSlotWatchlist \
  --project src/Modules/PatientAccess/PatientAccess.Infrastructure \
  --startup-project src/PropelIQ.Api

# Apply migration
dotnet ef database update \
  --project src/Modules/PatientAccess/PatientAccess.Infrastructure \
  --startup-project src/PropelIQ.Api

# Verify migration SQL (review before apply)
dotnet ef migrations script \
  --project src/Modules/PatientAccess/PatientAccess.Infrastructure \
  --startup-project src/PropelIQ.Api
```

---

## Implementation Validation Strategy

- [ ] `Appointment.preferred_slot_id` column exists in PostgreSQL as `uuid NULL`
- [ ] FK constraint `fk_appointments_preferred_slot_id` references `appointments(id)` with `ON DELETE SET NULL`
- [ ] Partial index `ix_appointments_preferred_slot_id` exists (`WHERE preferred_slot_id IS NOT NULL`)
- [ ] EF Core query `WHERE preferred_slot_id IS NOT NULL` uses the partial index (verify via `EXPLAIN ANALYZE`)
- [ ] Testcontainers integration test: apply migration to isolated PostgreSQL container, assert schema matches expectations
- [ ] Migration `Down()` rolls back cleanly (drops index → drops FK → drops column)
- [ ] Zero table lock during migration (index created with `CONCURRENTLY`)

---

## Implementation Checklist

- [x] Inspect `Appointment.cs` entity — add `PreferredSlotId` (Guid?), `PreferredSlot` (Appointment?), and `SetPreferredSlot(Guid?)` if not already present from EP-DATA
- [x] Create `AppointmentConfiguration.cs` with self-referencing optional FK (`OnDelete = SetNull`) and partial index (`HasFilter("preferred_slot_id IS NOT NULL")`)
- [x] Generate EF Core migration `AddPreferredSlotWatchlist` using `dotnet ef migrations add`
- [x] Manually edit migration `Up()` to use `CREATE INDEX CONCURRENTLY IF NOT EXISTS` for zero-downtime compliance (NFR-012)
- [x] Verify `Down()` rollback: drops index, FK, and column in correct order
- [x] Apply migration to local development PostgreSQL and confirm schema via `\d appointments` in psql
- [x] Write Testcontainers integration test asserting column, FK, and index existence post-migration

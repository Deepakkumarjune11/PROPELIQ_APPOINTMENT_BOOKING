# Task - task_002_db_audit_schema_migration

## Requirement Reference

- **User Story**: US_026 — Immutable Audit Log & Compliance Logging
- **Story Location**: `.propel/context/tasks/EP-006/us_026/us_026.md`
- **Acceptance Criteria**:
  - AC-1: `audit_log` table gains explicit `ip_address`, `old_values`, `new_values` columns enabling per-row field-level audit capture per FR-016 and DR-012.
  - AC-2: PostgreSQL-level BEFORE UPDATE / BEFORE DELETE triggers (`trg_audit_log_no_update`, `trg_audit_log_no_delete`) backed by function `fn_audit_log_immutable()` **raise a hard exception** for any attempt to modify or remove an audit row — enforcement survives even if the application layer is bypassed (e.g., direct `psql` access) per NFR-007.
  - AC-3: Three composite indexes added via `CREATE INDEX CONCURRENTLY IF NOT EXISTS` ensure paginated queries over 1M rows complete within 2s per NFR-013.
  - AC-4: `previous_hash` (text, nullable) and `chain_hash` (text, NOT NULL DEFAULT '') columns added — populated by `AuditLogger` service (task_001) at insert time; existing rows receive empty-string defaults.
- **Edge Cases**:
  - `chain_hash` column must be `NOT NULL DEFAULT ''` so that the `ALTER TABLE ADD COLUMN` succeeds without a table rewrite on a non-empty table (existing rows get `''`; new rows will always be populated by `AuditLogger`).
  - `previous_hash` is nullable — genesis entry will store `NULL` (no predecessor).
  - `DROP INDEX CONCURRENTLY IF EXISTS` in `Down()` must precede the `ALTER TABLE DROP COLUMN` statements to avoid orphaned index artefacts.
  - Triggers must be dropped before the function in `Down()` to satisfy PostgreSQL dependency ordering.

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
| ORM / Migration | Entity Framework Core | 8.0 |
| Database | PostgreSQL | 15.x |
| Language | C# | 12 |

> **Current `audit_log` schema** (ModelSnapshot state):
> Columns: `id` (uuid PK), `action_type` (varchar 50), `actor_id` (uuid), `actor_type` (varchar 20), `created_at` (timestamptz DEFAULT NOW()), `payload` (jsonb NOT NULL), `target_entity_id` (uuid), `target_entity_type` (varchar 100).
> Indexes: `ix_audit_log_actor_id`, `ix_audit_log_created_at`, `ix_audit_log_target`.
> No DB-level triggers — only EF Core `SaveChangesInterceptor` currently guards mutability.

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

Create the EF Core migration `AddAuditLogComplianceSchema` that hardens the `audit_log` table for US_026. This migration:

1. **Adds five new columns** (`ip_address`, `old_values`, `new_values`, `previous_hash`, `chain_hash`) using `ALTER TABLE ADD COLUMN` patterns — no table rewrite, zero downtime.
2. **Installs PostgreSQL triggers** that raise a hard exception on any UPDATE or DELETE to `audit_log`, enforcing the immutability contract at the database layer independent of the application (AC-2 / NFR-007).
3. **Adds three composite indexes** via `CREATE INDEX CONCURRENTLY IF NOT EXISTS` targeting the filter combinations used by `AuditLogController` (date-range + actor, entity-type + date, action-type) to guarantee the `<2s` query target at 1M rows (AC-3 / NFR-013).
4. Provides a safe `Down()` that drops triggers → function → indexes → columns in dependency order.

---

## Dependent Tasks

- **task_001_be_audit_logger_api.md** (US_026) — must be completed first; the entity C# model changes (new properties) must exist before EF Core migration scaffolding can reference them.
- **Existing migrations in sequence**: `20260416095359_AddStaffAdminSchema` is currently the latest migration. The new migration must follow this migration in the sequence.

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Data/Migrations/<timestamp>_AddAuditLogComplianceSchema.cs` | Up/Down migration with new columns, triggers, indexes |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Data/Migrations/PropelIQDbContextModelSnapshot.cs` | Auto-updated by EF tooling to reflect 5 new columns |

---

## Implementation Plan

### Step 1 — Generate migration scaffold

```bash
cd server/src/Modules/PatientAccess/PatientAccess.Data
dotnet ef migrations add AddAuditLogComplianceSchema \
  --context PropelIQDbContext \
  --project . \
  --startup-project ../../../../PropelIQ.Api
```

### Step 2 — Replace generated `Up()` body

After scaffold generation, replace the `Up()` body with the following (EF-generated `CREATE TABLE` / `AddColumn` calls are insufficient for triggers and `CONCURRENTLY` indexes):

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // ── New compliance columns ────────────────────────────────────────────────

    migrationBuilder.AddColumn<string>(
        name: "ip_address",
        table: "audit_log",
        type: "character varying(45)",
        maxLength: 45,
        nullable: true);

    migrationBuilder.AddColumn<string>(
        name: "old_values",
        table: "audit_log",
        type: "jsonb",
        nullable: true);

    migrationBuilder.AddColumn<string>(
        name: "new_values",
        table: "audit_log",
        type: "jsonb",
        nullable: true);

    migrationBuilder.AddColumn<string>(
        name: "previous_hash",
        table: "audit_log",
        type: "text",
        nullable: true);

    // chain_hash is NOT NULL; existing rows receive '' — AuditLogger populates on all new inserts.
    migrationBuilder.AddColumn<string>(
        name: "chain_hash",
        table: "audit_log",
        type: "text",
        nullable: false,
        defaultValue: "");

    // ── DB-level immutability triggers (AC-2 / NFR-007) ──────────────────────
    // These fire even when the application layer is bypassed (direct psql, migrations).
    // The EF Core SaveChangesInterceptor remains as the in-process guard.

    migrationBuilder.Sql("""
        CREATE OR REPLACE FUNCTION fn_audit_log_immutable()
        RETURNS TRIGGER
        LANGUAGE plpgsql AS
        $$
        BEGIN
            RAISE EXCEPTION 'audit_log records are immutable: UPDATE and DELETE are prohibited per NFR-007 and DR-008';
        END;
        $$;
        """);

    migrationBuilder.Sql("""
        CREATE TRIGGER trg_audit_log_no_update
            BEFORE UPDATE ON audit_log
            FOR EACH ROW
            EXECUTE FUNCTION fn_audit_log_immutable();
        """);

    migrationBuilder.Sql("""
        CREATE TRIGGER trg_audit_log_no_delete
            BEFORE DELETE ON audit_log
            FOR EACH ROW
            EXECUTE FUNCTION fn_audit_log_immutable();
        """);

    // ── Performance indexes — CONCURRENTLY to avoid table lock (NFR-012) ─────
    // Each uses CONCURRENTLY for zero-downtime; must run outside EF transaction block.
    // Developer note: run via `dotnet ef database update` in a maintenance window or
    // pre-run these SQL statements with `SET lock_timeout = '2s'` in CI.

    // ix_audit_log_action_type — single-column; supports actionType filter in compliance queries
    migrationBuilder.Sql(
        "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_audit_log_action_type " +
        "ON audit_log(action_type);");

    // ix_audit_log_created_at_actor — composite for date-range + actor_id filter (most frequent pattern)
    migrationBuilder.Sql(
        "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_audit_log_created_at_actor " +
        "ON audit_log(created_at DESC, actor_id);");

    // ix_audit_log_entity_type_created_at — composite for entity-type filter + date sort
    migrationBuilder.Sql(
        "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_audit_log_entity_type_created_at " +
        "ON audit_log(target_entity_type, created_at DESC);");
}
```

### Step 3 — Replace generated `Down()` body

```csharp
protected override void Down(MigrationBuilder migrationBuilder)
{
    // Drop indexes FIRST — CONCURRENTLY avoids lock contention during rollback
    migrationBuilder.Sql(
        "DROP INDEX CONCURRENTLY IF EXISTS ix_audit_log_entity_type_created_at;");

    migrationBuilder.Sql(
        "DROP INDEX CONCURRENTLY IF EXISTS ix_audit_log_created_at_actor;");

    migrationBuilder.Sql(
        "DROP INDEX CONCURRENTLY IF EXISTS ix_audit_log_action_type;");

    // Drop triggers before function (PostgreSQL dependency ordering)
    migrationBuilder.Sql(
        "DROP TRIGGER IF EXISTS trg_audit_log_no_delete ON audit_log;");

    migrationBuilder.Sql(
        "DROP TRIGGER IF EXISTS trg_audit_log_no_update ON audit_log;");

    migrationBuilder.Sql(
        "DROP FUNCTION IF EXISTS fn_audit_log_immutable();");

    // Drop columns last
    migrationBuilder.DropColumn(name: "chain_hash",    table: "audit_log");
    migrationBuilder.DropColumn(name: "previous_hash", table: "audit_log");
    migrationBuilder.DropColumn(name: "new_values",    table: "audit_log");
    migrationBuilder.DropColumn(name: "old_values",    table: "audit_log");
    migrationBuilder.DropColumn(name: "ip_address",    table: "audit_log");
}
```

### Step 4 — Apply migration

```bash
cd server
dotnet ef database update AddAuditLogComplianceSchema \
  --project src/Modules/PatientAccess/PatientAccess.Data \
  --startup-project src/PropelIQ.Api
```

### Step 5 — Verify in psql

```sql
-- Verify columns exist
SELECT column_name, data_type, is_nullable
FROM information_schema.columns
WHERE table_name = 'audit_log'
ORDER BY ordinal_position;

-- Verify triggers
SELECT trigger_name, event_manipulation, action_timing
FROM information_schema.triggers
WHERE event_object_table = 'audit_log';

-- Verify new indexes
SELECT indexname, indexdef
FROM pg_indexes
WHERE tablename = 'audit_log';

-- Verify immutability — must raise exception:
UPDATE audit_log SET ip_address = 'test' WHERE id = (SELECT id FROM audit_log LIMIT 1);
-- Expected: ERROR: audit_log records are immutable...
```

---

## Current Project State

```
server/src/
  Modules/
    PatientAccess/
      PatientAccess.Data/
        Migrations/
          20260415120726_Initial.cs
          20260416080103_AddPatientAppointmentSchema.cs
          20260416090558_AddClinicalIntakeSchema.cs
          20260416094749_AddViewsCodesAuditSchema.cs
          20260416095359_AddStaffAdminSchema.cs           ← current latest migration
          <timestamp>_AddAuditLogComplianceSchema.cs      ← THIS TASK (create via dotnet ef)
          PropelIQDbContextModelSnapshot.cs               ← auto-updated by EF tooling
```

Current `audit_log` table has **no** DB-level triggers and lacks the five new compliance columns.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/.../Migrations/<timestamp>_AddAuditLogComplianceSchema.cs` | `Up()`: 5 columns + 2 triggers + function + 3 indexes. `Down()`: reverse all in dependency order |
| MODIFY | `server/.../Migrations/PropelIQDbContextModelSnapshot.cs` | EF tooling auto-update — adds 5 new properties to `AuditLog` entity block |

---

## External References

- [EF Core — Add migration command](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/)
- [PostgreSQL 15 — CREATE TRIGGER](https://www.postgresql.org/docs/15/sql-createtrigger.html)
- [PostgreSQL 15 — PL/pgSQL RAISE EXCEPTION](https://www.postgresql.org/docs/15/plpgsql-errors-and-messages.html)
- [PostgreSQL 15 — CREATE INDEX CONCURRENTLY](https://www.postgresql.org/docs/15/sql-createindex.html#SQL-CREATEINDEX-CONCURRENTLY)
- [NFR-007 — Immutable audit log entries for all data modifications](../.propel/context/docs/design.md)
- [NFR-012 — Zero-downtime migrations; CONCURRENTLY for index creation](../.propel/context/docs/design.md)
- [NFR-013 — HIPAA compliance: 100% audit logging coverage](../.propel/context/docs/design.md)
- [DR-008 — AuditLog immutable append-only per design](../.propel/context/docs/design.md)
- [DR-012 — AuditLog indefinite HIPAA retention](../.propel/context/docs/design.md)

---

## Build Commands

```bash
# Generate scaffold (run from solution root)
cd server
dotnet ef migrations add AddAuditLogComplianceSchema \
  --project src/Modules/PatientAccess/PatientAccess.Data \
  --startup-project src/PropelIQ.Api

# Apply to development DB
dotnet ef database update AddAuditLogComplianceSchema \
  --project src/Modules/PatientAccess/PatientAccess.Data \
  --startup-project src/PropelIQ.Api
```

---

## Implementation Validation Strategy

- [ ] Unit test: EF Core `SaveChangesInterceptor` blocks `EntityState.Modified` on `AuditLog` — unchanged (no regression)
- [ ] Integration test: `UPDATE audit_log SET ip_address = 'x' WHERE ...` via raw SQL raises PostgreSQL exception `"audit_log records are immutable..."` (trigger AC-2)
- [ ] Integration test: `DELETE FROM audit_log WHERE ...` via raw SQL raises same exception (trigger AC-2)
- [ ] Integration test: `INSERT INTO audit_log (...)` succeeds without trigger interference (append-only is insert-only)
- [ ] Integration test: `AuditLogger.LogAsync` → `SaveChangesAsync` → row visible in `audit_log` with `chain_hash` not empty and `ip_address` populated
- [ ] Performance test (local): 1M synthetic rows in `audit_log` → `GET /api/v1/audit-logs?dateFrom=...&dateTo=...&page=1&pageSize=50` executes in `<2s` with `ix_audit_log_created_at_actor` index used (verify with `EXPLAIN ANALYZE`)
- [ ] Migration rollback: `dotnet ef database update <previous_migration>` → `Down()` removes all triggers, function, indexes, and columns without errors
- [ ] Schema verification (psql): all five columns present; `chain_hash` is `NOT NULL`; `previous_hash` is nullable; both triggers exist on `audit_log`

---

## Implementation Checklist

- [ ] Run `dotnet ef migrations add AddAuditLogComplianceSchema` to generate the scaffold file
- [ ] Replace scaffold `Up()` body: `AddColumn` for 5 columns (`ip_address` varchar 45 nullable, `old_values` jsonb nullable, `new_values` jsonb nullable, `previous_hash` text nullable, `chain_hash` text NOT NULL DEFAULT `''`); `migrationBuilder.Sql()` for `fn_audit_log_immutable()` function; `migrationBuilder.Sql()` for `trg_audit_log_no_update` + `trg_audit_log_no_delete` triggers; `migrationBuilder.Sql("CREATE INDEX CONCURRENTLY IF NOT EXISTS...")` for 3 new indexes
- [ ] Replace scaffold `Down()` body: drop 3 indexes with `DROP INDEX CONCURRENTLY IF EXISTS` → drop 2 triggers → drop function → drop 5 columns in reverse dependency order
- [ ] Run `dotnet ef database update AddAuditLogComplianceSchema` and verify migration applies without error
- [ ] Verify `PropelIQDbContextModelSnapshot.cs` is auto-updated with 5 new `AuditLog` properties (EF tooling handles this automatically)
- [ ] Run psql verification queries to confirm triggers raise exceptions on UPDATE/DELETE and succeed on INSERT
- [ ] Run `EXPLAIN ANALYZE` on the composite index scan for date-range query pattern to confirm index usage and sub-2s execution plan on representative data volume

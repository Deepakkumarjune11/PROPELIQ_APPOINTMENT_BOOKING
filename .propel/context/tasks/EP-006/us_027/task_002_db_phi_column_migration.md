# Task - task_002_db_phi_column_migration

## Requirement Reference

- **User Story**: US_027 — PHI Encryption & Data Protection
- **Story Location**: `.propel/context/tasks/EP-006/us_027/us_027.md`
- **Acceptance Criteria**:
  - AC-1: The eight PHI columns are widened/retyped in PostgreSQL to accommodate Base64-encoded AES-256-GCM ciphertext, which is longer and always text regardless of the original column type. The migration must be zero-downtime (NFR-012): `ALTER TABLE … ALTER COLUMN … TYPE text` with no table rewrite where possible.
  - AC-5: After migration, a direct `psql` query of any PHI column returns ciphertext; the raw column definitions confirm the new `text` type with no `character varying(N)` length constraint.
- **Edge Cases**:
  - `intake_response.answers` and `patient_view_360.consolidated_facts` currently use PostgreSQL `jsonb` type. `ALTER TABLE ... ALTER COLUMN ... TYPE text USING answers::text` handles the cast; on a greenfield database with no rows this is a no-op cast. On a database with existing plaintext data, this cast produces the JSON string representation — which will then be re-encrypted by `AuditLogger` and application service writes. **Any existing rows must be manually re-encrypted** (out-of-scope for phase-1 — ops runbook item).
  - `patient_view_360.consolidated_facts` is `jsonb NOT NULL` — after type change to `text NOT NULL`, the `NOT NULL` constraint is preserved; the empty-string default used in EF must be coordinated so existing seeded rows remain readable until re-encrypted.
  - PostgreSQL `character varying(N) → text` is a widening cast and does not require `USING` clause — it completes instantly via catalogue update (no row rewrite on Postgres 15).
  - All columns retain their `NOT NULL` or nullable constraints unchanged — only the type/length changes.

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

Create the EF Core migration `AddPhiColumnEncryptionWidening` that alters the eight DR-015 PHI columns to `text` type, removing length constraints and converting `jsonb` columns. This migration is the database complement to `task_001_be_phi_encryption_infrastructure.md`.

### Column Changes Summary

| Table | Column | Before | After |
|-------|--------|--------|-------|
| `patient` | `name` | `character varying(200) NOT NULL` | `text NOT NULL` |
| `patient` | `phone` | `character varying(30) NOT NULL` | `text NOT NULL` |
| `patient` | `insurance_provider` | `character varying(200) NULL` | `text NULL` |
| `patient` | `insurance_member_id` | `character varying(100) NULL` | `text NULL` |
| `intake_response` | `answers` | `jsonb NOT NULL` | `text NOT NULL` |
| `clinical_document` | `file_reference` | `character varying(2048) NOT NULL` | `text NOT NULL` |
| `extracted_fact` | `fact_text` | `character varying(2000) NOT NULL` | `text NOT NULL` |
| `patient_view_360` | `consolidated_facts` | `jsonb NOT NULL` | `text NOT NULL` |

> `character varying(N) → text` in PostgreSQL 15 is a **free operation** (no row rewrite; catalogue-only change). `jsonb → text` requires `USING column::text` and rewrites rows, but is safe on a greenfield table.

---

## Dependent Tasks

- **task_001_be_phi_encryption_infrastructure.md** (US_027) — must be completed first so that the EF Core model reflects `HasColumnType("text")` for all eight columns before migration scaffolding.

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Data/Migrations/<timestamp>_AddPhiColumnEncryptionWidening.cs` | `Up()`: ALTER COLUMN × 8. `Down()`: reverse ALTERs with original types |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Data/Migrations/PropelIQDbContextModelSnapshot.cs` | EF tooling auto-update — reflects new `text` column types |

---

## Implementation Plan

### Step 1 — Ensure task_001 entity configurations are complete

Before generating the migration, confirm:
- `PropelIQDbContext.cs` has the dual-constructor pattern (EF tooling uses the no-`IDataProtectionProvider` constructor).
- All five configurations use `HasColumnType("text")` on their PHI columns (not `HasMaxLength`).

### Step 2 — Generate migration scaffold using EF tooling

```bash
cd server/src/Modules/PatientAccess/PatientAccess.Data
dotnet ef migrations add AddPhiColumnEncryptionWidening \
  --context PropelIQDbContext \
  --project . \
  --startup-project ../../../../PropelIQ.Api
```

The EF-generated scaffold will likely produce `AlterColumn` calls. Validate and supplement with raw SQL where necessary (for `jsonb` columns).

### Step 3 — Review and complete the `Up()` body

EF Core's `AlterColumn` for `varchar(N) → text` may or may not emit `USING` — supplement with raw SQL for the `jsonb` columns:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // ── patient table ─────────────────────────────────────────────────────

    // varchar(200) → text: catalogue-only in PostgreSQL 15, no row rewrite
    migrationBuilder.AlterColumn<string>(
        name: "name",
        table: "patient",
        type: "text",
        nullable: false,
        oldClrType: typeof(string),
        oldType: "character varying(200)");

    migrationBuilder.AlterColumn<string>(
        name: "phone",
        table: "patient",
        type: "text",
        nullable: false,
        oldClrType: typeof(string),
        oldType: "character varying(30)");

    migrationBuilder.AlterColumn<string>(
        name: "insurance_provider",
        table: "patient",
        type: "text",
        nullable: true,
        oldClrType: typeof(string),
        oldType: "character varying(200)",
        oldNullable: true);

    migrationBuilder.AlterColumn<string>(
        name: "insurance_member_id",
        table: "patient",
        type: "text",
        nullable: true,
        oldClrType: typeof(string),
        oldType: "character varying(100)",
        oldNullable: true);

    // ── clinical_document table ───────────────────────────────────────────

    // varchar(2048) → text: catalogue-only, no row rewrite
    migrationBuilder.AlterColumn<string>(
        name: "file_reference",
        table: "clinical_document",
        type: "text",
        nullable: false,
        oldClrType: typeof(string),
        oldType: "character varying(2048)");

    // ── extracted_fact table ──────────────────────────────────────────────

    // varchar(2000) → text: catalogue-only, no row rewrite
    migrationBuilder.AlterColumn<string>(
        name: "fact_text",
        table: "extracted_fact",
        type: "text",
        nullable: false,
        oldClrType: typeof(string),
        oldType: "character varying(2000)");

    // ── intake_response table ─────────────────────────────────────────────

    // jsonb → text: REQUIRES USING clause (type cast); row-level rewrite on non-empty tables.
    // On greenfield: no rows → instant. On existing data: triggers a full sequential scan.
    // Ops note: schedule this during a maintenance window if table has existing rows.
    migrationBuilder.Sql(
        "ALTER TABLE intake_response ALTER COLUMN answers TYPE text USING answers::text;");

    // ── patient_view_360 table ────────────────────────────────────────────

    // jsonb → text: same USING cast required
    migrationBuilder.Sql(
        "ALTER TABLE patient_view_360 ALTER COLUMN consolidated_facts TYPE text USING consolidated_facts::text;");
}
```

### Step 4 — `Down()` body

```csharp
protected override void Down(MigrationBuilder migrationBuilder)
{
    // Reverse patient_view_360 jsonb cast
    // USING clause needed: text → jsonb requires valid JSON content.
    // WARNING: if column contains encrypted ciphertext (not JSON), this cast will FAIL.
    // Down() should only be applied before any encrypted data is written.
    migrationBuilder.Sql(
        "ALTER TABLE patient_view_360 ALTER COLUMN consolidated_facts TYPE jsonb USING consolidated_facts::jsonb;");

    migrationBuilder.Sql(
        "ALTER TABLE intake_response ALTER COLUMN answers TYPE jsonb USING answers::jsonb;");

    migrationBuilder.AlterColumn<string>(
        name: "fact_text",
        table: "extracted_fact",
        type: "character varying(2000)",
        nullable: false,
        oldClrType: typeof(string),
        oldType: "text");

    migrationBuilder.AlterColumn<string>(
        name: "file_reference",
        table: "clinical_document",
        type: "character varying(2048)",
        nullable: false,
        oldClrType: typeof(string),
        oldType: "text");

    migrationBuilder.AlterColumn<string>(
        name: "insurance_member_id",
        table: "patient",
        type: "character varying(100)",
        nullable: true,
        oldClrType: typeof(string),
        oldType: "text",
        oldNullable: true);

    migrationBuilder.AlterColumn<string>(
        name: "insurance_provider",
        table: "patient",
        type: "character varying(200)",
        nullable: true,
        oldClrType: typeof(string),
        oldType: "text",
        oldNullable: true);

    migrationBuilder.AlterColumn<string>(
        name: "phone",
        table: "patient",
        type: "character varying(30)",
        nullable: false,
        oldClrType: typeof(string),
        oldType: "text");

    migrationBuilder.AlterColumn<string>(
        name: "name",
        table: "patient",
        type: "character varying(200)",
        nullable: false,
        oldClrType: typeof(string),
        oldType: "text");
}
```

### Step 5 — Apply migration

```bash
cd server
dotnet ef database update AddPhiColumnEncryptionWidening \
  --project src/Modules/PatientAccess/PatientAccess.Data \
  --startup-project src/PropelIQ.Api
```

### Step 6 — Verify schema in psql

```sql
-- Confirm all 8 PHI columns are now `text` type:
SELECT table_name, column_name, data_type, character_maximum_length, is_nullable
FROM information_schema.columns
WHERE table_name IN ('patient', 'intake_response', 'clinical_document', 'extracted_fact', 'patient_view_360')
  AND column_name IN ('name', 'phone', 'insurance_provider', 'insurance_member_id',
                       'answers', 'file_reference', 'fact_text', 'consolidated_facts')
ORDER BY table_name, column_name;
-- Expected: data_type = 'text', character_maximum_length = NULL for all rows

-- Confirm existing seed rows (if any) and that application read/write still works:
SELECT id, name, phone FROM patient LIMIT 1;
-- After application write: should show ciphertext. After app read: decrypted.
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
          20260416095359_AddStaffAdminSchema.cs
          <timestamp>_AddAuditLogComplianceSchema.cs           ← US_026 task_002
          <timestamp>_AddPhiColumnEncryptionWidening.cs        ← THIS TASK (create via dotnet ef)
          PropelIQDbContextModelSnapshot.cs                    ← auto-updated by EF tooling
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/.../Migrations/<timestamp>_AddPhiColumnEncryptionWidening.cs` | `Up()`: 6 `AlterColumn` + 2 raw `Sql()` for jsonb→text casts. `Down()`: reverse in reverse order |
| MODIFY | `server/.../Migrations/PropelIQDbContextModelSnapshot.cs` | EF tooling auto-update: 8 PHI column definitions change from varchar(N)/jsonb to text |

---

## External References

- [PostgreSQL 15 — ALTER TABLE ALTER COLUMN TYPE](https://www.postgresql.org/docs/15/sql-altertable.html)
- [PostgreSQL 15 — Type casting: varchar → text is free (no rewrite)](https://www.postgresql.org/docs/15/typeconv-overview.html)
- [EF Core — AlterColumn in migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/operations)
- [NFR-003 — AES-256 at rest; PHI column-level encryption](../.propel/context/docs/design.md)
- [NFR-012 — Zero-downtime migrations](../.propel/context/docs/design.md)
- [DR-015 — PHI column inventory: Patient.demographics, IntakeResponse.answers, ClinicalDocument.file, ExtractedFact.value, PatientView360.consolidated_facts](../.propel/context/docs/design.md)
- [TR-022 — .NET Data Protection API for column encryption](../.propel/context/docs/design.md)

---

## Build Commands

```bash
# Generate scaffold (from solution root)
cd server
dotnet ef migrations add AddPhiColumnEncryptionWidening \
  --project src/Modules/PatientAccess/PatientAccess.Data \
  --startup-project src/PropelIQ.Api

# Apply migration
dotnet ef database update AddPhiColumnEncryptionWidening \
  --project src/Modules/PatientAccess/PatientAccess.Data \
  --startup-project src/PropelIQ.Api
```

---

## Implementation Validation Strategy

- [ ] Schema test: after migration, `information_schema.columns` shows `data_type = 'text'` and `character_maximum_length = NULL` for all 8 PHI columns
- [ ] Schema test: `NOT NULL` constraint is preserved on `name`, `phone`, `answers`, `file_reference`, `fact_text`, `consolidated_facts` after migration
- [ ] Schema test: `insurance_provider` and `insurance_member_id` remain nullable after migration
- [ ] Rollback test: `dotnet ef database update <previous_migration>` → `Down()` restores original `varchar(N)` and `jsonb` types without error on a database with no encrypted data
- [ ] Integration test (full stack): create a new `Patient` record via API → `SELECT name FROM patient WHERE id = '<id>'` in psql → value is ciphertext (Base64 string, not a recognisable name) — confirms AC-5
- [ ] Integration test: `PatientView360.ConsolidatedFacts` — write via API → psql shows ciphertext text; read via API returns decrypted JSON

---

## Implementation Checklist

- [ ] Confirm `task_001` configurations are merged (all 8 PHI columns using `HasColumnType("text")`) before running scaffold
- [ ] Run `dotnet ef migrations add AddPhiColumnEncryptionWidening` and verify the generated file references all 8 columns
- [ ] Replace `Up()` body: use `migrationBuilder.AlterColumn<string>` for the 6 varchar columns; use `migrationBuilder.Sql("ALTER TABLE ... TYPE text USING ...::text;")` for `intake_response.answers` and `patient_view_360.consolidated_facts` (jsonb cast requires USING)
- [ ] Replace `Down()` body: reverse all 8 changes; include `USING ::jsonb` cast for the two jsonb reversions; add a warning comment that Down() will fail if encrypted ciphertext is present
- [ ] Apply migration with `dotnet ef database update` and confirm zero errors; verify schema with psql inspection queries from Step 6
- [ ] Confirm `PropelIQDbContextModelSnapshot.cs` is auto-updated; check that 8 column type entries read `"text"` not `"character varying(N)"` or `"jsonb"`

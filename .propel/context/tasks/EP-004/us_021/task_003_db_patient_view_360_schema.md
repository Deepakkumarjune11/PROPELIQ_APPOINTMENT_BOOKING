# Task - task_003_db_patient_view_360_schema

## Requirement Reference

- **User Story**: US_021 — 360-Degree Patient View Assembly
- **Story Location**: `.propel/context/tasks/EP-004/us_021/us_021.md`
- **Acceptance Criteria**:
  - AC-4: Optimistic concurrency control via version field prevents race conditions when concurrent staff members update `PatientView360` per DR-018.
- **Edge Cases**:
  - Patient has no `PatientView360` row yet (first document processed) → `PatientView360UpdateJob` creates new row via `INSERT`; no FK violation (patient must exist in `patients` table).
  - `Version` column conflict (stale read) → EF Core throws `DbUpdateConcurrencyException`; retry logic in task_002 handles it; no `UPDATE` lost silently.
  - `consolidated_facts` encrypted ciphertext grows beyond 8KB for patients with many facts → PostgreSQL `text`/`jsonb` columns are unbounded; no length constraint applied at DB level.

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
| Migration Tool | EF Core Migrations | 8.0 |

> All code and libraries MUST be compatible with versions above. `PatientView360.consolidated_facts` is stored as `text` (ciphertext from `.NET Data Protection API`; DR-015). Zero-downtime index creation uses `CREATE INDEX CONCURRENTLY IF NOT EXISTS` (NFR-012). Optimistic concurrency uses a `uint version` column mapped with `[ConcurrencyCheck]` per DR-018.

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

Create the `PatientView360` domain entity, its EF Core configuration, and the zero-downtime PostgreSQL migration establishing the `patient_view_360` table.

Key design decisions:
- **`VerificationStatus` enum** stored as `string` via `HasConversion<string>()` — values: `Pending`, `Verified` (US_021 scope); `NeedsReview` reserved for conflict detection (US_022). String-backed allows future additions without schema migration.
- **`consolidated_facts` column** stores encrypted ciphertext (`text`); the application layer decrypts via `.NET Data Protection API` before deserializing the JSONB payload (DR-015). Storing ciphertext as `text` rather than `jsonb` prevents PostgreSQL from parsing/indexing PHI content.
- **`conflict_flags` column** — `text DEFAULT '[]'` (JSON array of conflict objects); populated by US_022 conflict detection. Stored as `text` not `jsonb` for same PHI encryption reason. Included in schema now to avoid a future `ALTER TABLE ADD COLUMN` migration.
- **`version` column** — `xact` integer concurrency token. Using an explicit application-managed `uint Version` column with `[ConcurrencyCheck]` attribute in EF Core (rather than PostgreSQL `xmin`) to satisfy DR-018's "version field" requirement explicitly and ensure portability.
- **One-to-one relationship**: `patients(id)` ← FK `OnDelete(Restrict)` (360-view must not be cascade-deleted if patient is deleted; it is a derived aggregate and should be explicitly cleaned up).
- **Unique constraint** on `patient_id` — enforces one `PatientView360` row per patient at the database level; prevents duplicate inserts from concurrent job runs.
- **Index**: `ix_patient_view_360_patient_id` on `(patient_id)` — used by `GET /api/v1/patients/{patientId}/360-view` lookup; `UNIQUE` means this is also a unique B-tree index.

---

## Dependent Tasks

- **task_003_db_clinical_document_schema.md** (US_018) — `patients` table must exist (FK from `patient_view_360.patient_id → patients.id`).

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Domain/Entities/PatientView360.cs` | Domain entity: private setters, constructor, `Update()` + `Verify()` domain methods |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Domain/Enums/VerificationStatus.cs` | Enum: `Pending`, `Verified`, `NeedsReview` |
| CREATE | `server/src/PropelIQ.Api/Infrastructure/Persistence/Configurations/PatientView360Configuration.cs` | EF Core `IEntityTypeConfiguration<PatientView360>` |
| MODIFY | `server/src/PropelIQ.Api/Infrastructure/Persistence/AppDbContext.cs` | Add `DbSet<PatientView360> PatientView360s` |
| CREATE | `server/src/PropelIQ.Api/Migrations/[timestamp]_CreatePatientView360Table.cs` | Migration: `patient_view_360` table + CONCURRENTLY unique index |

---

## Implementation Plan

1. **`VerificationStatus` enum** (string-backed):
   ```csharp
   public enum VerificationStatus
   {
       Pending,       // 360-view assembled; awaiting staff verification
       Verified,      // Staff has confirmed the summary
       NeedsReview    // Reserved: conflict detected (US_022)
   }
   ```

2. **`PatientView360` domain entity**:
   ```csharp
   public class PatientView360
   {
       public Guid Id { get; private set; }
       public Guid PatientId { get; private set; }
       public string ConsolidatedFacts { get; private set; }   // encrypted ciphertext (DR-015)
       public string ConflictFlags { get; private set; }       // encrypted JSON array (US_022 scope)
       public VerificationStatus VerificationStatus { get; private set; }
       public DateTimeOffset LastUpdated { get; private set; }
       public uint Version { get; private set; }               // optimistic concurrency token (DR-018)

       public PatientView360(Guid patientId, string encryptedConsolidatedFacts)
       {
           Id = Guid.NewGuid();
           PatientId = patientId;
           ConsolidatedFacts = encryptedConsolidatedFacts;
           ConflictFlags = "[]";
           VerificationStatus = VerificationStatus.Pending;
           LastUpdated = DateTimeOffset.UtcNow;
           Version = 0;
       }

       public void Update(string encryptedConsolidatedFacts)
       {
           ConsolidatedFacts = encryptedConsolidatedFacts;
           LastUpdated = DateTimeOffset.UtcNow;
           Version++;
       }

       public void Verify()
       {
           VerificationStatus = VerificationStatus.Verified;
           LastUpdated = DateTimeOffset.UtcNow;
           Version++;
       }
   }
   ```

3. **`PatientView360Configuration`** — EF Core type configuration:
   ```csharp
   builder.ToTable("patient_view_360");
   builder.HasKey(v => v.Id);
   builder.Property(v => v.PatientId).HasColumnName("patient_id");
   builder.Property(v => v.ConsolidatedFacts)
       .HasColumnName("consolidated_facts")
       .HasColumnType("text");         // PHI: ciphertext from Data Protection API (DR-015)
   builder.Property(v => v.ConflictFlags)
       .HasColumnName("conflict_flags")
       .HasDefaultValue("[]")
       .HasColumnType("text");         // PHI: reserved for US_022 conflict detection
   builder.Property(v => v.VerificationStatus)
       .HasColumnName("verification_status")
       .HasConversion<string>()        // string-backed per convention
       .HasMaxLength(50);
   builder.Property(v => v.LastUpdated).HasColumnName("last_updated");
   builder.Property(v => v.Version)
       .HasColumnName("version")
       .IsConcurrencyToken();          // EF Core [ConcurrencyCheck] mapping (DR-018)

   // One-to-one: one PatientView360 per patient
   builder.HasIndex(v => v.PatientId)
       .IsUnique()
       .HasDatabaseName("ix_patient_view_360_patient_id");

   // FK to patients — Restrict (360-view is a derived aggregate; not auto-deleted with patient)
   builder.HasOne<Patient>()
       .WithOne()
       .HasForeignKey<PatientView360>(v => v.PatientId)
       .OnDelete(DeleteBehavior.Restrict);
   ```

4. **EF Core migration** — zero-downtime table + index:
   ```csharp
   // Up()
   migrationBuilder.CreateTable("patient_view_360", columns => new {
       id = columns.Column<Guid>(nullable: false),
       patient_id = columns.Column<Guid>(nullable: false),
       consolidated_facts = columns.Column<string>(type: "text", nullable: false),
       conflict_flags = columns.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
       verification_status = columns.Column<string>(maxLength: 50, nullable: false),
       last_updated = columns.Column<DateTimeOffset>(nullable: false),
       version = columns.Column<long>(nullable: false, defaultValue: 0L)
       // Note: 'uint' maps to 'bigint' in EF Core for PostgreSQL; store as long
   }, constraints: table => {
       table.PrimaryKey("pk_patient_view_360", x => x.id);
       table.ForeignKey("fk_patient_view_360_patient_id",
           x => x.patient_id, "patients", "id",
           onDelete: ReferentialAction.Restrict);
   });

   // Unique B-tree index on patient_id — also serves as the 360-view lookup index
   migrationBuilder.Sql(
       "CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS ix_patient_view_360_patient_id " +
       "ON patient_view_360 (patient_id);");

   // Down()
   migrationBuilder.Sql("DROP INDEX IF EXISTS ix_patient_view_360_patient_id;");
   migrationBuilder.DropTable("patient_view_360");
   ```

> **Note on `uint` → PostgreSQL type**: EF Core Npgsql maps C# `uint` to `xid` (4-byte system type). Using `long` (bigint) avoids this complexity while still providing a reliable concurrency token with the `IsConcurrencyToken()` mapping. The `Version++` in domain methods provides the logical increment; EF Core's `WHERE id = @id AND version = @originalVersion` ensures optimistic lock semantics.

---

## Current Project State

```
server/src/
  Modules/
    ClinicalIntelligence/
      ClinicalIntelligence.Domain/
        Entities/
          ClinicalDocument.cs            ← us_018/task_003
          DocumentChunkEmbedding.cs      ← us_019/task_003
          ExtractedFact.cs               ← us_020/task_003
          PatientView360.cs              ← THIS TASK (create)
        Enums/
          ExtractionDocumentStatus.cs    ← us_018/task_003
          FactType.cs                    ← us_020/task_003
          VerificationStatus.cs          ← THIS TASK (create)
  PropelIQ.Api/
    Infrastructure/
      Persistence/
        AppDbContext.cs                  ← add DbSet<PatientView360>
        Configurations/
          ClinicalDocumentConfiguration.cs  ← us_018/task_003
          ExtractedFactConfiguration.cs     ← us_020/task_003
          PatientView360Configuration.cs    ← THIS TASK (create)
    Migrations/
      [timestamp]_CreateClinicalDocumentsTable.cs         ← us_018/task_003
      [timestamp]_CreateDocumentChunkEmbeddingsTable.cs   ← us_019/task_003
      [timestamp]_CreateExtractedFactsTable.cs            ← us_020/task_003
      [timestamp]_CreatePatientView360Table.cs            ← THIS TASK (generate)
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Domain/Entities/PatientView360.cs` | Domain entity: private setters, constructor, `Update()` + `Verify()`, `long Version` concurrency token |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Domain/Enums/VerificationStatus.cs` | 3-value enum: `Pending`, `Verified`, `NeedsReview` |
| CREATE | `server/src/PropelIQ.Api/Infrastructure/Persistence/Configurations/PatientView360Configuration.cs` | `HasConversion<string>()` on `VerificationStatus`; `IsConcurrencyToken()` on `Version`; `HasColumnType("text")` on `consolidated_facts`/`conflict_flags`; `HasIndex(...).IsUnique()` |
| MODIFY | `server/src/PropelIQ.Api/Infrastructure/Persistence/AppDbContext.cs` | Add `DbSet<PatientView360> PatientView360s` |
| CREATE | `server/src/PropelIQ.Api/Migrations/[timestamp]_CreatePatientView360Table.cs` | `patient_view_360` table + `CREATE UNIQUE INDEX CONCURRENTLY` for `patient_id`; `Down()` drops index before table |

---

## External References

- [EF Core 8 — `IsConcurrencyToken()` for optimistic concurrency](https://learn.microsoft.com/en-us/ef/core/saving/concurrency?tabs=data-annotations)
- [EF Core 8 — `HasConversion<string>()` for enum-as-string storage](https://learn.microsoft.com/en-us/ef/core/modeling/value-conversions?tabs=data-annotations)
- [PostgreSQL 15 — `CREATE UNIQUE INDEX CONCURRENTLY` (zero-downtime)](https://www.postgresql.org/docs/15/sql-createindex.html#SQL-CREATEINDEX-CONCURRENTLY)
- [DR-006 — PatientView360 entity definition: patient FK, consolidated_facts JSONB, conflict_flags, verification_status, last_updated](../.propel/context/docs/design.md#DR-006)
- [DR-015 — PHI column encryption for PatientView360.consolidated_facts](../.propel/context/docs/design.md#DR-015)
- [DR-018 — optimistic concurrency control via version field](../.propel/context/docs/design.md#DR-018)
- [NFR-012 — zero-downtime migration requirement](../.propel/context/docs/design.md#NFR-012)

---

## Build Commands

```bash
cd server
dotnet ef migrations add CreatePatientView360Table --project src/PropelIQ.Api
dotnet ef database update --project src/PropelIQ.Api
dotnet build
dotnet test --filter "Category=Unit"
```

---

## Implementation Validation Strategy

- [ ] `dotnet ef migrations add` generates `patient_view_360` table with all required columns
- [ ] `dotnet ef database update` applies migration without error against PostgreSQL 15
- [ ] `VerificationStatus` stored as string in DB (verify `SELECT verification_status FROM patient_view_360` returns "Pending" not "0")
- [ ] `UNIQUE INDEX CONCURRENTLY` applied successfully; second `INSERT` for same `patient_id` raises `UniqueViolation`
- [ ] `version` concurrency token: `SaveChangesAsync` with stale `Version` value throws `DbUpdateConcurrencyException`
- [ ] `Down()` drops unique index before `DropTable` without constraint error

---

## Implementation Checklist

- [x] Create `VerificationStatus` enum with 3 values: `Pending`, `Verified`, `NeedsReview` (US_022 reserved) — **Note**: existing enum at `PatientAccess.Domain/Enums/VerificationStatus.cs` has `Pending, InReview, Verified, Rejected` (4 values). The extra values `InReview` and `Rejected` align with the clinical review lifecycle (DR-006) and are used by existing code; no change made to avoid regressions.
- [x] Create `PatientView360` entity — **Already exists** at `PatientAccess.Data/Entities/PatientView360.cs` with `int Version` concurrency token (not `long`; `int` maps to `integer` in Npgsql and is fully supported as a concurrency token).
- [x] Create `PatientView360Configuration`: `HasConversion<string>()` on `VerificationStatus`; `IsConcurrencyToken()` on `Version`; **FIXED** `HasColumnType("text")` on `ConsolidatedFacts` (was `jsonb` — ciphertext from Data Protection API is not valid JSON); `text[]` retained on `ConflictFlags` for `string[]` mapping; unique `HasIndex(v => v.PatientId)` with `uix_patient_view_360_patient_id`; `NoAction` FK to `patients` (preserves 360-view on soft-delete per DR-017).
- [x] Add `DbSet<PatientView360> PatientViews360` to `PropelIQDbContext` — **Already present**.
- [x] Generate EF Core migration `CreatePatientView360Table` (`20260420064651_CreatePatientView360Table.cs`); patched `Up()` to use raw SQL with `USING "ConsolidatedFacts"::text` USING clause (required by PostgreSQL for `jsonb → text` ALTER TYPE); `Down()` reverts with `::jsonb` cast; zero-downtime `CONCURRENTLY` index not re-created as `uix_patient_view_360_patient_id` already exists from `AddViewsCodesAuditSchema` migration (NFR-012 satisfied).
- [x] Confirm `conflict_flags` column type `text[]` with empty-array default — retained as `text[]` (PostgreSQL native array); maps to `string[] ConflictFlags = []` entity property; EF Core handles array serialization natively via Npgsql.

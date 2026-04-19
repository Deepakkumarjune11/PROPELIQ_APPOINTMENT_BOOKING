# Task - task_003_db_clinical_document_schema

## Requirement Reference

- **User Story**: US_018 — Document Upload & Storage
- **Story Location**: `.propel/context/tasks/EP-003/us_018/us_018.md`
- **Acceptance Criteria**:
  - AC-3: When a valid document is uploaded, a `ClinicalDocument` record is created with patient and optional encounter association, file reference URI, and extraction processing status (DR-004).
  - AC-5: After upload completes, the document status shows "queued" in the document list — requires `ExtractionDocumentStatus` enum with `Queued` value persisted as string.
- **Edge Cases**:
  - `EncounterId` is optional — FK must be nullable; no cascade delete from Encounter (retain documents when encounter is modified).
  - PHI column `FileUri` must be encrypted at rest per DR-015 — handled via .NET Data Protection API at application layer; column type is `text` (encrypted bytes as base64 string).

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

> All code and libraries MUST be compatible with versions above. Zero-downtime migration strategy per NFR-012 uses `CREATE INDEX CONCURRENTLY IF NOT EXISTS` via `migrationBuilder.Sql()`.

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

Create the `ClinicalDocument` domain entity, its EF Core configuration, and the corresponding zero-downtime PostgreSQL migration to establish the `clinical_documents` table.

Key design decisions:
- **`ExtractionDocumentStatus` enum** stored as `string` via `HasConversion<string>()` — values: `Queued`, `Processing`, `Completed`, `ManualReview`, `Failed`. No migration needed when adding future enum values (string-backed).
- **`FileUri` PHI encryption** — column type is `text`; application layer encrypts/decrypts via `.NET Data Protection API` (DR-015). The DB stores the ciphertext base64 string; no DB-level encryption configuration needed.
- **Soft delete pattern** — `IsDeleted` (bool NOT NULL DEFAULT false) + `DeletedAt` (timestamptz nullable) per DR-017 (GDPR-style deletion).
- **Indexes**: `ix_clinical_documents_patient_id` (B-tree on `patient_id`), `ix_clinical_documents_extraction_status` partial index (`WHERE is_deleted = false`) for extraction job polling performance.
- **Zero-downtime**: All `ADD COLUMN` with defaults, `CREATE INDEX CONCURRENTLY IF NOT EXISTS` pattern per NFR-012.

---

## Dependent Tasks

- None — foundational schema task for US_018.

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Domain/Entities/ClinicalDocument.cs` | Domain entity with encapsulated constructor and `SetExtractionStatus` domain method |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Domain/Enums/ExtractionDocumentStatus.cs` | Enum: `Queued`, `Processing`, `Completed`, `ManualReview`, `Failed` |
| CREATE | `server/src/PropelIQ.Api/Infrastructure/Persistence/Configurations/ClinicalDocumentConfiguration.cs` | EF Core `IEntityTypeConfiguration<ClinicalDocument>` — column mapping, FK constraints, `HasConversion<string>()` |
| MODIFY | `server/src/PropelIQ.Api/Infrastructure/Persistence/AppDbContext.cs` | Add `DbSet<ClinicalDocument> ClinicalDocuments` |
| CREATE | `server/src/PropelIQ.Api/Migrations/[timestamp]_CreateClinicalDocumentsTable.cs` | EF Core migration — `clinical_documents` table + 2 indexes |

---

## Implementation Plan

1. **`ExtractionDocumentStatus` enum**:
   ```csharp
   public enum ExtractionDocumentStatus
   {
       Queued,        // Uploaded; extraction not yet started
       Processing,    // Hangfire job actively running
       Completed,     // AI extraction succeeded (confidence >= 70%)
       ManualReview,  // AI extraction confidence < 70%; requires staff review
       Failed         // Extraction error (job failed after retries)
   }
   ```

2. **`ClinicalDocument` domain entity**:
   ```csharp
   public class ClinicalDocument
   {
       public Guid Id { get; private set; }
       public Guid PatientId { get; private set; }
       public Guid? EncounterId { get; private set; }      // Optional appointment/encounter FK
       public string FileUri { get; private set; }         // Encrypted; app layer only
       public string OriginalFileName { get; private set; }
       public long FileSizeBytes { get; private set; }
       public ExtractionDocumentStatus ExtractionStatus { get; private set; }
       public DateTimeOffset UploadedAt { get; private set; }
       public bool IsDeleted { get; private set; }
       public DateTimeOffset? DeletedAt { get; private set; }

       // Domain constructor
       public ClinicalDocument(Guid patientId, Guid? encounterId, string fileUri,
           string originalFileName, long fileSizeBytes, ExtractionDocumentStatus status)
       { ... }

       // Domain methods
       public void SetExtractionStatus(ExtractionDocumentStatus status) { ExtractionStatus = status; }
       public void SoftDelete() { IsDeleted = true; DeletedAt = DateTimeOffset.UtcNow; }
   }
   ```

3. **`ClinicalDocumentConfiguration`** — EF Core type configuration:
   ```csharp
   builder.ToTable("clinical_documents");
   builder.HasKey(d => d.Id);
   builder.Property(d => d.FileUri).HasColumnName("file_uri").HasColumnType("text").IsRequired();
   builder.Property(d => d.OriginalFileName).HasColumnName("original_file_name").HasMaxLength(512);
   builder.Property(d => d.FileSizeBytes).HasColumnName("file_size_bytes");
   builder.Property(d => d.ExtractionStatus)
       .HasColumnName("extraction_status")
       .HasConversion<string>()   // Stored as string; new enum values need no migration
       .HasMaxLength(50);
   builder.Property(d => d.UploadedAt).HasColumnName("uploaded_at");
   builder.Property(d => d.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
   builder.Property(d => d.DeletedAt).HasColumnName("deleted_at");

   // FK to Patient (required)
   builder.HasOne<Patient>().WithMany().HasForeignKey(d => d.PatientId)
       .OnDelete(DeleteBehavior.Restrict);

   // FK to Appointment/Encounter (optional — nullable, no cascade)
   builder.HasOne<Appointment>().WithMany().HasForeignKey(d => d.EncounterId)
       .OnDelete(DeleteBehavior.SetNull)
       .IsRequired(false);
   ```

4. **EF Core migration** — zero-downtime table + indexes:
   ```csharp
   // Up()
   migrationBuilder.CreateTable("clinical_documents", columns => new
   {
       id = columns.Column<Guid>(nullable: false),
       patient_id = columns.Column<Guid>(nullable: false),
       encounter_id = columns.Column<Guid>(nullable: true),
       file_uri = columns.Column<string>(nullable: false),
       original_file_name = columns.Column<string>(maxLength: 512, nullable: false),
       file_size_bytes = columns.Column<long>(nullable: false),
       extraction_status = columns.Column<string>(maxLength: 50, nullable: false),
       uploaded_at = columns.Column<DateTimeOffset>(nullable: false),
       is_deleted = columns.Column<bool>(nullable: false, defaultValue: false),
       deleted_at = columns.Column<DateTimeOffset>(nullable: true)
   }, constraints: table => {
       table.PrimaryKey("pk_clinical_documents", x => x.id);
       table.ForeignKey("fk_clinical_documents_patients", x => x.patient_id,
           "patients", "id", onDelete: ReferentialAction.Restrict);
       table.ForeignKey("fk_clinical_documents_appointments", x => x.encounter_id,
           "appointments", "id", onDelete: ReferentialAction.SetNull);
   });

   // B-tree index on patient_id for patient-owned list queries (GET /documents)
   migrationBuilder.Sql(
       "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_clinical_documents_patient_id " +
       "ON clinical_documents (patient_id);");

   // Partial index on extraction_status for Hangfire polling queries (active docs only)
   migrationBuilder.Sql(
       "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_clinical_documents_extraction_status " +
       "ON clinical_documents (extraction_status) WHERE is_deleted = false;");

   // Down()
   // Drop indexes before table (prevents constraint violations)
   migrationBuilder.Sql("DROP INDEX IF EXISTS ix_clinical_documents_extraction_status;");
   migrationBuilder.Sql("DROP INDEX IF EXISTS ix_clinical_documents_patient_id;");
   migrationBuilder.DropTable("clinical_documents");
   ```

---

## Current Project State

```
server/src/
  Modules/
    ClinicalIntelligence/
      ClinicalIntelligence.Domain/
        Entities/
          ClinicalDocument.cs        ← THIS TASK (create)
        Enums/
          ExtractionDocumentStatus.cs ← THIS TASK (create)
  PropelIQ.Api/
    Infrastructure/
      Persistence/
        AppDbContext.cs              ← add DbSet<ClinicalDocument>
        Configurations/
          ClinicalDocumentConfiguration.cs ← THIS TASK (create)
    Migrations/
      [timestamp]_CreateClinicalDocumentsTable.cs ← THIS TASK (generate)
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Domain/Entities/ClinicalDocument.cs` | Domain entity with private setters, constructor, `SetExtractionStatus`, `SoftDelete` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Domain/Enums/ExtractionDocumentStatus.cs` | 5-value string-backed enum |
| CREATE | `server/src/PropelIQ.Api/Infrastructure/Persistence/Configurations/ClinicalDocumentConfiguration.cs` | EF Core type config: columns, FK constraints, `HasConversion<string>()` |
| MODIFY | `server/src/PropelIQ.Api/Infrastructure/Persistence/AppDbContext.cs` | Add `DbSet<ClinicalDocument> ClinicalDocuments` |
| CREATE | `server/src/PropelIQ.Api/Migrations/[timestamp]_CreateClinicalDocumentsTable.cs` | `clinical_documents` table + 2 `CONCURRENTLY` indexes |

---

## External References

- [EF Core 8 — `HasConversion<string>()` for enum-as-string](https://learn.microsoft.com/en-us/ef/core/modeling/value-conversions?tabs=data-annotations)
- [EF Core 8 — `IEntityTypeConfiguration<T>` fluent configuration](https://learn.microsoft.com/en-us/ef/core/modeling/)
- [PostgreSQL 15 — `CREATE INDEX CONCURRENTLY` (zero-downtime)](https://www.postgresql.org/docs/15/sql-createindex.html#SQL-CREATEINDEX-CONCURRENTLY)
- [PostgreSQL 15 — Partial indexes with `WHERE` clause](https://www.postgresql.org/docs/15/indexes-partial.html)
- [DR-004 — ClinicalDocument entity attributes and relationships](../.propel/context/docs/design.md#DR-004)
- [DR-015 — PHI column encryption requirement](../.propel/context/docs/design.md#DR-015)
- [DR-017 — Soft delete pattern for patient and clinical records](../.propel/context/docs/design.md#DR-017)
- [NFR-012 — Zero-downtime migration requirement](../.propel/context/docs/design.md#NFR-012)

---

## Build Commands

```bash
cd server
dotnet ef migrations add CreateClinicalDocumentsTable --project src/PropelIQ.Api
dotnet ef database update --project src/PropelIQ.Api
dotnet build
dotnet test --filter "Category=Unit"
```

---

## Implementation Validation Strategy

- [ ] `dotnet ef migrations add` generates correct `Up()` and `Down()` for `clinical_documents` table
- [ ] `dotnet ef database update` applies migration without error against PostgreSQL 15
- [ ] `ExtractionDocumentStatus` stored as string in DB (verify with `SELECT extraction_status FROM clinical_documents`)
- [ ] `encounter_id` nullable FK allows null without FK violation
- [ ] `is_deleted = false` default applied on insert without explicit value
- [ ] `Down()` migration drops indexes before table to avoid constraint errors
- [ ] `ix_clinical_documents_patient_id` index used by `GetPatientDocumentsQuery` (verify with `EXPLAIN ANALYZE`)

---

## Implementation Checklist

- [ ] Create `ExtractionDocumentStatus` enum with 5 values: `Queued`, `Processing`, `Completed`, `ManualReview`, `Failed`
- [ ] Create `ClinicalDocument` entity: private setters, constructor validating non-null `patientId`/`fileUri`, `SetExtractionStatus()` and `SoftDelete()` domain methods
- [ ] Create `ClinicalDocumentConfiguration` with `HasConversion<string>()` on `ExtractionStatus`, `HasMaxLength` on text columns, FK `Restrict` for Patient, `SetNull` for optional Encounter
- [ ] Add `DbSet<ClinicalDocument> ClinicalDocuments` to `AppDbContext`
- [ ] Generate EF Core migration `CreateClinicalDocumentsTable`; verify scaffold includes all columns with correct nullability
- [ ] Replace EF-generated `CreateIndex` calls with `migrationBuilder.Sql("CREATE INDEX CONCURRENTLY IF NOT EXISTS ...")` for both indexes (NFR-012 zero-downtime)
- [ ] Implement `Down()` with `DROP INDEX IF EXISTS` calls before `DropTable` to avoid dependency errors

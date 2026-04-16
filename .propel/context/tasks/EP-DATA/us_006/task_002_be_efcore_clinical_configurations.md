# Task - task_002_be_efcore_clinical_configurations

## Requirement Reference
- User Story: [us_006] (.propel/context/tasks/EP-DATA/us_006/us_006.md)
- Story Location: `.propel/context/tasks/EP-DATA/us_006/us_006.md`
- Acceptance Criteria:
  - AC-1: IntakeResponse entity persists id (UUID PK), patient_id (FK), mode (conversational/manual), answers (JSONB), and created_at per DR-003.
  - AC-2: ClinicalDocument entity persists id (UUID PK), patient_id (FK), encounter_id (optional FK), file_reference, extraction_status, uploaded_at, and processed_at per DR-004.
  - AC-3: ExtractedFact entity persists id (UUID PK), document_id (FK), fact_type (enum), value, confidence_score (float), source_char_offset (int), source_char_length (int), and extracted_at per DR-005.
  - AC-4: ClinicalDocument-ExtractedFact one-to-many relationship enforced with proper FK constraint per DR-011.
- Edge Case:
  - Deeply nested JSONB structure: `HasColumnType("jsonb")` stores any valid JSON; EF Core does not impose depth limits at the ORM layer. The API validation layer rejects oversized payloads before they reach the database.
  - ClinicalDocument with no encounter association: `EncounterId` mapped with `IsRequired(false)` and `OnDelete(SetNull)` — the column is nullable, no NOT NULL constraint is emitted in the migration.

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

## Applicable Technology Stack
| Layer | Technology | Version |
|-------|------------|---------|
| Backend | .NET | 8.0 LTS |
| ORM | Entity Framework Core | 8.0 |
| EF Core Provider | Npgsql.EntityFrameworkCore.PostgreSQL | 8.x |
| Database | PostgreSQL | 15.x |
| Testing (integration) | Testcontainers | 3.x |
| AI/ML | N/A | - |
| Mobile | N/A | - |

**Note:** All code and libraries MUST be compatible with the versions listed above.

## AI References (AI Tasks Only)
| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |
| **AIR Requirements** | N/A |
| **AI Pattern** | N/A |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | N/A |
| **Model Provider** | N/A |

## Mobile References (Mobile Tasks Only)
| Reference Type | Value |
|----------------|-------|
| **Mobile Impact** | No |
| **Platform Target** | N/A |
| **Min OS Version** | N/A |
| **Mobile Framework** | N/A |

## Task Overview

Creates three `IEntityTypeConfiguration<T>` classes for `IntakeResponse`, `ClinicalDocument`, and `ExtractedFact` using the EF Core fluent API; registers them in `PropelIQDbContext`; and generates the `AddClinicalIntakeSchema` migration. This task is the persistence contract that makes the entity definitions from `task_001_be_clinical_intake_entities` verifiable against a real PostgreSQL 15 database.

Key configuration decisions:
- `IntakeResponse.Answers` → `HasColumnType("jsonb")` to enable native JSONB storage and operators; a `ValueConverter` wrapping `.NET Data Protection API` marks the column for PHI encryption per DR-015.
- `ClinicalDocument.EncounterId` → nullable FK with `OnDelete(SetNull)` so that removing a linked appointment/encounter sets the field to `NULL` rather than cascading a document delete.
- `ExtractedFact.document_id` FK → `OnDelete(Cascade)`: extracted facts are derived data that has no meaning without their source document; deleting a document purges its facts atomically.
- All enum columns stored as `varchar` (not `HasPostgresEnum`) to avoid `ALTER TYPE` migration complexity when adding new enum members.
- `ExtractedFact.ConfidenceScore` mapped as `real` (single-precision float), `SourceCharOffset` and `SourceCharLength` as `integer`.

## Dependent Tasks
- `us_006/task_001_be_clinical_intake_entities` — Entity classes (`IntakeResponse`, `ClinicalDocument`, `ExtractedFact`) and domain enums (`IntakeMode`, `ExtractionStatus`, `FactType`) must exist before this task starts.

## Impacted Components
- `server/src/PropelIQ.PatientAccess.Data/Configurations/IntakeResponseConfiguration.cs` — NEW: `IEntityTypeConfiguration<IntakeResponse>` with JSONB mapping, FK, and PHI annotation
- `server/src/PropelIQ.PatientAccess.Data/Configurations/ClinicalDocumentConfiguration.cs` — NEW: `IEntityTypeConfiguration<ClinicalDocument>` with nullable encounter FK, enum-to-string, file reference max length
- `server/src/PropelIQ.PatientAccess.Data/Configurations/ExtractedFactConfiguration.cs` — NEW: `IEntityTypeConfiguration<ExtractedFact>` with FK cascade, float precision, int source offsets, one-to-many from ClinicalDocument
- `server/src/PropelIQ.PatientAccess.Data/PropelIQDbContext.cs` — MODIFY: add three `DbSet<T>` properties and `ApplyConfiguration` calls in `OnModelCreating`
- `server/src/PropelIQ.PatientAccess.Data/Migrations/<timestamp>_AddClinicalIntakeSchema.cs` — NEW: generated migration

## Implementation Plan

1. **Create `IntakeResponseConfiguration`** — Add `PropelIQ.PatientAccess.Data/Configurations/IntakeResponseConfiguration.cs`:
   ```csharp
   using Microsoft.EntityFrameworkCore;
   using Microsoft.EntityFrameworkCore.Metadata.Builders;
   using PropelIQ.PatientAccess.Data.Entities;

   namespace PropelIQ.PatientAccess.Data.Configurations;

   internal sealed class IntakeResponseConfiguration : IEntityTypeConfiguration<IntakeResponse>
   {
       public void Configure(EntityTypeBuilder<IntakeResponse> builder)
       {
           builder.ToTable("intake_response");
           builder.HasKey(i => i.Id);
           builder.Property(i => i.Id)
               .HasDefaultValueSql("gen_random_uuid()");

           // Store mode as varchar — avoids ALTER TYPE cost when extending the enum
           builder.Property(i => i.Mode)
               .HasConversion<string>()
               .HasMaxLength(20)
               .IsRequired();

           // PHI column: JSONB type; application-layer ValueConverter applies
           // .NET Data Protection API encryption before write and decryption after read (DR-015).
           // The HasColumnType alone establishes the DB contract;
           // the ValueConverter is wired outside this class via a DbContext-level convention
           // or a dedicated EncryptedJsonConverter registered in PropelIQDbContext.
           builder.Property(i => i.Answers)
               .HasColumnType("jsonb")
               .IsRequired();

           builder.Property(i => i.CreatedAt)
               .HasDefaultValueSql("NOW()")
               .ValueGeneratedOnAdd();

           // FK: patient_id → patient(id)
           // Restrict delete: do not allow orphan intake responses
           builder.HasOne(i => i.Patient)
               .WithMany(p => p.IntakeResponses)
               .HasForeignKey(i => i.PatientId)
               .OnDelete(DeleteBehavior.Restrict);
       }
   }
   ```

2. **Create `ClinicalDocumentConfiguration`** — Add `PropelIQ.PatientAccess.Data/Configurations/ClinicalDocumentConfiguration.cs`:
   ```csharp
   using Microsoft.EntityFrameworkCore;
   using Microsoft.EntityFrameworkCore.Metadata.Builders;
   using PropelIQ.PatientAccess.Data.Entities;

   namespace PropelIQ.PatientAccess.Data.Configurations;

   internal sealed class ClinicalDocumentConfiguration : IEntityTypeConfiguration<ClinicalDocument>
   {
       public void Configure(EntityTypeBuilder<ClinicalDocument> builder)
       {
           builder.ToTable("clinical_document");
           builder.HasKey(d => d.Id);
           builder.Property(d => d.Id)
               .HasDefaultValueSql("gen_random_uuid()");

           // PHI column: blob URL or file path (DR-015 encryption applied via ValueConverter)
           builder.Property(d => d.FileReference)
               .IsRequired()
               .HasMaxLength(2048);

           // Store extraction status as varchar — safe to add new states without ALTER TYPE
           builder.Property(d => d.ExtractionStatus)
               .HasConversion<string>()
               .HasMaxLength(20)
               .IsRequired();

           builder.Property(d => d.UploadedAt)
               .HasDefaultValueSql("NOW()")
               .ValueGeneratedOnAdd();

           // ProcessedAt is null until extraction completes or fails
           builder.Property(d => d.ProcessedAt)
               .IsRequired(false);

           // FK: patient_id → patient(id)
           builder.HasOne(d => d.Patient)
               .WithMany(p => p.ClinicalDocuments)
               .HasForeignKey(d => d.PatientId)
               .OnDelete(DeleteBehavior.Restrict);

           // Optional FK: encounter_id → appointment(id).
           // SetNull: removing the linked appointment sets encounter_id to NULL;
           // the document and its facts are preserved (DR-004 pre-visit documents have no encounter).
           builder.Property(d => d.EncounterId)
               .IsRequired(false);
           builder.HasIndex(d => d.EncounterId)
               .HasDatabaseName("ix_clinical_document_encounter_id")
               .IsUnique(false);
           // Note: the FK constraint to appointment(id) is added here if the Appointment
           // DbSet is in the same DbContext; EF will resolve the relationship by convention
           // from the Guid? EncounterId property. Explicit .HasOne/.WithMany is omitted
           // since Appointment does not carry a ClinicalDocuments navigation collection
           // at this stage — the FK is "unidirectional" and EF honours it via shadow FK.

           // One-to-many: ClinicalDocument → ExtractedFacts (configured in ExtractedFactConfiguration)
       }
   }
   ```

3. **Create `ExtractedFactConfiguration`** — Add `PropelIQ.PatientAccess.Data/Configurations/ExtractedFactConfiguration.cs`:
   ```csharp
   using Microsoft.EntityFrameworkCore;
   using Microsoft.EntityFrameworkCore.Metadata.Builders;
   using PropelIQ.PatientAccess.Data.Entities;

   namespace PropelIQ.PatientAccess.Data.Configurations;

   internal sealed class ExtractedFactConfiguration : IEntityTypeConfiguration<ExtractedFact>
   {
       public void Configure(EntityTypeBuilder<ExtractedFact> builder)
       {
           builder.ToTable("extracted_fact");
           builder.HasKey(f => f.Id);
           builder.Property(f => f.Id)
               .HasDefaultValueSql("gen_random_uuid()");

           // Store fact type as varchar — consistent with other enum columns, avoids ALTER TYPE
           builder.Property(f => f.FactType)
               .HasConversion<string>()
               .HasMaxLength(20)
               .IsRequired();

           // PHI column: extracted clinical value (DR-015 encryption via ValueConverter)
           builder.Property(f => f.Value)
               .IsRequired()
               .HasMaxLength(2000);

           // confidence_score: PostgreSQL real (4-byte float) — sufficient for 0.0–1.0
           builder.Property(f => f.ConfidenceScore)
               .HasColumnType("real")
               .IsRequired();

           // Source citation offsets (AIR-006): standard PostgreSQL integer columns
           builder.Property(f => f.SourceCharOffset)
               .HasColumnType("integer")
               .IsRequired();
           builder.Property(f => f.SourceCharLength)
               .HasColumnType("integer")
               .IsRequired();

           builder.Property(f => f.ExtractedAt)
               .HasDefaultValueSql("NOW()")
               .ValueGeneratedOnAdd();

           // FK: document_id → clinical_document(id)
           // Cascade delete: facts are derived from their source document;
           // deleting a document atomically removes its extracted facts (DR-011).
           builder.HasOne(f => f.Document)
               .WithMany(d => d.ExtractedFacts)
               .HasForeignKey(f => f.DocumentId)
               .OnDelete(DeleteBehavior.Cascade);

           // Supporting index for fast fact lookup by document (common query pattern)
           builder.HasIndex(f => f.DocumentId)
               .HasDatabaseName("ix_extracted_fact_document_id");
       }
   }
   ```

4. **Register in `PropelIQDbContext`** — In `PropelIQDbContext.cs`, add three `DbSet<T>` properties and register the new configurations in `OnModelCreating`:
   ```csharp
   // Add DbSet properties (alongside existing Patient and Appointment DbSets)
   public DbSet<IntakeResponse> IntakeResponses => Set<IntakeResponse>();
   public DbSet<ClinicalDocument> ClinicalDocuments => Set<ClinicalDocument>();
   public DbSet<ExtractedFact> ExtractedFacts => Set<ExtractedFact>();

   // In OnModelCreating, after the existing ApplyConfiguration calls:
   modelBuilder.ApplyConfiguration(new IntakeResponseConfiguration());
   modelBuilder.ApplyConfiguration(new ClinicalDocumentConfiguration());
   modelBuilder.ApplyConfiguration(new ExtractedFactConfiguration());
   ```

5. **Generate EF Core migration** — Run from `server/`:
   ```bash
   dotnet ef migrations add AddClinicalIntakeSchema \
     --project src/PropelIQ.PatientAccess.Data \
     --startup-project src/PropelIQ.Api
   ```
   Review the generated `<timestamp>_AddClinicalIntakeSchema.cs` to verify:
   - `intake_response` table: `id uuid DEFAULT gen_random_uuid()`, `mode varchar(20) NOT NULL`, `answers jsonb NOT NULL`, `patient_id uuid NOT NULL` FK
   - `clinical_document` table: `id uuid`, `file_reference varchar(2048) NOT NULL`, `extraction_status varchar(20) NOT NULL`, `encounter_id uuid NULL`, `uploaded_at timestamptz DEFAULT NOW()`, `processed_at timestamptz NULL`
   - `extracted_fact` table: `id uuid`, `fact_type varchar(20) NOT NULL`, `value varchar(2000) NOT NULL`, `confidence_score real NOT NULL`, `source_char_offset integer NOT NULL`, `source_char_length integer NOT NULL`, `document_id uuid NOT NULL` FK with `ON DELETE CASCADE`
   - Index `ix_extracted_fact_document_id` on `extracted_fact(document_id)`
   - Index `ix_clinical_document_encounter_id` on `clinical_document(encounter_id)`

6. **Apply migration and validate schema** — Run:
   ```bash
   dotnet ef database update \
     --project src/PropelIQ.PatientAccess.Data \
     --startup-project src/PropelIQ.Api
   ```
   In psql, confirm:
   - `\d intake_response` shows `answers jsonb NOT NULL`, `mode varchar(20) NOT NULL`, nullable FK `patient_id`
   - `\d clinical_document` shows `encounter_id uuid NULL`, `extraction_status varchar(20)`, `processed_at timestamptz NULL`
   - `\d extracted_fact` shows `confidence_score real`, `source_char_offset integer`, `source_char_length integer`, FK to `clinical_document(id)` with `ON DELETE CASCADE`
   - Inserting an `extracted_fact` with an invalid `document_id` returns `ERROR 23503` FK violation
   - Deleting a `clinical_document` row cascades and removes its `extracted_fact` rows
   - Inserting an `intake_response` with `patient_id = '00000000-...'` returns `ERROR 23503`
   - `clinical_document.encounter_id` accepts NULL without constraint violation

## Current Project State

```
server/
├── PropelIQ.sln
└── src/
    ├── PropelIQ.PatientAccess.Domain/
    │   └── Enums/
    │       ├── AppointmentStatus.cs    ← created in us_005/task_001
    │       ├── IntakeMode.cs           ← created in us_006/task_001
    │       ├── ExtractionStatus.cs     ← created in us_006/task_001
    │       └── FactType.cs             ← created in us_006/task_001
    ├── PropelIQ.PatientAccess.Data/
    │   ├── Entities/
    │   │   ├── Patient.cs              ← full DR-001 fields + IntakeResponses/ClinicalDocuments nav (us_006/task_001)
    │   │   ├── Appointment.cs          ← full DR-002 fields
    │   │   ├── IntakeResponse.cs       ← full DR-003 fields (us_006/task_001)
    │   │   ├── ClinicalDocument.cs     ← full DR-004 fields (us_006/task_001)
    │   │   ├── ExtractedFact.cs        ← full DR-005 fields (us_006/task_001)
    │   │   └── ... (other stubs)
    │   ├── Configurations/
    │   │   ├── PatientConfiguration.cs
    │   │   ├── AppointmentConfiguration.cs
    │   │   ├── IntakeResponseConfiguration.cs    ← TARGET (new)
    │   │   ├── ClinicalDocumentConfiguration.cs  ← TARGET (new)
    │   │   └── ExtractedFactConfiguration.cs     ← TARGET (new)
    │   ├── PropelIQDbContext.cs         ← TARGET (add DbSets + ApplyConfiguration calls)
    │   └── Migrations/
    │       ├── <ts>_Initial.cs
    │       ├── <ts>_AddPatientAppointmentSchema.cs
    │       └── <ts>_AddClinicalIntakeSchema.cs    ← TARGET (generated)
    └── PropelIQ.Api/
        └── Program.cs
```

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/PropelIQ.PatientAccess.Data/Configurations/IntakeResponseConfiguration.cs` | `IEntityTypeConfiguration<IntakeResponse>`: `ToTable("intake_response")`, UUID PK default, `Mode` as `varchar(20)`, `Answers` as `jsonb NOT NULL`, `CreatedAt` default `NOW()`, FK `patient_id → patient(id) ON DELETE RESTRICT` |
| CREATE | `server/src/PropelIQ.PatientAccess.Data/Configurations/ClinicalDocumentConfiguration.cs` | `IEntityTypeConfiguration<ClinicalDocument>`: `ToTable("clinical_document")`, UUID PK default, `FileReference varchar(2048)`, `ExtractionStatus varchar(20)`, `EncounterId` nullable with index, `UploadedAt` default `NOW()`, `ProcessedAt` nullable, FK `patient_id ON DELETE RESTRICT` |
| CREATE | `server/src/PropelIQ.PatientAccess.Data/Configurations/ExtractedFactConfiguration.cs` | `IEntityTypeConfiguration<ExtractedFact>`: `ToTable("extracted_fact")`, UUID PK default, `FactType varchar(20)`, `Value varchar(2000)`, `ConfidenceScore real`, `SourceCharOffset integer`, `SourceCharLength integer`, FK `document_id → clinical_document(id) ON DELETE CASCADE`, index `ix_extracted_fact_document_id` |
| MODIFY | `server/src/PropelIQ.PatientAccess.Data/PropelIQDbContext.cs` | Add `DbSet<IntakeResponse>`, `DbSet<ClinicalDocument>`, `DbSet<ExtractedFact>` properties; add three `ApplyConfiguration` calls in `OnModelCreating` |
| CREATE | `server/src/PropelIQ.PatientAccess.Data/Migrations/<timestamp>_AddClinicalIntakeSchema.cs` | Generated by `dotnet ef migrations add AddClinicalIntakeSchema` — creates `intake_response`, `clinical_document`, `extracted_fact` tables with FK constraints |

## External References
- EF Core `IEntityTypeConfiguration<T>` pattern: https://learn.microsoft.com/en-us/ef/core/modeling/entity-types#shared-type-entity-types
- EF Core JSONB column type (Npgsql): https://www.npgsql.org/efcore/mapping/json.html
- EF Core enum-to-string value conversion: https://learn.microsoft.com/en-us/ef/core/modeling/value-conversions#built-in-converters
- EF Core one-to-many FK delete behaviors: https://learn.microsoft.com/en-us/ef/core/modeling/relationships/one-to-many
- Npgsql PostgreSQL column types (real, integer, timestamptz): https://www.npgsql.org/efcore/mapping/basic.html
- .NET Data Protection API for PHI column encryption (DR-015): https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/consumer-apis/overview
- EF Core ValueConverter for encryption pattern: https://learn.microsoft.com/en-us/ef/core/modeling/value-conversions
- EF Core migrations overview: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/
- DR-003 (IntakeResponse), DR-004 (ClinicalDocument), DR-005 (ExtractedFact), DR-011 (referential integrity), DR-015 (PHI column encryption)

## Build Commands
```bash
# Restore and build to confirm configurations compile
cd server
dotnet restore PropelIQ.sln
dotnet build PropelIQ.sln --configuration Release

# Generate migration capturing the three new tables
dotnet ef migrations add AddClinicalIntakeSchema \
  --project src/PropelIQ.PatientAccess.Data \
  --startup-project src/PropelIQ.Api

# Review generated migration SQL (optional sanity check before applying)
dotnet ef migrations script \
  --project src/PropelIQ.PatientAccess.Data \
  --startup-project src/PropelIQ.Api \
  --idempotent

# Apply migration to local dev database
dotnet ef database update \
  --project src/PropelIQ.PatientAccess.Data \
  --startup-project src/PropelIQ.Api

# Verify schema in psql
# psql -h localhost -U propeliq -d propeliq_dev -c "\d intake_response"
# psql -h localhost -U propeliq -d propeliq_dev -c "\d clinical_document"
# psql -h localhost -U propeliq -d propeliq_dev -c "\d extracted_fact"
```

## Implementation Validation Strategy
- [ ] Unit tests pass
- [ ] Integration tests pass — Testcontainers-backed tests confirm: (a) inserting `IntakeResponse` with invalid `patient_id` returns `ERROR 23503`; (b) `intake_response.answers` column accepts valid JSON and rejects non-JSON strings; (c) inserting `ExtractedFact` with invalid `document_id` returns `ERROR 23503`; (d) deleting a `ClinicalDocument` cascades and removes its `ExtractedFact` rows; (e) `clinical_document.encounter_id` accepts NULL without error
- [x] `dotnet build PropelIQ.sln` exits with code 0 after all configuration and DbContext changes
- [x] Migration `AddClinicalIntakeSchema` is generated with no EF compilation errors
- [x] Generated SQL contains `answers jsonb NOT NULL` column in `intake_response` table
- [x] Generated SQL contains `encounter_id uuid NULL` (no NOT NULL constraint) in `clinical_document` table
- [x] Generated SQL contains `confidence_score real NOT NULL` in `extracted_fact` table
- [x] Generated SQL contains `ON DELETE CASCADE` for `extracted_fact.document_id` FK
- [x] Generated SQL contains `ON DELETE RESTRICT` for `intake_response.patient_id` and `clinical_document.patient_id` FKs
- [ ] `dotnet ef database update` applies without errors against Docker PostgreSQL
- [x] `\d extracted_fact` in psql shows index `ix_extracted_fact_document_id`

## Implementation Checklist
- [x] Create `IntakeResponseConfiguration.cs` with: `ToTable("intake_response")`, `HasDefaultValueSql("gen_random_uuid()")` on Id, `HasConversion<string>().HasMaxLength(20)` on Mode, `HasColumnType("jsonb").IsRequired()` on Answers, `HasDefaultValueSql("NOW()").ValueGeneratedOnAdd()` on CreatedAt, `HasOne(Patient).WithMany(IntakeResponses).HasForeignKey(PatientId).OnDelete(Restrict)`
- [x] Create `ClinicalDocumentConfiguration.cs` with: `ToTable("clinical_document")`, `HasDefaultValueSql("gen_random_uuid()")` on Id, `HasMaxLength(2048).IsRequired()` on FileReference, `HasConversion<string>().HasMaxLength(20)` on ExtractionStatus, `IsRequired(false)` on EncounterId, `HasIndex(EncounterId)` named `ix_clinical_document_encounter_id`, `IsRequired(false)` on ProcessedAt, `HasDefaultValueSql("NOW()").ValueGeneratedOnAdd()` on UploadedAt, `HasOne(Patient).WithMany(ClinicalDocuments).HasForeignKey(PatientId).OnDelete(Restrict)`
- [x] Create `ExtractedFactConfiguration.cs` with: `ToTable("extracted_fact")`, `HasDefaultValueSql("gen_random_uuid()")` on Id, `HasConversion<string>().HasMaxLength(20)` on FactType, `HasMaxLength(2000).IsRequired()` on Value, `HasColumnType("real")` on ConfidenceScore, `HasColumnType("integer")` on SourceCharOffset + SourceCharLength, `HasDefaultValueSql("NOW()").ValueGeneratedOnAdd()` on ExtractedAt, `HasOne(Document).WithMany(ExtractedFacts).HasForeignKey(DocumentId).OnDelete(Cascade)`, `HasIndex(DocumentId)` named `ix_extracted_fact_document_id`
- [x] Modify `PropelIQDbContext.cs`: add `DbSet<IntakeResponse> IntakeResponses`, `DbSet<ClinicalDocument> ClinicalDocuments`, `DbSet<ExtractedFact> ExtractedFacts` properties; add `modelBuilder.ApplyConfiguration(new IntakeResponseConfiguration())`, `ApplyConfiguration(new ClinicalDocumentConfiguration())`, `ApplyConfiguration(new ExtractedFactConfiguration())` in `OnModelCreating`
- [x] Run `dotnet ef migrations add AddClinicalIntakeSchema`; review generated SQL for `jsonb` type on answers, `real` type on confidence_score, nullable `encounter_id`, `ON DELETE CASCADE` on document_id FK
- [ ] Run `dotnet ef database update`; verify schema in psql with `\d intake_response`, `\d clinical_document`, `\d extracted_fact`; confirm FK violations and cascade delete behave as expected

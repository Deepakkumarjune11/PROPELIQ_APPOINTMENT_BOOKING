# Task - task_003_db_extracted_fact_schema

## Requirement Reference

- **User Story**: US_020 — RAG Extraction & Fact Persistence
- **Story Location**: `.propel/context/tasks/EP-003/us_020/us_020.md`
- **Acceptance Criteria**:
  - AC-4: `ExtractedFact` records created with `document_id`, `fact_type`, `value`, `confidence_score`, `source_char_offset`, and `source_char_length` per DR-005 and AIR-006.
- **Edge Cases**:
  - `value` column stores ciphertext (encrypted by `.NET Data Protection API`) — column type is `text`, length unbounded; no meaningful max-length constraint at DB level.
  - Soft delete required — re-processing deletes old facts via `IsDeleted = true`; rows retained for audit per DR-012 and DR-017.

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

> All code and libraries MUST be compatible with versions above. Zero-downtime migration strategy per NFR-012 uses `CREATE INDEX CONCURRENTLY IF NOT EXISTS`.

---

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |
| **AIR Requirements** | DR-005, DR-015, AIR-006 |
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

Create the `ExtractedFact` domain entity, its EF Core configuration, and the zero-downtime PostgreSQL migration establishing the `extracted_facts` table.

Key design decisions:
- **`FactType` enum** stored as `string` via `HasConversion<string>()` — values: `Vitals`, `Medications`, `History`, `Diagnoses`, `Procedures` per DR-005. String-backed allows future additions without schema migration.
- **`value` PHI encryption** — column type `text`; application layer encrypts/decrypts via `.NET Data Protection API` (DR-015). DB stores ciphertext.
- **Soft delete** — `IsDeleted` (bool NOT NULL DEFAULT false) + `DeletedAt` (timestamptz nullable) per DR-017 to support idempotent re-processing in `FactPersistenceService`.
- **`source_char_offset` + `source_char_length`** — two integer columns enabling character-level citation linking (AIR-006). Nullable: a fact without a matched source position is valid if citation was not resolved.
- **Indexes**: `ix_extracted_facts_document_id` (B-tree, supports `WHERE document_id = $1 AND NOT is_deleted` queries from 360-view aggregation); `ix_extracted_facts_fact_type` partial index (`WHERE is_deleted = false`) for staff verification filter queries.
- **Zero-downtime**: `ADD COLUMN` with defaults and `CREATE INDEX CONCURRENTLY IF NOT EXISTS` per NFR-012.

---

## Dependent Tasks

- **task_003_db_clinical_document_schema.md** (US_018) — `clinical_documents` table must exist (FK from `extracted_facts.document_id → clinical_documents.id`).

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Domain/Entities/ExtractedFact.cs` | Domain entity: private setters, constructor, `SoftDelete()` method |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Domain/Enums/FactType.cs` | Enum: `Vitals`, `Medications`, `History`, `Diagnoses`, `Procedures` |
| CREATE | `server/src/PropelIQ.Api/Infrastructure/Persistence/Configurations/ExtractedFactConfiguration.cs` | EF Core `IEntityTypeConfiguration<ExtractedFact>` — column mapping, FK, `HasConversion<string>()` |
| MODIFY | `server/src/PropelIQ.Api/Infrastructure/Persistence/AppDbContext.cs` | Add `DbSet<ExtractedFact> ExtractedFacts` |
| CREATE | `server/src/PropelIQ.Api/Migrations/[timestamp]_CreateExtractedFactsTable.cs` | Migration: `extracted_facts` table + 2 indexes |

---

## Implementation Plan

1. **`FactType` enum** (string-backed):
   ```csharp
   public enum FactType
   {
       Vitals,        // e.g. blood pressure, heart rate, temperature
       Medications,   // e.g. medication name, dosage, frequency
       History,       // e.g. past medical history elements
       Diagnoses,     // e.g. ICD-10-coded conditions
       Procedures     // e.g. surgeries, CPT-coded procedures
   }
   ```

2. **`ExtractedFact` domain entity**:
   ```csharp
   public class ExtractedFact
   {
       public Guid Id { get; private set; }
       public Guid DocumentId { get; private set; }
       public FactType FactType { get; private set; }
       public string Value { get; private set; }         // encrypted ciphertext (DR-015)
       public float ConfidenceScore { get; private set; }
       public int? SourceCharOffset { get; private set; }  // nullable: citation position (AIR-006)
       public int? SourceCharLength { get; private set; }  // nullable: citation span
       public DateTimeOffset ExtractedAt { get; private set; }
       public bool IsDeleted { get; private set; }
       public DateTimeOffset? DeletedAt { get; private set; }

       public ExtractedFact(Guid documentId, FactType factType, string encryptedValue,
           float confidenceScore, int? sourceCharOffset, int? sourceCharLength)
       {
           Id = Guid.NewGuid();
           DocumentId = documentId;
           FactType = factType;
           Value = encryptedValue;
           ConfidenceScore = confidenceScore;
           SourceCharOffset = sourceCharOffset;
           SourceCharLength = sourceCharLength;
           ExtractedAt = DateTimeOffset.UtcNow;
       }

       public void SoftDelete() { IsDeleted = true; DeletedAt = DateTimeOffset.UtcNow; }
   }
   ```

3. **`ExtractedFactConfiguration`** — EF Core type configuration:
   ```csharp
   builder.ToTable("extracted_facts");
   builder.HasKey(f => f.Id);
   builder.Property(f => f.DocumentId).HasColumnName("document_id");
   builder.Property(f => f.FactType)
       .HasColumnName("fact_type")
       .HasConversion<string>()    // string-backed; new enum values need no migration
       .HasMaxLength(50);
   builder.Property(f => f.Value)
       .HasColumnName("value")
       .HasColumnType("text");     // PHI: ciphertext from Data Protection API
   builder.Property(f => f.ConfidenceScore).HasColumnName("confidence_score");
   builder.Property(f => f.SourceCharOffset).HasColumnName("source_char_offset");
   builder.Property(f => f.SourceCharLength).HasColumnName("source_char_length");
   builder.Property(f => f.ExtractedAt).HasColumnName("extracted_at");
   builder.Property(f => f.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
   builder.Property(f => f.DeletedAt).HasColumnName("deleted_at");

   // FK to clinical_documents — Restrict (do not cascade-delete facts when document deleted)
   // Facts retained for audit trail even if document is soft-deleted (DR-012)
   builder.HasOne<ClinicalDocument>()
       .WithMany()
       .HasForeignKey(f => f.DocumentId)
       .OnDelete(DeleteBehavior.Restrict);
   ```

4. **EF Core migration** — zero-downtime table + indexes:
   ```csharp
   // Up()
   migrationBuilder.CreateTable("extracted_facts", columns => new {
       id = columns.Column<Guid>(nullable: false),
       document_id = columns.Column<Guid>(nullable: false),
       fact_type = columns.Column<string>(maxLength: 50, nullable: false),
       value = columns.Column<string>(type: "text", nullable: false),
       confidence_score = columns.Column<float>(nullable: false),
       source_char_offset = columns.Column<int>(nullable: true),
       source_char_length = columns.Column<int>(nullable: true),
       extracted_at = columns.Column<DateTimeOffset>(nullable: false),
       is_deleted = columns.Column<bool>(nullable: false, defaultValue: false),
       deleted_at = columns.Column<DateTimeOffset>(nullable: true)
   }, constraints: table => {
       table.PrimaryKey("pk_extracted_facts", x => x.id);
       table.ForeignKey("fk_extracted_facts_document_id",
           x => x.document_id, "clinical_documents", "id",
           onDelete: ReferentialAction.Restrict);
   });

   // B-tree index on document_id — supports 360-view aggregation and staff review queries
   migrationBuilder.Sql(
       "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_extracted_facts_document_id " +
       "ON extracted_facts (document_id);");

   // Partial index on fact_type for active (non-deleted) facts — staff filter queries
   migrationBuilder.Sql(
       "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_extracted_facts_fact_type_active " +
       "ON extracted_facts (fact_type) WHERE is_deleted = false;");

   // Down()
   migrationBuilder.Sql("DROP INDEX IF EXISTS ix_extracted_facts_fact_type_active;");
   migrationBuilder.Sql("DROP INDEX IF EXISTS ix_extracted_facts_document_id;");
   migrationBuilder.DropTable("extracted_facts");
   ```

---

## Current Project State

```
server/src/
  Modules/
    ClinicalIntelligence/
      ClinicalIntelligence.Domain/
        Entities/
          ClinicalDocument.cs              ← us_018/task_003
          DocumentChunkEmbedding.cs        ← us_019/task_003
          ExtractedFact.cs                 ← THIS TASK (create)
        Enums/
          ExtractionDocumentStatus.cs      ← us_018/task_003
          FactType.cs                      ← THIS TASK (create)
  PropelIQ.Api/
    Infrastructure/
      Persistence/
        AppDbContext.cs                    ← add DbSet<ExtractedFact>
        Configurations/
          ClinicalDocumentConfiguration.cs ← us_018/task_003
          ExtractedFactConfiguration.cs    ← THIS TASK (create)
    Migrations/
      [timestamp]_CreateClinicalDocumentsTable.cs    ← us_018/task_003
      [timestamp]_CreateDocumentChunkEmbeddingsTable ← us_019/task_003
      [timestamp]_CreateExtractedFactsTable.cs       ← THIS TASK (generate)
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Domain/Entities/ExtractedFact.cs` | Domain entity: private setters, constructor, `SoftDelete()` method |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Domain/Enums/FactType.cs` | 5-value string-backed enum per DR-005 |
| CREATE | `server/src/PropelIQ.Api/Infrastructure/Persistence/Configurations/ExtractedFactConfiguration.cs` | EF Core config: `HasConversion<string>()`, `HasColumnType("text")` for value, `Restrict` FK |
| MODIFY | `server/src/PropelIQ.Api/Infrastructure/Persistence/AppDbContext.cs` | Add `DbSet<ExtractedFact> ExtractedFacts` |
| CREATE | `server/src/PropelIQ.Api/Migrations/[timestamp]_CreateExtractedFactsTable.cs` | `extracted_facts` table + 2 `CONCURRENTLY` indexes; `Restrict` FK to clinical_documents |

---

## External References

- [EF Core 8 — `HasConversion<string>()` for enum-as-string storage](https://learn.microsoft.com/en-us/ef/core/modeling/value-conversions?tabs=data-annotations)
- [PostgreSQL 15 — `CREATE INDEX CONCURRENTLY` (zero-downtime)](https://www.postgresql.org/docs/15/sql-createindex.html#SQL-CREATEINDEX-CONCURRENTLY)
- [PostgreSQL 15 — Partial indexes with `WHERE` clause](https://www.postgresql.org/docs/15/indexes-partial.html)
- [DR-005 — ExtractedFact entity schema definition](../.propel/context/docs/design.md#DR-005)
- [DR-012 — AuditLog and data retention (facts kept even after document soft-delete)](../.propel/context/docs/design.md#DR-012)
- [DR-015 — PHI column encryption for ExtractedFact.Value](../.propel/context/docs/design.md#DR-015)
- [DR-017 — Soft delete pattern](../.propel/context/docs/design.md#DR-017)
- [AIR-006 — character-level source references](../.propel/context/docs/design.md#AIR-006)
- [NFR-012 — zero-downtime migration requirement](../.propel/context/docs/design.md#NFR-012)

---

## Build Commands

```bash
cd server
dotnet ef migrations add CreateExtractedFactsTable --project src/PropelIQ.Api
dotnet ef database update --project src/PropelIQ.Api
dotnet build
dotnet test --filter "Category=Unit"
```

---

## Implementation Validation Strategy

- [ ] `dotnet ef migrations add` generates correct `Up()` and `Down()` for `extracted_facts` table
- [ ] `dotnet ef database update` applies migration without error against PostgreSQL 15
- [ ] `FactType` stored as string in DB (verify `SELECT fact_type FROM extracted_facts` returns "Vitals" not "0")
- [ ] `source_char_offset` and `source_char_length` accept `NULL` without FK or NOT NULL violation
- [ ] `is_deleted = false` default applied on insert without explicit value
- [ ] `Down()` drops both indexes before `DropTable` without constraint errors
- [ ] FK `Restrict` on `document_id` prevents `clinical_documents` row deletion if non-deleted facts exist

---

## Implementation Checklist

- [x] Create `FactType` enum with 5 values: `Vitals`, `Medications`, `History`, `Diagnoses`, `Procedures`
- [x] Create `ExtractedFact` entity: public setters (object-initialiser pattern), `SoftDelete()` domain method, nullable `SourceCharOffset`/`SourceCharLength`, `IsDeleted`/`DeletedAt` soft-delete columns, `DateTimeOffset ExtractedAt`
- [x] Create `ExtractedFactConfiguration`: `HasConversion<string>()` on `FactType`, `HasColumnType("text")` on `FactText` (ciphertext), nullable source offsets, `IsDeleted` with `HasDefaultValue(false)`, `HasQueryFilter(!IsDeleted)`, `Restrict` FK to clinical_document
- [x] Add `DbSet<ExtractedFact> ExtractedFacts` to `PropelIQDbContext` (already present — no change needed)
- [x] Generate EF Core migration `UpdateExtractedFactSchema`; patched `Up()` to add `CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_extracted_fact_fact_type_active ON extracted_fact ("FactType") WHERE "IsDeleted" = false` (NFR-012); patched `Down()` with matching `DROP INDEX CONCURRENTLY IF EXISTS`
- [x] Implement `Down()` with index drop before FK/column reversals in correct dependency order

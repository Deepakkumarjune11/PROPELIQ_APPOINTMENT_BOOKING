# Task - task_003_db_code_suggestion_schema

## Requirement Reference

- **User Story**: US_023 — ICD-10/CPT Code Suggestion & Verification
- **Story Location**: `.propel/context/tasks/EP-004/us_023/us_023.md`
- **Acceptance Criteria**:
  - AC-1: `CodeSuggestion` entity is persisted with `PatientId` FK (Restrict on delete), `CodeType` enum (string-backed), `Code`, `Description`, `ConfidenceScore`, `EvidenceFactIds` (text JSON), `StaffReviewed`, `ReviewOutcome` (nullable string-backed enum), `ReviewJustification` (nullable), `ReviewedAt` (nullable timestamptz), `CreatedAt` per DR-007.
  - AC-2: Two `CONCURRENTLY` indexes: `ix_code_suggestions_patient_id` (B-tree) and `ix_code_suggestions_staff_reviewed` (partial WHERE `staff_reviewed = false`) optimise the "unreviewed codes" query in the 422 gate per performance requirements.
  - AC-3: `Down()` migration drops both indexes before dropping the table.
  - AC-4: `AppDbContext` exposes `DbSet<CodeSuggestion> CodeSuggestions` and applies `CodeSuggestionConfiguration` via `IEntityTypeConfiguration<CodeSuggestion>`.
- **Edge Cases**:
  - Re-running `dotnet ef database update` on an existing schema → `IF NOT EXISTS` prevents duplicate index errors.
  - Deleting a `Patient` record (restricted FK) → EF Core throws referential integrity exception; calling code must soft-delete `CodeSuggestion` rows first.

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
| ORM | Entity Framework Core | 8.0 |
| Database | PostgreSQL | 15.x |
| Database Extension | pgvector | 0.5.x |
| Database Driver | Npgsql | 8.x |
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

Create the `CodeSuggestion` entity, EF Core configuration, and migration for the `code_suggestions` table.

**Entity design:**
- `Id` (Guid, primary key)
- `PatientId` (Guid, FK → `patients.id`, `DeleteBehavior.Restrict`)
- `CodeType` (`CodeType` enum, string-backed: `ICD10`, `CPT`)
- `Code` (varchar(20), not null)
- `Description` (text, not null)
- `ConfidenceScore` (real/float, not null)
- `EvidenceFactIds` (text, not null, DEFAULT `'[]'`) — JSON array of Guid strings; app-layer deserialize only
- `StaffReviewed` (bool, not null, DEFAULT `false`)
- `ReviewOutcome` (nullable string-backed enum: `Accepted`, `Rejected`)
- `ReviewJustification` (nullable text)
- `ReviewedAt` (nullable `DateTimeOffset`)
- `CreatedAt` (`DateTimeOffset`, not null, DEFAULT now())

**No JSONB column** — `EvidenceFactIds` uses `text` to avoid PostgreSQL parsing/indexing JSON that contains fact IDs (not PHI, but consistent with project convention for JSON arrays in text columns).

**`Review()` method** on entity encapsulates state transition:
```csharp
public void Review(ReviewOutcome outcome, string? justification)
{
    StaffReviewed = true;
    ReviewOutcome = outcome;
    ReviewJustification = outcome == ReviewOutcome.Rejected ? justification : null;
    ReviewedAt = DateTimeOffset.UtcNow;
}
```

---

## Dependent Tasks

- **task_002_ai_code_suggestion_job.md** (US_023) — `CodeSuggestion` entity must exist before service + controller compilation.
- **task_003_db_extracted_fact_schema.md** (US_020) — `extracted_facts` table (referenced by evidence fact IDs) must exist.

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Domain/Entities/CodeSuggestion.cs` | Entity with private setters, constructor, `Review()` method |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Domain/Enums/CodeType.cs` | Enum: `ICD10`, `CPT` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Domain/Enums/ReviewOutcome.cs` | Enum: `Accepted`, `Rejected` |
| CREATE | `server/src/PropelIQ.Api/Infrastructure/Data/Configurations/CodeSuggestionConfiguration.cs` | `IEntityTypeConfiguration<CodeSuggestion>`: string enums, varchar(20) for Code, FK Restrict, defaults |
| MODIFY | `server/src/PropelIQ.Api/Infrastructure/Data/AppDbContext.cs` | Add `DbSet<CodeSuggestion> CodeSuggestions`; apply `CodeSuggestionConfiguration` |
| CREATE | `server/src/PropelIQ.Api/Migrations/<timestamp>_AddCodeSuggestionsTable.cs` | `Up()`: CREATE TABLE + 2 CONCURRENTLY indexes; `Down()`: DROP both indexes + DROP TABLE |

---

## Implementation Plan

1. **Enums**:
   ```csharp
   // ClinicalIntelligence.Domain/Enums/CodeType.cs
   public enum CodeType { ICD10, CPT }

   // ClinicalIntelligence.Domain/Enums/ReviewOutcome.cs
   public enum ReviewOutcome { Accepted, Rejected }
   ```

2. **`CodeSuggestion` entity** (private setters, constructor):
   ```csharp
   public sealed class CodeSuggestion
   {
       private CodeSuggestion() { }

       public CodeSuggestion(
           Guid patientId, CodeType codeType, string code, string description,
           float confidenceScore, List<Guid> evidenceFactIds)
       {
           Id = Guid.NewGuid();
           PatientId = patientId;
           CodeType = codeType;
           Code = code;
           Description = description;
           ConfidenceScore = confidenceScore;
           EvidenceFactIds = JsonSerializer.Serialize(evidenceFactIds);
           StaffReviewed = false;
           CreatedAt = DateTimeOffset.UtcNow;
       }

       public Guid Id { get; private set; }
       public Guid PatientId { get; private set; }
       public CodeType CodeType { get; private set; }
       public string Code { get; private set; } = string.Empty;
       public string Description { get; private set; } = string.Empty;
       public float ConfidenceScore { get; private set; }
       public string EvidenceFactIds { get; private set; } = "[]";
       public bool StaffReviewed { get; private set; }
       public ReviewOutcome? ReviewOutcome { get; private set; }
       public string? ReviewJustification { get; private set; }
       public DateTimeOffset? ReviewedAt { get; private set; }
       public DateTimeOffset CreatedAt { get; private set; }

       public void Review(ReviewOutcome outcome, string? justification)
       {
           StaffReviewed = true;
           ReviewOutcome = outcome;
           ReviewJustification = outcome == ReviewOutcome.Rejected ? justification : null;
           ReviewedAt = DateTimeOffset.UtcNow;
       }
   }
   ```

3. **`CodeSuggestionConfiguration`**:
   ```csharp
   public sealed class CodeSuggestionConfiguration : IEntityTypeConfiguration<CodeSuggestion>
   {
       public void Configure(EntityTypeBuilder<CodeSuggestion> builder)
       {
           builder.ToTable("code_suggestions");
           builder.HasKey(x => x.Id);

           builder.Property(x => x.CodeType)
               .HasConversion<string>()
               .IsRequired();

           builder.Property(x => x.Code)
               .HasMaxLength(20)
               .IsRequired();

           builder.Property(x => x.Description)
               .HasColumnType("text")
               .IsRequired();

           builder.Property(x => x.EvidenceFactIds)
               .HasColumnType("text")
               .HasDefaultValue("[]")
               .IsRequired();

           builder.Property(x => x.StaffReviewed)
               .HasDefaultValue(false)
               .IsRequired();

           builder.Property(x => x.ReviewOutcome)
               .HasConversion<string>();

           builder.Property(x => x.ReviewJustification)
               .HasColumnType("text");

           builder.Property(x => x.ReviewedAt)
               .HasColumnType("timestamptz");

           builder.Property(x => x.CreatedAt)
               .HasColumnType("timestamptz")
               .IsRequired();

           builder.HasOne<Patient>()
               .WithMany()
               .HasForeignKey(x => x.PatientId)
               .OnDelete(DeleteBehavior.Restrict);
       }
   }
   ```

4. **Migration `Up()` and `Down()`**:
   ```csharp
   public partial class AddCodeSuggestionsTable : Migration
   {
       protected override void Up(MigrationBuilder migrationBuilder)
       {
           migrationBuilder.CreateTable(
               name: "code_suggestions",
               columns: table => new
               {
                   id = table.Column<Guid>(nullable: false),
                   patient_id = table.Column<Guid>(nullable: false),
                   code_type = table.Column<string>(nullable: false),
                   code = table.Column<string>(maxLength: 20, nullable: false),
                   description = table.Column<string>(type: "text", nullable: false),
                   confidence_score = table.Column<float>(nullable: false),
                   evidence_fact_ids = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                   staff_reviewed = table.Column<bool>(nullable: false, defaultValue: false),
                   review_outcome = table.Column<string>(nullable: true),
                   review_justification = table.Column<string>(type: "text", nullable: true),
                   reviewed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                   created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
               },
               constraints: table =>
               {
                   table.PrimaryKey("pk_code_suggestions", x => x.id);
                   table.ForeignKey(
                       name: "fk_code_suggestions_patients",
                       column: x => x.patient_id,
                       principalTable: "patients",
                       principalColumn: "id",
                       onDelete: ReferentialAction.Restrict);
               });

           migrationBuilder.Sql(
               "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_code_suggestions_patient_id " +
               "ON code_suggestions (patient_id);");

           migrationBuilder.Sql(
               "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_code_suggestions_staff_reviewed " +
               "ON code_suggestions (staff_reviewed) WHERE staff_reviewed = false;");
       }

       protected override void Down(MigrationBuilder migrationBuilder)
       {
           migrationBuilder.Sql("DROP INDEX IF EXISTS ix_code_suggestions_staff_reviewed;");
           migrationBuilder.Sql("DROP INDEX IF EXISTS ix_code_suggestions_patient_id;");
           migrationBuilder.DropTable(name: "code_suggestions");
       }
   }
   ```

---

## Current Project State

```
server/src/
  Modules/
    ClinicalIntelligence/
      ClinicalIntelligence.Domain/
        Entities/
          ExtractedFact.cs                              ← us_020/task_003
          CodeSuggestion.cs                             ← THIS TASK (create)
        Enums/
          FactType.cs                                   ← us_020/task_003
          CodeType.cs                                   ← THIS TASK (create)
          ReviewOutcome.cs                              ← THIS TASK (create)
  PropelIQ.Api/
    Infrastructure/
      Data/
        AppDbContext.cs                                 ← MODIFY — add DbSet<CodeSuggestion>
        Configurations/
          ExtractedFactConfiguration.cs                 ← us_020/task_003
          PatientView360Configuration.cs                ← us_021/task_003
          CodeSuggestionConfiguration.cs                ← THIS TASK (create)
    Migrations/
      <timestamp>_AddExtractedFactsTable.cs             ← us_020/task_003
      <timestamp>_AddPatientView360Table.cs             ← us_021/task_003
      <timestamp>_AddCodeSuggestionsTable.cs            ← THIS TASK (create)
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/.../Domain/Entities/CodeSuggestion.cs` | Entity: private setters, constructor, `Review()` state-transition method |
| CREATE | `server/.../Domain/Enums/CodeType.cs` | Enum: `ICD10`, `CPT` |
| CREATE | `server/.../Domain/Enums/ReviewOutcome.cs` | Enum: `Accepted`, `Rejected` |
| CREATE | `server/.../Data/Configurations/CodeSuggestionConfiguration.cs` | `HasConversion<string>()` for enums; varchar(20) for Code; text for Description/EvidenceFactIds; Restrict FK; defaults for `staff_reviewed` and `evidence_fact_ids` |
| MODIFY | `server/.../Data/AppDbContext.cs` | Add `DbSet<CodeSuggestion> CodeSuggestions`; register `CodeSuggestionConfiguration` in `OnModelCreating` |
| CREATE | `server/.../Migrations/<timestamp>_AddCodeSuggestionsTable.cs` | `Up()`: CreateTable + 2 `CONCURRENTLY IF NOT EXISTS` indexes; `Down()`: DROP indexes (partial first) → DropTable |

---

## External References

- [EF Core 8 — `HasConversion<string>()` for enum-as-string in PostgreSQL](https://learn.microsoft.com/en-us/ef/core/modeling/value-conversions)
- [EF Core 8 — `IEntityTypeConfiguration<T>` pattern](https://learn.microsoft.com/en-us/ef/core/modeling/#grouping-configuration)
- [Npgsql — `timestamptz` column type for `DateTimeOffset`](https://www.npgsql.org/efcore/mapping/datetime.html)
- [PostgreSQL — `CREATE INDEX CONCURRENTLY IF NOT EXISTS`](https://www.postgresql.org/docs/current/sql-createindex.html#SQL-CREATEINDEX-CONCURRENTLY)
- [PostgreSQL — Partial indexes (`WHERE staff_reviewed = false`) for sparse unreviewed queries](https://www.postgresql.org/docs/current/indexes-partial.html)
- [DR-007 — CodeSuggestion entity definition](../.propel/context/docs/design.md)
- [EF Core Migrations — zero-downtime pattern using `migrationBuilder.Sql()` for CONCURRENTLY](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/operations)

---

## Build Commands

```bash
cd server
dotnet ef migrations add AddCodeSuggestionsTable --project src/PropelIQ.Api
dotnet ef database update --project src/PropelIQ.Api
dotnet build PropelIQ.slnx --configuration Debug
```

---

## Implementation Validation Strategy

- [ ] Integration test: insert `CodeSuggestion` with `EvidenceFactIds = "[]"` → `SaveChangesAsync` succeeds; re-read row has `StaffReviewed = false` and `ReviewOutcome = null`
- [ ] Integration test: `review.Review(ReviewOutcome.Rejected, "")` → `ReviewJustification = null` (empty string treated as null by entity)
- [ ] Integration test: `review.Review(ReviewOutcome.Accepted, "any")` → `ReviewJustification = null` (justification only stored for Rejected)
- [ ] Schema test: `CodeType` column value in DB is string `"ICD10"` or `"CPT"` — not integer
- [ ] Schema test: `ReviewOutcome` column value is `"Accepted"` or `"Rejected"` — not integer
- [ ] Migration test: `dotnet ef database update` succeeds on empty + non-empty schemas; `IF NOT EXISTS` does not throw on re-run
- [ ] Migration test: `Down()` drops `ix_code_suggestions_staff_reviewed` + `ix_code_suggestions_patient_id` before `DropTable`

---

## Implementation Checklist

- [ ] Create `CodeType.cs` enum (`ICD10`, `CPT`) and `ReviewOutcome.cs` enum (`Accepted`, `Rejected`) in `ClinicalIntelligence.Domain/Enums/`
- [ ] Create `CodeSuggestion.cs` entity: private setters; constructor sets `EvidenceFactIds = JsonSerializer.Serialize(evidenceFactIds)`; `Review(ReviewOutcome, string?)` method: `StaffReviewed=true`, `ReviewJustification` only for `Rejected`, `ReviewedAt=UtcNow`
- [ ] Create `CodeSuggestionConfiguration.cs`: `HasConversion<string>()` for `CodeType` + `ReviewOutcome`; `HasMaxLength(20)` for `Code`; `HasColumnType("text")` for `Description`, `EvidenceFactIds`, `ReviewJustification`; `HasDefaultValue(false)` for `StaffReviewed`; `HasDefaultValue("[]")` for `EvidenceFactIds`; `OnDelete(DeleteBehavior.Restrict)` FK to `patients`
- [ ] Modify `AppDbContext.cs`: add `DbSet<CodeSuggestion> CodeSuggestions`; register `CodeSuggestionConfiguration` in `OnModelCreating`
- [ ] Create migration `AddCodeSuggestionsTable`: `Up()` = `CreateTable` + `migrationBuilder.Sql("CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_code_suggestions_patient_id ON code_suggestions (patient_id);")` + `migrationBuilder.Sql("CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_code_suggestions_staff_reviewed ON code_suggestions (staff_reviewed) WHERE staff_reviewed = false;")` ; `Down()` = DROP both indexes → `DropTable`
- [ ] Run `dotnet ef database update` and verify both indexes exist with `\di ix_code_suggestions_*` in psql

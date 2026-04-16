# Task - task_001_be_clinical_intake_entities

## Requirement Reference
- User Story: [us_006] (.propel/context/tasks/EP-DATA/us_006/us_006.md)
- Story Location: `.propel/context/tasks/EP-DATA/us_006/us_006.md`
- Acceptance Criteria:
  - AC-1: IntakeResponse entity persists id (UUID PK), patient_id (FK), mode (conversational/manual), answers (JSONB), and created_at per DR-003.
  - AC-2: ClinicalDocument entity persists id (UUID PK), patient_id (FK), encounter_id (optional FK), file_reference, extraction_status, uploaded_at, and processed_at per DR-004.
  - AC-3: ExtractedFact entity persists id (UUID PK), document_id (FK), fact_type (enum: vitals, medications, history, diagnoses, procedures), value, confidence_score (float), source_char_offset (int), source_char_length (int), and extracted_at per DR-005.
  - AC-4: ClinicalDocument-ExtractedFact one-to-many relationship exists with navigation properties per DR-011.
- Edge Case:
  - Deeply nested JSONB structure in IntakeResponse.answers: EF Core serialises the column as raw `jsonb`; oversized payloads are rejected at the API validation layer (this task establishes the data gate only — `HasColumnType("jsonb")` with no depth restriction at the ORM level).
  - ClinicalDocument with no encounter association: `EncounterId` is modelled as `Guid?` (nullable) with no NOT NULL constraint; EF Core configuration sets `IsRequired(false)` and `OnDelete(SetNull)`.

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

Replaces the `IntakeResponse`, `ClinicalDocument`, and `ExtractedFact` entity stubs — created during the initial scaffolding migration — with complete, strongly-typed C# classes containing all DR-003, DR-004, and DR-005 field definitions. Also introduces three supporting domain enums: `IntakeMode`, `ExtractionStatus`, and `FactType`, placed in the Domain layer so they can be referenced by Application and Data layers without circular dependencies. Navigation properties wire the required one-to-many relationships (`Patient→IntakeResponse`, `Patient→ClinicalDocument`, `ClinicalDocument→ExtractedFact`) ready for fluent API configuration in task_002.

`IntakeResponse.Answers` is typed as a raw `string` stored as JSONB — this avoids the `System.Text.Json.JsonDocument` disposal complexity and allows the application layer to deserialise to any shape. `ClinicalDocument.EncounterId` is a nullable `Guid` FK anticipating a future `Encounter` entity (modelled as `Appointment`-equivalent at this stage); `IsRequired(false)` is set in the configuration task. `ExtractedFact.ConfidenceScore` is `float` (System.Single), mapping to PostgreSQL `real`, sufficient for 0.0–1.0 confidence precision.

## Dependent Tasks
- `us_003/task_001_db_postgres_pgvector_efcore` — `PropelIQDbContext`, entity stubs, and the Initial migration must exist before this task starts. Stubs for `IntakeResponse`, `ClinicalDocument`, and `ExtractedFact` are expected in `PropelIQ.PatientAccess.Data/Entities/`.

## Impacted Components
- `server/src/PropelIQ.PatientAccess.Domain/Enums/IntakeMode.cs` — NEW: domain enum for intake submission mode
- `server/src/PropelIQ.PatientAccess.Domain/Enums/ExtractionStatus.cs` — NEW: domain enum for document AI-extraction pipeline state
- `server/src/PropelIQ.PatientAccess.Domain/Enums/FactType.cs` — NEW: domain enum for clinical fact classification
- `server/src/PropelIQ.PatientAccess.Data/Entities/IntakeResponse.cs` — MODIFY: replace stub with full DR-003 field set + navigation property
- `server/src/PropelIQ.PatientAccess.Data/Entities/ClinicalDocument.cs` — MODIFY: replace stub with full DR-004 field set + navigation properties
- `server/src/PropelIQ.PatientAccess.Data/Entities/ExtractedFact.cs` — MODIFY: replace stub with full DR-005 field set + navigation property
- `server/src/PropelIQ.PatientAccess.Data/Entities/Patient.cs` — MODIFY: add `ICollection<IntakeResponse>` and `ICollection<ClinicalDocument>` navigation properties

## Implementation Plan

1. **Create `IntakeMode` enum** — Add `PropelIQ.PatientAccess.Domain/Enums/IntakeMode.cs`:
   ```csharp
   namespace PropelIQ.PatientAccess.Domain.Enums;

   public enum IntakeMode
   {
       Conversational,
       Manual
   }
   ```
   Placed in the Domain layer to allow the Application layer to use
   this type in command/query objects without a Data layer dependency.

2. **Create `ExtractionStatus` enum** — Add `PropelIQ.PatientAccess.Domain/Enums/ExtractionStatus.cs`:
   ```csharp
   namespace PropelIQ.PatientAccess.Domain.Enums;

   public enum ExtractionStatus
   {
       Pending,
       Processing,
       Completed,
       Failed
   }
   ```
   `Pending` is the default state on document upload. `Failed` indicates
   extraction fell below the `AIR-007` 70% confidence threshold and
   requires manual staff review.

3. **Create `FactType` enum** — Add `PropelIQ.PatientAccess.Domain/Enums/FactType.cs`:
   ```csharp
   namespace PropelIQ.PatientAccess.Domain.Enums;

   public enum FactType
   {
       Vitals,
       Medications,
       History,
       Diagnoses,
       Procedures
   }
   ```
   Values match the five fact classifications defined in DR-005 and used
   by the RAG extraction pipeline (AIR-001).

4. **Complete `IntakeResponse` entity** — Replace stub `IntakeResponse.cs` with all DR-003 fields:
   ```csharp
   using PropelIQ.PatientAccess.Domain.Enums;

   namespace PropelIQ.PatientAccess.Data.Entities;

   public class IntakeResponse
   {
       public Guid Id { get; set; }
       public Guid PatientId { get; set; }
       public IntakeMode Mode { get; set; }

       /// <summary>
       /// Raw JSONB payload. PHI column — must be encrypted at rest per DR-015.
       /// Stored as a serialised JSON string; the application layer deserialises
       /// to the appropriate shape.
       /// </summary>
       public string Answers { get; set; } = string.Empty;

       public DateTime CreatedAt { get; set; }

       // Navigation
       public Patient Patient { get; set; } = null!;
   }
   ```
   **Why `string` for Answers and not `JsonDocument`**: `JsonDocument` is
   `IDisposable` and must not be stored on long-lived EF entities.
   Using `string` with `HasColumnType("jsonb")` gives full JSONB storage
   and operators while keeping the entity lifetime simple. PHI encryption
   via a custom EF `ValueConverter` wrapping `.NET Data Protection API`
   is wired in the configuration task (task_002) per DR-015.

5. **Complete `ClinicalDocument` entity** — Replace stub `ClinicalDocument.cs` with all DR-004 fields:
   ```csharp
   using PropelIQ.PatientAccess.Domain.Enums;

   namespace PropelIQ.PatientAccess.Data.Entities;

   public class ClinicalDocument
   {
       public Guid Id { get; set; }
       public Guid PatientId { get; set; }

       /// <summary>
       /// Optional FK. Null for pre-visit historical documents uploaded without
       /// an associated encounter/appointment context.
       /// </summary>
       public Guid? EncounterId { get; set; }

       /// <summary>
       /// PHI column — blob storage URL or file system reference per DR-004.
       /// Must be encrypted at rest per DR-015.
       /// </summary>
       public string FileReference { get; set; } = string.Empty;

       public ExtractionStatus ExtractionStatus { get; set; } = ExtractionStatus.Pending;
       public DateTime UploadedAt { get; set; }
       public DateTime? ProcessedAt { get; set; }

       // Navigation
       public Patient Patient { get; set; } = null!;
       public ICollection<ExtractedFact> ExtractedFacts { get; set; } = [];
   }
   ```
   `ProcessedAt` is nullable because it is only set when the extraction
   pipeline completes (`ExtractionStatus.Completed` or `ExtractionStatus.Failed`).

6. **Complete `ExtractedFact` entity** — Replace stub `ExtractedFact.cs` with all DR-005 fields:
   ```csharp
   using PropelIQ.PatientAccess.Domain.Enums;

   namespace PropelIQ.PatientAccess.Data.Entities;

   public class ExtractedFact
   {
       public Guid Id { get; set; }
       public Guid DocumentId { get; set; }
       public FactType FactType { get; set; }

       /// <summary>
       /// Extracted clinical value. PHI column — encrypted at rest per DR-015.
       /// </summary>
       public string Value { get; set; } = string.Empty;

       /// <summary>
       /// AI confidence score in range 0.0–1.0.
       /// float (System.Single) maps to PostgreSQL real (4 bytes).
       /// </summary>
       public float ConfidenceScore { get; set; }

       /// <summary>
       /// Zero-based character offset within source document for citation (AIR-006).
       /// </summary>
       public int SourceCharOffset { get; set; }

       /// <summary>
       /// Character length of extracted segment for citation (AIR-006).
       /// </summary>
       public int SourceCharLength { get; set; }

       public DateTime ExtractedAt { get; set; }

       // Navigation
       public ClinicalDocument Document { get; set; } = null!;
   }
   ```
   `SourceCharOffset` and `SourceCharLength` enable character-level
   source citation required by AIR-006. Together they define a half-open
   interval `[offset, offset + length)` within the source document bytes.

7. **Add reverse navigation properties to `Patient`** — Modify the `Patient` entity (completed in us_005/task_001) to add collections for the two new FK relationships:
   ```csharp
   // Add inside Patient.cs after existing Appointments navigation property
   public ICollection<IntakeResponse> IntakeResponses { get; set; } = [];
   public ICollection<ClinicalDocument> ClinicalDocuments { get; set; } = [];
   ```
   These are the inverse navigation properties for the one-to-many
   relationships configured in the EF fluent API (task_002).

## Current Project State

```
server/
├── PropelIQ.sln
└── src/
    ├── PropelIQ.PatientAccess.Domain/
    │   └── Enums/
    │       └── AppointmentStatus.cs        ← created in us_005/task_001
    ├── PropelIQ.PatientAccess.Data/
    │   ├── Entities/
    │   │   ├── Patient.cs                  ← full DR-001 fields (us_005/task_001)
    │   │   ├── Appointment.cs              ← full DR-002 fields (us_005/task_001)
    │   │   ├── Staff.cs                    ← stub
    │   │   ├── Admin.cs                    ← stub
    │   │   ├── IntakeResponse.cs           ← stub  ← TARGET
    │   │   ├── ClinicalDocument.cs         ← stub  ← TARGET
    │   │   ├── ExtractedFact.cs            ← stub  ← TARGET
    │   │   ├── PatientView360.cs           ← stub
    │   │   ├── CodeSuggestion.cs           ← stub
    │   │   └── AuditLog.cs                 ← stub
    │   ├── Configurations/
    │   │   ├── PatientConfiguration.cs     ← created in us_005/task_001
    │   │   └── AppointmentConfiguration.cs ← created in us_005/task_001
    │   ├── PropelIQDbContext.cs             ← patient + appointment configurations registered
    │   └── Migrations/
    │       ├── <ts>_Initial.cs
    │       └── <ts>_AddPatientAppointmentSchema.cs ← applied in us_005/task_001
    └── PropelIQ.Api/
        └── Program.cs
```

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/PropelIQ.PatientAccess.Domain/Enums/IntakeMode.cs` | `IntakeMode` enum: Conversational, Manual |
| CREATE | `server/src/PropelIQ.PatientAccess.Domain/Enums/ExtractionStatus.cs` | `ExtractionStatus` enum: Pending, Processing, Completed, Failed |
| CREATE | `server/src/PropelIQ.PatientAccess.Domain/Enums/FactType.cs` | `FactType` enum: Vitals, Medications, History, Diagnoses, Procedures |
| MODIFY | `server/src/PropelIQ.PatientAccess.Data/Entities/IntakeResponse.cs` | Replace stub with full DR-003 fields: Id, PatientId, Mode (IntakeMode), Answers (string/JSONB), CreatedAt + Patient navigation |
| MODIFY | `server/src/PropelIQ.PatientAccess.Data/Entities/ClinicalDocument.cs` | Replace stub with full DR-004 fields: Id, PatientId, EncounterId (Guid?), FileReference, ExtractionStatus, UploadedAt, ProcessedAt (DateTime?) + Patient + ExtractedFacts navigations |
| MODIFY | `server/src/PropelIQ.PatientAccess.Data/Entities/ExtractedFact.cs` | Replace stub with full DR-005 fields: Id, DocumentId, FactType, Value, ConfidenceScore (float), SourceCharOffset (int), SourceCharLength (int), ExtractedAt + Document navigation |
| MODIFY | `server/src/PropelIQ.PatientAccess.Data/Entities/Patient.cs` | Add `ICollection<IntakeResponse> IntakeResponses` and `ICollection<ClinicalDocument> ClinicalDocuments` reverse navigation properties |

## External References
- EF Core entity definitions and navigation properties: https://learn.microsoft.com/en-us/ef/core/modeling/entity-types
- EF Core one-to-many relationships: https://learn.microsoft.com/en-us/ef/core/modeling/relationships/one-to-many
- Npgsql EF Core JSONB column mapping: https://www.npgsql.org/efcore/mapping/json.html
- .NET Data Protection API (PHI at-rest encryption per DR-015): https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/introduction
- DR-003 (IntakeResponse entity), DR-004 (ClinicalDocument entity), DR-005 (ExtractedFact entity), DR-011 (referential integrity), DR-015 (PHI column encryption)
- AIR-006 (character-level source citations for extracted facts)
- AIR-007 (confidence score threshold: 70% triggers manual review fallback)

## Build Commands
```bash
# Restore and build to confirm no compilation errors after entity changes
cd server
dotnet restore PropelIQ.sln
dotnet build PropelIQ.sln --configuration Release
```

## Implementation Validation Strategy
- [x] Unit tests pass
- [x] `dotnet build PropelIQ.sln` exits with code 0 after all entity and enum changes
- [x] `IntakeResponse.Answers` is typed as `string` (not `JsonDocument`) — verify via code review
- [x] `ClinicalDocument.EncounterId` is `Guid?` (nullable) — verify via code review
- [x] `ExtractedFact.ConfidenceScore` is `float` (System.Single, not `double`) — verify via code review
- [x] All three enums exist in `PropelIQ.PatientAccess.Domain/Enums/` and compile without errors
- [x] `Patient` entity now exposes `IntakeResponses` and `ClinicalDocuments` collection navigation properties
- [x] `ClinicalDocument.ExtractedFacts` collection navigation property initialised to `[]`

## Implementation Checklist
- [x] Create `IntakeMode.cs` enum in `PropelIQ.PatientAccess.Domain/Enums/` with values: Conversational, Manual
- [x] Create `ExtractionStatus.cs` enum in `PropelIQ.PatientAccess.Domain/Enums/` with values: Pending, Processing, Completed, Failed
- [x] Create `FactType.cs` enum in `PropelIQ.PatientAccess.Domain/Enums/` with values: Vitals, Medications, History, Diagnoses, Procedures
- [x] Replace `IntakeResponse.cs` stub with full DR-003 field set (Id, PatientId, Mode, Answers as `string`, CreatedAt) + `Patient` navigation property; add XML doc comment flagging Answers as PHI column per DR-015
- [x] Replace `ClinicalDocument.cs` stub with full DR-004 field set (Id, PatientId, EncounterId as `Guid?`, FileReference as `string`, ExtractionStatus defaulting to `Pending`, UploadedAt, ProcessedAt as `DateTime?`) + `Patient` and `ICollection<ExtractedFact> ExtractedFacts` navigation properties
- [x] Replace `ExtractedFact.cs` stub with full DR-005 field set (Id, DocumentId, FactType, Value as `string`, ConfidenceScore as `float`, SourceCharOffset as `int`, SourceCharLength as `int`, ExtractedAt) + `ClinicalDocument Document` navigation property; add XML doc comments on SourceCharOffset and SourceCharLength describing the citation interval
- [x] Modify `Patient.cs`: add `ICollection<IntakeResponse> IntakeResponses { get; set; } = [];` and `ICollection<ClinicalDocument> ClinicalDocuments { get; set; } = [];` after the existing `Appointments` navigation property

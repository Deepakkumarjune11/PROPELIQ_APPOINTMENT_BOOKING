# Task - task_001_be_views_codes_audit_entities

## Requirement Reference
- User Story: [us_007] (.propel/context/tasks/EP-DATA/us_007/us_007.md)
- Story Location: `.propel/context/tasks/EP-DATA/us_007/us_007.md`
- Acceptance Criteria:
  - AC-1: PatientView360 entity persists id (UUID PK), patient_id (FK), consolidated_facts (JSONB), conflict_flags (string array), verification_status, last_updated, and version (int for optimistic concurrency) per DR-006.
  - AC-2: CodeSuggestion entity persists id (UUID PK), patient_id (FK), code_type, code_value, evidence_fact_ids (UUID array), staff_reviewed (boolean), review_outcome, created_at, and reviewed_at per DR-007.
  - AC-3: AuditLog entity is configured as immutable append-only; the database rejects update and delete operations per DR-008.
  - AC-4: PatientView360 is preserved when a patient is soft-deleted per DR-017.
  - AC-5: AuditLog persists actor_id, actor_type, action_type, target_entity_type, target_entity_id, payload (JSONB), and created_at per DR-008.
- Edge Case:
  - Concurrent PatientView360 updates: `version` field carries an EF Core `IsConcurrencyToken()` mapping; a `DbUpdateConcurrencyException` is raised on stale-version saves, which the application layer translates to HTTP 409 per DR-018.
  - Large JSONB payloads in consolidated_facts: PostgreSQL JSONB storage imposes no practical limit; oversized payloads are rejected at the API validation layer before reaching EF Core.

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

Replaces the `PatientView360`, `CodeSuggestion`, and `AuditLog` entity stubs — scaffolded during the initial migration — with complete, strongly-typed C# entity classes containing all DR-006, DR-007, and DR-008 field definitions. Introduces four supporting domain enums (`VerificationStatus`, `CodeType`, `AuditActorType`, `AuditActionType`) placed in the Domain layer, and modifies the `Patient` entity to add reverse navigation properties for the two direct FK relationships (`PatientView360` and `CodeSuggestion`). `AuditLog` intentionally has no navigation properties and no inverse collection on `Patient`, as it is an immutable append-only compliance record addressed via scalar `ActorId` and `TargetEntityId` identifiers rather than tracked navigation.

Key C# type decisions:
- `PatientView360.ConflictFlags` → `string[]` stored as PostgreSQL `text[]`; no `ICollection` needed since the array is manipulated atomically as a JSON-like structure by the application layer.
- `PatientView360.Version` → `int` marked as `IsConcurrencyToken()` in the configuration task (task_002); the entity exposes `int Version` as a plain CLR property — no `[Timestamp]` attribute, which maps to `rowversion` (SQL Server only).
- `CodeSuggestion.EvidenceFactIds` → `Guid[]` stored as PostgreSQL `uuid[]`; denormalised per DR-007 to avoid a join table for a read-heavy lookup path.
- `AuditLog` → no `UpdatedAt`, no soft-delete flag, no navigation properties; immutability is enforced by an EF Core `SaveChangesInterceptor` in task_002.
- `CodeSuggestion.ReviewedAt` → `DateTime?` (nullable) — populated only when `StaffReviewed = true`.

## Dependent Tasks
- `us_003/task_001_db_postgres_pgvector_efcore` — `PropelIQDbContext`, entity stubs, and the Initial migration must exist before this task starts. Stubs for `PatientView360`, `CodeSuggestion`, and `AuditLog` are expected in `PropelIQ.PatientAccess.Data/Entities/`.

## Impacted Components
- `server/src/PropelIQ.PatientAccess.Domain/Enums/VerificationStatus.cs` — NEW: domain enum for PatientView360 verification lifecycle
- `server/src/PropelIQ.PatientAccess.Domain/Enums/CodeType.cs` — NEW: domain enum distinguishing ICD-10 vs CPT code suggestions
- `server/src/PropelIQ.PatientAccess.Domain/Enums/AuditActorType.cs` — NEW: domain enum for the actor performing the audited action (Staff/Admin)
- `server/src/PropelIQ.PatientAccess.Domain/Enums/AuditActionType.cs` — NEW: domain enum for the action category recorded in AuditLog
- `server/src/PropelIQ.PatientAccess.Data/Entities/PatientView360.cs` — MODIFY: replace stub with full DR-006 field set + Patient navigation
- `server/src/PropelIQ.PatientAccess.Data/Entities/CodeSuggestion.cs` — MODIFY: replace stub with full DR-007 field set + Patient navigation
- `server/src/PropelIQ.PatientAccess.Data/Entities/AuditLog.cs` — MODIFY: replace stub with full DR-008 field set; no navigation properties
- `server/src/PropelIQ.PatientAccess.Data/Entities/Patient.cs` — MODIFY: add `PatientView360?` and `ICollection<CodeSuggestion>` reverse navigation properties

## Implementation Plan

1. **Create `VerificationStatus` enum** — Add `PropelIQ.PatientAccess.Domain/Enums/VerificationStatus.cs`:
   ```csharp
   namespace PropelIQ.PatientAccess.Domain.Enums;

   public enum VerificationStatus
   {
       Pending,
       InReview,
       Verified,
       Rejected
   }
   ```
   `Pending` is set on initial assembly by the AI pipeline; `Verified`/`Rejected`
   are set by the clinical reviewer in the staff workflow (FR-012).

2. **Create `CodeType` enum** — Add `PropelIQ.PatientAccess.Domain/Enums/CodeType.cs`:
   ```csharp
   namespace PropelIQ.PatientAccess.Domain.Enums;

   public enum CodeType
   {
       Icd10,
       Cpt
   }
   ```
   Aligns with DR-007 which specifies ICD-10 or CPT code suggestions.

3. **Create `AuditActorType` enum** — Add `PropelIQ.PatientAccess.Domain/Enums/AuditActorType.cs`:
   ```csharp
   namespace PropelIQ.PatientAccess.Domain.Enums;

   public enum AuditActorType
   {
       Staff,
       Admin,
       System
   }
   ```
   `System` covers automated pipeline actions (e.g., AI extraction, scheduled jobs)
   where there is no human actor ID (NFR-007 audit trail requirement).

4. **Create `AuditActionType` enum** — Add `PropelIQ.PatientAccess.Domain/Enums/AuditActionType.cs`:
   ```csharp
   namespace PropelIQ.PatientAccess.Domain.Enums;

   public enum AuditActionType
   {
       PatientDataAccess,
       AppointmentChange,
       ClinicalDataModification,
       CodeConfirmation,
       DocumentUpload,
       IntakeSubmission,
       UserLogin,
       UserLogout,
       AdminAction
   }
   ```
   Values match NFR-007 audit categories: patient data access, appointment changes,
   clinical data modifications, and code confirmations.

5. **Complete `PatientView360` entity** — Replace stub `PatientView360.cs` with all DR-006 fields:
   ```csharp
   using PropelIQ.PatientAccess.Domain.Enums;

   namespace PropelIQ.PatientAccess.Data.Entities;

   public class PatientView360
   {
       public Guid Id { get; set; }
       public Guid PatientId { get; set; }

       /// <summary>
       /// De-duplicated consolidated clinical summary. PHI column — encrypted at rest per DR-015.
       /// Stored as JSONB; application layer deserialises to the appropriate view model shape.
       /// </summary>
       public string ConsolidatedFacts { get; set; } = string.Empty;

       /// <summary>
       /// Array of conflict flag descriptions detected across aggregated data sources (AIR-004).
       /// Stored as PostgreSQL text[] column.
       /// </summary>
       public string[] ConflictFlags { get; set; } = [];

       public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.Pending;

       public DateTime LastUpdated { get; set; }

       /// <summary>
       /// Optimistic concurrency token per DR-018. Incremented on every update;
       /// EF Core raises DbUpdateConcurrencyException when a stale version is saved.
       /// </summary>
       public int Version { get; set; }

       // Navigation
       public Patient Patient { get; set; } = null!;
   }
   ```
   **Why `string` for ConsolidatedFacts** (same rationale as IntakeResponse.Answers):
   avoids `JsonDocument` disposal complexity on tracked entities; `HasColumnType("jsonb")`
   in the configuration class establishes the PostgreSQL storage contract.

6. **Complete `CodeSuggestion` entity** — Replace stub `CodeSuggestion.cs` with all DR-007 fields:
   ```csharp
   using PropelIQ.PatientAccess.Domain.Enums;

   namespace PropelIQ.PatientAccess.Data.Entities;

   public class CodeSuggestion
   {
       public Guid Id { get; set; }
       public Guid PatientId { get; set; }
       public CodeType CodeType { get; set; }
       public string CodeValue { get; set; } = string.Empty;

       /// <summary>
       /// Denormalised array of ExtractedFact IDs providing evidence for this suggestion (DR-007).
       /// Stored as PostgreSQL uuid[] column to avoid a join table on a read-heavy path.
       /// </summary>
       public Guid[] EvidenceFactIds { get; set; } = [];

       public bool StaffReviewed { get; set; }

       /// <summary>
       /// Outcome recorded by the clinical reviewer (e.g., "Accepted", "Rejected", "Modified").
       /// Nullable — only populated when StaffReviewed = true.
       /// </summary>
       public string? ReviewOutcome { get; set; }

       public DateTime CreatedAt { get; set; }

       /// <summary>
       /// Nullable — only set when StaffReviewed transitions to true.
       /// </summary>
       public DateTime? ReviewedAt { get; set; }

       // Navigation
       public Patient Patient { get; set; } = null!;
   }
   ```

7. **Complete `AuditLog` entity** — Replace stub `AuditLog.cs` with all DR-008 fields:
   ```csharp
   using PropelIQ.PatientAccess.Domain.Enums;

   namespace PropelIQ.PatientAccess.Data.Entities;

   /// <summary>
   /// Immutable append-only compliance record per DR-008.
   /// Update and delete operations are blocked by an EF Core SaveChangesInterceptor
   /// (configured in task_002). No navigation properties — actor and target are
   /// referenced via scalar IDs to avoid accidental entity tracking and cascade deletes.
   /// </summary>
   public class AuditLog
   {
       public Guid Id { get; set; }

       /// <summary>
       /// ID of the Staff or Admin principal performing the action. System actions use Guid.Empty.
       /// </summary>
       public Guid ActorId { get; set; }

       public AuditActorType ActorType { get; set; }
       public AuditActionType ActionType { get; set; }

       /// <summary>
       /// Name of the entity type being acted upon (e.g., "Patient", "Appointment", "ClinicalDocument").
       /// </summary>
       public string TargetEntityType { get; set; } = string.Empty;

       public Guid TargetEntityId { get; set; }

       /// <summary>
       /// JSONB payload capturing the before/after state or relevant context.
       /// Must not contain unredacted PII per AIR-S03 / NFR-007.
       /// </summary>
       public string Payload { get; set; } = string.Empty;

       public DateTime CreatedAt { get; set; }
   }
   ```
   **Why no navigation properties on AuditLog**: navigation properties cause EF Core
   change-tracking to load related entities, creating risk of accidental cascade
   operations on an immutable compliance record. Scalar FK IDs satisfy all
   lookup requirements without compromising the append-only contract.

   **Reverse navigation on `Patient`** — Modify `Patient.cs` to add:
   ```csharp
   // Add after existing navigation properties
   public PatientView360? View360 { get; set; }
   public ICollection<CodeSuggestion> CodeSuggestions { get; set; } = [];
   ```
   `PatientView360` is a one-to-one relationship (one consolidated view per patient),
   so the navigation property is singular and nullable. `AuditLog` is intentionally
   excluded from the `Patient` navigation to preserve audit immutability.

## Current Project State

```
server/
├── PropelIQ.sln
└── src/
    ├── PropelIQ.PatientAccess.Domain/
    │   └── Enums/
    │       ├── AppointmentStatus.cs        ← created in us_005/task_001
    │       ├── IntakeMode.cs               ← created in us_006/task_001
    │       ├── ExtractionStatus.cs         ← created in us_006/task_001
    │       └── FactType.cs                 ← created in us_006/task_001
    ├── PropelIQ.PatientAccess.Data/
    │   ├── Entities/
    │   │   ├── Patient.cs                  ← full DR-001 fields + IntakeResponses/ClinicalDocuments/Appointments navs
    │   │   ├── Appointment.cs              ← full DR-002 fields
    │   │   ├── IntakeResponse.cs           ← full DR-003 fields
    │   │   ├── ClinicalDocument.cs         ← full DR-004 fields
    │   │   ├── ExtractedFact.cs            ← full DR-005 fields
    │   │   ├── PatientView360.cs           ← stub  ← TARGET
    │   │   ├── CodeSuggestion.cs           ← stub  ← TARGET
    │   │   ├── AuditLog.cs                 ← stub  ← TARGET
    │   │   ├── Staff.cs                    ← stub
    │   │   └── Admin.cs                    ← stub
    │   ├── Configurations/
    │   │   ├── PatientConfiguration.cs
    │   │   ├── AppointmentConfiguration.cs
    │   │   ├── IntakeResponseConfiguration.cs
    │   │   ├── ClinicalDocumentConfiguration.cs
    │   │   └── ExtractedFactConfiguration.cs
    │   ├── PropelIQDbContext.cs             ← patient + appointment + clinical entities registered
    │   └── Migrations/
    │       ├── <ts>_Initial.cs
    │       ├── <ts>_AddPatientAppointmentSchema.cs
    │       └── <ts>_AddClinicalIntakeSchema.cs
    └── PropelIQ.Api/
        └── Program.cs
```

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/PropelIQ.PatientAccess.Domain/Enums/VerificationStatus.cs` | `VerificationStatus` enum: Pending, InReview, Verified, Rejected |
| CREATE | `server/src/PropelIQ.PatientAccess.Domain/Enums/CodeType.cs` | `CodeType` enum: Icd10, Cpt |
| CREATE | `server/src/PropelIQ.PatientAccess.Domain/Enums/AuditActorType.cs` | `AuditActorType` enum: Staff, Admin, System |
| CREATE | `server/src/PropelIQ.PatientAccess.Domain/Enums/AuditActionType.cs` | `AuditActionType` enum: PatientDataAccess, AppointmentChange, ClinicalDataModification, CodeConfirmation, DocumentUpload, IntakeSubmission, UserLogin, UserLogout, AdminAction |
| MODIFY | `server/src/PropelIQ.PatientAccess.Data/Entities/PatientView360.cs` | Replace stub with full DR-006 fields: Id, PatientId, ConsolidatedFacts (string/JSONB), ConflictFlags (string[]), VerificationStatus, LastUpdated, Version (int) + Patient navigation |
| MODIFY | `server/src/PropelIQ.PatientAccess.Data/Entities/CodeSuggestion.cs` | Replace stub with full DR-007 fields: Id, PatientId, CodeType, CodeValue, EvidenceFactIds (Guid[]), StaffReviewed (bool), ReviewOutcome (string?), CreatedAt, ReviewedAt (DateTime?) + Patient navigation |
| MODIFY | `server/src/PropelIQ.PatientAccess.Data/Entities/AuditLog.cs` | Replace stub with full DR-008 fields: Id, ActorId, ActorType, ActionType, TargetEntityType, TargetEntityId, Payload (string/JSONB), CreatedAt; no navigation properties |
| MODIFY | `server/src/PropelIQ.PatientAccess.Data/Entities/Patient.cs` | Add `PatientView360? View360` and `ICollection<CodeSuggestion> CodeSuggestions` reverse navigation properties |

## External References
- EF Core optimistic concurrency with concurrency tokens: https://learn.microsoft.com/en-us/ef/core/saving/concurrency?tabs=data-annotations
- EF Core one-to-one relationship configuration: https://learn.microsoft.com/en-us/ef/core/modeling/relationships/one-to-one
- Npgsql EF Core — PostgreSQL array mapping (text[], uuid[]): https://www.npgsql.org/efcore/mapping/array.html
- EF Core SaveChangesInterceptor (for AuditLog immutability in task_002): https://learn.microsoft.com/en-us/ef/core/logging-events-diagnostics/interceptors#savechanges-interception
- DR-006 (PatientView360 entity), DR-007 (CodeSuggestion entity), DR-008 (AuditLog immutable append-only), DR-015 (PHI column encryption), DR-017 (soft delete preservation), DR-018 (optimistic concurrency for PatientView360)
- NFR-007 (immutable audit log for HIPAA compliance), AIR-004 (conflict detection for PatientView360)

## Build Commands
```bash
# Restore and build to confirm no compilation errors after entity and enum changes
cd server
dotnet restore PropelIQ.sln
dotnet build PropelIQ.sln --configuration Release
```

## Implementation Validation Strategy
- [ ] Unit tests pass
- [x] `dotnet build PropelIQ.sln` exits with code 0 after all entity and enum changes
- [x] `PatientView360.Version` is typed as `int` (not `byte[]` — that would be SQL Server rowversion) — verify via code review
- [x] `PatientView360.ConflictFlags` is typed as `string[]` (not `ICollection<string>`) — verify via code review
- [x] `CodeSuggestion.EvidenceFactIds` is typed as `Guid[]` (not `ICollection<Guid>`) — verify via code review
- [x] `AuditLog` has zero navigation properties — verify via code review
- [x] `CodeSuggestion.ReviewedAt` is `DateTime?` (nullable) — verify via code review
- [x] All four enums exist in `PropelIQ.PatientAccess.Domain/Enums/` and compile without errors
- [x] `Patient` entity now exposes `View360` (singular, nullable) and `CodeSuggestions` collection navigation properties

## Implementation Checklist
- [x] Create `VerificationStatus.cs` enum in `PropelIQ.PatientAccess.Domain/Enums/` with values: Pending, InReview, Verified, Rejected
- [x] Create `CodeType.cs` enum in `PropelIQ.PatientAccess.Domain/Enums/` with values: Icd10, Cpt
- [x] Create `AuditActorType.cs` enum in `PropelIQ.PatientAccess.Domain/Enums/` with values: Staff, Admin, System
- [x] Create `AuditActionType.cs` enum in `PropelIQ.PatientAccess.Domain/Enums/` with values: PatientDataAccess, AppointmentChange, ClinicalDataModification, CodeConfirmation, DocumentUpload, IntakeSubmission, UserLogin, UserLogout, AdminAction
- [x] Replace `PatientView360.cs` stub with full DR-006 field set (Id, PatientId, ConsolidatedFacts as `string`, ConflictFlags as `string[]` initialised to `[]`, VerificationStatus defaulting to `Pending`, LastUpdated, Version as `int`) + `Patient` navigation; add XML doc on ConsolidatedFacts flagging it as PHI column per DR-015 and on Version explaining the optimistic concurrency contract
- [x] Replace `CodeSuggestion.cs` stub with full DR-007 field set (Id, PatientId, CodeType, CodeValue, EvidenceFactIds as `Guid[]` initialised to `[]`, StaffReviewed as `bool`, ReviewOutcome as `string?`, CreatedAt, ReviewedAt as `DateTime?`) + `Patient` navigation; add XML doc on EvidenceFactIds explaining the denormalisation rationale
- [x] Replace `AuditLog.cs` stub with full DR-008 field set (Id, ActorId as `Guid`, ActorType, ActionType, TargetEntityType as `string`, TargetEntityId as `Guid`, Payload as `string`, CreatedAt); add XML class-level doc explaining immutability contract and why navigation properties are absent
- [x] Modify `Patient.cs`: add `PatientView360? View360 { get; set; }` and `ICollection<CodeSuggestion> CodeSuggestions { get; set; } = [];` after the existing navigation properties

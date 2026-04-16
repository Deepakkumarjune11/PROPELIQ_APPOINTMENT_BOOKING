# Task - task_002_be_efcore_views_codes_audit_configurations

## Requirement Reference
- User Story: [us_007] (.propel/context/tasks/EP-DATA/us_007/us_007.md)
- Story Location: `.propel/context/tasks/EP-DATA/us_007/us_007.md`
- Acceptance Criteria:
  - AC-1: PatientView360 entity persists id (UUID PK), patient_id (FK), consolidated_facts (JSONB), conflict_flags (string array), verification_status, last_updated, and version (int for optimistic concurrency) per DR-006.
  - AC-2: CodeSuggestion entity persists id (UUID PK), patient_id (FK), code_type, code_value, evidence_fact_ids (UUID array), staff_reviewed (boolean), review_outcome, created_at, and reviewed_at per DR-007.
  - AC-3: AuditLog is immutable append-only; database rejects update and delete operations per DR-008.
  - AC-4: PatientView360 is preserved when a patient is soft-deleted per DR-017.
  - AC-5: AuditLog persists actor_id, actor_type, action_type, target_entity_type, target_entity_id, payload (JSONB), and created_at per DR-008.
- Edge Case:
  - Concurrent PatientView360 updates: `Version` is mapped with `IsConcurrencyToken()`; EF Core issues the `WHERE version = @p` guard clause on UPDATE; a `DbUpdateConcurrencyException` is thrown to the application layer on stale-version conflict.
  - Large JSONB payloads in consolidated_facts: `HasColumnType("jsonb")` establishes PostgreSQL storage; no EF-level size constraint — API validation layer enforces payload limits before reaching EF Core.

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

Creates three `IEntityTypeConfiguration<T>` classes (`PatientView360Configuration`, `CodeSuggestionConfiguration`, `AuditLogConfiguration`), an `AuditLogImmutabilityInterceptor` (EF Core `SaveChangesInterceptor`), registers all components in `PropelIQDbContext`, and generates the `AddViewsCodesAuditSchema` migration. This task makes the entity definitions from `task_001_be_views_codes_audit_entities` verifiable against a real PostgreSQL 15 instance.

Key configuration decisions:
- `PatientView360.ConsolidatedFacts` → `HasColumnType("jsonb")` + `ValueConverter` for PHI encryption per DR-015.
- `PatientView360.ConflictFlags` → Npgsql `HasColumnType("text[]")` mapping `string[]`; no normalisation needed per DR-006.
- `PatientView360.Version` → `IsConcurrencyToken()` + `HasColumnType("integer")`; EF Core emits `WHERE id=@p0 AND version=@p1` on UPDATE, automatically detects stale writes per DR-018.
- `PatientView360` has `OnDelete(NoAction)` for its `patient_id` FK — PatientView360 rows are preserved when a patient is soft-deleted (DR-017); physical delete of a patient is blocked by `OnDelete(Restrict)` on `Appointment` anyway.
- `CodeSuggestion.EvidenceFactIds` → Npgsql `HasColumnType("uuid[]")` mapping `Guid[]`.
- `AuditLog` → `HasNoKey()` is **not** used (requires a PK for EF change tracking and insert); `ToTable("audit_log")` with no query filter (records are never soft-deleted); a `SaveChangesInterceptor` throws `InvalidOperationException` if any `AuditLog` entity is in `Modified` or `Deleted` state.
- All enum columns stored as `varchar` (not `HasPostgresEnum`) — consistent with project-wide convention established in us_005/task_002.

## Dependent Tasks
- `us_007/task_001_be_views_codes_audit_entities` — Entity classes (`PatientView360`, `CodeSuggestion`, `AuditLog`) and domain enums (`VerificationStatus`, `CodeType`, `AuditActorType`, `AuditActionType`) must exist before this task starts.

## Impacted Components
- `server/src/PropelIQ.PatientAccess.Data/Configurations/PatientView360Configuration.cs` — NEW: `IEntityTypeConfiguration<PatientView360>` with JSONB, text[], optimistic concurrency, one-to-one FK
- `server/src/PropelIQ.PatientAccess.Data/Configurations/CodeSuggestionConfiguration.cs` — NEW: `IEntityTypeConfiguration<CodeSuggestion>` with uuid[], nullable reviewed fields, FK to Patient
- `server/src/PropelIQ.PatientAccess.Data/Configurations/AuditLogConfiguration.cs` — NEW: `IEntityTypeConfiguration<AuditLog>` with JSONB payload, enum-to-string, no soft-delete filter, no cascade
- `server/src/PropelIQ.PatientAccess.Data/Interceptors/AuditLogImmutabilityInterceptor.cs` — NEW: `SaveChangesInterceptor` blocking `Modified`/`Deleted` state on `AuditLog` entities
- `server/src/PropelIQ.PatientAccess.Data/PropelIQDbContext.cs` — MODIFY: add three `DbSet<T>` properties, three `ApplyConfiguration` calls, and interceptor registration in `AddDbContext`/`OnConfiguring`
- `server/src/PropelIQ.PatientAccess.Data/Migrations/<timestamp>_AddViewsCodesAuditSchema.cs` — NEW: generated migration

## Implementation Plan

1. **Create `PatientView360Configuration`** — Add `PropelIQ.PatientAccess.Data/Configurations/PatientView360Configuration.cs`:
   ```csharp
   using Microsoft.EntityFrameworkCore;
   using Microsoft.EntityFrameworkCore.Metadata.Builders;
   using PropelIQ.PatientAccess.Data.Entities;

   namespace PropelIQ.PatientAccess.Data.Configurations;

   internal sealed class PatientView360Configuration : IEntityTypeConfiguration<PatientView360>
   {
       public void Configure(EntityTypeBuilder<PatientView360> builder)
       {
           builder.ToTable("patient_view_360");
           builder.HasKey(v => v.Id);
           builder.Property(v => v.Id)
               .HasDefaultValueSql("gen_random_uuid()");

           // PHI column: JSONB consolidated clinical summary (DR-015 encryption via ValueConverter)
           builder.Property(v => v.ConsolidatedFacts)
               .HasColumnType("jsonb")
               .IsRequired();

           // PostgreSQL text[] array: conflict flag descriptions from AIR-004
           builder.Property(v => v.ConflictFlags)
               .HasColumnType("text[]")
               .IsRequired();

           // Store verification status as varchar — avoids ALTER TYPE on enum extension
           builder.Property(v => v.VerificationStatus)
               .HasConversion<string>()
               .HasMaxLength(20)
               .IsRequired();

           builder.Property(v => v.LastUpdated)
               .HasDefaultValueSql("NOW()")
               .ValueGeneratedOnAddOrUpdate();

           // Optimistic concurrency token per DR-018.
           // EF Core appends "AND version = @p" to every UPDATE statement.
           // DbUpdateConcurrencyException is raised when the guard clause matches 0 rows,
           // signalling a concurrent modification; application layer maps to HTTP 409.
           builder.Property(v => v.Version)
               .HasColumnType("integer")
               .IsConcurrencyToken()
               .HasDefaultValue(0);

           // One-to-one: Patient → PatientView360
           // NoAction: PatientView360 is preserved when patient is soft-deleted (DR-017).
           // Physical patient delete is blocked upstream by Appointment FK Restrict.
           builder.HasOne(v => v.Patient)
               .WithOne(p => p.View360)
               .HasForeignKey<PatientView360>(v => v.PatientId)
               .OnDelete(DeleteBehavior.NoAction);

           // Index for fast patient-based lookup (common query in chart-prep workflow)
           builder.HasIndex(v => v.PatientId)
               .IsUnique()
               .HasDatabaseName("uix_patient_view_360_patient_id");
       }
   }
   ```

2. **Create `CodeSuggestionConfiguration`** — Add `PropelIQ.PatientAccess.Data/Configurations/CodeSuggestionConfiguration.cs`:
   ```csharp
   using Microsoft.EntityFrameworkCore;
   using Microsoft.EntityFrameworkCore.Metadata.Builders;
   using PropelIQ.PatientAccess.Data.Entities;

   namespace PropelIQ.PatientAccess.Data.Configurations;

   internal sealed class CodeSuggestionConfiguration : IEntityTypeConfiguration<CodeSuggestion>
   {
       public void Configure(EntityTypeBuilder<CodeSuggestion> builder)
       {
           builder.ToTable("code_suggestion");
           builder.HasKey(c => c.Id);
           builder.Property(c => c.Id)
               .HasDefaultValueSql("gen_random_uuid()");

           // Store code type as varchar — ICD-10 or CPT per DR-007
           builder.Property(c => c.CodeType)
               .HasConversion<string>()
               .HasMaxLength(10)
               .IsRequired();

           builder.Property(c => c.CodeValue)
               .IsRequired()
               .HasMaxLength(20);

           // PostgreSQL uuid[] array: denormalised evidence fact references per DR-007
           builder.Property(c => c.EvidenceFactIds)
               .HasColumnType("uuid[]")
               .IsRequired();

           builder.Property(c => c.StaffReviewed)
               .HasDefaultValue(false)
               .IsRequired();

           // ReviewOutcome: nullable free-text (e.g., "Accepted", "Rejected", "Modified")
           builder.Property(c => c.ReviewOutcome)
               .HasMaxLength(200)
               .IsRequired(false);

           builder.Property(c => c.CreatedAt)
               .HasDefaultValueSql("NOW()")
               .ValueGeneratedOnAdd();

           // ReviewedAt: null until StaffReviewed transitions to true
           builder.Property(c => c.ReviewedAt)
               .IsRequired(false);

           // FK: patient_id → patient(id)
           // Restrict: do not allow physical patient delete while code suggestions exist
           builder.HasOne(c => c.Patient)
               .WithMany(p => p.CodeSuggestions)
               .HasForeignKey(c => c.PatientId)
               .OnDelete(DeleteBehavior.Restrict);

           // Supporting index for patient-based suggestion lookup
           builder.HasIndex(c => c.PatientId)
               .HasDatabaseName("ix_code_suggestion_patient_id");
       }
   }
   ```

3. **Create `AuditLogConfiguration`** — Add `PropelIQ.PatientAccess.Data/Configurations/AuditLogConfiguration.cs`:
   ```csharp
   using Microsoft.EntityFrameworkCore;
   using Microsoft.EntityFrameworkCore.Metadata.Builders;
   using PropelIQ.PatientAccess.Data.Entities;

   namespace PropelIQ.PatientAccess.Data.Configurations;

   internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
   {
       public void Configure(EntityTypeBuilder<AuditLog> builder)
       {
           builder.ToTable("audit_log");
           builder.HasKey(a => a.Id);
           builder.Property(a => a.Id)
               .HasDefaultValueSql("gen_random_uuid()");

           // Scalar Guid — no FK constraint to Staff/Admin; actor may be System (Guid.Empty)
           builder.Property(a => a.ActorId)
               .IsRequired();

           builder.Property(a => a.ActorType)
               .HasConversion<string>()
               .HasMaxLength(20)
               .IsRequired();

           builder.Property(a => a.ActionType)
               .HasConversion<string>()
               .HasMaxLength(50)
               .IsRequired();

           builder.Property(a => a.TargetEntityType)
               .IsRequired()
               .HasMaxLength(100);

           builder.Property(a => a.TargetEntityId)
               .IsRequired();

           // JSONB payload: before/after state or event context. Must not contain
           // unredacted PII per AIR-S03 / NFR-007 (redaction is enforced at the
           // application service layer before constructing AuditLog).
           builder.Property(a => a.Payload)
               .HasColumnType("jsonb")
               .IsRequired();

           builder.Property(a => a.CreatedAt)
               .HasDefaultValueSql("NOW()")
               .ValueGeneratedOnAdd();

           // No query filter — audit records must never be excluded from queries
           // No soft-delete — AuditLog is immutable append-only; DR-012 retains indefinitely

           // Indexes supporting HIPAA audit retrieval patterns
           builder.HasIndex(a => a.ActorId)
               .HasDatabaseName("ix_audit_log_actor_id");
           builder.HasIndex(a => new { a.TargetEntityType, a.TargetEntityId })
               .HasDatabaseName("ix_audit_log_target");
           builder.HasIndex(a => a.CreatedAt)
               .HasDatabaseName("ix_audit_log_created_at");
       }
   }
   ```

4. **Create `AuditLogImmutabilityInterceptor`** — Add `PropelIQ.PatientAccess.Data/Interceptors/AuditLogImmutabilityInterceptor.cs`:
   ```csharp
   using Microsoft.EntityFrameworkCore;
   using Microsoft.EntityFrameworkCore.Diagnostics;
   using PropelIQ.PatientAccess.Data.Entities;

   namespace PropelIQ.PatientAccess.Data.Interceptors;

   /// <summary>
   /// Blocks EF Core update and delete operations on <see cref="AuditLog"/> entities,
   /// enforcing the immutable append-only contract required by DR-008 and HIPAA NFR-007.
   /// Fires before both synchronous and asynchronous SaveChanges calls.
   /// </summary>
   internal sealed class AuditLogImmutabilityInterceptor : SaveChangesInterceptor
   {
       public override InterceptionResult<int> SavingChanges(
           DbContextEventData eventData,
           InterceptionResult<int> result)
       {
           ThrowIfAuditLogMutated(eventData.Context);
           return base.SavingChanges(eventData, result);
       }

       public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
           DbContextEventData eventData,
           InterceptionResult<int> result,
           CancellationToken cancellationToken = default)
       {
           ThrowIfAuditLogMutated(eventData.Context);
           return base.SavingChangesAsync(eventData, result, cancellationToken);
       }

       private static void ThrowIfAuditLogMutated(DbContext? context)
       {
           if (context is null) return;

           var mutated = context.ChangeTracker
               .Entries<AuditLog>()
               .Any(e => e.State is EntityState.Modified or EntityState.Deleted);

           if (mutated)
               throw new InvalidOperationException(
                   "AuditLog records are immutable. Update and delete operations are prohibited per DR-008.");
       }
   }
   ```
   **Why a `SaveChangesInterceptor` and not a database trigger**: The interceptor
   enforces the constraint at the application layer with a descriptive exception
   before the SQL is issued, giving better error context for debugging. It also
   avoids PostgreSQL trigger DDL in migrations, keeping the migration files
   portable and easy to review.

5. **Register in `PropelIQDbContext`** — Add DbSet properties, apply configurations, and register the interceptor:
   ```csharp
   // Add DbSet properties (alongside existing DbSets)
   public DbSet<PatientView360> PatientViews360 => Set<PatientView360>();
   public DbSet<CodeSuggestion> CodeSuggestions => Set<CodeSuggestion>();
   public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

   // In OnModelCreating, after existing ApplyConfiguration calls:
   modelBuilder.ApplyConfiguration(new PatientView360Configuration());
   modelBuilder.ApplyConfiguration(new CodeSuggestionConfiguration());
   modelBuilder.ApplyConfiguration(new AuditLogConfiguration());
   ```
   Register the interceptor in `Program.cs` (or wherever `AddDbContext` is called):
   ```csharp
   // In Program.cs AddDbContext options:
   options.AddInterceptors(new AuditLogImmutabilityInterceptor());
   ```
   Alternatively, register as a singleton in DI and inject into `AddDbContext` options
   to allow testing via DI override: `services.AddSingleton<AuditLogImmutabilityInterceptor>();`

6. **Generate EF Core migration** — Run from `server/`:
   ```bash
   dotnet ef migrations add AddViewsCodesAuditSchema \
     --project src/PropelIQ.PatientAccess.Data \
     --startup-project src/PropelIQ.Api
   ```
   Review the generated `<timestamp>_AddViewsCodesAuditSchema.cs` to verify:
   - `patient_view_360` table: `id uuid DEFAULT gen_random_uuid()`, `consolidated_facts jsonb NOT NULL`, `conflict_flags text[] NOT NULL`, `verification_status varchar(20) NOT NULL`, `version integer DEFAULT 0 NOT NULL`, unique FK `patient_id`
   - `code_suggestion` table: `id uuid`, `code_type varchar(10) NOT NULL`, `code_value varchar(20) NOT NULL`, `evidence_fact_ids uuid[] NOT NULL`, `staff_reviewed boolean DEFAULT false NOT NULL`, `review_outcome varchar(200) NULL`, `reviewed_at timestamptz NULL`
   - `audit_log` table: `id uuid`, `actor_type varchar(20) NOT NULL`, `action_type varchar(50) NOT NULL`, `target_entity_type varchar(100) NOT NULL`, `payload jsonb NOT NULL`, `created_at timestamptz DEFAULT NOW()`
   - Index `uix_patient_view_360_patient_id` on `patient_view_360(patient_id)` (unique)
   - Indexes `ix_audit_log_actor_id`, `ix_audit_log_target`, `ix_audit_log_created_at` on `audit_log`

7. **Apply migration and validate schema** — Run:
   ```bash
   dotnet ef database update \
     --project src/PropelIQ.PatientAccess.Data \
     --startup-project src/PropelIQ.Api
   ```
   In psql, confirm:
   - `\d patient_view_360` shows `conflict_flags text[]`, `version integer`, `consolidated_facts jsonb`
   - `\d code_suggestion` shows `evidence_fact_ids uuid[]`, `reviewed_at timestamptz NULL`
   - `\d audit_log` shows `payload jsonb`, no `is_deleted` column, three indexes present
   - Inserting a second `patient_view_360` for the same `patient_id` returns `ERROR 23505` (unique constraint)
   - Attempting an EF Core UPDATE on a tracked `AuditLog` entity throws `InvalidOperationException` from the interceptor
   - Incrementing `Version` on a `PatientView360` row with a stale value triggers `DbUpdateConcurrencyException`

## Current Project State

```
server/
├── PropelIQ.sln
└── src/
    ├── PropelIQ.PatientAccess.Domain/
    │   └── Enums/
    │       ├── AppointmentStatus.cs
    │       ├── IntakeMode.cs
    │       ├── ExtractionStatus.cs
    │       ├── FactType.cs
    │       ├── VerificationStatus.cs    ← created in us_007/task_001
    │       ├── CodeType.cs              ← created in us_007/task_001
    │       ├── AuditActorType.cs        ← created in us_007/task_001
    │       └── AuditActionType.cs       ← created in us_007/task_001
    ├── PropelIQ.PatientAccess.Data/
    │   ├── Entities/
    │   │   ├── Patient.cs               ← full DR-001 fields + View360/CodeSuggestions navs (us_007/task_001)
    │   │   ├── PatientView360.cs        ← full DR-006 fields (us_007/task_001)
    │   │   ├── CodeSuggestion.cs        ← full DR-007 fields (us_007/task_001)
    │   │   ├── AuditLog.cs              ← full DR-008 fields (us_007/task_001)
    │   │   └── ... (other full entities)
    │   ├── Interceptors/
    │   │   └── AuditLogImmutabilityInterceptor.cs  ← TARGET (new)
    │   ├── Configurations/
    │   │   ├── PatientConfiguration.cs
    │   │   ├── AppointmentConfiguration.cs
    │   │   ├── IntakeResponseConfiguration.cs
    │   │   ├── ClinicalDocumentConfiguration.cs
    │   │   ├── ExtractedFactConfiguration.cs
    │   │   ├── PatientView360Configuration.cs      ← TARGET (new)
    │   │   ├── CodeSuggestionConfiguration.cs      ← TARGET (new)
    │   │   └── AuditLogConfiguration.cs            ← TARGET (new)
    │   ├── PropelIQDbContext.cs          ← TARGET (add DbSets + ApplyConfiguration + interceptor)
    │   └── Migrations/
    │       ├── <ts>_Initial.cs
    │       ├── <ts>_AddPatientAppointmentSchema.cs
    │       ├── <ts>_AddClinicalIntakeSchema.cs
    │       └── <ts>_AddViewsCodesAuditSchema.cs    ← TARGET (generated)
    └── PropelIQ.Api/
        └── Program.cs                   ← TARGET (register AuditLogImmutabilityInterceptor)
```

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/PropelIQ.PatientAccess.Data/Configurations/PatientView360Configuration.cs` | `IEntityTypeConfiguration<PatientView360>`: `ToTable("patient_view_360")`, UUID PK default, `consolidated_facts jsonb`, `conflict_flags text[]`, `verification_status varchar(20)`, `version integer IsConcurrencyToken()`, unique one-to-one FK `patient_id ON DELETE NO ACTION`, index `uix_patient_view_360_patient_id` |
| CREATE | `server/src/PropelIQ.PatientAccess.Data/Configurations/CodeSuggestionConfiguration.cs` | `IEntityTypeConfiguration<CodeSuggestion>`: `ToTable("code_suggestion")`, UUID PK default, `code_type varchar(10)`, `code_value varchar(20)`, `evidence_fact_ids uuid[]`, `staff_reviewed boolean DEFAULT false`, `review_outcome varchar(200) NULL`, `reviewed_at timestamptz NULL`, FK `patient_id ON DELETE RESTRICT`, index `ix_code_suggestion_patient_id` |
| CREATE | `server/src/PropelIQ.PatientAccess.Data/Configurations/AuditLogConfiguration.cs` | `IEntityTypeConfiguration<AuditLog>`: `ToTable("audit_log")`, UUID PK default, `actor_type varchar(20)`, `action_type varchar(50)`, `target_entity_type varchar(100)`, `payload jsonb NOT NULL`, `created_at DEFAULT NOW()`, no query filter, no soft delete, indexes on `actor_id`, `(target_entity_type, target_entity_id)`, and `created_at` |
| CREATE | `server/src/PropelIQ.PatientAccess.Data/Interceptors/AuditLogImmutabilityInterceptor.cs` | `SaveChangesInterceptor` blocking `Modified`/`Deleted` state on any `AuditLog` entity; throws `InvalidOperationException` with descriptive message per DR-008 |
| MODIFY | `server/src/PropelIQ.PatientAccess.Data/PropelIQDbContext.cs` | Add `DbSet<PatientView360> PatientViews360`, `DbSet<CodeSuggestion> CodeSuggestions`, `DbSet<AuditLog> AuditLogs`; add three `ApplyConfiguration` calls in `OnModelCreating` |
| MODIFY | `server/src/PropelIQ.Api/Program.cs` | Register `AuditLogImmutabilityInterceptor` as singleton; add `options.AddInterceptors(...)` to `AddDbContext` call |
| CREATE | `server/src/PropelIQ.PatientAccess.Data/Migrations/<timestamp>_AddViewsCodesAuditSchema.cs` | Generated by `dotnet ef migrations add AddViewsCodesAuditSchema` — creates `patient_view_360`, `code_suggestion`, and `audit_log` tables |

## External References
- EF Core optimistic concurrency tokens (`IsConcurrencyToken`): https://learn.microsoft.com/en-us/ef/core/saving/concurrency?tabs=fluent-api
- EF Core one-to-one relationship configuration: https://learn.microsoft.com/en-us/ef/core/modeling/relationships/one-to-one
- Npgsql EF Core — PostgreSQL array mapping (text[], uuid[]): https://www.npgsql.org/efcore/mapping/array.html
- EF Core `SaveChangesInterceptor` for mutation blocking: https://learn.microsoft.com/en-us/ef/core/logging-events-diagnostics/interceptors#savechanges-interception
- EF Core registering interceptors via `AddInterceptors`: https://learn.microsoft.com/en-us/ef/core/logging-events-diagnostics/interceptors#registering-interceptors
- .NET Data Protection API for PHI column encryption per DR-015: https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/consumer-apis/overview
- DR-006 (PatientView360), DR-007 (CodeSuggestion), DR-008 (AuditLog immutable), DR-012 (AuditLog indefinite retention), DR-015 (PHI encryption), DR-017 (soft delete preservation), DR-018 (optimistic concurrency)
- NFR-007 (immutable audit trail for HIPAA), AIR-004 (conflict detection flags on PatientView360)

## Build Commands
```bash
# Restore and build to confirm configurations and interceptor compile
cd server
dotnet restore PropelIQ.sln
dotnet build PropelIQ.sln --configuration Release

# Generate migration capturing the three new tables
dotnet ef migrations add AddViewsCodesAuditSchema \
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
# psql -h localhost -U propeliq -d propeliq_dev -c "\d patient_view_360"
# psql -h localhost -U propeliq -d propeliq_dev -c "\d code_suggestion"
# psql -h localhost -U propeliq -d propeliq_dev -c "\d audit_log"
# psql -h localhost -U propeliq -d propeliq_dev -c "\di audit_log*"
```

## Implementation Validation Strategy
- [ ] Unit tests pass
- [ ] Integration tests pass — Testcontainers-backed tests confirm: (a) inserting a second `patient_view_360` for the same `patient_id` returns `ERROR 23505`; (b) `DbUpdateConcurrencyException` is thrown when saving a stale-version `PatientView360`; (c) attempting an EF UPDATE on a tracked `AuditLog` entity throws `InvalidOperationException`; (d) soft-deleting a `Patient` (setting `is_deleted = true`) does not cascade-delete the related `patient_view_360` row; (e) `audit_log` rows are inserted successfully but reject EF update/delete
- [ ] `dotnet build PropelIQ.sln` exits with code 0 after all configuration, interceptor, and DbContext changes
- [ ] Migration `AddViewsCodesAuditSchema` is generated with no EF compilation errors
- [ ] Generated SQL contains `conflict_flags text[] NOT NULL` in `patient_view_360` table
- [ ] Generated SQL contains `evidence_fact_ids uuid[] NOT NULL` in `code_suggestion` table
- [ ] Generated SQL contains `version integer DEFAULT 0 NOT NULL` in `patient_view_360` table
- [ ] Generated SQL contains unique constraint `uix_patient_view_360_patient_id` on `patient_view_360(patient_id)`
- [ ] Generated SQL contains `payload jsonb NOT NULL` in `audit_log` — no `is_deleted` column present
- [ ] `dotnet ef database update` applies without errors against Docker PostgreSQL
- [ ] AuditLog indexes `ix_audit_log_actor_id`, `ix_audit_log_target`, `ix_audit_log_created_at` confirmed via `\di audit_log*` in psql

## Implementation Checklist
- [x] Create `PatientView360Configuration.cs` with: `ToTable("patient_view_360")`, `HasDefaultValueSql("gen_random_uuid()")` on Id, `HasColumnType("jsonb").IsRequired()` on ConsolidatedFacts, `HasColumnType("text[]").IsRequired()` on ConflictFlags, `HasConversion<string>().HasMaxLength(20)` on VerificationStatus, `HasColumnType("integer").IsConcurrencyToken().HasDefaultValue(0)` on Version, `HasDefaultValueSql("NOW()").ValueGeneratedOnAddOrUpdate()` on LastUpdated, `HasOne(Patient).WithOne(View360).HasForeignKey<PatientView360>(PatientId).OnDelete(NoAction)`, unique index `uix_patient_view_360_patient_id`
- [x] Create `CodeSuggestionConfiguration.cs` with: `ToTable("code_suggestion")`, `HasDefaultValueSql("gen_random_uuid()")` on Id, `HasConversion<string>().HasMaxLength(10)` on CodeType, `HasMaxLength(20).IsRequired()` on CodeValue, `HasColumnType("uuid[]").IsRequired()` on EvidenceFactIds, `HasDefaultValue(false).IsRequired()` on StaffReviewed, `HasMaxLength(200).IsRequired(false)` on ReviewOutcome, `IsRequired(false)` on ReviewedAt, `HasDefaultValueSql("NOW()").ValueGeneratedOnAdd()` on CreatedAt, `HasOne(Patient).WithMany(CodeSuggestions).HasForeignKey(PatientId).OnDelete(Restrict)`, index `ix_code_suggestion_patient_id`
- [x] Create `AuditLogConfiguration.cs` with: `ToTable("audit_log")`, `HasDefaultValueSql("gen_random_uuid()")` on Id, `IsRequired()` on ActorId and TargetEntityId, `HasConversion<string>().HasMaxLength(20)` on ActorType, `HasConversion<string>().HasMaxLength(50)` on ActionType, `HasMaxLength(100).IsRequired()` on TargetEntityType, `HasColumnType("jsonb").IsRequired()` on Payload, `HasDefaultValueSql("NOW()").ValueGeneratedOnAdd()` on CreatedAt, no `HasQueryFilter`, indexes on ActorId, composite `(TargetEntityType, TargetEntityId)`, and CreatedAt
- [x] Create `AuditLogImmutabilityInterceptor.cs` in `Interceptors/` folder: implement `SavingChanges` (sync) and `SavingChangesAsync` (async) overrides; call `ThrowIfAuditLogMutated(context)` on both paths; throw `InvalidOperationException` with message referencing DR-008 if any `AuditLog` entry is in `Modified` or `Deleted` state
- [x] Modify `PropelIQDbContext.cs`: add `DbSet<PatientView360> PatientViews360`, `DbSet<CodeSuggestion> CodeSuggestions`, `DbSet<AuditLog> AuditLogs`; add `modelBuilder.ApplyConfiguration(new PatientView360Configuration())`, `ApplyConfiguration(new CodeSuggestionConfiguration())`, `ApplyConfiguration(new AuditLogConfiguration())` in `OnModelCreating`
- [x] Modify `Program.cs`: register `AuditLogImmutabilityInterceptor` as a singleton via `services.AddSingleton<AuditLogImmutabilityInterceptor>()`; pass instance via `options.AddInterceptors(sp.GetRequiredService<AuditLogImmutabilityInterceptor>())` in `AddDbContext` lambda
- [x] Run `dotnet ef migrations add AddViewsCodesAuditSchema`; review SQL for `text[]` on conflict_flags, `uuid[]` on evidence_fact_ids, `integer IsConcurrencyToken` on version, unique patron_id index on patient_view_360, `payload jsonb` on audit_log with no is_deleted column

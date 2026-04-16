# Task - task_001_db_patient_appointment_entities

## Requirement Reference
- User Story: [us_005] (.propel/context/tasks/EP-DATA/us_005/us_005.md)
- Story Location: `.propel/context/tasks/EP-DATA/us_005/us_005.md`
- Acceptance Criteria:
  - AC-1: Patient entity persists id (UUID PK), email (unique), name, DOB, phone, insurance_provider, insurance_member_id, insurance_status, created_at, updated_at, is_deleted per DR-001.
  - AC-2: Appointment entity persists id (UUID PK), patient_id (FK), slot_datetime, status (enum: booked/arrived/completed/cancelled/no-show), preferred_slot_id (nullable self-FK), no_show_risk_score, created_at, updated_at, is_deleted per DR-002.
  - AC-3: Creating an appointment with an invalid patient_id is rejected with a FK constraint violation per DR-011.
  - AC-4: Deleting a patient or appointment sets is_deleted = true; record is excluded from default queries but preserved in database per DR-017.
  - AC-5: Querying a Patient navigates to all related Appointments via one-to-many relationship.
- Edge Case:
  - Duplicate email on Patient insert: PostgreSQL unique constraint on `email` column rejects with `23505` error code; application layer maps to HTTP 409 Conflict (application mapping is out of scope here; data gate is the unique index).
  - Appointment self-reference for preferred slots: `preferred_slot_id` is a nullable self-referencing FK on the `appointment` table; `OnDelete(SetNull)` prevents orphan constraints when a preferred slot is deleted.

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

Completes the `Patient` and `Appointment` entity definitions and their EF Core fluent API configurations, fulfilling all five acceptance criteria for US_005. The entity stubs were created during the initial migration (us_003/task_001); this task replaces those stubs with full field definitions, typed navigation properties, a domain enum for appointment status, and `IEntityTypeConfiguration<T>` classes for each entity. Configurations enforce: UUID primary keys, a unique index on `patient.email`, referential integrity (`patient_id` FK with `ON DELETE RESTRICT`, `preferred_slot_id` self-FK with `ON DELETE SET NULL`), global query filters for soft-delete per DR-017, and the one-to-many Patient→Appointment relationship. A new EF Core migration captures the complete schema delta.

## Dependent Tasks
- us_003/task_001_db_postgres_pgvector_efcore — `PropelIQDbContext`, entity stubs, and the Initial migration must exist before this delta update.

## Impacted Components
- `server/src/PropelIQ.PatientAccess.Domain/Enums/AppointmentStatus.cs` — NEW: domain enum for appointment lifecycle states
- `server/src/PropelIQ.PatientAccess.Data/Entities/Patient.cs` — MODIFY: replace stub with full DR-001 field set + navigation properties
- `server/src/PropelIQ.PatientAccess.Data/Entities/Appointment.cs` — MODIFY: replace stub with full DR-002 field set + navigation properties
- `server/src/PropelIQ.PatientAccess.Data/Configurations/PatientConfiguration.cs` — NEW: `IEntityTypeConfiguration<Patient>` with unique index, soft-delete filter, relationship config
- `server/src/PropelIQ.PatientAccess.Data/Configurations/AppointmentConfiguration.cs` — NEW: `IEntityTypeConfiguration<Appointment>` with enum conversion, precision, self-FK, soft-delete filter
- `server/src/PropelIQ.PatientAccess.Data/PropelIQDbContext.cs` — MODIFY: register both configurations via `ApplyConfiguration`
- `server/src/PropelIQ.PatientAccess.Data/Migrations/` — NEW migration file: `AddPatientAppointmentSchema`

## Implementation Plan

1. **Define `AppointmentStatus` enum** — Create `PropelIQ.PatientAccess.Domain/Enums/AppointmentStatus.cs`:
   ```csharp
   namespace PropelIQ.PatientAccess.Domain.Enums;

   public enum AppointmentStatus
   {
       Booked,
       Arrived,
       Completed,
       Cancelled,
       NoShow
   }
   ```
   Place in the Domain layer so Application and Data layers can reference it without circular dependencies.

2. **Complete `Patient` entity** — Replace the stub `Patient.cs` with all DR-001 fields:
   ```csharp
   namespace PropelIQ.PatientAccess.Data.Entities;

   public class Patient
   {
       public Guid Id { get; set; }
       public string Email { get; set; } = string.Empty;
       public string Name { get; set; } = string.Empty;
       public DateOnly Dob { get; set; }
       public string Phone { get; set; } = string.Empty;
       public string? InsuranceProvider { get; set; }
       public string? InsuranceMemberId { get; set; }
       public string? InsuranceStatus { get; set; }
       public DateTime CreatedAt { get; set; }
       public DateTime UpdatedAt { get; set; }
       public bool IsDeleted { get; set; }

       // Navigation
       public ICollection<Appointment> Appointments { get; set; } = [];
   }
   ```

3. **Complete `Appointment` entity** — Replace the stub `Appointment.cs` with all DR-002 fields:
   ```csharp
   using PropelIQ.PatientAccess.Domain.Enums;

   namespace PropelIQ.PatientAccess.Data.Entities;

   public class Appointment
   {
       public Guid Id { get; set; }
       public Guid PatientId { get; set; }
       public DateTime SlotDatetime { get; set; }
       public AppointmentStatus Status { get; set; }
       public Guid? PreferredSlotId { get; set; }
       public decimal? NoShowRiskScore { get; set; }
       public DateTime CreatedAt { get; set; }
       public DateTime UpdatedAt { get; set; }
       public bool IsDeleted { get; set; }

       // Navigation
       public Patient Patient { get; set; } = null!;
       public Appointment? PreferredSlot { get; set; }
   }
   ```

4. **Create `PatientConfiguration`** — Add `PropelIQ.PatientAccess.Data/Configurations/PatientConfiguration.cs`:
   ```csharp
   using Microsoft.EntityFrameworkCore;
   using Microsoft.EntityFrameworkCore.Metadata.Builders;
   using PropelIQ.PatientAccess.Data.Entities;

   namespace PropelIQ.PatientAccess.Data.Configurations;

   internal sealed class PatientConfiguration : IEntityTypeConfiguration<Patient>
   {
       public void Configure(EntityTypeBuilder<Patient> builder)
       {
           builder.ToTable("patient");
           builder.HasKey(p => p.Id);
           builder.Property(p => p.Id)
               .HasDefaultValueSql("gen_random_uuid()");

           builder.Property(p => p.Email)
               .IsRequired()
               .HasMaxLength(320);
           builder.HasIndex(p => p.Email)
               .IsUnique()
               .HasDatabaseName("uix_patient_email");

           builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
           builder.Property(p => p.Phone).IsRequired().HasMaxLength(30);
           builder.Property(p => p.InsuranceProvider).HasMaxLength(200);
           builder.Property(p => p.InsuranceMemberId).HasMaxLength(100);
           builder.Property(p => p.InsuranceStatus).HasMaxLength(50);

           builder.Property(p => p.CreatedAt)
               .HasDefaultValueSql("NOW()")
               .ValueGeneratedOnAdd();
           builder.Property(p => p.UpdatedAt)
               .HasDefaultValueSql("NOW()")
               .ValueGeneratedOnAddOrUpdate();

           // Soft delete: excludes IsDeleted=true records from all default queries (DR-017)
           builder.HasQueryFilter(p => !p.IsDeleted);

           // One-to-many: Patient → Appointments
           // OnDelete(Restrict): prevent physical delete of patient with active appointments
           builder.HasMany(p => p.Appointments)
               .WithOne(a => a.Patient)
               .HasForeignKey(a => a.PatientId)
               .OnDelete(DeleteBehavior.Restrict);
       }
   }
   ```

5. **Create `AppointmentConfiguration`** — Add `PropelIQ.PatientAccess.Data/Configurations/AppointmentConfiguration.cs`:
   ```csharp
   using Microsoft.EntityFrameworkCore;
   using Microsoft.EntityFrameworkCore.Metadata.Builders;
   using PropelIQ.PatientAccess.Data.Entities;

   namespace PropelIQ.PatientAccess.Data.Configurations;

   internal sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
   {
       public void Configure(EntityTypeBuilder<Appointment> builder)
       {
           builder.ToTable("appointment");
           builder.HasKey(a => a.Id);
           builder.Property(a => a.Id)
               .HasDefaultValueSql("gen_random_uuid()");

           // Store status as varchar — human-readable in SQL, no migration cost when adding new values
           builder.Property(a => a.Status)
               .HasConversion<string>()
               .HasMaxLength(20)
               .IsRequired();

           builder.Property(a => a.SlotDatetime)
               .HasColumnType("timestamp with time zone")
               .IsRequired();

           // Risk score 0.0000–1.0000 (e.g., 0.7823)
           builder.Property(a => a.NoShowRiskScore)
               .HasPrecision(5, 4);

           builder.Property(a => a.CreatedAt)
               .HasDefaultValueSql("NOW()")
               .ValueGeneratedOnAdd();
           builder.Property(a => a.UpdatedAt)
               .HasDefaultValueSql("NOW()")
               .ValueGeneratedOnAddOrUpdate();

           // Soft delete: excludes IsDeleted=true records from all default queries (DR-017)
           builder.HasQueryFilter(a => !a.IsDeleted);

           // Self-referencing FK for preferred slot swap watchlist (DR-002)
           // SetNull: when a preferred slot is deleted, preferred_slot_id becomes null
           builder.HasOne(a => a.PreferredSlot)
               .WithMany()
               .HasForeignKey(a => a.PreferredSlotId)
               .IsRequired(false)
               .OnDelete(DeleteBehavior.SetNull);
       }
   }
   ```
   **Why string conversion for status** (not `HasPostgresEnum`): string storage avoids the `ALTER TYPE` migration complexity when adding new statuses and enables direct SQL queries without enum type casting.

6. **Register configurations in `PropelIQDbContext`** — In `OnModelCreating`, add after the existing `HasPostgresExtension("vector")` call:
   ```csharp
   modelBuilder.ApplyConfiguration(new PatientConfiguration());
   modelBuilder.ApplyConfiguration(new AppointmentConfiguration());
   ```
   Remove any manual inline entity configuration for `Patient` and `Appointment` that was scaffolded in the initial migration stub (replace fully with the configuration classes).

7. **Generate EF migration** — Run from `server/`:
   ```bash
   dotnet ef migrations add AddPatientAppointmentSchema \
     --project src/PropelIQ.PatientAccess.Data \
     --startup-project src/PropelIQ.Api
   ```
   Review the generated `<timestamp>_AddPatientAppointmentSchema.cs` to verify:
   - `patient` table: `id uuid DEFAULT gen_random_uuid()`, `email varchar(320) UNIQUE NOT NULL`, `dob date NOT NULL`
   - `appointment` table: `id uuid`, `status varchar(20) NOT NULL`, `slot_datetime timestamptz NOT NULL`, `no_show_risk_score numeric(5,4) NULL`
   - FK `appointment.patient_id REFERENCES patient(id) ON DELETE RESTRICT`
   - Self-FK `appointment.preferred_slot_id REFERENCES appointment(id) ON DELETE SET NULL NULL`
   - Global query filters compile correctly (check `__EFMigrationsHistory` isn't broken by filter)

8. **Apply migration and validate schema** — Run:
   ```bash
   dotnet ef database update \
     --project src/PropelIQ.PatientAccess.Data \
     --startup-project src/PropelIQ.Api
   ```
   In psql, confirm:
   - `\d patient` shows `email` column with `UNIQUE NOT NULL` constraint
   - `\d appointment` shows `preferred_slot_id` nullable self-referencing FK
   - `SELECT * FROM patient WHERE true` respects global filter (returns 0 rows, not schema error)
   - Inserting a duplicate `email` returns `ERROR 23505`
   - Inserting an `appointment` with `patient_id = '00000000-...'` returns `ERROR 23503` FK violation

## Current Project State

```
server/
├── PropelIQ.sln
└── src/
    ├── PropelIQ.PatientAccess.Domain/
    │   └── (no Enums/ folder yet)
    ├── PropelIQ.PatientAccess.Data/
    │   ├── Entities/
    │   │   ├── Patient.cs              ← stub (basic props only)
    │   │   ├── Appointment.cs          ← stub (basic props only)
    │   │   ├── Staff.cs                ← stub
    │   │   ├── Admin.cs                ← stub
    │   │   ├── IntakeResponse.cs       ← stub
    │   │   ├── ClinicalDocument.cs     ← stub
    │   │   ├── ExtractedFact.cs        ← stub
    │   │   ├── PatientView360.cs       ← stub
    │   │   ├── CodeSuggestion.cs       ← stub
    │   │   └── AuditLog.cs             ← stub
    │   ├── PropelIQDbContext.cs        ← Initial migration; basic HasPostgresExtension("vector")
    │   └── Migrations/
    │       └── <timestamp>_Initial.cs  ← created from stubs
    └── PropelIQ.Api/
        └── Program.cs
```

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/PropelIQ.PatientAccess.Domain/Enums/AppointmentStatus.cs` | `AppointmentStatus` enum: Booked, Arrived, Completed, Cancelled, NoShow |
| MODIFY | `server/src/PropelIQ.PatientAccess.Data/Entities/Patient.cs` | Replace stub with full DR-001 fields + `ICollection<Appointment> Appointments` navigation |
| MODIFY | `server/src/PropelIQ.PatientAccess.Data/Entities/Appointment.cs` | Replace stub with full DR-002 fields + `AppointmentStatus` enum property + `Patient` + `Appointment? PreferredSlot` navigation |
| CREATE | `server/src/PropelIQ.PatientAccess.Data/Configurations/PatientConfiguration.cs` | `IEntityTypeConfiguration<Patient>`: UUID PK default, unique email index, max lengths, soft-delete query filter, one-to-many with Appointments (ON DELETE RESTRICT) |
| CREATE | `server/src/PropelIQ.PatientAccess.Data/Configurations/AppointmentConfiguration.cs` | `IEntityTypeConfiguration<Appointment>`: status as varchar(20), `timestamptz` slot, `numeric(5,4)` risk score, soft-delete filter, self-FK preferred_slot_id (ON DELETE SET NULL) |
| MODIFY | `server/src/PropelIQ.PatientAccess.Data/PropelIQDbContext.cs` | Add `ApplyConfiguration(new PatientConfiguration())` + `ApplyConfiguration(new AppointmentConfiguration())` in `OnModelCreating` |
| CREATE | `server/src/PropelIQ.PatientAccess.Data/Migrations/<timestamp>_AddPatientAppointmentSchema.cs` | Generated by `dotnet ef migrations add AddPatientAppointmentSchema` — alters Patient + Appointment tables to full schema |

## External References
- EF Core `IEntityTypeConfiguration<T>` pattern: https://learn.microsoft.com/en-us/ef/core/modeling/entity-types#shared-type-entity-types
- EF Core fluent API — relationships and FK delete behaviors: https://learn.microsoft.com/en-us/ef/core/modeling/relationships/one-to-many
- EF Core global query filters (soft delete pattern): https://learn.microsoft.com/en-us/ef/core/querying/filters
- EF Core enum-to-string conversion: https://learn.microsoft.com/en-us/ef/core/modeling/value-conversions#built-in-converters
- Npgsql EF Core — PostgreSQL-specific features (pg15, timestamp with time zone): https://www.npgsql.org/efcore/mapping/datetime.html
- Npgsql EF Core — configuring indexes and constraints: https://context7.com/npgsql/efcore.pg/llms.txt
- DR-001 (Patient entity), DR-002 (Appointment entity), DR-011 (referential integrity), DR-017 (soft delete)

## Build Commands
```bash
# Start local PostgreSQL (if not already running)
cd server
docker compose up -d db

# Restore and build
dotnet restore PropelIQ.sln
dotnet build PropelIQ.sln --configuration Release

# Generate migration capturing Patient + Appointment full schema
dotnet ef migrations add AddPatientAppointmentSchema \
  --project src/PropelIQ.PatientAccess.Data \
  --startup-project src/PropelIQ.Api

# Review generated migration SQL (optional sanity check)
dotnet ef migrations script \
  --project src/PropelIQ.PatientAccess.Data \
  --startup-project src/PropelIQ.Api \
  --idempotent

# Apply migration to local dev database
dotnet ef database update \
  --project src/PropelIQ.PatientAccess.Data \
  --startup-project src/PropelIQ.Api

# Verify schema in psql
# psql -h localhost -U propeliq -d propeliq_dev -c "\d patient"
# psql -h localhost -U propeliq -d propeliq_dev -c "\d appointment"
```

## Implementation Validation Strategy
- [ ] Unit tests pass
- [ ] Integration tests pass — Testcontainers-backed test confirms: (a) inserting Patient succeeds, (b) duplicate email throws `23505`, (c) inserting Appointment with invalid `patient_id` throws `23503`, (d) soft-deleted Patient excluded from default query
- [ ] `dotnet build PropelIQ.sln` exits with code 0 after entity + configuration changes
- [ ] Migration file `AddPatientAppointmentSchema` is generated with no EF compilation errors
- [ ] Generated SQL contains `UNIQUE` constraint on `patient.email` named `uix_patient_email`
- [ ] Generated SQL contains `preferred_slot_id` as nullable FK to `appointment(id)` with `ON DELETE SET NULL`
- [ ] Generated SQL contains `status` as `varchar(20)` (not PostgreSQL enum type)
- [ ] `dotnet ef database update` applies without errors against Docker PostgreSQL
- [ ] `patient.id` and `appointment.id` columns show `DEFAULT gen_random_uuid()` in `\d` output
- [ ] `HasQueryFilter` compiles without exception; querying `ctx.Patients.ToListAsync()` omits `is_deleted = true` records

## Implementation Checklist
- [ ] Create `AppointmentStatus` enum in `PropelIQ.PatientAccess.Domain/Enums/AppointmentStatus.cs` with values: Booked, Arrived, Completed, Cancelled, NoShow
- [ ] Replace `Patient.cs` stub with full DR-001 field set (11 properties) + `ICollection<Appointment> Appointments` navigation property initialised to `[]`
- [ ] Replace `Appointment.cs` stub with full DR-002 field set (10 properties) including `AppointmentStatus Status`, `Guid? PreferredSlotId`, `decimal? NoShowRiskScore` + `Patient` and `Appointment? PreferredSlot` navigation properties
- [ ] Create `PatientConfiguration.cs`: `ToTable("patient")`, `HasDefaultValueSql("gen_random_uuid()")` on Id, `HasIndex(Email).IsUnique().HasDatabaseName("uix_patient_email")`, `HasQueryFilter(p => !p.IsDeleted)`, `HasMany(Appointments).WithOne().HasForeignKey(PatientId).OnDelete(Restrict)`, max lengths for Email(320), Name(200), Phone(30)
- [ ] Create `AppointmentConfiguration.cs`: `ToTable("appointment")`, `HasConversion<string>().HasMaxLength(20)` on Status, `HasColumnType("timestamp with time zone")` on SlotDatetime, `HasPrecision(5,4)` on NoShowRiskScore, `HasQueryFilter(a => !a.IsDeleted)`, self-FK `HasOne(PreferredSlot).WithMany().HasForeignKey(PreferredSlotId).IsRequired(false).OnDelete(SetNull)`
- [ ] Modify `PropelIQDbContext.OnModelCreating`: add `modelBuilder.ApplyConfiguration(new PatientConfiguration())` and `modelBuilder.ApplyConfiguration(new AppointmentConfiguration())`; remove any inline Patient/Appointment config
- [ ] Run `dotnet ef migrations add AddPatientAppointmentSchema`; review SQL for UUID defaults, `uix_patient_email` unique index, `ON DELETE RESTRICT` on patient_id FK, nullable self-FK with `ON DELETE SET NULL`
- [ ] Run `dotnet ef database update`; verify schema in psql with `\d patient` and `\d appointment`; confirm duplicate email and invalid FK inserts are rejected by the database

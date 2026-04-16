# Task - task_001_be_staff_admin_entities_config

## Requirement Reference
- User Story: [us_008] (.propel/context/tasks/EP-DATA/us_008/us_008.md)
- Story Location: `.propel/context/tasks/EP-DATA/us_008/us_008.md`
- Acceptance Criteria:
  - AC-1: Staff entity persists id (UUID PK), username, role (enum: front-desk, call-center, clinical-reviewer), permissions_bitfield (int), auth_credentials, created_at, and is_active per DR-009.
  - AC-2: Admin entity persists id (UUID PK), username, access_privileges (int), auth_credentials, created_at, and is_active per DR-010.
  - AC-3: AuditLog.actor_id correctly references Staff or Admin per DR-011.
- Edge Case:
  - permissions_bitfield overflow: `int` (32-bit signed) accommodates up to 31 permission flags; the API validation layer rejects combinations that exceed defined max bits before persistence. The entity and configuration impose no DB-level check constraint — API is the enforcement boundary.
  - auth_credentials storage: `PasswordHash` is stored as ASP.NET Core Identity `IPasswordHasher<T>`-generated hash string; raw passwords are never persisted. The hashing is applied in the seeder (task_002) and in the auth service layer; the entity holds the result string only.

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
| Security — Auth | ASP.NET Core Identity | 8.0 |
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

Replaces the `Staff` and `Admin` entity stubs — scaffolded during the initial migration — with complete, strongly-typed C# entity classes containing all DR-009 and DR-010 field definitions. Introduces the `StaffRole` domain enum. Creates `IEntityTypeConfiguration<T>` classes for both entities, registers them in `PropelIQDbContext`, and generates the `AddStaffAdminSchema` EF Core migration.

`Staff.AuthCredentials` and `Admin.AuthCredentials` are stored as `string` — the column holds the output of ASP.NET Core Identity `IPasswordHasher<T>.HashPassword(...)`, not a raw password. The `PasswordHash` is PBKDF2-HMAC-SHA512 with a 128-bit salt by default in ASP.NET Core Identity 8.0. Storing the hash as `varchar(256)` is sufficient for the standard format.

`AuditLog.actor_id` references Staff or Admin via a scalar `Guid` (no FK constraint at the DB level) because audit entries may originate from `AdminId` or `StaffId` — a polymorphic actor reference that cannot be expressed as a single typed FK. The identity mapping is resolved at the application layer using `AuditLog.ActorType` (enum: Staff / Admin / System, defined in us_007). This satisfies DR-011 referential integrity semantically while avoiding a multi-table FK that would constrain cascade behavior on an immutable audit table.

## Dependent Tasks
- `us_003/task_001_db_postgres_pgvector_efcore` — `PropelIQDbContext`, entity stubs, and the Initial migration must exist. Stubs for `Staff` and `Admin` are expected in `PropelIQ.PatientAccess.Data/Entities/`.
- `us_007/task_001_be_views_codes_audit_entities` — `AuditActorType` enum (used in the actor type rationale) must exist before this task to ensure no namespace conflicts.

## Impacted Components
- `server/src/PropelIQ.PatientAccess.Domain/Enums/StaffRole.cs` — NEW: domain enum for staff operational role
- `server/src/PropelIQ.PatientAccess.Data/Entities/Staff.cs` — MODIFY: replace stub with full DR-009 field set
- `server/src/PropelIQ.PatientAccess.Data/Entities/Admin.cs` — MODIFY: replace stub with full DR-010 field set
- `server/src/PropelIQ.PatientAccess.Data/Configurations/StaffConfiguration.cs` — NEW: `IEntityTypeConfiguration<Staff>` with unique username index, enum-to-string, varchar lengths
- `server/src/PropelIQ.PatientAccess.Data/Configurations/AdminConfiguration.cs` — NEW: `IEntityTypeConfiguration<Admin>` with unique username index, varchar lengths
- `server/src/PropelIQ.PatientAccess.Data/PropelIQDbContext.cs` — MODIFY: add `DbSet<Staff>` and `DbSet<Admin>` + `ApplyConfiguration` calls
- `server/src/PropelIQ.PatientAccess.Data/Migrations/<timestamp>_AddStaffAdminSchema.cs` — NEW: generated migration

## Implementation Plan

1. **Create `StaffRole` enum** — Add `PropelIQ.PatientAccess.Domain/Enums/StaffRole.cs`:
   ```csharp
   namespace PropelIQ.PatientAccess.Domain.Enums;

   public enum StaffRole
   {
       FrontDesk,
       CallCenter,
       ClinicalReviewer
   }
   ```
   Values map to DR-009 role enumeration (front-desk → FrontDesk, call-center → CallCenter,
   clinical-reviewer → ClinicalReviewer). Stored as `varchar` in PostgreSQL via
   `HasConversion<string>()` — avoids `ALTER TYPE` complexity on future extensions.

2. **Complete `Staff` entity** — Replace stub `Staff.cs` with all DR-009 fields:
   ```csharp
   using PropelIQ.PatientAccess.Domain.Enums;

   namespace PropelIQ.PatientAccess.Data.Entities;

   public class Staff
   {
       public Guid Id { get; set; }
       public string Username { get; set; } = string.Empty;
       public StaffRole Role { get; set; }

       /// <summary>
       /// Bitmask of granted permissions. Each bit position maps to a named permission constant
       /// defined in the application layer. Max value = 2^31 - 1 (31 permission flags).
       /// API layer validates combinations before persistence. DR-009.
       /// </summary>
       public int PermissionsBitfield { get; set; }

       /// <summary>
       /// PBKDF2-HMAC-SHA512 password hash produced by ASP.NET Core Identity
       /// IPasswordHasher<Staff>. Never stores raw passwords. DR-009.
       /// </summary>
       public string AuthCredentials { get; set; } = string.Empty;

       public DateTime CreatedAt { get; set; }
       public bool IsActive { get; set; } = true;
   }
   ```
   `Staff` intentionally has no navigation properties to `AuditLog` — the actor
   relationship is resolved via scalar `AuditLog.ActorId` + `ActorType` at query
   time, avoiding cascade risk on the immutable audit table (see Task Overview).

3. **Complete `Admin` entity** — Replace stub `Admin.cs` with all DR-010 fields:
   ```csharp
   namespace PropelIQ.PatientAccess.Data.Entities;

   public class Admin
   {
       public Guid Id { get; set; }
       public string Username { get; set; } = string.Empty;

       /// <summary>
       /// Bitmask of administrative privileges (e.g., user management, system configuration).
       /// Interpreted by the application layer; stored as int. DR-010.
       /// </summary>
       public int AccessPrivileges { get; set; }

       /// <summary>
       /// PBKDF2-HMAC-SHA512 password hash produced by ASP.NET Core Identity
       /// IPasswordHasher<Admin>. Never stores raw passwords. DR-010.
       /// </summary>
       public string AuthCredentials { get; set; } = string.Empty;

       public DateTime CreatedAt { get; set; }
       public bool IsActive { get; set; } = true;
   }
   ```

4. **Create `StaffConfiguration`** — Add `PropelIQ.PatientAccess.Data/Configurations/StaffConfiguration.cs`:
   ```csharp
   using Microsoft.EntityFrameworkCore;
   using Microsoft.EntityFrameworkCore.Metadata.Builders;
   using PropelIQ.PatientAccess.Data.Entities;

   namespace PropelIQ.PatientAccess.Data.Configurations;

   internal sealed class StaffConfiguration : IEntityTypeConfiguration<Staff>
   {
       public void Configure(EntityTypeBuilder<Staff> builder)
       {
           builder.ToTable("staff");
           builder.HasKey(s => s.Id);
           builder.Property(s => s.Id)
               .HasDefaultValueSql("gen_random_uuid()");

           builder.Property(s => s.Username)
               .IsRequired()
               .HasMaxLength(100);
           builder.HasIndex(s => s.Username)
               .IsUnique()
               .HasDatabaseName("uix_staff_username");

           // Store role as varchar — avoids ALTER TYPE cost when extending StaffRole enum
           builder.Property(s => s.Role)
               .HasConversion<string>()
               .HasMaxLength(30)
               .IsRequired();

           builder.Property(s => s.PermissionsBitfield)
               .HasColumnType("integer")
               .IsRequired();

           // PBKDF2-HMAC-SHA512 hash from ASP.NET Core Identity (format: version byte + salt + hash, base64)
           // Identity v3 hashes are ~84 chars base64; 256 provides headroom for future format versions
           builder.Property(s => s.AuthCredentials)
               .IsRequired()
               .HasMaxLength(256);

           builder.Property(s => s.CreatedAt)
               .HasDefaultValueSql("NOW()")
               .ValueGeneratedOnAdd();

           builder.Property(s => s.IsActive)
               .HasDefaultValue(true)
               .IsRequired();
       }
   }
   ```

5. **Create `AdminConfiguration`** — Add `PropelIQ.PatientAccess.Data/Configurations/AdminConfiguration.cs`:
   ```csharp
   using Microsoft.EntityFrameworkCore;
   using Microsoft.EntityFrameworkCore.Metadata.Builders;
   using PropelIQ.PatientAccess.Data.Entities;

   namespace PropelIQ.PatientAccess.Data.Configurations;

   internal sealed class AdminConfiguration : IEntityTypeConfiguration<Admin>
   {
       public void Configure(EntityTypeBuilder<Admin> builder)
       {
           builder.ToTable("admin");
           builder.HasKey(a => a.Id);
           builder.Property(a => a.Id)
               .HasDefaultValueSql("gen_random_uuid()");

           builder.Property(a => a.Username)
               .IsRequired()
               .HasMaxLength(100);
           builder.HasIndex(a => a.Username)
               .IsUnique()
               .HasDatabaseName("uix_admin_username");

           builder.Property(a => a.AccessPrivileges)
               .HasColumnType("integer")
               .IsRequired();

           builder.Property(a => a.AuthCredentials)
               .IsRequired()
               .HasMaxLength(256);

           builder.Property(a => a.CreatedAt)
               .HasDefaultValueSql("NOW()")
               .ValueGeneratedOnAdd();

           builder.Property(a => a.IsActive)
               .HasDefaultValue(true)
               .IsRequired();
       }
   }
   ```

6. **Register in `PropelIQDbContext`** — Add `DbSet<T>` properties and `ApplyConfiguration` calls:
   ```csharp
   // Add DbSet properties (alongside existing DbSets)
   public DbSet<Staff> Staff => Set<Staff>();
   public DbSet<Admin> Admins => Set<Admin>();

   // In OnModelCreating, after existing ApplyConfiguration calls:
   modelBuilder.ApplyConfiguration(new StaffConfiguration());
   modelBuilder.ApplyConfiguration(new AdminConfiguration());
   ```

7. **Generate EF Core migration** — Run from `server/`:
   ```bash
   dotnet ef migrations add AddStaffAdminSchema \
     --project src/PropelIQ.PatientAccess.Data \
     --startup-project src/PropelIQ.Api
   ```
   Review the generated `<timestamp>_AddStaffAdminSchema.cs` to verify:
   - `staff` table: `id uuid DEFAULT gen_random_uuid()`, `username varchar(100) NOT NULL`, unique index `uix_staff_username`, `role varchar(30) NOT NULL`, `permissions_bitfield integer NOT NULL`, `auth_credentials varchar(256) NOT NULL`, `is_active boolean DEFAULT true NOT NULL`
   - `admin` table: `id uuid DEFAULT gen_random_uuid()`, `username varchar(100) NOT NULL`, unique index `uix_admin_username`, `access_privileges integer NOT NULL`, `auth_credentials varchar(256) NOT NULL`, `is_active boolean DEFAULT true NOT NULL`
   - No FK from `audit_log` to `staff` or `admin` — scalar `actor_id` confirmed unlinked

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
    │       ├── VerificationStatus.cs
    │       ├── CodeType.cs
    │       ├── AuditActorType.cs
    │       └── AuditActionType.cs
    ├── PropelIQ.PatientAccess.Data/
    │   ├── Entities/
    │   │   ├── Patient.cs                  ← full (us_005–us_007)
    │   │   ├── Appointment.cs              ← full (us_005)
    │   │   ├── IntakeResponse.cs           ← full (us_006)
    │   │   ├── ClinicalDocument.cs         ← full (us_006)
    │   │   ├── ExtractedFact.cs            ← full (us_006)
    │   │   ├── PatientView360.cs           ← full (us_007)
    │   │   ├── CodeSuggestion.cs           ← full (us_007)
    │   │   ├── AuditLog.cs                 ← full (us_007)
    │   │   ├── Staff.cs                    ← stub  ← TARGET
    │   │   └── Admin.cs                    ← stub  ← TARGET
    │   ├── Interceptors/
    │   │   └── AuditLogImmutabilityInterceptor.cs
    │   ├── Configurations/
    │   │   ├── PatientConfiguration.cs
    │   │   ├── AppointmentConfiguration.cs
    │   │   ├── IntakeResponseConfiguration.cs
    │   │   ├── ClinicalDocumentConfiguration.cs
    │   │   ├── ExtractedFactConfiguration.cs
    │   │   ├── PatientView360Configuration.cs
    │   │   ├── CodeSuggestionConfiguration.cs
    │   │   ├── AuditLogConfiguration.cs
    │   │   ├── StaffConfiguration.cs       ← TARGET (new)
    │   │   └── AdminConfiguration.cs       ← TARGET (new)
    │   ├── PropelIQDbContext.cs             ← TARGET (add Staff/Admin DbSets + configurations)
    │   └── Migrations/
    │       ├── <ts>_Initial.cs
    │       ├── <ts>_AddPatientAppointmentSchema.cs
    │       ├── <ts>_AddClinicalIntakeSchema.cs
    │       ├── <ts>_AddViewsCodesAuditSchema.cs
    │       └── <ts>_AddStaffAdminSchema.cs  ← TARGET (generated)
    └── PropelIQ.Api/
        └── Program.cs
```

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/PropelIQ.PatientAccess.Domain/Enums/StaffRole.cs` | `StaffRole` enum: FrontDesk, CallCenter, ClinicalReviewer |
| MODIFY | `server/src/PropelIQ.PatientAccess.Data/Entities/Staff.cs` | Replace stub with DR-009 fields: Id, Username, Role (StaffRole), PermissionsBitfield (int), AuthCredentials (string), CreatedAt, IsActive (bool); XML docs on PermissionsBitfield and AuthCredentials |
| MODIFY | `server/src/PropelIQ.PatientAccess.Data/Entities/Admin.cs` | Replace stub with DR-010 fields: Id, Username, AccessPrivileges (int), AuthCredentials (string), CreatedAt, IsActive (bool); XML docs on AccessPrivileges and AuthCredentials |
| CREATE | `server/src/PropelIQ.PatientAccess.Data/Configurations/StaffConfiguration.cs` | `IEntityTypeConfiguration<Staff>`: `ToTable("staff")`, UUID PK default, `username varchar(100)` unique index `uix_staff_username`, `role varchar(30)` enum-to-string, `permissions_bitfield integer`, `auth_credentials varchar(256)`, `created_at DEFAULT NOW()`, `is_active DEFAULT true` |
| CREATE | `server/src/PropelIQ.PatientAccess.Data/Configurations/AdminConfiguration.cs` | `IEntityTypeConfiguration<Admin>`: `ToTable("admin")`, UUID PK default, `username varchar(100)` unique index `uix_admin_username`, `access_privileges integer`, `auth_credentials varchar(256)`, `created_at DEFAULT NOW()`, `is_active DEFAULT true` |
| MODIFY | `server/src/PropelIQ.PatientAccess.Data/PropelIQDbContext.cs` | Add `DbSet<Staff> Staff`, `DbSet<Admin> Admins`; add `ApplyConfiguration(new StaffConfiguration())` and `ApplyConfiguration(new AdminConfiguration())` in `OnModelCreating` |
| CREATE | `server/src/PropelIQ.PatientAccess.Data/Migrations/<timestamp>_AddStaffAdminSchema.cs` | Generated by `dotnet ef migrations add AddStaffAdminSchema` — creates `staff` and `admin` tables |

## External References
- ASP.NET Core Identity `IPasswordHasher<T>` and PBKDF2-HMAC-SHA512 format: https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/consumer-apis/password-hashing
- EF Core `IEntityTypeConfiguration<T>` pattern: https://learn.microsoft.com/en-us/ef/core/modeling/entity-types#shared-type-entity-types
- EF Core enum-to-string value conversion: https://learn.microsoft.com/en-us/ef/core/modeling/value-conversions#built-in-converters
- EF Core unique indexes and constraints: https://learn.microsoft.com/en-us/ef/core/modeling/indexes
- DR-009 (Staff entity), DR-010 (Admin entity), DR-011 (referential integrity — actor_id semantic reference)
- NFR-004 (RBAC with strict role separation between patient, staff, and admin actions)

## Build Commands
```bash
# Restore and build to confirm no compilation errors
cd server
dotnet restore PropelIQ.sln
dotnet build PropelIQ.sln --configuration Release

# Generate migration capturing Staff and Admin tables
dotnet ef migrations add AddStaffAdminSchema \
  --project src/PropelIQ.PatientAccess.Data \
  --startup-project src/PropelIQ.Api

# Review generated migration SQL
dotnet ef migrations script \
  --project src/PropelIQ.PatientAccess.Data \
  --startup-project src/PropelIQ.Api \
  --idempotent

# Apply migration to local dev database
dotnet ef database update \
  --project src/PropelIQ.PatientAccess.Data \
  --startup-project src/PropelIQ.Api

# Verify schema in psql
# psql -h localhost -U propeliq -d propeliq_dev -c "\d staff"
# psql -h localhost -U propeliq -d propeliq_dev -c "\d admin"
```

## Implementation Validation Strategy
- [ ] Unit tests pass
- [ ] `dotnet build PropelIQ.sln` exits with code 0 after all entity, enum, and configuration changes
- [ ] `Staff.AuthCredentials` and `Admin.AuthCredentials` are typed as `string` — no raw password fields present; verify via code review
- [ ] `Staff.PermissionsBitfield` and `Admin.AccessPrivileges` are typed as `int` (not `long`) — verify via code review
- [ ] `StaffRole` enum exists in `PropelIQ.PatientAccess.Domain/Enums/` and compiles without errors
- [ ] Migration `AddStaffAdminSchema` is generated with no EF compilation errors
- [ ] Generated SQL contains `role varchar(30) NOT NULL` in `staff` table (not PostgreSQL enum type)
- [ ] Generated SQL contains unique constraints `uix_staff_username` and `uix_admin_username`
- [ ] Generated SQL contains `is_active boolean DEFAULT true NOT NULL` on both tables
- [ ] `dotnet ef database update` applies without errors; `\d staff` and `\d admin` confirm schema in psql
- [ ] Inserting a duplicate `username` in `staff` or `admin` returns `ERROR 23505`

## Implementation Checklist
- [x] Create `StaffRole.cs` enum in `PropelIQ.PatientAccess.Domain/Enums/` with values: FrontDesk, CallCenter, ClinicalReviewer
- [x] Replace `Staff.cs` stub with DR-009 field set (Id, Username, Role as `StaffRole`, PermissionsBitfield as `int`, AuthCredentials as `string`, CreatedAt as `DateTime`, IsActive as `bool` defaulting to `true`); add XML doc on PermissionsBitfield explaining 31-flag limit and API enforcement; add XML doc on AuthCredentials stating PBKDF2 hash storage contract
- [x] Replace `Admin.cs` stub with DR-010 field set (Id, Username, AccessPrivileges as `int`, AuthCredentials as `string`, CreatedAt as `DateTime`, IsActive as `bool` defaulting to `true`); add XML docs consistent with Staff counterparts
- [x] Create `StaffConfiguration.cs`: `ToTable("staff")`, `HasDefaultValueSql("gen_random_uuid()")` on Id, `HasMaxLength(100).IsRequired()` + unique index `uix_staff_username` on Username, `HasConversion<string>().HasMaxLength(30).IsRequired()` on Role, `HasColumnType("integer")` on PermissionsBitfield, `HasMaxLength(256).IsRequired()` on AuthCredentials, `HasDefaultValueSql("NOW()").ValueGeneratedOnAdd()` on CreatedAt, `HasDefaultValue(true).IsRequired()` on IsActive
- [x] Create `AdminConfiguration.cs`: `ToTable("admin")`, `HasDefaultValueSql("gen_random_uuid()")` on Id, `HasMaxLength(100).IsRequired()` + unique index `uix_admin_username` on Username, `HasColumnType("integer")` on AccessPrivileges, `HasMaxLength(256).IsRequired()` on AuthCredentials, `HasDefaultValueSql("NOW()").ValueGeneratedOnAdd()` on CreatedAt, `HasDefaultValue(true).IsRequired()` on IsActive
- [x] Modify `PropelIQDbContext.cs`: add `DbSet<Staff> Staff => Set<Staff>();` and `DbSet<Admin> Admins => Set<Admin>();`; add `modelBuilder.ApplyConfiguration(new StaffConfiguration())` and `modelBuilder.ApplyConfiguration(new AdminConfiguration())` in `OnModelCreating`
- [x] Run `dotnet ef migrations add AddStaffAdminSchema`; review SQL for `role varchar(30)`, unique indexes, `auth_credentials varchar(256)`, `is_active boolean DEFAULT true`; run `dotnet ef database update` and verify schema in psql

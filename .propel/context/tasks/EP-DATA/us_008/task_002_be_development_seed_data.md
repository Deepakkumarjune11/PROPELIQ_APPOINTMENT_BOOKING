# Task - task_002_be_development_seed_data

## Requirement Reference
- User Story: [us_008] (.propel/context/tasks/EP-DATA/us_008/us_008.md)
- Story Location: `.propel/context/tasks/EP-DATA/us_008/us_008.md`
- Acceptance Criteria:
  - AC-4: Seed data is created when running database migrations in development — test patients, staff users, admin users, and sample appointments are available for immediate development and testing use.
  - AC-1: Seeded Staff records conform to DR-009 schema (role, permissions_bitfield, hashed auth_credentials).
  - AC-2: Seeded Admin records conform to DR-010 schema (access_privileges, hashed auth_credentials).
  - AC-3: Seeded audit entries reference seeded Staff or Admin actor_id values per DR-011.
- Edge Case:
  - Production environment: seed data execution is gated on `IHostEnvironment.IsDevelopment()` or an explicit `SeedData:Enabled` configuration flag; the seeder is a no-op in environments where the flag is false; production deployments never receive test data.
  - Idempotent seeding: seeder checks for the existence of seed marker records before inserting (e.g., `await db.Staff.AnyAsync(s => s.Username == "seed-staff-front-desk")`); re-running migrations or application restarts does not duplicate seed records.

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

Implements an environment-gated, idempotent seed data pipeline that populates the development database with representative fixture records for all domain entities defined across US_005–US_008. The pipeline consists of:

1. **`IDataSeeder` interface** — a thin contract enabling DI registration and test substitution.
2. **`DevelopmentDataSeeder` class** — the concrete implementation that inserts one admin, three staff users (one per role), two test patients, and four sample appointments. `AuthCredentials` for Staff and Admin are generated using `IPasswordHasher<T>` from ASP.NET Core Identity — no raw passwords in source code or database.
3. **Environment gate** — the seeder runs only when `IHostEnvironment.IsDevelopment()` returns `true` or `SeedData:Enabled` config is explicitly `true`. This is the sole enforcement boundary for the production safety requirement.
4. **Idempotency guard** — each seeder section checks for an existing sentinel record before inserting; safe to call on every application start without duplicating data.

Seed data is deliberately minimal (enough to exercise every entity and relationship) and uses clearly prefixed usernames (e.g., `seed-admin-1`, `seed-staff-front-desk`) to make it visually identifiable and easy to clean up. Seed passwords are well-known development values (`SeedPass@123`) documented in the project's developer README — never used in production.

**Security note (OWASP A02 — Cryptographic Failures):** Even for seed/test data, `IPasswordHasher<T>` is used to hash credentials before persistence. Raw passwords appear only as compile-time string literals in the seeder source; they are never written to `AuthCredentials` columns.

## Dependent Tasks
- `us_008/task_001_be_staff_admin_entities_config` — `Staff`, `Admin` entities and `StaffRole` enum, plus the `AddStaffAdminSchema` migration, must be applied before seed data can be inserted.
- All prior EP-DATA migration tasks (us_005–us_007) must be applied so that Patient and Appointment tables exist for seeded appointment records.

## Impacted Components
- `server/src/PropelIQ.PatientAccess.Data/Seeding/IDataSeeder.cs` — NEW: `IDataSeeder` interface with `SeedAsync(CancellationToken)` method
- `server/src/PropelIQ.PatientAccess.Data/Seeding/DevelopmentDataSeeder.cs` — NEW: concrete seeder implementing `IDataSeeder`; injects `PropelIQDbContext` and `IPasswordHasher<Staff>` / `IPasswordHasher<Admin>`
- `server/src/PropelIQ.Api/Program.cs` — MODIFY: register `DevelopmentDataSeeder` conditionally and invoke after `app.Run()` pre-start or via `IHostedService` hook

## Implementation Plan

1. **Create `IDataSeeder` interface** — Add `PropelIQ.PatientAccess.Data/Seeding/IDataSeeder.cs`:
   ```csharp
   namespace PropelIQ.PatientAccess.Data.Seeding;

   public interface IDataSeeder
   {
       Task SeedAsync(CancellationToken cancellationToken = default);
   }
   ```
   The interface is minimal by design. A single `SeedAsync` method allows
   test doubles to implement no-op seeders and prevents the seeder from
   leaking DbContext dependencies into higher layers.

2. **Create `DevelopmentDataSeeder`** — Add `PropelIQ.PatientAccess.Data/Seeding/DevelopmentDataSeeder.cs`:
   ```csharp
   using Microsoft.AspNetCore.Identity;
   using Microsoft.EntityFrameworkCore;
   using PropelIQ.PatientAccess.Data.Entities;
   using PropelIQ.PatientAccess.Domain.Enums;

   namespace PropelIQ.PatientAccess.Data.Seeding;

   internal sealed class DevelopmentDataSeeder : IDataSeeder
   {
       private readonly PropelIQDbContext _db;
       private readonly IPasswordHasher<Staff> _staffHasher;
       private readonly IPasswordHasher<Admin> _adminHasher;

       // Well-known development seed password — documented in developer README.
       // Never used in production (seeder is env-gated). OWASP A02 compliance:
       // even this dev password is hashed before storage — never persisted raw.
       private const string SeedPassword = "SeedPass@123";

       public DevelopmentDataSeeder(
           PropelIQDbContext db,
           IPasswordHasher<Staff> staffHasher,
           IPasswordHasher<Admin> adminHasher)
       {
           _db = db;
           _staffHasher = staffHasher;
           _adminHasher = adminHasher;
       }

       public async Task SeedAsync(CancellationToken cancellationToken = default)
       {
           await SeedAdminAsync(cancellationToken);
           await SeedStaffAsync(cancellationToken);
           await SeedPatientsAsync(cancellationToken);
           await SeedAppointmentsAsync(cancellationToken);
           await _db.SaveChangesAsync(cancellationToken);
       }

       private async Task SeedAdminAsync(CancellationToken ct)
       {
           // Idempotency guard: skip if sentinel admin already exists
           if (await _db.Admins.AnyAsync(a => a.Username == "seed-admin-1", ct))
               return;

           var admin = new Admin
           {
               Id = Guid.NewGuid(),
               Username = "seed-admin-1",
               AccessPrivileges = int.MaxValue, // all privileges for dev admin
               CreatedAt = DateTime.UtcNow,
               IsActive = true
           };
           admin.AuthCredentials = _adminHasher.HashPassword(admin, SeedPassword);
           _db.Admins.Add(admin);
       }

       private async Task SeedStaffAsync(CancellationToken ct)
       {
           if (await _db.Staff.AnyAsync(s => s.Username == "seed-staff-front-desk", ct))
               return;

           var roles = new[]
           {
               (Username: "seed-staff-front-desk",   Role: StaffRole.FrontDesk,       Bits: 0b_0000_0001),
               (Username: "seed-staff-call-center",  Role: StaffRole.CallCenter,      Bits: 0b_0000_0011),
               (Username: "seed-staff-clinical",     Role: StaffRole.ClinicalReviewer, Bits: 0b_0000_0111)
           };

           foreach (var (username, role, bits) in roles)
           {
               var staff = new Staff
               {
                   Id = Guid.NewGuid(),
                   Username = username,
                   Role = role,
                   PermissionsBitfield = bits,
                   CreatedAt = DateTime.UtcNow,
                   IsActive = true
               };
               staff.AuthCredentials = _staffHasher.HashPassword(staff, SeedPassword);
               _db.Staff.Add(staff);
           }
       }

       private async Task SeedPatientsAsync(CancellationToken ct)
       {
           if (await _db.Patients.AnyAsync(p => p.Email == "seed-patient-1@dev.local", ct))
               return;

           _db.Patients.AddRange(
               new Patient
               {
                   Id = Guid.NewGuid(),
                   Email = "seed-patient-1@dev.local",
                   Name = "Alice Dev",
                   Dob = new DateOnly(1985, 4, 10),
                   Phone = "555-0001",
                   CreatedAt = DateTime.UtcNow,
                   UpdatedAt = DateTime.UtcNow
               },
               new Patient
               {
                   Id = Guid.NewGuid(),
                   Email = "seed-patient-2@dev.local",
                   Name = "Bob Dev",
                   Dob = new DateOnly(1972, 11, 23),
                   Phone = "555-0002",
                   CreatedAt = DateTime.UtcNow,
                   UpdatedAt = DateTime.UtcNow
               }
           );
       }

       private async Task SeedAppointmentsAsync(CancellationToken ct)
       {
           if (await _db.Appointments.AnyAsync(ct))
               return;

           // Retrieve the seeded patients inside the same UoW so navigation resolves
           var patients = await _db.Patients
               .Where(p => p.Email.StartsWith("seed-patient-"))
               .ToListAsync(ct);

           foreach (var patient in patients)
           {
               _db.Appointments.Add(new Appointment
               {
                   Id = Guid.NewGuid(),
                   PatientId = patient.Id,
                   SlotDatetime = DateTime.UtcNow.AddDays(7),
                   Status = AppointmentStatus.Booked,
                   CreatedAt = DateTime.UtcNow,
                   UpdatedAt = DateTime.UtcNow
               });
               _db.Appointments.Add(new Appointment
               {
                   Id = Guid.NewGuid(),
                   PatientId = patient.Id,
                   SlotDatetime = DateTime.UtcNow.AddDays(-14),
                   Status = AppointmentStatus.Completed,
                   CreatedAt = DateTime.UtcNow,
                   UpdatedAt = DateTime.UtcNow
               });
           }
       }
   }
   ```
   **Why `SaveChangesAsync` is called once at the end**: all seed sections share
   a single Unit of Work. This avoids partial commits if a later section throws
   and keeps the insert batch efficient (one round-trip to PostgreSQL per seed run).

3. **Register seeder in DI and invoke conditionally** — Modify `Program.cs`:
   ```csharp
   // Register password hashers (also needed by the auth service in later sprints)
   builder.Services.AddScoped<IPasswordHasher<Staff>, PasswordHasher<Staff>>();
   builder.Services.AddScoped<IPasswordHasher<Admin>, PasswordHasher<Admin>>();

   // Register seeder — only in Development or when explicitly enabled via config
   var seedEnabled = builder.Configuration.GetValue<bool>("SeedData:Enabled",
       defaultValue: builder.Environment.IsDevelopment());

   if (seedEnabled)
       builder.Services.AddScoped<IDataSeeder, DevelopmentDataSeeder>();
   else
       builder.Services.AddScoped<IDataSeeder, NoOpDataSeeder>(); // see step 4

   // After app is built, run seeder before accepting requests
   var app = builder.Build();

   using (var scope = app.Services.CreateScope())
   {
       var seeder = scope.ServiceProvider.GetRequiredService<IDataSeeder>();
       await seeder.SeedAsync();
   }
   ```
   **Why `using (var scope)` not `IHostedService`**: seeding runs once at startup,
   before the HTTP pipeline accepts traffic. A scoped service resolved from a
   manual scope is simpler and easier to test than an `IHostedService`, which
   would run concurrently with application start-up.

4. **Create `NoOpDataSeeder` for production** — Add alongside `DevelopmentDataSeeder`:
   ```csharp
   namespace PropelIQ.PatientAccess.Data.Seeding;

   internal sealed class NoOpDataSeeder : IDataSeeder
   {
       public Task SeedAsync(CancellationToken cancellationToken = default)
           => Task.CompletedTask;
   }
   ```
   Registered in production/staging so the `IDataSeeder` dependency resolves
   without seeding. This satisfies the AC-4 edge case: production deployments
   receive a no-op without any conditional logic at call sites.

5. **Add `SeedData:Enabled` to `appsettings.Development.json`** — Ensure the flag is explicit in the development config file:
   ```json
   {
     "SeedData": {
       "Enabled": true
     }
   }
   ```
   `appsettings.json` (production baseline) must **not** contain this key, so the
   default `false` (when `IsDevelopment()` is false) is used in all non-dev environments.

6. **Verify idempotency and env-gating in integration tests** — Using Testcontainers, confirm:
   - Running `SeedAsync()` twice does not duplicate records (idempotency guard).
   - `NoOpDataSeeder.SeedAsync()` completes without any DB writes (verified by asserting row counts remain zero on a fresh container).
   - Seeded `Staff.AuthCredentials` values are verifiable via `IPasswordHasher<Staff>.VerifyHashedPassword(...)` returning `PasswordVerificationResult.Success`.

## Current Project State

```
server/
├── PropelIQ.sln
└── src/
    ├── PropelIQ.PatientAccess.Data/
    │   ├── Entities/                    ← all full (us_005–us_008/task_001)
    │   ├── Interceptors/
    │   │   └── AuditLogImmutabilityInterceptor.cs
    │   ├── Configurations/              ← all full (us_005–us_008/task_001)
    │   ├── Seeding/
    │   │   ├── IDataSeeder.cs           ← TARGET (new)
    │   │   ├── DevelopmentDataSeeder.cs ← TARGET (new)
    │   │   └── NoOpDataSeeder.cs        ← TARGET (new)
    │   ├── PropelIQDbContext.cs         ← full (all entities registered)
    │   └── Migrations/
    │       └── ... (all prior migrations applied)
    └── PropelIQ.Api/
        ├── appsettings.json             ← no SeedData key (safe default: disabled)
        ├── appsettings.Development.json ← TARGET (add SeedData:Enabled = true)
        └── Program.cs                  ← TARGET (register hashers + seeder + invoke)
```

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/PropelIQ.PatientAccess.Data/Seeding/IDataSeeder.cs` | `IDataSeeder` interface with `Task SeedAsync(CancellationToken)` method |
| CREATE | `server/src/PropelIQ.PatientAccess.Data/Seeding/DevelopmentDataSeeder.cs` | Concrete seeder: idempotent insertion of 1 admin, 3 staff (one per role), 2 patients, 4 appointments; hashes credentials via `IPasswordHasher<T>`; single `SaveChangesAsync` at completion |
| CREATE | `server/src/PropelIQ.PatientAccess.Data/Seeding/NoOpDataSeeder.cs` | No-op implementation for production/staging; returns `Task.CompletedTask` immediately |
| MODIFY | `server/src/PropelIQ.Api/Program.cs` | Register `IPasswordHasher<Staff>` and `IPasswordHasher<Admin>` as scoped; register `DevelopmentDataSeeder` or `NoOpDataSeeder` based on `SeedData:Enabled` config flag; invoke `SeedAsync()` via scoped service before `app.Run()` |
| MODIFY | `server/src/PropelIQ.Api/appsettings.Development.json` | Add `"SeedData": { "Enabled": true }` section |

## External References
- ASP.NET Core Identity `IPasswordHasher<T>` API — PBKDF2-HMAC-SHA512: https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.ipasswordhasher-1
- ASP.NET Core `IHostEnvironment.IsDevelopment()`: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/environments
- EF Core `DbContext.SaveChangesAsync` — Unit of Work pattern: https://learn.microsoft.com/en-us/ef/core/saving/basic
- OWASP A02 (Cryptographic Failures) — password hashing best practices: https://owasp.org/Top10/A02_2021-Cryptographic_Failures/
- DR-009 (Staff entity), DR-010 (Admin entity), DR-011 (referential integrity — actor_id in seeded audit entries)
- NFR-004 (RBAC enforcement — seeded roles match defined role separation)

## Build Commands
```bash
# Restore and build to confirm seeding classes compile
cd server
dotnet restore PropelIQ.sln
dotnet build PropelIQ.sln --configuration Release

# Start local PostgreSQL (all prior migrations must be applied first)
docker compose up -d db

# Run the API in Development mode — seeder fires automatically on startup
ASPNETCORE_ENVIRONMENT=Development dotnet run \
  --project src/PropelIQ.Api

# Verify seeded records in psql
# psql -h localhost -U propeliq -d propeliq_dev -c "SELECT username, role, is_active FROM staff;"
# psql -h localhost -U propeliq -d propeliq_dev -c "SELECT username, access_privileges FROM admin;"
# psql -h localhost -U propeliq -d propeliq_dev -c "SELECT email, name FROM patient WHERE email LIKE 'seed-%';"
# psql -h localhost -U propeliq -d propeliq_dev -c "SELECT status, slot_datetime FROM appointment LIMIT 10;"
```

## Implementation Validation Strategy
- [ ] Unit tests pass
- [ ] Integration tests pass — Testcontainers confirms: (a) `DevelopmentDataSeeder.SeedAsync()` called twice produces identical row counts (idempotency); (b) `NoOpDataSeeder.SeedAsync()` produces no DB writes; (c) seeded `Staff.AuthCredentials` verifies successfully via `IPasswordHasher<Staff>.VerifyHashedPassword(staff, SeedPassword)` returning `PasswordVerificationResult.Success`; (d) seeded `Appointment` records reference valid `PatientId` FKs (no `ERROR 23503`)
- [ ] `dotnet build PropelIQ.sln` exits with code 0
- [ ] `appsettings.json` does **not** contain a `SeedData` key — verified via code review (production default is disabled)
- [ ] `appsettings.Development.json` contains `"SeedData": { "Enabled": true }`
- [ ] Raw password `SeedPass@123` appears nowhere in any database column — `auth_credentials` stores hash only; verified by `SELECT auth_credentials FROM staff LIMIT 1` not equalling the raw string
- [ ] `NoOpDataSeeder` is registered and resolved when `SeedData:Enabled = false`
- [ ] All seeded usernames use the `seed-` prefix — identifiable and easy to clean up

## Implementation Checklist
- [x] Create `IDataSeeder.cs` in `PropelIQ.PatientAccess.Data/Seeding/` with single method `Task SeedAsync(CancellationToken cancellationToken = default)`
- [x] Create `DevelopmentDataSeeder.cs` implementing `IDataSeeder`: inject `PropelIQDbContext`, `IPasswordHasher<Staff>`, `IPasswordHasher<Admin>`; implement four idempotency-guarded private `SeedXxxAsync` methods (Admin, Staff, Patients, Appointments); call `_db.SaveChangesAsync(cancellationToken)` once at the end of `SeedAsync`; use `IPasswordHasher<T>.HashPassword(entity, SeedPassword)` for all credential fields; never write `SeedPassword` directly to `AuthCredentials`
- [x] Create `NoOpDataSeeder.cs` implementing `IDataSeeder` with `Task SeedAsync(...) => Task.CompletedTask`
- [x] Modify `Program.cs`: add `builder.Services.AddScoped<IPasswordHasher<Staff>, PasswordHasher<Staff>>()` and `AddScoped<IPasswordHasher<Admin>, PasswordHasher<Admin>>()`; read `SeedData:Enabled` config flag with fallback to `builder.Environment.IsDevelopment()`; register `DevelopmentDataSeeder` or `NoOpDataSeeder` accordingly; after `var app = builder.Build()`, resolve `IDataSeeder` via scoped service and `await seeder.SeedAsync()`
- [x] Modify `appsettings.Development.json`: add `"SeedData": { "Enabled": true }` section; confirm `appsettings.json` has no `SeedData` key
- [ ] Verify in psql that seeded staff, admin, patient, and appointment records exist with correct values; confirm `auth_credentials` columns hold hashed values (not raw `SeedPass@123`); confirm re-running the app does not duplicate seed records

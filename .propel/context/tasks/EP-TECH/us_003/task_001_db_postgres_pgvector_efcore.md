# Task - task_001_db_postgres_pgvector_efcore

## Requirement Reference
- User Story: [us_003] (.propel/context/tasks/EP-TECH/us_003/us_003.md)
- Story Location: `.propel/context/tasks/EP-TECH/us_003/us_003.md`
- Acceptance Criteria:
  - AC-1: PostgreSQL 15 is running with the pgvector extension enabled and ready to accept connections on the configured port.
  - AC-2: Running `dotnet ef migrations add Initial` generates a migration file with the initial schema; `dotnet ef database update` applies it successfully.
- Edge Case:
  - When PostgreSQL is unreachable: Application logs connection failure and health check returns degraded status with specific error details (covered in task_002).

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
| Database | PostgreSQL | 15.x |
| Vector Extension | pgvector | 0.5.x |
| EF Core Provider | Npgsql.EntityFrameworkCore.PostgreSQL | 8.x |
| Dev Environment | Docker | 24.x |
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

Provisions the local development PostgreSQL 15 instance with the pgvector extension via Docker Compose and wires Entity Framework Core 8 to the database. Creates `PropelIQDbContext` with DbSets for all domain entities defined in DR-001–DR-018, enables the `vector` PostgreSQL extension on the model builder, configures Npgsql as the EF Core provider with connection pooling and retry-on-failure, and produces a runnable initial code-first migration. After this task the command `dotnet ef migrations add Initial && dotnet ef database update` must succeed and the schema must be created in the target database.

## Dependent Tasks
- us_002/task_001_be_solution_modular_structure — Solution and module scaffold must exist before adding packages and DbContext files.

## Impacted Components
- `server/docker-compose.yml` — New Docker Compose file (PostgreSQL 15 + pgvector service)
- `server/docker-compose.override.yml` — Dev environment overrides (exposed ports, env vars)
- `server/src/PropelIQ.PatientAccess.Data/PropelIQ.PatientAccess.Data.csproj` — Add EF Core + Npgsql NuGet packages
- `server/src/PropelIQ.ClinicalIntelligence.Data/PropelIQ.ClinicalIntelligence.Data.csproj` — Add EF Core packages
- `server/src/PropelIQ.Admin.Data/PropelIQ.Admin.Data.csproj` — Add EF Core packages
- `server/src/PropelIQ.PatientAccess.Data/PropelIQDbContext.cs` — New EF Core DbContext with all DR entities
- `server/src/PropelIQ.Api/Program.cs` — Register DbContext via `AddDbContext` / `AddNpgsql`
- `server/src/PropelIQ.Api/appsettings.json` — `ConnectionStrings:DefaultConnection` placeholder
- `server/src/PropelIQ.Api/appsettings.Development.json` — Docker-local PostgreSQL connection string

## Implementation Plan

1. **Docker Compose setup** — Create `server/docker-compose.yml` using `pgvector/pgvector:pg15` image. Define service `db` with environment variables `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD`. Create `docker-compose.override.yml` that maps container port 5432 to host port 5432 and loads values from `.env`. Add `server/.env.example` with safe placeholder values for the three Postgres env vars.

2. **Add NuGet packages** — In each `*.Data.csproj`, add:
   - `Npgsql.EntityFrameworkCore.PostgreSQL` version `8.*`
   - `Microsoft.EntityFrameworkCore` version `8.*`

   In `PropelIQ.Api.csproj`, add:
   - `Microsoft.EntityFrameworkCore.Design` version `8.*` (required for `dotnet ef` tooling)

3. **Define entity classes** — Create C# entity records/classes in `PropelIQ.PatientAccess.Data/Entities/` (and the other Data modules) for all domain entities from DR-001–DR-010:
   - `Patient`, `Appointment`, `Staff`, `Admin`, `IntakeResponse`, `ClinicalDocument`, `ExtractedFact`, `PatientView360`, `CodeSuggestion`, `AuditLog`
   - All PHI-bearing column properties marked with `[Encrypted]` comment per DR-015 (encryption implemented separately in security task).
   - `AuditLog` table: no `HasSoftDelete` — append-only per DR-012.
   - `Patient`, `Appointment`, `IntakeResponse`, `ClinicalDocument` use soft-delete shadow property `IsDeleted` per DR-017.

4. **Create PropelIQDbContext** — Implement `PropelIQDbContext : DbContext` in `PropelIQ.PatientAccess.Data/PropelIQDbContext.cs`:
   - Add `DbSet<T>` for all ten entities.
   - In `OnModelCreating`: call `modelBuilder.HasPostgresExtension("vector")` to enable pgvector per TR-003 / DR-016.
   - Configure soft-delete global query filters for `IsDeleted` entities.
   - Configure optimistic concurrency `xmin` column on `PatientView360` per DR-018 using `UseXminAsConcurrencyToken()`.
   - Configure referential integrity with cascade rules per DR-011.

5. **Configure EF Core in DI** — In `Program.cs`, add:
   ```csharp
   builder.Services.AddDbContext<PropelIQDbContext>(options =>
       options.UseNpgsql(
           builder.Configuration.GetConnectionString("DefaultConnection"),
           npgsql => {
               npgsql.SetPostgresVersion(15, 0);
               npgsql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorCodesToAdd: null);
               npgsql.MigrationsAssembly("PropelIQ.PatientAccess.Data");
           }));
   ```

6. **Configure connection pooling** — Set `Minimum Pool Size=10;Maximum Pool Size=100` in the connection string per TR-021. Document these values in `appsettings.json` alongside the connection string placeholder.

7. **appsettings configuration** — In `appsettings.json` add `"ConnectionStrings": { "DefaultConnection": "Host=CHANGE_ME;Database=propeliq;Username=CHANGE_ME;Password=CHANGE_ME;Minimum Pool Size=10;Maximum Pool Size=100" }`. In `appsettings.Development.json` add the Docker-local value: `Host=localhost;Port=5432;Database=propeliq_dev;Username=propeliq;Password=devpassword;Minimum Pool Size=10;Maximum Pool Size=100`.

8. **Validate migrations** — From `server/` directory, run:
   ```bash
   dotnet ef migrations add Initial --project src/PropelIQ.PatientAccess.Data --startup-project src/PropelIQ.Api
   dotnet ef database update --project src/PropelIQ.PatientAccess.Data --startup-project src/PropelIQ.Api
   ```
   Confirm migration file is generated under `PropelIQ.PatientAccess.Data/Migrations/`. Confirm `CREATE TABLE` SQL includes all DR entities and `CREATE EXTENSION IF NOT EXISTS vector` is present.

## Current Project State

```
server/
├── global.json
├── PropelIQ.sln
└── src/
    ├── PropelIQ.Api/
    │   ├── Program.cs
    │   ├── appsettings.json
    │   ├── appsettings.Development.json
    │   ├── ConfigurationValidator.cs
    │   ├── HealthCheck/
    │   │   └── HealthCheckResponse.cs
    │   └── Properties/
    │       └── PublishProfiles/
    ├── PropelIQ.PatientAccess.Domain/
    ├── PropelIQ.PatientAccess.Application/
    ├── PropelIQ.PatientAccess.Data/
    │   └── ServiceCollectionExtensions.cs
    ├── PropelIQ.PatientAccess.Presentation/
    ├── PropelIQ.ClinicalIntelligence.Domain/
    ├── PropelIQ.ClinicalIntelligence.Application/
    ├── PropelIQ.ClinicalIntelligence.Data/
    │   └── ServiceCollectionExtensions.cs
    ├── PropelIQ.ClinicalIntelligence.Presentation/
    ├── PropelIQ.Admin.Domain/
    ├── PropelIQ.Admin.Application/
    ├── PropelIQ.Admin.Data/
    │   └── ServiceCollectionExtensions.cs
    └── PropelIQ.Admin.Presentation/
```

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/docker-compose.yml` | PostgreSQL 15 + pgvector service definition (`pgvector/pgvector:pg15` image) |
| CREATE | `server/docker-compose.override.yml` | Port binding (5432:5432) and env var loading from `.env` for local dev |
| CREATE | `server/.env.example` | Safe placeholder values for `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` |
| MODIFY | `server/src/PropelIQ.PatientAccess.Data/PropelIQ.PatientAccess.Data.csproj` | Add `Npgsql.EntityFrameworkCore.PostgreSQL 8.*`, `Microsoft.EntityFrameworkCore 8.*` |
| MODIFY | `server/src/PropelIQ.ClinicalIntelligence.Data/PropelIQ.ClinicalIntelligence.Data.csproj` | Add `Npgsql.EntityFrameworkCore.PostgreSQL 8.*`, `Microsoft.EntityFrameworkCore 8.*` |
| MODIFY | `server/src/PropelIQ.Admin.Data/PropelIQ.Admin.Data.csproj` | Add `Npgsql.EntityFrameworkCore.PostgreSQL 8.*`, `Microsoft.EntityFrameworkCore 8.*` |
| MODIFY | `server/src/PropelIQ.Api/PropelIQ.Api.csproj` | Add `Microsoft.EntityFrameworkCore.Design 8.*` for EF tooling |
| CREATE | `server/src/PropelIQ.PatientAccess.Data/Entities/` | Entity classes (Patient, Appointment, Staff, Admin, IntakeResponse, ClinicalDocument, ExtractedFact, PatientView360, CodeSuggestion, AuditLog) |
| CREATE | `server/src/PropelIQ.PatientAccess.Data/PropelIQDbContext.cs` | EF Core DbContext with all DbSets, `HasPostgresExtension("vector")`, soft delete, optimistic concurrency, referential integrity |
| MODIFY | `server/src/PropelIQ.Api/Program.cs` | Add `AddDbContext<PropelIQDbContext>` with `UseNpgsql`, retry policy, pool sizes |
| MODIFY | `server/src/PropelIQ.Api/appsettings.json` | Add `ConnectionStrings:DefaultConnection` placeholder with pool size parameters |
| MODIFY | `server/src/PropelIQ.Api/appsettings.Development.json` | Add Docker-local PostgreSQL connection string |
| CREATE | `server/src/PropelIQ.PatientAccess.Data/Migrations/` | Generated by `dotnet ef migrations add Initial` — contains pgvector extension + full schema SQL |

## External References
- Npgsql EF Core provider — UseNpgsql + SetPostgresVersion: https://www.npgsql.org/efcore/index.html
- pgvector Npgsql extension setup: https://github.com/pgvector/pgvector-dotnet
- Docker image with pgvector pre-installed: https://hub.docker.com/r/pgvector/pgvector (tag `pg15`)
- EF Core 8 code-first migrations: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/
- TR-003 (PostgreSQL 15 + pgvector), TR-008 (EF Core 8 migrations), DR-016 (vector in Postgres), DR-014 (zero-downtime migrations), TR-021 (connection pool min 10 / max 100)
- Context7 Npgsql EF Core docs: https://context7.com/npgsql/efcore.pg/llms.txt

## Build Commands
```bash
# Start local PostgreSQL 15 + pgvector via Docker Compose
cd server
docker compose up -d db

# Restore and build
dotnet restore PropelIQ.sln
dotnet build PropelIQ.sln --configuration Release

# Add initial migration (run from server/ after Docker is up)
dotnet ef migrations add Initial \
  --project src/PropelIQ.PatientAccess.Data \
  --startup-project src/PropelIQ.Api

# Apply migration to local dev database
dotnet ef database update \
  --project src/PropelIQ.PatientAccess.Data \
  --startup-project src/PropelIQ.Api

# Verify pgvector extension was created
# (run in psql or pgAdmin after migration)
# SELECT * FROM pg_extension WHERE extname = 'vector';
```

## Implementation Validation Strategy
- [ ] Unit tests pass
- [ ] Integration tests pass — `docker compose up -d db` before running; Testcontainers can be used as alternative for CI
- [ ] `dotnet build PropelIQ.sln` exits with code 0
- [ ] Migration file exists at `PropelIQ.PatientAccess.Data/Migrations/` and SQL contains `CREATE EXTENSION IF NOT EXISTS "vector"`
- [ ] `dotnet ef database update` applies without errors against Docker PostgreSQL
- [ ] All ten entity tables are present in the DB after migration (`\dt` in psql)
- [ ] `SELECT * FROM pg_extension WHERE extname = 'vector'` returns one row

## Implementation Checklist
- [x] Create `server/docker-compose.yml` with `pgvector/pgvector:pg15` image, `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` env vars
- [x] Create `server/docker-compose.override.yml` mapping port 5432 and loading from `.env`; create `server/.env.example` with placeholder values
- [x] Add `Npgsql.EntityFrameworkCore.PostgreSQL 8.*` and `Microsoft.EntityFrameworkCore 8.*` to all three `*.Data.csproj` files; add `Microsoft.EntityFrameworkCore.Design 8.*` to `PropelIQ.Api.csproj`
- [x] Create entity classes in `PropelIQ.PatientAccess.Data/Entities/` for all 10 DR entities (Patient, Appointment, Staff, Admin, IntakeResponse, ClinicalDocument, ExtractedFact, PatientView360, CodeSuggestion, AuditLog) with properties matching DR-001–DR-010
- [x] Create `PropelIQDbContext.cs` with all 10 `DbSet<T>` properties; call `modelBuilder.HasPostgresExtension("vector")` in `OnModelCreating`; configure soft-delete global query filters, xmin concurrency token on `PatientView360`, cascade rules
- [x] Register DbContext in `Program.cs` with `AddDbContext<PropelIQDbContext>` using `UseNpgsql`, `SetPostgresVersion(15, 0)`, `EnableRetryOnFailure(5, 30s)`, `MigrationsAssembly("PatientAccess.Data")`
- [x] Add connection string to `appsettings.json` (placeholder) and `appsettings.Development.json` (Docker local) with pool size params (min 10, max 100)
- [x] Run `dotnet ef migrations add Initial`; verified migration SQL contains pgvector extension annotation and all 10 entity tables

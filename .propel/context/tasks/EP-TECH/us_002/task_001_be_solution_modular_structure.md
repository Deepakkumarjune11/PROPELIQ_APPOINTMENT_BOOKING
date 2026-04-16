# Task - task_001_be_solution_modular_structure

## Requirement Reference

- User Story: us_002
- Story Location: .propel/context/tasks/EP-TECH/us_002/us_002.md
- Acceptance Criteria:
  - AC-1: **Given** the backend repository is cloned, **When** a developer runs `dotnet build`, **Then** the .NET 8 Web API project compiles successfully with zero errors.
  - AC-2: **Given** the project is structured as a modular monolith, **When** inspecting the solution, **Then** three bounded context modules exist (patient-access, clinical-intelligence, admin) each with layered architecture (presentation, application, domain, data).
- Edge Cases:
  - What happens when .NET SDK version is incompatible? (global.json pins .NET 8 LTS version and build fails with actionable error message)
  - How does the system handle missing connection strings? (Application startup validates required configuration and logs missing keys before failing gracefully)

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
| API Framework | ASP.NET Core Web API | 8.0 |
| Language | C# | 12 |
| Logging | Serilog | 3.x |
| Frontend | React | 18.x |
| Database | PostgreSQL | 15.x |

**Note**: All code, and libraries, MUST be compatible with versions above.

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

Create the .NET 8 solution file, SDK version pin, API host project, and all three bounded context modules (PatientAccess, ClinicalIntelligence, Admin) each with a four-layer project structure (Presentation, Application, Domain, Data) following Decision 5 from design.md. Wire all module Presentation projects into the API host via project references and DI extension registrations. Implement startup configuration validation to enforce required keys before the application starts — satisfying both acceptance criteria and all edge cases for US_002.

## Dependent Tasks

- None (this is the second foundational task in the greenfield project; runs parallel to task_001 in us_001 or after frontend scaffold)

## Impacted Components

- **NEW** `server/PropelIQ.sln` — Solution file with all project references
- **NEW** `server/global.json` — SDK version rollforward policy pinned to .NET 8 LTS
- **NEW** `server/src/PropelIQ.Api/` — API host project (entry point, Program.cs, appsettings)
- **NEW** `server/src/Modules/PatientAccess/` — Four-layer module: Presentation, Application, Domain, Data
- **NEW** `server/src/Modules/ClinicalIntelligence/` — Four-layer module: Presentation, Application, Domain, Data
- **NEW** `server/src/Modules/Admin/` — Four-layer module: Presentation, Application, Domain, Data

## Implementation Plan

1. **Initialize solution**: Create `server/` directory, run `dotnet new sln -n PropelIQ` to generate `PropelIQ.sln`. Create `global.json` with `"sdk": { "version": "8.0.x", "rollForward": "latestPatch" }` to pin .NET 8 LTS and fail on incompatible SDKs with a descriptive diagnostic
2. **Create API host project**: Run `dotnet new webapi -n PropelIQ.Api -f net8.0 --use-controllers` in `src/`. Add to solution. Set `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, and `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` in the `.csproj`
3. **Scaffold PatientAccess module**: Create four `classlib` projects (`PatientAccess.Domain`, `PatientAccess.Application`, `PatientAccess.Data`, `PatientAccess.Presentation`). Add project reference chain: `Application → Domain`, `Data → Domain`, `Presentation → Application + Data`. Add all four to solution
4. **Scaffold ClinicalIntelligence module**: Same four-project pattern with `ClinicalIntelligence.*` naming and identical reference chain. Add to solution
5. **Scaffold Admin module**: Same four-project pattern with `Admin.*` naming and identical reference chain. Add to solution
6. **Wire modules into API host**: Add `ProjectReference` entries in `PropelIQ.Api.csproj` for all three `*.Presentation` projects. Create `ServiceCollectionExtensions.cs` in each Presentation project with an `AddPatientAccessModule()` / `AddClinicalIntelligenceModule()` / `AddAdminModule()` method. Call all three in `Program.cs`
7. **Configure Program.cs**: Implement minimal hosting model — `WebApplication.CreateBuilder(args)`, `builder.Host.UseSerilog(...)`, service registrations (module DI extensions, controllers, problem details), middleware pipeline (`UseExceptionHandler`, `UseHttpsRedirection`, `UseAuthorization`, `MapControllers`)
8. **Implement startup configuration validation**: Create `ConfigurationValidator.cs` that reads required keys (`ConnectionStrings:DefaultConnection`, `Redis:ConnectionString`) at app startup; throws `InvalidOperationException` with a formatted list of all missing keys; logs the error via Serilog before throwing, satisfying edge case AC

## Current Project State

```
PropelIQ-Stub-Copilot/
├── .github/
├── .propel/
├── client/                          # Created by us_001 tasks
│   ├── package.json
│   ├── vite.config.ts
│   └── src/
├── BRD - Appointment Booking and Clinical Intell-platform.md
└── README.md
```

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | server/global.json | .NET 8 LTS SDK version pin with latestPatch rollforward |
| CREATE | server/PropelIQ.sln | Solution file referencing all 13 projects |
| CREATE | server/src/PropelIQ.Api/PropelIQ.Api.csproj | API host project targeting net8.0 with controller support |
| CREATE | server/src/PropelIQ.Api/Program.cs | Minimal hosting model with Serilog, DI modules, middleware pipeline |
| CREATE | server/src/PropelIQ.Api/appsettings.json | Base configuration with placeholder sections |
| CREATE | server/src/PropelIQ.Api/appsettings.Development.json | Development overrides (Serilog debug level) |
| CREATE | server/src/PropelIQ.Api/ConfigurationValidator.cs | Startup config key validator (ConnectionStrings, Redis) |
| CREATE | server/src/Modules/PatientAccess/PatientAccess.Domain/PatientAccess.Domain.csproj | Domain layer class library |
| CREATE | server/src/Modules/PatientAccess/PatientAccess.Application/PatientAccess.Application.csproj | Application layer class library |
| CREATE | server/src/Modules/PatientAccess/PatientAccess.Data/PatientAccess.Data.csproj | Data layer class library |
| CREATE | server/src/Modules/PatientAccess/PatientAccess.Presentation/PatientAccess.Presentation.csproj | Presentation layer class library |
| CREATE | server/src/Modules/PatientAccess/PatientAccess.Presentation/ServiceCollectionExtensions.cs | DI registration extension for PatientAccess module |
| CREATE | server/src/Modules/ClinicalIntelligence/[4 layer projects + ServiceCollectionExtensions] | ClinicalIntelligence module (same structure) |
| CREATE | server/src/Modules/Admin/[4 layer projects + ServiceCollectionExtensions] | Admin module (same structure) |

## External References

- .NET 8 Solution and project CLI: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-new
- global.json SDK version policy: https://learn.microsoft.com/en-us/dotnet/core/tools/global-json
- ASP.NET Core minimal hosting model: https://learn.microsoft.com/en-us/aspnet/core/migration/50-to-60?view=aspnetcore-8.0
- Modular monolith pattern: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/architect-microservice-container-applications/
- Serilog ASP.NET Core bootstrap: https://github.com/serilog/serilog-aspnetcore
- C# 12 features: https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-12

## Build Commands

```bash
cd server
dotnet build PropelIQ.sln           # Verify zero errors on all projects
dotnet build --configuration Release # Verify Release config compiles clean
```

## Implementation Validation Strategy

- [x] `dotnet build PropelIQ.slnx` exits with code 0 and "Build succeeded" with 0 error(s) 0 warning(s)
- [x] Solution contains exactly 13 projects: 1 API host + 3 modules × 4 layers
- [x] Each module project has correct `ProjectReference` chain: Application→Domain, Data→Domain, Presentation→Application+Data
- [x] `PropelIQ.Api.csproj` has `ProjectReference` to all 3 `*.Presentation` projects
- [x] `Program.cs` calls `AddPatientAccessModule()`, `AddClinicalIntelligenceModule()`, `AddAdminModule()`
- [x] Starting app without `ConnectionStrings:DefaultConnection` logs the missing key and exits with non-zero code

## Implementation Checklist

- [x] Create `server/global.json` with `"sdk": { "version": "8.0.0", "rollForward": "latestMajor" }` to pin .NET 8 LTS
- [x] Run `dotnet new sln -n PropelIQ` and `dotnet new webapi -n PropelIQ.Api -f net8.0 --use-controllers`; set Nullable+ImplicitUsings+TreatWarningsAsErrors in .csproj
- [x] Create 4 classlib projects for PatientAccess module (`Domain`, `Application`, `Data`, `Presentation`) with correct `ProjectReference` chain and add all to `PropelIQ.slnx`
- [x] Create 4 classlib projects for ClinicalIntelligence module (same structure) and add to solution
- [x] Create 4 classlib projects for Admin module (same structure) and add to solution
- [x] Add `ProjectReference` to all 3 `*.Presentation` projects in `PropelIQ.Api.csproj`; create `ServiceCollectionExtensions.cs` in each Presentation project with `Add[Module]Module(IServiceCollection)` extension method
- [x] Configure `Program.cs`: Serilog bootstrap, call all 3 module DI extensions, add controllers, configure middleware pipeline (`UseExceptionHandler`, `UseHttpsRedirection`, `UseAuthorization`, `MapControllers`)
- [x] Implement `ConfigurationValidator.cs`: validate required config keys at startup, log all missing keys via Serilog, throw `InvalidOperationException` if any are missing

# Task - task_002_be_swagger_healthcheck_iis

## Requirement Reference

- User Story: us_002
- Story Location: .propel/context/tasks/EP-TECH/us_002/us_002.md
- Acceptance Criteria:
  - AC-3: **Given** the API is running, **When** navigating to the Swagger endpoint, **Then** OpenAPI documentation is rendered with all configured endpoints visible per TR-011.
  - AC-4: **Given** the backend targets IIS deployment, **When** publishing the application, **Then** a web deploy package is generated that can be deployed to IIS on Windows Server per NFR-014.
  - AC-5: **Given** the RESTful API is configured, **When** making a health check request, **Then** the API returns HTTP 200 with environment and version metadata.
- Edge Cases:
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
| API Documentation | Swashbuckle.AspNetCore | 6.x |
| Health Checks | Microsoft.AspNetCore.Diagnostics.HealthChecks | 8.0 (built-in) |
| Deployment | IIS (Windows Server) via AspNetCoreModuleV2 | - |
| Language | C# | 12 |

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

Integrate Swashbuckle.AspNetCore 6.x to generate and serve OpenAPI documentation at `/swagger`, implement the `/api/health` endpoint using ASP.NET Core built-in health checks returning environment name, app version, and timestamp, configure `web.config` for IIS hosting with AspNetCoreModuleV2.InProcess, and create a web deploy publish profile so the application can be published as an IIS deployment package. This task satisfies AC-3 (Swagger), AC-4 (IIS deploy), and AC-5 (health check) for US_002.

## Dependent Tasks

- task_001_be_solution_modular_structure — API host project (`PropelIQ.Api`) must exist before adding Swagger, health checks, and deployment configuration

## Impacted Components

- **MODIFY** `server/src/PropelIQ.Api/PropelIQ.Api.csproj` — Add Swashbuckle NuGet package reference, enable XML doc generation
- **MODIFY** `server/src/PropelIQ.Api/Program.cs` — Add Swagger middleware, health check registration and mapping
- **NEW** `server/src/PropelIQ.Api/HealthCheck/HealthCheckResponse.cs` — Typed response model for the health endpoint
- **MODIFY** `server/src/PropelIQ.Api/appsettings.json` — Add `Application.Version` and Swagger enabled flag
- **NEW** `server/src/PropelIQ.Api/web.config` — IIS AspNetCoreModuleV2 InProcess configuration
- **NEW** `server/src/PropelIQ.Api/Properties/PublishProfiles/IIS-WebDeploy.pubxml` — MsDeploy publish profile for web deploy package generation

## Implementation Plan

1. **Install Swashbuckle**: Add `<PackageReference Include="Swashbuckle.AspNetCore" Version="6.*" />` to `PropelIQ.Api.csproj`. Enable XML doc output: `<GenerateDocumentationFile>true</GenerateDocumentationFile>`. Suppress warning 1591 (`<NoWarn>$(NoWarn);1591</NoWarn>`) to avoid false positives on auto-generated files
2. **Configure SwaggerGen**: In `Program.cs` add `AddEndpointsApiExplorer()` and `AddSwaggerGen()` with `SwaggerDoc("v1", new OpenApiInfo { Title = "PropelIQ API", Version = "v1", Description = "..." })`. Integrate XML comments via `IncludeXmlComments(xmlPath, includeControllerXmlComments: true)`. Enable per-request — not environment-gated — per TR-011 (all environments)
3. **Add Swagger middleware**: In the request pipeline after `app.Build()`, register `app.UseSwagger()` and `app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "PropelIQ API V1"))`. Map Swagger UI at `/swagger`
4. **Register health checks**: In services, call `builder.Services.AddHealthChecks()`. Create `HealthCheckResponse` record with `Status`, `Environment`, `Version`, `Timestamp` properties. Map endpoint: `app.MapHealthChecks("/api/health", new HealthCheckOptions { ResponseWriter = WriteJsonHealthResponse })` where the writer serializes the typed response; HTTP 200 for Healthy, 503 for Unhealthy
5. **Create web.config**: Add `server/src/PropelIQ.Api/web.config` with AspNetCoreModuleV2 handler, `processPath="dotnet"`, `arguments=".\PropelIQ.Api.dll"`, `hostingModel="inprocess"`, `stdoutLogEnabled="false"`. Set `<CopyToOutputDirectory>Always</CopyToOutputDirectory>` in project file so it is included in publish output
6. **Create IIS publish profile**: Add `Properties/PublishProfiles/IIS-WebDeploy.pubxml` with `PublishMethod=MSDeploy`, `MSDeployPublishMethod=Package`, `PackageAsMSDeployment=true`, `DeployIisAppPath=PropelIQ`, `PackageLocation=publish/PropelIQ.zip`. Enables `dotnet publish -p:PublishProfile=IIS-WebDeploy` to generate the zip package

## Current Project State

```
server/
├── global.json                          # .NET 8 SDK pin (task_001)
├── PropelIQ.sln                         # All 13 projects (task_001)
└── src/
    ├── PropelIQ.Api/
    │   ├── PropelIQ.Api.csproj          # API host (task_001)
    │   ├── Program.cs                   # Minimal hosting (task_001)
    │   ├── ConfigurationValidator.cs    # Startup validation (task_001)
    │   ├── appsettings.json             # Base config (task_001)
    │   └── appsettings.Development.json # Dev overrides (task_001)
    └── Modules/
        ├── PatientAccess/               # 4-layer module (task_001)
        ├── ClinicalIntelligence/        # 4-layer module (task_001)
        └── Admin/                       # 4-layer module (task_001)
```

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | server/src/PropelIQ.Api/PropelIQ.Api.csproj | Add Swashbuckle.AspNetCore 6.x package; enable GenerateDocumentationFile; suppress CS1591; set web.config CopyToOutputDirectory=Always |
| MODIFY | server/src/PropelIQ.Api/Program.cs | Add Swagger services (AddEndpointsApiExplorer, AddSwaggerGen); add health check service (AddHealthChecks); enable Swagger + health check middleware (UseSwagger, UseSwaggerUI, MapHealthChecks) |
| CREATE | server/src/PropelIQ.Api/HealthCheck/HealthCheckResponse.cs | Typed record: Status, Environment, Version (from assembly), Timestamp (UTC) |
| MODIFY | server/src/PropelIQ.Api/appsettings.json | Add Application.Name, Application.Version, Serilog section, Swagger.Enabled flag |
| CREATE | server/src/PropelIQ.Api/web.config | IIS AspNetCoreModuleV2 configuration with InProcess hosting model |
| CREATE | server/src/PropelIQ.Api/Properties/PublishProfiles/IIS-WebDeploy.pubxml | MsDeploy web deploy package publish profile |

## External References

- Swashbuckle.AspNetCore 6.x getting started: https://github.com/domaindrivendev/Swashbuckle.AspNetCore
- ASP.NET Core OpenAPI/Swagger: https://learn.microsoft.com/en-us/aspnet/core/tutorials/web-api-help-pages-using-swagger?view=aspnetcore-8.0
- ASP.NET Core health checks: https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-8.0
- IIS hosting model (InProcess): https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/?view=aspnetcore-8.0
- web.config reference for ASP.NET Core Module: https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/web-config?view=aspnetcore-8.0
- dotnet publish profiles: https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/visual-studio-publish-profiles?view=aspnetcore-8.0

## Build Commands

```bash
cd server
dotnet add src/PropelIQ.Api package Swashbuckle.AspNetCore --version 6.*
dotnet build PropelIQ.sln
dotnet run --project src/PropelIQ.Api      # Verify Swagger at http://localhost:5000/swagger
# Health check:
curl http://localhost:5000/api/health
# IIS deploy package:
dotnet publish src/PropelIQ.Api -p:PublishProfile=IIS-WebDeploy
```

## Implementation Validation Strategy

- [x] `GET /swagger/v1/swagger.json` returns HTTP 200 with valid OpenAPI JSON
- [x] Swagger UI loads at `GET /swagger` and renders all mapped controller endpoints
- [x] `GET /api/health` returns HTTP 200 with JSON body containing `status`, `environment`, `version`, and `timestamp` fields
- [x] `dotnet publish -p:PublishProfile=IIS-WebDeploy` creates `publish/PropelIQ.zip` without errors
- [x] `publish/PropelIQ.zip` contains `web.config`, `PropelIQ.Api.dll`, and all dependency files
- [x] `web.config` in publish output has `hostingModel="inprocess"` and references `PropelIQ.Api.dll`

## Implementation Checklist

- [x] Add `Swashbuckle.AspNetCore 6.*` package reference to `PropelIQ.Api.csproj`; enable `GenerateDocumentationFile`; suppress `CS1591`; set `web.config` as `CopyToOutputDirectory=Always`
- [x] Configure `AddSwaggerGen` in `Program.cs` with `OpenApiInfo` (title, version, description) and XML comments integration via `IncludeXmlComments(xmlPath)`
- [x] Add `UseSwagger()` and `UseSwaggerUI()` middleware in the request pipeline (not environment-gated per TR-011)
- [x] Register `AddHealthChecks()`, create `HealthCheckResponse` record, implement custom JSON `ResponseWriter` serializing environment + version + timestamp; map at `GET /api/health`
- [x] Create `server/src/PropelIQ.Api/web.config` with AspNetCoreModuleV2 handler targeting `PropelIQ.Api.dll` with InProcess hosting model
- [x] Create `Properties/PublishProfiles/IIS-WebDeploy.pubxml` for web deploy package output to `publish/PropelIQ.zip`

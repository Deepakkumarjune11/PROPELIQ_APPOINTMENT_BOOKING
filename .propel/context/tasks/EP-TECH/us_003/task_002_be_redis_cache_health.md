# Task - task_002_be_redis_cache_health

## Requirement Reference
- User Story: [us_003] (.propel/context/tasks/EP-TECH/us_003/us_003.md)
- Story Location: `.propel/context/tasks/EP-TECH/us_003/us_003.md`
- Acceptance Criteria:
  - AC-3: Upstash Redis cache client connects successfully on application start and can perform set/get operations for session state and query caching per TR-004.
  - AC-4: Both PostgreSQL and Redis connections are validated by health check endpoints returning healthy status when integration tests run.
- Edge Case:
  - When PostgreSQL is unreachable: Health check returns degraded status with specific connection error details logged via Serilog.
  - When Redis is temporarily down: Application falls back to direct database queries with degraded performance; cache miss events are logged per Serilog structured logging (TR-018).

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
| Caching Client | StackExchange.Redis | 2.x |
| Distributed Cache | Microsoft.Extensions.Caching.StackExchangeRedis | 8.x |
| Health Checks – Redis | AspNetCore.HealthChecks.Redis | 8.x |
| Health Checks – NpgSql | AspNetCore.HealthChecks.NpgSql | 8.x |
| Caching Service | Upstash Redis | Cloud |
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

Establishes the Upstash Redis caching layer and enriches the existing health check endpoint to cover both the PostgreSQL and Redis connections. Defines an `ICacheService` abstraction in the Application layer, implements `RedisCacheService` backed by `IConnectionMultiplexer` with graceful fallback logging on `RedisConnectionException`, registers the Redis connection and `IDistributedCache` in the DI container using the Upstash connection string from configuration, and wires `AspNetCore.HealthChecks.Redis` and `AspNetCore.HealthChecks.NpgSql` health checks to the existing `/api/health` endpoint. Updates `HealthCheckResponse` to include per-check status details and a top-level degraded state when either dependency is unavailable — enabling integration tests per AC-4.

## Dependent Tasks
- us_003/task_001_db_postgres_pgvector_efcore — `PropelIQDbContext` must be registered before adding the NpgSql health check.
- us_002/task_002_be_swagger_healthcheck_iis — `/api/health` endpoint and `HealthCheckResponse` scaffold must exist before enriching them.

## Impacted Components
- `server/src/PropelIQ.Api/PropelIQ.Api.csproj` — Add StackExchange.Redis, IDistributedCache extension, and health check NuGet packages
- `server/src/PropelIQ.PatientAccess.Application/Infrastructure/ICacheService.cs` — New cache service interface
- `server/src/PropelIQ.Api/Infrastructure/Caching/RedisCacheService.cs` — New Redis implementation with fallback
- `server/src/PropelIQ.Api/Program.cs` — Register ConnectionMultiplexer, IDistributedCache, ICacheService, and health checks
- `server/src/PropelIQ.Api/HealthCheck/HealthCheckResponse.cs` — Extend to include per-check status and degraded details
- `server/src/PropelIQ.Api/appsettings.json` — Add `Redis:ConnectionString` placeholder
- `server/src/PropelIQ.Api/appsettings.Development.json` — Add local Redis connection string for Docker dev

## Implementation Plan

1. **NuGet package additions** — Add the following packages to `PropelIQ.Api.csproj`:
   - `StackExchange.Redis` version `2.*`
   - `Microsoft.Extensions.Caching.StackExchangeRedis` version `8.*`
   - `AspNetCore.HealthChecks.Redis` version `8.*`
   - `AspNetCore.HealthChecks.NpgSql` version `8.*`

2. **Define ICacheService interface** — Create `ICacheService` in `PropelIQ.PatientAccess.Application/Infrastructure/ICacheService.cs`:
   ```csharp
   public interface ICacheService
   {
       Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
       Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default);
       Task RemoveAsync(string key, CancellationToken ct = default);
   }
   ```
   The interface lives in the Application layer so that domain/application services can depend on the abstraction without referencing the Redis client directly.

3. **Implement RedisCacheService** — Create `RedisCacheService : ICacheService` in `PropelIQ.Api/Infrastructure/Caching/RedisCacheService.cs`:
   - Inject `IConnectionMultiplexer` and `ILogger<RedisCacheService>`.
   - `GetAsync<T>`: call `db.StringGetAsync(key)`, deserialize JSON, return null on `RedisConnectionException` and log a structured warning `"Cache miss (Redis unavailable) for key {Key}"` to enable the degraded-performance fallback path per edge case spec.
   - `SetAsync<T>`: call `db.StringSetAsync(key, json, expiry)`, catch `RedisConnectionException` and log structured warning `"Cache set skipped (Redis unavailable) for key {Key}"`.
   - `RemoveAsync`: call `db.KeyDeleteAsync(key)`, same catch-log pattern.
   - `AbortOnConnectFail = false` ensures the app starts even when Redis is down.

4. **Register Redis in DI** — In `Program.cs`:
   ```csharp
   // IConnectionMultiplexer singleton — shared across the app lifetime
   var redisConnectionString = builder.Configuration["Redis:ConnectionString"]
       ?? throw new InvalidOperationException("Redis:ConnectionString is required.");
   builder.Services.AddSingleton<IConnectionMultiplexer>(
       ConnectionMultiplexer.Connect(new ConfigurationOptions
       {
           EndPoints = { redisConnectionString },
           AbortOnConnectFail = false,
           ConnectRetry = 3,
           ConnectTimeout = 5000,
           SyncTimeout = 3000,
           Ssl = true   // Upstash requires TLS
       }));
   builder.Services.AddScoped<ICacheService, RedisCacheService>();

   // IDistributedCache for ASP.NET Core session and built-in cache consumers
   builder.Services.AddStackExchangeRedisCache(options =>
       options.Configuration = redisConnectionString);
   ```

5. **Wire health checks** — In `Program.cs`, extend the existing `.AddHealthChecks()` call:
   ```csharp
   builder.Services.AddHealthChecks()
       .AddNpgSql(
           builder.Configuration.GetConnectionString("DefaultConnection")!,
           name: "postgresql",
           failureStatus: HealthStatus.Degraded,
           tags: ["database", "readiness"])
       .AddRedis(
           redisConnectionString,
           name: "redis",
           failureStatus: HealthStatus.Degraded,
           tags: ["cache", "readiness"]);
   ```
   Using `HealthStatus.Degraded` (not `Unhealthy`) for individual checks ensures the app continues running even when one dependency is down — the top-level `/api/health` status will be `Degraded` for partial failures.

6. **Enrich HealthCheckResponse** — Update `HealthCheckResponse.cs` to include:
   ```csharp
   public record HealthCheckResponse(
       string Status,
       string Environment,
       string Version,
       DateTimeOffset Timestamp,
       IReadOnlyDictionary<string, string> Checks);
   ```
   Update the `ResponseWriter` lambda in `Program.cs` (from us_002 task_002) to populate `Checks` using `healthReport.Entries.ToDictionary(e => e.Key, e => e.Value.Status.ToString())`.

7. **appsettings configuration** — Add to `appsettings.json`:
   ```json
   "Redis": {
     "ConnectionString": "CHANGE_ME.upstash.io:6380,password=CHANGE_ME,ssl=True,abortConnect=False"
   }
   ```
   Add to `appsettings.Development.json`:
   ```json
   "Redis": {
     "ConnectionString": "localhost:6379,abortConnect=False"
   }
   ```
   Add a local Redis service to `docker-compose.override.yml` (port 6379) for dev convenience.

8. **Update ConfigurationValidator** — In `ConfigurationValidator.cs` (from us_002), add a check for `Redis:ConnectionString` alongside the existing `ConnectionStrings:DefaultConnection` validation, so missing config fails fast at startup.

## Current Project State

```
server/
├── docker-compose.yml               ← Created in task_001
├── docker-compose.override.yml      ← Created in task_001
├── global.json
├── PropelIQ.sln
└── src/
    ├── PropelIQ.Api/
    │   ├── Program.cs               ← AddDbContext + Swagger + basic /api/health registered
    │   ├── PropelIQ.Api.csproj      ← Swashbuckle, EF Design packages present
    │   ├── appsettings.json         ← ConnectionStrings:DefaultConnection present
    │   ├── appsettings.Development.json
    │   ├── ConfigurationValidator.cs
    │   ├── HealthCheck/
    │   │   └── HealthCheckResponse.cs  ← Basic status/env/version/timestamp record
    │   └── Properties/PublishProfiles/
    ├── PropelIQ.PatientAccess.Application/
    ├── PropelIQ.PatientAccess.Data/
    │   ├── Entities/                ← All 10 entity classes from task_001
    │   ├── Migrations/              ← Initial migration from task_001
    │   └── PropelIQDbContext.cs
    └── (other modules)
```

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | `server/src/PropelIQ.Api/PropelIQ.Api.csproj` | Add `StackExchange.Redis 2.*`, `Microsoft.Extensions.Caching.StackExchangeRedis 8.*`, `AspNetCore.HealthChecks.Redis 8.*`, `AspNetCore.HealthChecks.NpgSql 8.*` |
| CREATE | `server/src/PropelIQ.PatientAccess.Application/Infrastructure/ICacheService.cs` | Generic cache abstraction (`GetAsync<T>`, `SetAsync<T>`, `RemoveAsync`) |
| CREATE | `server/src/PropelIQ.Api/Infrastructure/Caching/RedisCacheService.cs` | Redis implementation of `ICacheService`; catch `RedisConnectionException` and log cache miss warning for fallback path |
| MODIFY | `server/src/PropelIQ.Api/Program.cs` | Register `IConnectionMultiplexer` singleton (Upstash TLS options), `IDistributedCache` via `AddStackExchangeRedisCache`, `ICacheService → RedisCacheService`, PostgreSQL + Redis health checks |
| MODIFY | `server/src/PropelIQ.Api/HealthCheck/HealthCheckResponse.cs` | Add `Checks` dictionary field; update `ResponseWriter` to populate per-check statuses and reflect degraded top-level status |
| MODIFY | `server/src/PropelIQ.Api/ConfigurationValidator.cs` | Add validation for `Redis:ConnectionString` key |
| MODIFY | `server/src/PropelIQ.Api/appsettings.json` | Add `Redis:ConnectionString` production placeholder (Upstash format with SSL) |
| MODIFY | `server/src/PropelIQ.Api/appsettings.Development.json` | Add `Redis:ConnectionString` Docker local value (`localhost:6379,abortConnect=False`) |

## External References
- StackExchange.Redis connection options: https://stackexchange.github.io/StackExchange.Redis/Configuration.html
- StackExchange.Redis error handling: https://context7.com/stackexchange/stackexchange.redis/llms.txt
- Microsoft.Extensions.Caching.StackExchangeRedis DI setup: https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed#distributed-redis-cache
- Upstash Redis .NET connection guide: https://upstash.com/docs/redis/sdks/dotnet/getting-started
- AspNetCore.HealthChecks.Redis: https://github.com/Xabaril/AspNetCore.Diagnostics.HealthChecks
- ASP.NET Core health checks middleware: https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks
- TR-004 (Upstash Redis for caching), TR-019 (health check endpoints), NFR-001 (2s response — Redis reduces DB load), NFR-015 (OSS), TR-018 (Serilog structured logging for cache miss events)

## Build Commands
```bash
# Start local PostgreSQL + Redis via Docker Compose
cd server
docker compose up -d

# Restore and build
dotnet restore PropelIQ.sln
dotnet build PropelIQ.sln --configuration Release

# Run the API and verify health endpoint
dotnet run --project src/PropelIQ.Api
curl http://localhost:5000/api/health
# Expected response (all healthy):
# { "status": "Healthy", "environment": "Development", "version": "1.0.0",
#   "timestamp": "...", "checks": { "postgresql": "Healthy", "redis": "Healthy" } }

# Simulate Redis down (stop Redis, re-hit /api/health):
# Expected: { "status": "Degraded", ..., "checks": { "postgresql": "Healthy", "redis": "Degraded" } }
```

## Implementation Validation Strategy
- [ ] Unit tests pass
- [ ] Integration tests pass — both health checks return `Healthy` when both services are up
- [ ] `dotnet build` exits with code 0
- [ ] `GET /api/health` returns `200 OK` with `"status": "Healthy"` and both `postgresql` and `redis` checks present when Docker services are running
- [ ] `GET /api/health` returns `200 OK` with `"status": "Degraded"` and `"redis": "Degraded"` when Redis is stopped (app must not crash — verify `AbortOnConnectFail=false`)
- [ ] `ICacheService.GetAsync<T>` returns null (not throws) when Redis is unavailable; Serilog logs structured warning with key name
- [ ] `ICacheService.SetAsync<T>` silently completes (not throws) when Redis is unavailable; Serilog logs structured warning
- [ ] `ConfigurationValidator` throws `InvalidOperationException` at startup when `Redis:ConnectionString` is missing from config
- [ ] No secrets or passwords appear in source files — configuration uses `appsettings.Development.json` or environment variable override per OWASP A02

## Implementation Checklist
- [x] Add NuGet packages to `PropelIQ.Api.csproj`: `StackExchange.Redis 2.*`, `Microsoft.Extensions.Caching.StackExchangeRedis 8.*`, `AspNetCore.HealthChecks.Redis 8.*`, `AspNetCore.HealthChecks.NpgSql 8.*`
- [x] Create `ICacheService` interface in `PropelIQ.PatientAccess.Application/Infrastructure/ICacheService.cs` with `GetAsync<T>`, `SetAsync<T>`, `RemoveAsync` methods
- [x] Implement `RedisCacheService : ICacheService` in `PropelIQ.Api/Infrastructure/Caching/RedisCacheService.cs`; use `IConnectionMultiplexer.GetDatabase()`; catch `RedisConnectionException` and log structured Serilog warning (do NOT rethrow)
- [x] Register `IConnectionMultiplexer` singleton in `Program.cs` using `ConfigurationOptions.Parse(redisConnectionString)` with `AbortOnConnectFail=false`, `ConnectRetry=3`; register `ICacheService → RedisCacheService`
- [x] Register `IDistributedCache` in `Program.cs` via `AddStackExchangeRedisCache(options => options.Configuration = redisConnectionString)`
- [x] Extend `AddHealthChecks()` in `Program.cs` with `.AddNpgSql(...)` and `.AddRedis(...)`, both using `HealthStatus.Degraded` as failure status
- [x] Update `HealthCheckResponse.cs` to include `IReadOnlyDictionary<string, string> Checks`; update `ResponseWriter` lambda to populate per-check statuses from `healthReport.Entries`
- [x] Add `Redis:ConnectionString` to `appsettings.json` (Upstash placeholder with `ssl=True`) and `appsettings.Development.json` (Docker local without SSL); `ConfigurationValidator` check for this key was already present

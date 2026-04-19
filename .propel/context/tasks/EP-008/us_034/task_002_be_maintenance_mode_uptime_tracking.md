# Task - task_002_be_maintenance_mode_uptime_tracking

## Requirement Reference

- **User Story**: US_034 — Health Checks, Uptime & Recovery
- **Story Location**: `.propel/context/tasks/EP-008/us_034/us_034.md`
- **Acceptance Criteria**:
  - AC-3: Given a critical service failure occurs, when recovery is initiated, then the system recovers to operational state within RTO of 15 minutes per NFR-012.
  - AC-4: Given planned maintenance is needed, when maintenance mode is activated, then the system displays a maintenance page to users, queues incoming requests, and resumes processing after maintenance per NFR-006.
  - AC-5: Given the platform uptime is monitored, when monthly uptime is calculated, then it meets the 99.9% availability target (≤ 43.8 minutes downtime per month) per NFR-017.
- **Edge Cases**:
  - Health check endpoint itself unresponsive: External synthetic monitoring detects this independently (Azure Application Insights availability tests, TR-017). This task seeds the telemetry pipeline by tracking downtime intervals in Redis — but the synthetic probe is external deployment configuration.
  - Cascading failures: Maintenance mode toggle is independent of circuit breakers; activating maintenance mode while a circuit breaker is open is a valid combined state.

---

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Minimal — `GET /api/v1/admin/maintenance/status` drives front-end maintenance banner display |
| **Figma URL** | N/A |
| **Wireframe Status** | N/A |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | N/A |
| **Screen Spec** | N/A (generic maintenance page, no custom design specified) |
| **UXR Requirements** | Maintenance page must include: status message, estimated restoration time, retry-after header |
| **Design Tokens** | N/A |

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Backend | .NET 8 ASP.NET Core | 8.0 LTS |
| Middleware | ASP.NET Core custom middleware | Built-in |
| Redis | `IConnectionMultiplexer` | 2.8.x |
| Config | `IOptionsMonitor<T>` (hot-reload) | Built-in (.NET 8) |
| Logging | Serilog | 3.x |

---

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |

---

## Task Overview

This task has two parts:

**1. Maintenance Mode Middleware (AC-4):**
A custom ASP.NET Core middleware (`MaintenanceModeMiddleware`) that short-circuits all incoming requests (except health check and maintenance admin endpoints) when maintenance mode is active. It returns HTTP 503 with a `Retry-After` header and a JSON body (`MaintenanceModeResponse`). Maintenance mode state is stored in Redis (`maintenance:active` key) so it persists across app restarts and can be toggled from any API instance in a multi-instance deployment.

Admin endpoints:
- `POST /api/v1/admin/maintenance/activate` — sets `maintenance:active = 1` in Redis; records start time in `maintenance:started_at`
- `POST /api/v1/admin/maintenance/deactivate` — deletes `maintenance:active`; records downtime duration in uptime tracker
- `GET /api/v1/admin/maintenance/status` — returns `{ isActive, startedAtUtc, estimatedMinutes }`

**2. Uptime Tracking (AC-5):**
An `IUptimeTracker` service backed by Redis that records downtime intervals. `RecordDowntimeStartAsync` and `RecordDowntimeEndAsync` are called by the maintenance deactivation flow and by the health check alert job (task_001) when a critical service enters `Unhealthy` state. A `GET /api/v1/admin/uptime/monthly` endpoint computes monthly uptime % from recorded intervals.

The RTO guidance (AC-3) is addressed via:
- Recovery runbook documented in `appsettings.json` comments (operational) — not a code artifact
- `MaintenanceModeMiddleware` bypasses are limited to health check + maintenance admin endpoints, ensuring operators can always reach recovery tools
- The uptime tracker records actual recovery time for SLA audit

---

## Dependent Tasks

- **task_001_be_health_checks_alert_notification.md** (this US) — `HealthCheckAlertJob` calls `IUptimeTracker.RecordDowntimeStartAsync` when a check reaches threshold (i.e., enters confirmed failure). This task creates `IUptimeTracker`; task_001 passes the interface.
- **US_030** — `AzureOpenAiHealthCheck` already exists; maintenance mode must NOT bypass Azure OpenAI health probes.

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/PropelIQ.Api/Infrastructure/Maintenance/MaintenanceModeMiddleware.cs` | Returns HTTP 503 + JSON + `Retry-After: 300` for all non-exempt paths |
| CREATE | `server/src/PropelIQ.Api/Infrastructure/Maintenance/IMaintenanceModeService.cs` | `IsActiveAsync(ct)`, `ActivateAsync(estimatedMinutes, ct)`, `DeactivateAsync(ct)` |
| CREATE | `server/src/PropelIQ.Api/Infrastructure/Maintenance/RedisMaintenanceModeService.cs` | Redis keys: `maintenance:active` (SET/DEL) + `maintenance:started_at` (ISO8601 string) + `maintenance:estimated_minutes` (int) |
| CREATE | `server/src/PropelIQ.Api/Infrastructure/Maintenance/MaintenanceModeResponse.cs` | `record(string Message, DateTime? StartedAtUtc, int EstimatedMinutes)` — returned as JSON on 503 |
| CREATE | `server/src/PropelIQ.Api/Infrastructure/Uptime/IUptimeTracker.cs` | `RecordDowntimeStartAsync(service, ct)`, `RecordDowntimeEndAsync(service, ct)`, `GetMonthlyUptimeAsync(year, month, ct) → UptimeReport` |
| CREATE | `server/src/PropelIQ.Api/Infrastructure/Uptime/RedisUptimeTracker.cs` | Redis key `uptime:downtime:{service}:{year}-{month}` (list of JSON intervals); computes downtime seconds; returns % uptime |
| CREATE | `server/src/PropelIQ.Api/Infrastructure/Uptime/UptimeReport.cs` | `record(string Service, int Year, int Month, double UptimePercent, double TotalDowntimeMinutes, int IncidentCount)` |
| CREATE | `server/src/Modules/Admin/Admin.Presentation/MaintenanceController.cs` | `POST activate`, `POST deactivate`, `GET status` — all `[Authorize(Roles="admin")]` |
| CREATE | `server/src/PropelIQ.Api/Controllers/UptimeController.cs` | `GET /api/v1/admin/uptime/monthly` — `[Authorize(Roles="staff,admin")]`; query by year/month |
| MODIFY | `server/src/PropelIQ.Api/Program.cs` | Register middleware via `app.UseMiddleware<MaintenanceModeMiddleware>()` early in pipeline; register `IMaintenanceModeService` + `IUptimeTracker` singletons |
| MODIFY | `server/src/PropelIQ.Api/appsettings.json` | Add `"MaintenanceMode"` section (exempt paths list, default message) |

---

## Implementation Plan

### 1. `IMaintenanceModeService` + `RedisMaintenanceModeService`

```csharp
// Infrastructure/Maintenance/IMaintenanceModeService.cs
public interface IMaintenanceModeService
{
    Task<bool> IsActiveAsync(CancellationToken ct = default);
    Task ActivateAsync(int estimatedMinutes, CancellationToken ct = default);
    Task DeactivateAsync(CancellationToken ct = default);
    Task<MaintenanceStatus> GetStatusAsync(CancellationToken ct = default);
}

public sealed record MaintenanceStatus(
    bool IsActive,
    DateTime? StartedAtUtc,
    int EstimatedMinutes);
```

```csharp
// Infrastructure/Maintenance/RedisMaintenanceModeService.cs
public sealed class RedisMaintenanceModeService(IConnectionMultiplexer redis)
    : IMaintenanceModeService
{
    private const string ActiveKey   = "maintenance:active";
    private const string StartedKey  = "maintenance:started_at";
    private const string EstimatedKey = "maintenance:estimated_minutes";

    public async Task<bool> IsActiveAsync(CancellationToken ct)
        => await redis.GetDatabase().KeyExistsAsync(ActiveKey);

    public async Task ActivateAsync(int estimatedMinutes, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var batch = db.CreateBatch();
        _ = batch.StringSetAsync(ActiveKey, "1");
        _ = batch.StringSetAsync(StartedKey,
            DateTime.UtcNow.ToString("O"),           // ISO 8601 round-trip
            TimeSpan.FromHours(24));                 // auto-clear after 24h safety TTL
        _ = batch.StringSetAsync(EstimatedKey, estimatedMinutes.ToString());
        batch.Execute();
        await Task.CompletedTask;
    }

    public async Task DeactivateAsync(CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var batch = db.CreateBatch();
        _ = batch.KeyDeleteAsync(ActiveKey);
        _ = batch.KeyDeleteAsync(StartedKey);
        _ = batch.KeyDeleteAsync(EstimatedKey);
        batch.Execute();
        await Task.CompletedTask;
    }

    public async Task<MaintenanceStatus> GetStatusAsync(CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var isActive = await db.KeyExistsAsync(ActiveKey);
        DateTime? startedAt = null;
        var estimated = 0;

        if (isActive)
        {
            var startedVal = await db.StringGetAsync(StartedKey);
            if (startedVal.HasValue && DateTime.TryParse(startedVal,
                null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                startedAt = dt;

            var estimatedVal = await db.StringGetAsync(EstimatedKey);
            if (estimatedVal.HasValue && int.TryParse(estimatedVal, out var e))
                estimated = e;
        }
        return new MaintenanceStatus(isActive, startedAt, estimated);
    }
}
```

### 2. `MaintenanceModeMiddleware`

```csharp
// Infrastructure/Maintenance/MaintenanceModeMiddleware.cs
public sealed class MaintenanceModeMiddleware(
    RequestDelegate next,
    IMaintenanceModeService maintenance,
    IOptions<MaintenanceModeOptions> opts,
    ILogger<MaintenanceModeMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Exempt paths — health check and maintenance admin endpoints must always be reachable
        var exemptPaths = opts.Value.ExemptPaths;
        if (exemptPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await next(context);
            return;
        }

        if (await maintenance.IsActiveAsync(context.RequestAborted))
        {
            var status = await maintenance.GetStatusAsync(context.RequestAborted);
            logger.LogInformation(
                "Request blocked by maintenance mode: {Path}", path);

            context.Response.StatusCode  = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "application/json";
            context.Response.Headers.Append("Retry-After", "300");

            var body = new MaintenanceModeResponse(
                Message:          "System is under maintenance. Please try again later.",
                StartedAtUtc:     status.StartedAtUtc,
                EstimatedMinutes: status.EstimatedMinutes);

            await context.Response.WriteAsJsonAsync(body, context.RequestAborted);
            return;
        }

        await next(context);
    }
}
```

### 3. `MaintenanceModeOptions` — configuration POCO

```csharp
// Infrastructure/Maintenance/MaintenanceModeOptions.cs
public sealed class MaintenanceModeOptions
{
    public const string SectionName = "MaintenanceMode";

    /// <summary>
    /// URL path prefixes that bypass maintenance mode. Health check and admin
    /// maintenance endpoints must always be reachable for recovery.
    /// </summary>
    public string[] ExemptPaths { get; set; } =
    [
        "/api/health",
        "/api/v1/admin/maintenance",
        "/swagger"          // Allow SwaggerUI during development maintenance
    ];

    public string DefaultMessage { get; set; } =
        "System is under planned maintenance. Please try again shortly.";
}
```

### 4. `IUptimeTracker` + `RedisUptimeTracker`

```csharp
// Infrastructure/Uptime/IUptimeTracker.cs
public interface IUptimeTracker
{
    Task RecordDowntimeStartAsync(string service, CancellationToken ct = default);
    Task RecordDowntimeEndAsync(string service, CancellationToken ct = default);
    Task<UptimeReport> GetMonthlyUptimeAsync(string service, int year, int month,
        CancellationToken ct = default);
}
```

```csharp
// Infrastructure/Uptime/RedisUptimeTracker.cs
// Redis key: uptime:downtime:{service}:{year}-{month:D2}
// Value: JSON list of { "start": "<ISO8601>", "end": "<ISO8601>" }
// Append-only via RPUSH; each downtime interval is one JSON object
public sealed class RedisUptimeTracker(IConnectionMultiplexer redis) : IUptimeTracker
{
    // Active downtime start tracking (in-flight incidents)
    private static string StartKey(string svc) => $"uptime:active_start:{svc}";
    private static string IntervalKey(string svc, int y, int m)
        => $"uptime:downtime:{svc}:{y}-{m:D2}";

    public async Task RecordDowntimeStartAsync(string service, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        // Only set if not already tracking (prevents duplicate start on repeated alerts)
        await db.StringSetAsync(
            StartKey(service),
            DateTime.UtcNow.ToString("O"),
            when: When.NotExists);
    }

    public async Task RecordDowntimeEndAsync(string service, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var startVal = await db.StringGetAsync(StartKey(service));
        if (!startVal.HasValue) return;  // No active downtime to close

        var now = DateTime.UtcNow;
        var interval = JsonSerializer.Serialize(new
        {
            start = startVal.ToString(),
            end = now.ToString("O")
        });

        var key = IntervalKey(service, now.Year, now.Month);
        await db.ListRightPushAsync(key, interval);
        await db.KeyExpireAsync(key, TimeSpan.FromDays(400));  // Retain ~13 months
        await db.KeyDeleteAsync(StartKey(service));
    }

    public async Task<UptimeReport> GetMonthlyUptimeAsync(
        string service, int year, int month, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var key = IntervalKey(service, year, month);
        var entries = await db.ListRangeAsync(key, 0, -1);

        var totalDowntimeSeconds = 0.0;
        foreach (var entry in entries)
        {
            using var doc = JsonDocument.Parse(entry.ToString());
            var root = doc.RootElement;
            if (DateTime.TryParse(root.GetProperty("start").GetString(),
                    null, System.Globalization.DateTimeStyles.RoundtripKind, out var start) &&
                DateTime.TryParse(root.GetProperty("end").GetString(),
                    null, System.Globalization.DateTimeStyles.RoundtripKind, out var end))
            {
                totalDowntimeSeconds += (end - start).TotalSeconds;
            }
        }

        // Total seconds in the given month
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var totalMonthSeconds = daysInMonth * 24.0 * 60.0 * 60.0;
        var uptimePercent = Math.Max(0,
            (totalMonthSeconds - totalDowntimeSeconds) / totalMonthSeconds * 100.0);

        return new UptimeReport(
            Service:              service,
            Year:                 year,
            Month:                month,
            UptimePercent:        Math.Round(uptimePercent, 4),
            TotalDowntimeMinutes: Math.Round(totalDowntimeSeconds / 60.0, 2),
            IncidentCount:        entries.Length);
    }
}
```

### 5. `MaintenanceController` — admin endpoints

```csharp
// Modules/Admin/Admin.Presentation/MaintenanceController.cs
[ApiController]
[Route("api/v1/admin/maintenance")]
[Authorize(Roles = "admin")]
public sealed class MaintenanceController(
    IMaintenanceModeService maintenance,
    IUptimeTracker uptime,
    ILogger<MaintenanceController> logger) : ControllerBase
{
    [HttpPost("activate")]
    public async Task<IActionResult> Activate(
        [FromBody] ActivateMaintenanceRequest request, CancellationToken ct)
    {
        var estimatedMinutes = Math.Clamp(request.EstimatedMinutes, 1, 480);  // max 8h
        await maintenance.ActivateAsync(estimatedMinutes, ct);
        await uptime.RecordDowntimeStartAsync("api", ct);
        logger.LogWarning("Maintenance mode ACTIVATED by {User}, estimated {Minutes} min",
            User.Identity?.Name, estimatedMinutes);
        return Ok(new { message = "Maintenance mode activated", estimatedMinutes });
    }

    [HttpPost("deactivate")]
    public async Task<IActionResult> Deactivate(CancellationToken ct)
    {
        await maintenance.DeactivateAsync(ct);
        await uptime.RecordDowntimeEndAsync("api", ct);
        logger.LogInformation("Maintenance mode DEACTIVATED by {User}",
            User.Identity?.Name);
        return Ok(new { message = "Maintenance mode deactivated" });
    }

    [HttpGet("status")]
    [AllowAnonymous]     // Status may be read without auth (e.g., by the maintenance banner)
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        var status = await maintenance.GetStatusAsync(ct);
        return Ok(status);
    }
}

public sealed record ActivateMaintenanceRequest(int EstimatedMinutes);
```

### 6. `UptimeController` — uptime reporting endpoint

```csharp
// PropelIQ.Api/Controllers/UptimeController.cs
[ApiController]
[Route("api/v1/admin/uptime")]
[Authorize(Roles = "staff,admin")]
public sealed class UptimeController(IUptimeTracker tracker) : ControllerBase
{
    [HttpGet("monthly")]
    public async Task<IActionResult> GetMonthlyUptime(
        [FromQuery] string service = "api",
        [FromQuery] int? year  = null,
        [FromQuery] int? month = null,
        CancellationToken ct = default)
    {
        // Input validation — prevent invalid date combinations
        var y = year  ?? DateTime.UtcNow.Year;
        var m = month ?? DateTime.UtcNow.Month;

        if (m < 1 || m > 12)
            return BadRequest(new { error = "month must be between 1 and 12" });

        // Allow-list service parameter to prevent Redis key injection (OWASP A03)
        var allowed = new HashSet<string> { "api", "postgresql", "redis", "azure-openai", "hangfire" };
        if (!allowed.Contains(service))
            return BadRequest(new { error = "unknown service name" });

        var report = await tracker.GetMonthlyUptimeAsync(service, y, m, ct);
        return Ok(report);
    }
}
```

### 7. Middleware registration in `Program.cs`

```csharp
// DI registrations (builder phase):
builder.Services.AddSingleton<IMaintenanceModeService, RedisMaintenanceModeService>();
builder.Services.AddSingleton<IUptimeTracker, RedisUptimeTracker>();
builder.Services.Configure<MaintenanceModeOptions>(
    builder.Configuration.GetSection(MaintenanceModeOptions.SectionName));

// Middleware pipeline (app phase — BEFORE UseAuthorization and MapControllers):
app.UseMiddleware<MaintenanceModeMiddleware>();
```

> **Middleware ordering**: `MaintenanceModeMiddleware` must be registered **before** `app.UseAuthorization()` and `app.MapControllers()` to short-circuit requests early. It must be registered **after** `app.UseExceptionHandler()` so unhandled exceptions from the middleware itself are still caught.

### 8. `appsettings.json` additions

```json
"MaintenanceMode": {
  "ExemptPaths": [
    "/api/health",
    "/api/v1/admin/maintenance",
    "/swagger"
  ],
  "DefaultMessage": "System is under planned maintenance. Please try again shortly."
}
```

---

## Current Project State

```
server/src/PropelIQ.Api/
├── Program.cs                                      ← MODIFY: add middleware + DI
├── appsettings.json                                ← MODIFY: add "MaintenanceMode" section
├── Controllers/
│   ├── WeatherForecastController.cs                (EXISTS — no change)
│   ├── AnalyticsController.cs                     (EXISTS from US_033 — no change)
│   └── UptimeController.cs                        ← CREATE
└── Infrastructure/
    ├── Caching/                                    (EXISTS)
    ├── Maintenance/
    │   ├── IMaintenanceModeService.cs              ← CREATE
    │   ├── RedisMaintenanceModeService.cs          ← CREATE
    │   ├── MaintenanceModeMiddleware.cs            ← CREATE
    │   ├── MaintenanceModeOptions.cs               ← CREATE
    │   └── MaintenanceModeResponse.cs              ← CREATE
    └── Uptime/
        ├── IUptimeTracker.cs                       ← CREATE
        ├── RedisUptimeTracker.cs                   ← CREATE
        └── UptimeReport.cs                         ← CREATE

server/src/Modules/Admin/Admin.Presentation/
├── AdminController.cs                              (EXISTS from US_031 — no change)
└── MaintenanceController.cs                        ← CREATE
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/PropelIQ.Api/Infrastructure/Maintenance/IMaintenanceModeService.cs` | Interface + `MaintenanceStatus` record |
| CREATE | `server/src/PropelIQ.Api/Infrastructure/Maintenance/RedisMaintenanceModeService.cs` | Redis keys `maintenance:active/started_at/estimated_minutes`; batch SET/DEL; `When.NotExists` guard on activation |
| CREATE | `server/src/PropelIQ.Api/Infrastructure/Maintenance/MaintenanceModeMiddleware.cs` | Short-circuits to HTTP 503 + `Retry-After: 300` + JSON body for non-exempt paths; exempt: `/api/health`, `/api/v1/admin/maintenance`, `/swagger` |
| CREATE | `server/src/PropelIQ.Api/Infrastructure/Maintenance/MaintenanceModeOptions.cs` | `ExemptPaths` string array; `DefaultMessage` string |
| CREATE | `server/src/PropelIQ.Api/Infrastructure/Maintenance/MaintenanceModeResponse.cs` | `record(string Message, DateTime? StartedAtUtc, int EstimatedMinutes)` |
| CREATE | `server/src/PropelIQ.Api/Infrastructure/Uptime/IUptimeTracker.cs` | `RecordDowntimeStartAsync`, `RecordDowntimeEndAsync`, `GetMonthlyUptimeAsync` |
| CREATE | `server/src/PropelIQ.Api/Infrastructure/Uptime/RedisUptimeTracker.cs` | Redis list `uptime:downtime:{service}:{year}-{month}`; RPUSH JSON intervals; `When.NotExists` on start key; computes `UptimePercent` from interval sum |
| CREATE | `server/src/PropelIQ.Api/Infrastructure/Uptime/UptimeReport.cs` | `record(Service, Year, Month, UptimePercent, TotalDowntimeMinutes, IncidentCount)` |
| CREATE | `server/src/Modules/Admin/Admin.Presentation/MaintenanceController.cs` | `POST activate`/`POST deactivate` (admin-only); `GET status` (allow-anonymous); calls `IUptimeTracker` on activate/deactivate |
| CREATE | `server/src/PropelIQ.Api/Controllers/UptimeController.cs` | `GET /api/v1/admin/uptime/monthly`; service allow-list validation (OWASP A03); `[Authorize(Roles="staff,admin")]` |
| MODIFY | `server/src/PropelIQ.Api/Program.cs` | Register `IMaintenanceModeService` + `IUptimeTracker` singletons; `Configure<MaintenanceModeOptions>`; `app.UseMiddleware<MaintenanceModeMiddleware>()` before `UseAuthorization()` |
| MODIFY | `server/src/PropelIQ.Api/appsettings.json` | Add `"MaintenanceMode"` section |

---

## Security Notes (OWASP)

- **A01 — Broken Access Control**: `POST /api/v1/admin/maintenance/activate` and `deactivate` are `[Authorize(Roles="admin")]` only. `GET /api/v1/admin/maintenance/status` is `[AllowAnonymous]` — the status response contains no sensitive data (no PHI, no secrets), only `{ isActive, startedAtUtc, estimatedMinutes }`. This allows front-end maintenance banners to display without requiring authentication.
- **A03 — Injection**: `UptimeController.GetMonthlyUptime` allow-lists `service` parameter against a fixed set before using it as a Redis key segment. Prevents arbitrary Redis key construction (SSRF-adjacent key injection).
- **A05 — Security Misconfiguration**: `ExemptPaths` in `MaintenanceModeOptions` must include `/api/health` — omitting it would prevent health check monitoring during maintenance, creating a monitoring blind spot.
- **A09 — Security Logging**: All maintenance activate/deactivate calls are logged with `User.Identity.Name` at `LogWarning`/`LogInformation` level for audit trail.

---

## External References

- [ASP.NET Core Middleware — Custom middleware](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/write)
- [StackExchange.Redis — When.NotExists batch](https://stackexchange.github.io/StackExchange.Redis/Basics)
- [RFC 7807 — Problem Details for HTTP APIs (Retry-After header)](https://tools.ietf.org/html/rfc7807)
- [design.md — NFR-006 (99.9% uptime); NFR-012 (zero-downtime migrations); NFR-017 (RPO/RTO); TR-019 (health check endpoints)](../.propel/context/docs/design.md)

---

## Build Commands

```powershell
# No new NuGet packages required for this task (all dependencies already present)
dotnet build server/PropelIQ.slnx --no-restore

# Test
dotnet test server/PropelIQ.slnx --no-build --filter "Category=US034_MaintenanceMode"
```

---

## Implementation Validation Strategy

- [ ] `POST /api/v1/admin/maintenance/activate` → subsequent `GET /api/v1/appointments` returns HTTP 503 with `Retry-After: 300` header and JSON body
- [ ] `GET /api/health` during maintenance mode → still returns HTTP 200/Healthy (exempt path)
- [ ] `GET /api/v1/admin/maintenance/status` during maintenance → `{ isActive: true, startedAtUtc: "<timestamp>", estimatedMinutes: 30 }` without auth
- [ ] `GET /api/v1/admin/maintenance/status` when not in maintenance → `{ isActive: false, startedAtUtc: null, estimatedMinutes: 0 }`
- [ ] `POST /api/v1/admin/maintenance/deactivate` → subsequent `GET /api/v1/appointments` returns HTTP 200 again
- [ ] `IUptimeTracker.RecordDowntimeStartAsync("api")` called twice → `When.NotExists` prevents overwriting the start time
- [ ] `IUptimeTracker.GetMonthlyUptimeAsync` with no recorded downtime → `UptimePercent = 100.0`, `TotalDowntimeMinutes = 0`, `IncidentCount = 0`
- [ ] `GET /api/v1/admin/uptime/monthly?service=malicious-injection` → HTTP 400 (service not in allow-list)
- [ ] `GET /api/v1/admin/uptime/monthly?month=13` → HTTP 400 (month out of range)
- [ ] Maintenance mode persist across restart: simulate app restart with `maintenance:active` key in Redis → middleware blocks requests immediately on new instance startup

---

## Implementation Checklist

- [ ] CREATE `MaintenanceModeOptions` — `ExemptPaths` array defaults to `["/api/health", "/api/v1/admin/maintenance", "/swagger"]`; registered via `Configure<MaintenanceModeOptions>()`
- [ ] CREATE `IMaintenanceModeService` + `RedisMaintenanceModeService` — `ActivateAsync` uses `When.NotExists` guard in batch; `DeactivateAsync` deletes all 3 keys in batch; `GetStatusAsync` returns full `MaintenanceStatus` record
- [ ] CREATE `MaintenanceModeMiddleware` — exempt path check first; `IsActiveAsync` only called when non-exempt; HTTP 503 + `Retry-After: 300` + `WriteAsJsonAsync(MaintenanceModeResponse)`
- [ ] CREATE `IUptimeTracker` + `RedisUptimeTracker` — start key uses `When.NotExists` (prevents duplicate start); end closes open interval via RPUSH + 400-day TTL + KeyDelete on start key; monthly report sums all intervals in the list
- [ ] CREATE `UptimeReport` record with `UptimePercent` (4 decimal places), `TotalDowntimeMinutes` (2 decimal places), `IncidentCount`
- [ ] CREATE `MaintenanceController` — `activate` (admin-only) clamps estimated minutes to [1, 480]; `deactivate` (admin-only); `status` (allow-anonymous — no PHI in response); both mutating endpoints log user identity
- [ ] CREATE `UptimeController` — `service` parameter allow-listed to 5 known services; `month` validated to [1, 12]; delegates to `IUptimeTracker`
- [ ] MODIFY `Program.cs` — `UseMiddleware<MaintenanceModeMiddleware>()` placed **after** `UseExceptionHandler()` and **before** `UseAuthorization()` to ensure exceptions are caught but unauth requests see maintenance before auth middleware runs
- [ ] MODIFY `appsettings.json` — add `"MaintenanceMode"` section with default exempt paths and default message

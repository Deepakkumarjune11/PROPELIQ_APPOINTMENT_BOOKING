# Task - task_001_be_health_checks_alert_notification

## Requirement Reference

- **User Story**: US_034 — Health Checks, Uptime & Recovery
- **Story Location**: `.propel/context/tasks/EP-008/us_034/us_034.md`
- **Acceptance Criteria**:
  - AC-1: Given the platform is running, when the health check endpoint is polled (every 30 seconds), then it returns status for API, database, Redis cache, Azure OpenAI, and background job processor per NFR-006.
  - AC-2: Given a service health check fails, when the failure persists for 2 consecutive checks (60 seconds), then the system triggers an alert to the on-call team via configured channels (email, SMS, PagerDuty) per NFR-017.
- **Edge Cases**:
  - Health check endpoint itself becomes unresponsive: External synthetic monitoring detects this independently (out of scope for this task — this task implements the endpoint; external monitoring is deployment/ops configuration). The health check endpoint must complete its own checks within 5 seconds total to avoid false-positive timeouts by uptime monitors.
  - Cascading failures: Circuit breakers (Polly, US_031) isolate failing services. Health check results reflect each dependency independently — one unhealthy dependency returns `Degraded` (not `Unhealthy`) so the app continues serving other requests.

---

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

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Backend | .NET 8 ASP.NET Core | 8.0 LTS |
| Health Checks | `Microsoft.Extensions.Diagnostics.HealthChecks` | Built-in (.NET 8) |
| Background Jobs | Hangfire | 1.8.x |
| Email | SendGrid (free tier) — `Sendgrid` NuGet | 9.x (TR-013) |
| SMS | Twilio (free tier) — `Twilio` NuGet | 6.x (TR-013) |
| Redis | StackExchange.Redis (`IConnectionMultiplexer`) | 2.8.x |
| Logging | Serilog | 3.x |
| Config | `IOptionsMonitor<T>` | Built-in (.NET 8) |

> **Alert channel libraries**: `Twilio` (MIT) and `Sendgrid` (MIT) are already referenced in design.md TR-013 for appointment reminders. This task reuses those same libraries for health alert notifications — no new dependency category. PagerDuty alerting is implemented as an HTTP POST to the PagerDuty Events API v2 — no PagerDuty SDK needed (satisfies NFR-015 OSS constraint).

---

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |

---

## Task Overview

This task has two distinct parts:

**1. Extended Health Checks (AC-1):**
The existing `GET /api/health` endpoint already covers PostgreSQL and Redis (from `Program.cs`). `AzureOpenAiHealthCheck` was established in US_030. This task adds the **missing** health check: **Hangfire background job processor** (`HangfireHealthCheck` — verifies the Hangfire server is processing jobs by querying `select count(*) from hangfire.servers`). It also extends the existing `MapHealthChecks("/api/health/...")` to surface a **unified health endpoint** at `/api/health` (already exists) that includes all five services: API (always Healthy — the endpoint itself responding is the check), PostgreSQL, Redis, Azure OpenAI, and Hangfire.

**2. Consecutive Failure Alert (AC-2):**
A thin Redis-backed state tracker (`IHealthAlertTracker`) that counts consecutive unhealthy check results per service check name. On reaching 2 consecutive failures (≥60 seconds, given 30-second polling), an alert is dispatched via `IAlertNotificationService`. The tracker is driven by a Hangfire recurring job (`HealthCheckAlertJob`) that polls `/api/health` every 30 seconds internally and dispatches alerts.

`IAlertNotificationService` supports three channels (configured in `HealthAlertOptions`):
- **Email** via SendGrid (TR-013)
- **SMS** via Twilio (TR-013)
- **PagerDuty** via Events API v2 HTTP POST

Alert channels are configurable: each can be enabled/disabled independently. Alert deduplication via Redis key `hc:alert:sent:{checkName}` with 5-minute TTL — prevents alert storms (alert once per 5 minutes per check).

---

## Dependent Tasks

- **task_002_be_ai_health_check_degradation.md** (US_030) — `AzureOpenAiHealthCheck` already registers `AddCheck<AzureOpenAiHealthCheck>()` in `Program.cs`; this task adds Hangfire health check alongside it.
- **task_001_be_circuit_breaker_polly.md** (US_031) — Circuit breaker is the isolation mechanism referenced in the cascading-failures edge case. Already implemented; this task does not change it.

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/PropelIQ.Api/HealthCheck/HangfireHealthCheck.cs` | `IHealthCheck`: queries `hangfire.servers` table; timeout 3s; `Degraded` if no active server row |
| CREATE | `server/src/PropelIQ.Api/HealthCheck/IHealthAlertTracker.cs` | `IncrementFailureAsync(checkName, ct)` + `ResetAsync(checkName, ct)` + `GetConsecutiveFailuresAsync(checkName, ct)` |
| CREATE | `server/src/PropelIQ.Api/HealthCheck/RedisHealthAlertTracker.cs` | Redis key `hc:failures:{checkName}` (INCR / DEL / GET); `IHealthAlertTracker` implementation; singleton |
| CREATE | `server/src/PropelIQ.Api/HealthCheck/IAlertNotificationService.cs` | `SendAlertAsync(checkName, status, detail, ct)` |
| CREATE | `server/src/PropelIQ.Api/HealthCheck/AlertNotificationService.cs` | Dispatches to enabled channels (SendGrid email, Twilio SMS, PagerDuty HTTP POST); deduplication via Redis `hc:alert:sent:{checkName}` (5-min TTL) |
| CREATE | `server/src/PropelIQ.Api/HealthCheck/HealthAlertOptions.cs` | `Email` (enabled, to, from, subject prefix), `Sms` (enabled, to, from), `PagerDuty` (enabled, routingKey, severity) |
| CREATE | `server/src/PropelIQ.Api/HealthCheck/HealthCheckAlertJob.cs` | Hangfire recurring job (every 30s); calls `IHealthCheckService`; increments/resets counters; dispatches on threshold=2 |
| MODIFY | `server/src/PropelIQ.Api/Program.cs` | Add `AddCheck<HangfireHealthCheck>()` + register `IHealthAlertTracker`, `IAlertNotificationService`; bind `HealthAlertOptions`; add Hangfire recurring job registration |
| MODIFY | `server/src/PropelIQ.Api/appsettings.json` | Add `"HealthAlert"` section with email/SMS/PagerDuty channel config |

---

## Implementation Plan

### 1. `HangfireHealthCheck` — Hangfire server liveness

```csharp
// PropelIQ.Api/HealthCheck/HangfireHealthCheck.cs
public sealed class HangfireHealthCheck(IConnectionMultiplexer redis,
    PropelIQDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            // Hangfire stores active servers in the hangfire.servers table
            // At least one row means the Hangfire server is alive
            var serverCount = await db.Database.ExecuteSqlRawAsync(
                "SELECT 1 FROM hangfire.servers LIMIT 1", cts.Token);
            // ExecuteSqlRawAsync returns rows affected; for SELECT use FormattableString approach
            var count = await db.Database
                .SqlQueryRaw<int>("SELECT COUNT(*)::INT FROM hangfire.servers")
                .FirstOrDefaultAsync(cts.Token);

            return count > 0
                ? HealthCheckResult.Healthy($"Hangfire: {count} active server(s)")
                : HealthCheckResult.Degraded("Hangfire: no active servers found");
        }
        catch (OperationCanceledException)
        {
            return HealthCheckResult.Degraded("Hangfire: health check timed out (3s)");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Hangfire: probe failed", ex);
        }
    }
}
```

### 2. `HealthAlertOptions` — configuration POCO

```csharp
// PropelIQ.Api/HealthCheck/HealthAlertOptions.cs
public sealed class HealthAlertOptions
{
    public const string SectionName = "HealthAlert";

    public int ConsecutiveFailureThreshold { get; set; } = 2;
    public int AlertDeduplicationMinutes { get; set; } = 5;

    public EmailAlertChannel Email { get; set; } = new();
    public SmsAlertChannel Sms { get; set; } = new();
    public PagerDutyAlertChannel PagerDuty { get; set; } = new();
}

public sealed class EmailAlertChannel
{
    public bool Enabled { get; set; }
    public string ToAddress { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string SubjectPrefix { get; set; } = "[PropelIQ ALERT]";
    public string SendGridApiKey { get; set; } = string.Empty;  // from env/secrets
}

public sealed class SmsAlertChannel
{
    public bool Enabled { get; set; }
    public string ToNumber { get; set; } = string.Empty;
    public string FromNumber { get; set; } = string.Empty;
    public string AccountSid { get; set; } = string.Empty;  // from env/secrets
    public string AuthToken { get; set; } = string.Empty;   // from env/secrets
}

public sealed class PagerDutyAlertChannel
{
    public bool Enabled { get; set; }
    public string RoutingKey { get; set; } = string.Empty;  // from env/secrets
    public string Severity { get; set; } = "critical";       // critical|error|warning|info
}
```

### 3. `IHealthAlertTracker` + `RedisHealthAlertTracker`

```csharp
// IHealthAlertTracker.cs
public interface IHealthAlertTracker
{
    Task<long> IncrementFailureAsync(string checkName, CancellationToken ct = default);
    Task ResetAsync(string checkName, CancellationToken ct = default);
    Task<long> GetConsecutiveFailuresAsync(string checkName, CancellationToken ct = default);
}

// RedisHealthAlertTracker.cs
public sealed class RedisHealthAlertTracker(IConnectionMultiplexer redis) : IHealthAlertTracker
{
    private static string FailureKey(string name) => $"hc:failures:{name}";

    public async Task<long> IncrementFailureAsync(string checkName, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var count = await db.StringIncrementAsync(FailureKey(checkName));
        // Set TTL of 2 minutes — auto-clears stale failure counts
        await db.KeyExpireAsync(FailureKey(checkName), TimeSpan.FromMinutes(2));
        return count;
    }

    public Task ResetAsync(string checkName, CancellationToken ct)
        => redis.GetDatabase().KeyDeleteAsync(FailureKey(checkName)).AsTask();

    public async Task<long> GetConsecutiveFailuresAsync(string checkName, CancellationToken ct)
    {
        var val = await redis.GetDatabase().StringGetAsync(FailureKey(checkName));
        return val.HasValue && long.TryParse(val, out var n) ? n : 0;
    }
}
```

### 4. `AlertNotificationService` — multi-channel alert dispatch

```csharp
// PropelIQ.Api/HealthCheck/AlertNotificationService.cs
public sealed class AlertNotificationService(
    IOptionsMonitor<HealthAlertOptions> opts,
    IConnectionMultiplexer redis,
    ILogger<AlertNotificationService> logger) : IAlertNotificationService
{
    private static string DedupeKey(string name) => $"hc:alert:sent:{name}";

    public async Task SendAlertAsync(
        string checkName, string status, string detail, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        // Deduplication: skip if alert sent in last AlertDeduplicationMinutes
        if (await db.KeyExistsAsync(DedupeKey(checkName)))
        {
            logger.LogDebug("Alert suppressed (dedup active) for {CheckName}", checkName);
            return;
        }

        var o = opts.CurrentValue;
        var message = $"[PropelIQ] Service alert: {checkName} is {status}. {detail}";

        if (o.Email.Enabled) await SendEmailAsync(o.Email, checkName, message, ct);
        if (o.Sms.Enabled) await SendSmsAsync(o.Sms, message, ct);
        if (o.PagerDuty.Enabled) await SendPagerDutyAsync(o.PagerDuty, checkName, status, detail, ct);

        // Mark alert as sent — expires after dedup window
        await db.StringSetAsync(
            DedupeKey(checkName), "1",
            TimeSpan.FromMinutes(o.AlertDeduplicationMinutes));
    }

    private async Task SendEmailAsync(EmailAlertChannel cfg, string checkName,
        string message, CancellationToken ct)
    {
        try
        {
            var client = new SendGridClient(cfg.SendGridApiKey);
            var msg = new SendGridMessage
            {
                From = new EmailAddress(cfg.FromAddress),
                Subject = $"{cfg.SubjectPrefix} {checkName} degraded",
                PlainTextContent = message
            };
            msg.AddTo(new EmailAddress(cfg.ToAddress));
            var response = await client.SendEmailAsync(msg, ct);
            if ((int)response.StatusCode >= 400)
                logger.LogWarning("SendGrid alert returned {Status}", response.StatusCode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Email alert dispatch failed for {CheckName}", checkName);
        }
    }

    private async Task SendSmsAsync(SmsAlertChannel cfg, string message, CancellationToken ct)
    {
        try
        {
            TwilioClient.Init(cfg.AccountSid, cfg.AuthToken);
            await MessageResource.CreateAsync(
                to: new Twilio.Types.PhoneNumber(cfg.ToNumber),
                from: new Twilio.Types.PhoneNumber(cfg.FromNumber),
                body: message[..Math.Min(message.Length, 160)]);  // SMS 160-char limit
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SMS alert dispatch failed");
        }
    }

    private async Task SendPagerDutyAsync(PagerDutyAlertChannel cfg,
        string checkName, string status, string detail, CancellationToken ct)
    {
        try
        {
            // PagerDuty Events API v2 — no SDK required
            using var http = new HttpClient();
            var payload = JsonSerializer.Serialize(new
            {
                routing_key = cfg.RoutingKey,
                event_action = "trigger",
                payload = new
                {
                    summary = $"PropelIQ: {checkName} is {status}",
                    severity = cfg.Severity,
                    source = "propeliq-health-check",
                    custom_details = new { checkName, status, detail }
                }
            });
            await http.PostAsync(
                "https://events.pagerduty.com/v2/enqueue",
                new StringContent(payload, Encoding.UTF8, "application/json"),
                ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PagerDuty alert dispatch failed for {CheckName}", checkName);
        }
    }
}
```

> **Security note**: `SendGridApiKey`, `AccountSid`, `AuthToken`, and `RoutingKey` MUST be stored in environment variables or Azure Key Vault — NOT in `appsettings.json`. The `appsettings.json` section contains placeholder empty strings; the actual secrets are injected at deployment (OWASP A02 — cryptographic failures / secret exposure).

### 5. `HealthCheckAlertJob` — Hangfire recurring job

```csharp
// PropelIQ.Api/HealthCheck/HealthCheckAlertJob.cs
[AutomaticRetry(Attempts = 0)]          // Health check jobs must not retry — stale results
[DisableConcurrentExecution(30)]        // Prevent overlap with next 30-second run
public sealed class HealthCheckAlertJob(
    HealthCheckService healthCheckService,
    IHealthAlertTracker tracker,
    IAlertNotificationService notifier,
    IOptions<HealthAlertOptions> opts,
    ILogger<HealthCheckAlertJob> logger)
{
    public async Task ExecuteAsync(CancellationToken ct)
    {
        var report = await healthCheckService.CheckHealthAsync(ct);
        var threshold = opts.Value.ConsecutiveFailureThreshold;

        foreach (var (name, entry) in report.Entries)
        {
            if (entry.Status != HealthStatus.Healthy)
            {
                var failures = await tracker.IncrementFailureAsync(name, ct);
                logger.LogWarning(
                    "Health check {CheckName} is {Status} (consecutive failures: {Count})",
                    name, entry.Status, failures);

                if (failures >= threshold)
                {
                    await notifier.SendAlertAsync(
                        name,
                        entry.Status.ToString(),
                        entry.Description ?? entry.Exception?.Message ?? "No detail",
                        ct);
                }
            }
            else
            {
                await tracker.ResetAsync(name, ct);
            }
        }
    }
}
```

### 6. DI registration and Hangfire recurring job in `Program.cs`

```csharp
// ADD to builder.Services registrations:
builder.Services.AddSingleton<IHealthAlertTracker, RedisHealthAlertTracker>();
builder.Services.AddScoped<IAlertNotificationService, AlertNotificationService>();
builder.Services.AddScoped<HealthCheckAlertJob>();
builder.Services.Configure<HealthAlertOptions>(
    builder.Configuration.GetSection(HealthAlertOptions.SectionName));

// ADD to AddHealthChecks() chain (alongside existing postgresql + redis + azure-openai):
builder.Services.AddHealthChecks()
    // ... existing checks ...
    .AddCheck<HangfireHealthCheck>(
        "hangfire",
        failureStatus: HealthStatus.Degraded,
        tags: ["jobs", "readiness"]);

// ADD after app.UseHangfireDashboard() or wherever Hangfire jobs are configured:
RecurringJob.AddOrUpdate<HealthCheckAlertJob>(
    "health-check-alert",
    job => job.ExecuteAsync(CancellationToken.None),
    "*/30 * * * * *");  // Every 30 seconds (using seconds-capable cron syntax)
```

> **Cron seconds note**: Hangfire supports 6-part cron expressions with seconds when using `Hangfire.Cron` helpers. `"*/30 * * * * *"` runs every 30 seconds. Standard Hangfire `Cron.MinuteInterval(1)` is the minimum for standard cron — for 30-second intervals, use `"*/30 * * * * *"` directly.

### 7. `appsettings.json` — `HealthAlert` section

```json
"HealthAlert": {
  "ConsecutiveFailureThreshold": 2,
  "AlertDeduplicationMinutes": 5,
  "Email": {
    "Enabled": false,
    "ToAddress": "",
    "FromAddress": "",
    "SubjectPrefix": "[PropelIQ ALERT]",
    "SendGridApiKey": ""
  },
  "Sms": {
    "Enabled": false,
    "ToNumber": "",
    "FromNumber": "",
    "AccountSid": "",
    "AuthToken": ""
  },
  "PagerDuty": {
    "Enabled": false,
    "RoutingKey": "",
    "Severity": "critical"
  }
}
```

> Sensitive keys (`SendGridApiKey`, `AccountSid`, `AuthToken`, `RoutingKey`) are empty in `appsettings.json` — populated via environment-specific `appsettings.Production.json` or Azure Key Vault / environment variables at deployment time.

---

## Current Project State

```
server/src/PropelIQ.Api/
├── Program.cs                              ← MODIFY: add HangfireHealthCheck + alert registrations
├── appsettings.json                        ← MODIFY: add "HealthAlert" section
└── HealthCheck/
    ├── HealthCheckResponse.cs              (EXISTS — no change)
    ├── AzureOpenAiHealthCheck.cs           (EXISTS from US_030 — no change)
    ├── HangfireHealthCheck.cs              ← CREATE
    ├── IHealthAlertTracker.cs              ← CREATE
    ├── RedisHealthAlertTracker.cs          ← CREATE
    ├── IAlertNotificationService.cs        ← CREATE
    ├── AlertNotificationService.cs         ← CREATE
    ├── HealthAlertOptions.cs               ← CREATE
    └── HealthCheckAlertJob.cs              ← CREATE
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/PropelIQ.Api/HealthCheck/HangfireHealthCheck.cs` | Queries `hangfire.servers`; 3s timeout; `Degraded` if no server rows |
| CREATE | `server/src/PropelIQ.Api/HealthCheck/IHealthAlertTracker.cs` | `IncrementFailureAsync`, `ResetAsync`, `GetConsecutiveFailuresAsync` |
| CREATE | `server/src/PropelIQ.Api/HealthCheck/RedisHealthAlertTracker.cs` | Redis key `hc:failures:{checkName}`; INCR + KeyExpire(2min); KeyDelete on reset |
| CREATE | `server/src/PropelIQ.Api/HealthCheck/IAlertNotificationService.cs` | `SendAlertAsync(checkName, status, detail, ct)` |
| CREATE | `server/src/PropelIQ.Api/HealthCheck/AlertNotificationService.cs` | SendGrid + Twilio + PagerDuty HTTP POST; Redis dedup key `hc:alert:sent:{name}` (5-min TTL) |
| CREATE | `server/src/PropelIQ.Api/HealthCheck/HealthAlertOptions.cs` | 3 nested channel configs; `ConsecutiveFailureThreshold=2`; `AlertDeduplicationMinutes=5` |
| CREATE | `server/src/PropelIQ.Api/HealthCheck/HealthCheckAlertJob.cs` | Hangfire job; `[AutomaticRetry(Attempts=0)]`; `[DisableConcurrentExecution(30)]`; iterates entries; increments/resets tracker; dispatches on `>= threshold` |
| MODIFY | `server/src/PropelIQ.Api/Program.cs` | `.AddCheck<HangfireHealthCheck>()` + DI registrations for tracker/notifier/options/job + `RecurringJob.AddOrUpdate` every 30s |
| MODIFY | `server/src/PropelIQ.Api/appsettings.json` | Add `"HealthAlert"` section (all secrets empty — injected at runtime) |

---

## External References

- [ASP.NET Core Health Checks — IHealthCheck interface](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks)
- [Hangfire — Recurring Jobs (cron expressions)](https://docs.hangfire.io/en/latest/background-methods/performing-recurrent-background-jobs.html)
- [Twilio C# REST API](https://www.twilio.com/docs/sms/api/message-resource#create-a-message-resource)
- [SendGrid C# SDK — SendEmailAsync](https://docs.sendgrid.com/for-developers/sending-email/v3-csharp-code-example)
- [PagerDuty Events API v2 — Trigger an alert](https://developer.pagerduty.com/api-reference/YXBpOjI3NDgyNjU-pager-duty-v2-events-api)
- [design.md — TR-013 Twilio+SendGrid; TR-019 health checks; NFR-006 99.9% uptime](../.propel/context/docs/design.md)
- [OWASP A02 — Cryptographic Failures (secret exposure in config)](https://owasp.org/Top10/A02_2021-Cryptographic_Failures/)

---

## Build Commands

```powershell
# Add NuGet packages (if not already present from appointment reminder features)
dotnet add server/src/PropelIQ.Api/PropelIQ.Api.csproj package SendGrid --version 9.29.3
dotnet add server/src/PropelIQ.Api/PropelIQ.Api.csproj package Twilio --version 6.17.0

# Build
dotnet build server/PropelIQ.slnx --no-restore

# Test
dotnet test server/PropelIQ.slnx --no-build --filter "Category=US034_HealthChecks"
```

---

## Implementation Validation Strategy

- [ ] `GET /api/health` returns all 5 checks: `api`, `postgresql`, `redis`, `azure-openai`, `hangfire`
- [ ] `GET /api/health` with Hangfire server stopped → `hangfire` entry = `Degraded`; overall status = `Degraded` (not `Unhealthy`); HTTP 200 still returned (degraded mode)
- [ ] `IHealthAlertTracker.IncrementFailureAsync("redis")` twice → `GetConsecutiveFailuresAsync` returns 2
- [ ] `IHealthAlertTracker.ResetAsync("redis")` → `GetConsecutiveFailuresAsync` returns 0
- [ ] Alert deduplication: `SendAlertAsync` called twice for same checkName within 5 minutes → second call is suppressed; `IDatabase.KeyExistsAsync(dedupeKey)` returns `true` after first send
- [ ] `HealthCheckAlertJob`: mock `HealthCheckService` returning 2 consecutive Degraded for `redis` → `IAlertNotificationService.SendAlertAsync` called with `checkName="redis"`, `status="Degraded"`
- [ ] `HealthCheckAlertJob`: mock returning Healthy after Degraded → `IHealthAlertTracker.ResetAsync` called for that check
- [ ] `HealthAlertOptions.Email.Enabled = false` → `SendEmailAsync` NOT called even when alert fires
- [ ] Secrets (`SendGridApiKey`, `AccountSid`, `AuthToken`, `RoutingKey`) NOT hardcoded in `appsettings.json`; values are empty strings in config file

---

## Implementation Checklist

- [ ] CREATE `HangfireHealthCheck` — queries `hangfire.servers` with 3s timeout; `Degraded` if zero rows; `Unhealthy` on exception
- [ ] CREATE `HealthAlertOptions` with 3 nested channel POCOs; `ConsecutiveFailureThreshold=2`; `AlertDeduplicationMinutes=5`; all secrets default to empty strings
- [ ] CREATE `IHealthAlertTracker` + `RedisHealthAlertTracker` — Redis INCR + 2-min TTL; KeyDelete on reset; `GetConsecutiveFailuresAsync` reads current value
- [ ] CREATE `IAlertNotificationService` + `AlertNotificationService` — (a) check Redis dedup key before sending; (b) send enabled channels in try/catch (single channel failure must NOT prevent others); (c) set dedup key after dispatch; (d) secrets read from `IOptionsMonitor.CurrentValue` (hot-reloadable)
- [ ] CREATE `HealthCheckAlertJob` — `[AutomaticRetry(Attempts=0)]` (no retry for health check jobs); iterate all health check entries; increment on non-Healthy; reset on Healthy; dispatch alert when failures >= threshold
- [ ] MODIFY `Program.cs` — `.AddCheck<HangfireHealthCheck>("hangfire", failureStatus: HealthStatus.Degraded, tags: ["jobs"])` + DI registrations + `RecurringJob.AddOrUpdate` every 30 seconds
- [ ] MODIFY `appsettings.json` — add `"HealthAlert"` section; all 3 channels defaulting to `"Enabled": false`; all secrets = empty strings
- [ ] Ensure PagerDuty dispatch uses `HttpClient` (NOT `IHttpClientFactory` — this is fire-and-forget alert dispatch, not a hot-path request); alert dispatch failures are caught and logged, never thrown

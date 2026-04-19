# Task - task_002_be_resilience_bulkhead_rate_limiter

## Requirement Reference

- **User Story**: US_035 — Performance Tuning & Resilience Patterns
- **Story Location**: `.propel/context/tasks/EP-008/us_035/us_035.md`
- **Acceptance Criteria**:
  - AC-4: Given an external dependency (Azure OpenAI, email service) fails, when detected, then bulkhead isolation (dedicated concurrency limits per dependency) and graceful degradation are applied per TR-021 and TR-023.
  - AC-1: Given 200 concurrent users, p95 < 500ms for CRUD operations (rate limiting prevents burst traffic from degrading p95).
  - AC-5: Linear degradation under ramp from 50→200 users; no dropped requests.
- **Edge Cases**:
  - Redis cache unavailable: `ICacheService` already falls through on `RedisConnectionException` — this task adds the HTTP-level resilience for external services not Redis.
  - Memory pressure under sustained load: Rate limiter bounds active request count; `AddDbContextPool` (task_001) bounds `DbContext` instances; no additional GC tuning required for 200-user scale.

> **NFR Tag Discrepancy Notice**:
> - US_035 AC-4 cites `TR-021` — design.md TR-021 = "database connection pooling min=10, max=100". Bulkhead isolation for external services is not covered by TR-021. The correct reference is NFR-016 (circuit breaker patterns) and TR-023 (retry for external APIs). This task implements per the described behavior.
> - US_035 AC-4 cites `TR-023` — design.md TR-023 = "retry with exponential backoff for external API calls (calendars, SMS, email)". This task implements exactly this.

---

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Minimal — rate-limited requests return HTTP 429 with `Retry-After: 1` header; FE should handle gracefully |
| **Figma URL** | N/A |
| **Wireframe Status** | N/A |
| **Screen Spec** | N/A |

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Backend | .NET 8 ASP.NET Core | 8.0 LTS |
| Resilience | Polly v8 (`Polly.Core` + `Microsoft.Extensions.Resilience`) | 8.x |
| HTTP Clients | `IHttpClientFactory` + `Microsoft.Extensions.Http.Resilience` | Built-in .NET 8 |
| Rate Limiting | `System.Threading.RateLimiting` + `Microsoft.AspNetCore.RateLimiting` | Built-in .NET 8 |
| Config | `IOptionsMonitor<ExternalServiceResilienceOptions>` | Built-in |

> **No new NuGet packages required**: `Microsoft.Extensions.Http.Resilience` is part of `Microsoft.Extensions.Resilience` already referenced from US_031. `System.Threading.RateLimiting` and `Microsoft.AspNetCore.RateLimiting` are built-in .NET 8 packages.

---

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No — this task adds bulkhead for non-AI external services. The AI gateway ("azure-openai" Polly pipeline) was established in US_031 and is NOT modified here. |

---

## Task Overview

This task has three parts:

**1. Named HTTP Clients with Polly Retry (TR-023/AC-4)**:
Register named `IHttpClientFactory` typed clients for external HTTP dependencies — `sendgrid-http`, `pagerduty-http`. Each uses `AddResilienceHandler` (from `Microsoft.Extensions.Http.Resilience`) to attach a Polly v8 pipeline with: retry 3× at 1s/2s/4s exponential backoff, transient HTTP error handling (`HttpStatusCode >= 500`, `429`, network failure).

Twilio's C# SDK manages its own HTTP internally — not available via `IHttpClientFactory`. Twilio resilience is handled via `IExternalServiceBulkhead` (SemaphoreSlim pattern) described below.

**2. Bulkhead Isolation via `IExternalServiceBulkhead` (AC-4)**:
A `SemaphoreSlim`-backed bulkhead service that limits concurrent calls to each named external dependency. This prevents a slow dependency (e.g., Azure OpenAI timeout storm) from consuming all .NET thread pool threads. Named bulkhead slots:
- `"azure-openai"` — max 20 concurrent (AI is already circuit-broken by Polly in US_031; bulkhead adds concurrency cap)
- `"email"` (SendGrid) — max 10 concurrent
- `"sms"` (Twilio) — max 5 concurrent  
- `"pagerduty"` — max 3 concurrent

`IExternalServiceBulkhead` wraps any async operation with `SemaphoreSlim.WaitAsync(timeout: 5s)`. On timeout → `BulkheadRejectedException` → caller logs and returns graceful fallback.

**3. ASP.NET Core Rate Limiting Middleware (AC-1/AC-5)**:
Two rate limit policies:
- `"api-global"` — Fixed window: 100 req/10s per user (JWT `sub` claim); allows burst within window while protecting p95 at 200 concurrent users
- `"auth"` — Token bucket: 5 tokens, 60s replenish per IP; for login endpoint (existing separate rate limiter in JWT auth — this replaces the manual IP-based rate limiter if present, or coexists as a policy)

HTTP 429 response includes `Retry-After` header. Rate limiting applies to all routes except `/api/health` and `/swagger`.

---

## Dependent Tasks

- **US_031 `"azure-openai"` Polly pipeline**: Already exists via `AddResiliencePipeline("azure-openai", ...)`. This task does NOT modify it — it adds the bulkhead wrapper on top.
- **US_034 `AlertNotificationService`**: Currently instantiates `HttpClient` directly for PagerDuty (`using var http = new HttpClient()`). This task upgrades the PagerDuty call to use `IHttpClientFactory` named client `"pagerduty-http"` — the only file-level modification to US_034 code.
- **task_001 (this US)**: Independent — no ordering dependency.

---

## Implementation Plan

### 1. `ExternalServiceResilienceOptions` — configuration POCO

```csharp
// PropelIQ.Api/Infrastructure/Resilience/ExternalServiceResilienceOptions.cs
public sealed class ExternalServiceResilienceOptions
{
    public const string SectionName = "ExternalServiceResilience";

    // Retry settings for HTTP-based external services
    public int RetryCount { get; set; } = 3;
    public double RetryBaseDelaySeconds { get; set; } = 1.0;     // 1s, 2s, 4s

    // Bulkhead (concurrency limiter) settings per dependency
    public int AzureOpenAiMaxConcurrent { get; set; } = 20;
    public int EmailMaxConcurrent       { get; set; } = 10;
    public int SmsMaxConcurrent         { get; set; } = 5;
    public int PagerDutyMaxConcurrent   { get; set; } = 3;
    public int BulkheadTimeoutSeconds   { get; set; } = 5;

    // Rate limiting
    public int GlobalWindowRequestLimit { get; set; } = 100;      // per user per window
    public int GlobalWindowSeconds      { get; set; } = 10;
    public int AuthBucketTokenLimit     { get; set; } = 5;
    public int AuthBucketReplenishSeconds { get; set; } = 60;
}
```

### 2. `IExternalServiceBulkhead` + `ExternalServiceBulkhead`

```csharp
// PropelIQ.Api/Infrastructure/Resilience/IExternalServiceBulkhead.cs
public interface IExternalServiceBulkhead
{
    /// <summary>
    /// Executes <paramref name="operation"/> with bulkhead protection for the named service.
    /// Throws <see cref="BulkheadRejectedException"/> if the semaphore timeout elapses.
    /// </summary>
    Task<T> ExecuteAsync<T>(
        string serviceName, Func<CancellationToken, Task<T>> operation,
        CancellationToken ct = default);

    Task ExecuteAsync(
        string serviceName, Func<CancellationToken, Task> operation,
        CancellationToken ct = default);
}

// PropelIQ.Api/Infrastructure/Resilience/BulkheadRejectedException.cs
public sealed class BulkheadRejectedException(string serviceName)
    : Exception($"Bulkhead rejected call to '{serviceName}': concurrency limit reached")
{
    public string ServiceName { get; } = serviceName;
}
```

```csharp
// PropelIQ.Api/Infrastructure/Resilience/ExternalServiceBulkhead.cs
public sealed class ExternalServiceBulkhead(
    IOptions<ExternalServiceResilienceOptions> opts,
    ILogger<ExternalServiceBulkhead> logger) : IExternalServiceBulkhead, IDisposable
{
    private readonly Dictionary<string, SemaphoreSlim> _semaphores = BuildSemaphores(opts.Value);
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(opts.Value.BulkheadTimeoutSeconds);

    private static Dictionary<string, SemaphoreSlim> BuildSemaphores(
        ExternalServiceResilienceOptions o) => new(StringComparer.OrdinalIgnoreCase)
    {
        ["azure-openai"] = new SemaphoreSlim(o.AzureOpenAiMaxConcurrent, o.AzureOpenAiMaxConcurrent),
        ["email"]        = new SemaphoreSlim(o.EmailMaxConcurrent,        o.EmailMaxConcurrent),
        ["sms"]          = new SemaphoreSlim(o.SmsMaxConcurrent,          o.SmsMaxConcurrent),
        ["pagerduty"]    = new SemaphoreSlim(o.PagerDutyMaxConcurrent,    o.PagerDutyMaxConcurrent),
    };

    public async Task<T> ExecuteAsync<T>(
        string serviceName, Func<CancellationToken, Task<T>> operation, CancellationToken ct)
    {
        if (!_semaphores.TryGetValue(serviceName, out var semaphore))
        {
            // Unknown service name — allow through without throttling (fail-open)
            logger.LogWarning("Bulkhead: unknown service '{Service}', bypassing", serviceName);
            return await operation(ct);
        }

        var acquired = await semaphore.WaitAsync(_timeout, ct);
        if (!acquired)
        {
            logger.LogWarning(
                "Bulkhead: concurrency limit reached for '{Service}' (timeout {Timeout}s)",
                serviceName, _timeout.TotalSeconds);
            throw new BulkheadRejectedException(serviceName);
        }

        try
        {
            return await operation(ct);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task ExecuteAsync(
        string serviceName, Func<CancellationToken, Task> operation, CancellationToken ct)
    {
        await ExecuteAsync<bool>(serviceName, async innerCt =>
        {
            await operation(innerCt);
            return true;
        }, ct);
    }

    public void Dispose()
    {
        foreach (var s in _semaphores.Values)
            s.Dispose();
    }
}
```

### 3. Named HTTP Clients with Polly retry

```csharp
// In Program.cs — builder phase:

// SendGrid HTTP client with Polly retry (exponential backoff, TR-023)
builder.Services.AddHttpClient("sendgrid-http", client =>
{
    client.BaseAddress = new Uri("https://api.sendgrid.com");
    client.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue("application/json"));
    client.Timeout = TimeSpan.FromSeconds(10);
})
.AddResilienceHandler("sendgrid-retry", pipeline =>
{
    pipeline.AddRetry(new HttpRetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        Delay = TimeSpan.FromSeconds(1),
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true,
        ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
            .Handle<HttpRequestException>()
            .HandleResult(r =>
                r.StatusCode == HttpStatusCode.TooManyRequests ||
                (int)r.StatusCode >= 500)
    });
});

// PagerDuty HTTP client with Polly retry (exponential backoff, TR-023)
builder.Services.AddHttpClient("pagerduty-http", client =>
{
    client.BaseAddress = new Uri("https://events.pagerduty.com");
    client.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue("application/json"));
    client.Timeout = TimeSpan.FromSeconds(8);
})
.AddResilienceHandler("pagerduty-retry", pipeline =>
{
    pipeline.AddRetry(new HttpRetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        Delay = TimeSpan.FromSeconds(1),
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true,
        ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
            .Handle<HttpRequestException>()
            .HandleResult(r =>
                r.StatusCode == HttpStatusCode.TooManyRequests ||
                (int)r.StatusCode >= 500)
    });
});
```

### 4. ASP.NET Core Rate Limiting Middleware

```csharp
// In Program.cs — builder phase:
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Global per-user fixed window: 100 requests per 10 seconds
    // Allows normal browsing while protecting against bot floods
    options.AddFixedWindowLimiter("api-global", o =>
    {
        o.PermitLimit = resilienceOpts.GlobalWindowRequestLimit;      // 100
        o.Window      = TimeSpan.FromSeconds(resilienceOpts.GlobalWindowSeconds);  // 10s
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        o.QueueLimit   = 10;  // Queue up to 10 additional requests (absorbs brief bursts)
    });

    // Auth endpoint token bucket: 5 tokens, refill 1 per 12s (5 per minute) per IP
    options.AddTokenBucketLimiter("auth", o =>
    {
        o.TokenLimit          = resilienceOpts.AuthBucketTokenLimit;      // 5
        o.ReplenishmentPeriod = TimeSpan.FromSeconds(
            resilienceOpts.AuthBucketReplenishSeconds / (double)resilienceOpts.AuthBucketTokenLimit);
        o.TokensPerPeriod     = 1;
        o.QueueLimit          = 0;  // No queuing for auth — reject immediately
    });

    // Attach Retry-After header on rejection
    options.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.Headers.RetryAfter = "1";
        await ctx.HttpContext.Response.WriteAsJsonAsync(
            new { status = 429, title = "Rate limit exceeded", retryAfterSeconds = 1 }, ct);
    };
});
```

```csharp
// In Program.cs — app (middleware) phase, BEFORE UseAuthorization:
app.UseRateLimiter();
```

```csharp
// Apply rate limit policies to controllers via [EnableRateLimiting] attribute
// or globally via MapControllers().RequireRateLimiting("api-global")

// Option A — apply globally (recommended for this codebase):
app.MapControllers().RequireRateLimiting("api-global");

// Exempt health check and swagger from rate limiting
app.MapHealthChecks("/api/health")
    .DisableRateLimiting();
```

### 5. `AlertNotificationService` — upgrade PagerDuty to `IHttpClientFactory`

The only US_034 code change in this task: replace the disposable `new HttpClient()` in `AlertNotificationService.SendPagerDutyAsync` with `IHttpClientFactory`:

```csharp
// BEFORE (US_034 task_001 — direct HttpClient instantiation, no retry):
using var http = new HttpClient();
await http.PostAsync("https://events.pagerduty.com/v2/enqueue", content, ct);

// AFTER (this task — IHttpClientFactory named client with Polly retry):
var http = _httpClientFactory.CreateClient("pagerduty-http");
// BaseAddress already set on named client; only specify the path
await http.PostAsync("/v2/enqueue", content, ct);
```

`AlertNotificationService` gains a new constructor parameter: `IHttpClientFactory httpClientFactory`. Store as `_httpClientFactory`.

### 6. Registrations in `Program.cs`

```csharp
// Bulkhead singleton
builder.Services.AddSingleton<IExternalServiceBulkhead, ExternalServiceBulkhead>();
builder.Services.Configure<ExternalServiceResilienceOptions>(
    builder.Configuration.GetSection(ExternalServiceResilienceOptions.SectionName));
```

### 7. `appsettings.json` additions

```json
"ExternalServiceResilience": {
  "RetryCount": 3,
  "RetryBaseDelaySeconds": 1.0,
  "AzureOpenAiMaxConcurrent": 20,
  "EmailMaxConcurrent": 10,
  "SmsMaxConcurrent": 5,
  "PagerDutyMaxConcurrent": 3,
  "BulkheadTimeoutSeconds": 5,
  "GlobalWindowRequestLimit": 100,
  "GlobalWindowSeconds": 10,
  "AuthBucketTokenLimit": 5,
  "AuthBucketReplenishSeconds": 60
}
```

---

## Current Project State

```
server/src/PropelIQ.Api/
├── Program.cs                                          ← MODIFY: AddHttpClient + AddRateLimiter + DI
├── appsettings.json                                    ← MODIFY: add "ExternalServiceResilience"
├── HealthCheck/
│   └── AlertNotificationService.cs                    ← MODIFY (US_034): inject IHttpClientFactory; use named "pagerduty-http" client
└── Infrastructure/
    └── Resilience/                                     ← CREATE folder
        ├── ExternalServiceResilienceOptions.cs         ← CREATE
        ├── IExternalServiceBulkhead.cs                 ← CREATE
        ├── BulkheadRejectedException.cs                ← CREATE
        └── ExternalServiceBulkhead.cs                  ← CREATE
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/PropelIQ.Api/Infrastructure/Resilience/ExternalServiceResilienceOptions.cs` | 10 config properties; `SectionName = "ExternalServiceResilience"` |
| CREATE | `server/src/PropelIQ.Api/Infrastructure/Resilience/IExternalServiceBulkhead.cs` | Generic + non-generic `ExecuteAsync` overloads |
| CREATE | `server/src/PropelIQ.Api/Infrastructure/Resilience/BulkheadRejectedException.cs` | `sealed class` with `ServiceName` property |
| CREATE | `server/src/PropelIQ.Api/Infrastructure/Resilience/ExternalServiceBulkhead.cs` | `Dictionary<string, SemaphoreSlim>` keyed by service name; `WaitAsync(_timeout)` → throw on false; `Release()` in finally; implements `IDisposable` |
| MODIFY | `server/src/PropelIQ.Api/Program.cs` | `AddHttpClient("sendgrid-http", ...)` + `AddHttpClient("pagerduty-http", ...)` both with `AddResilienceHandler`; `AddRateLimiter` with `api-global` (fixed window) + `auth` (token bucket); `app.UseRateLimiter()`; `MapControllers().RequireRateLimiting("api-global")`; `MapHealthChecks(...)DisableRateLimiting()`; register `IExternalServiceBulkhead` singleton + `Configure<ExternalServiceResilienceOptions>` |
| MODIFY | `server/src/PropelIQ.Api/HealthCheck/AlertNotificationService.cs` | Add `IHttpClientFactory` constructor param; use `_httpClientFactory.CreateClient("pagerduty-http")` in `SendPagerDutyAsync`; remove `using var http = new HttpClient()` |
| MODIFY | `server/src/PropelIQ.Api/appsettings.json` | Add `"ExternalServiceResilience"` section |

---

## Security Notes (OWASP)

- **A04 — Insecure Design**: Rate limiting (`api-global`) prevents brute force enumeration attacks. Auth token bucket (5 req/60s per IP) protects login endpoint — consistent with existing IP-based rate limiter in JWT auth from US_025.
- **A07 — Identification and Authentication Failures**: Token bucket on `"auth"` policy applied to `POST /api/v1/auth/login` — enforce via `[EnableRateLimiting("auth")]` on the auth controller action in addition to global policy.
- **A05 — Security Misconfiguration**: `IDisposable` pattern on `ExternalServiceBulkhead` ensures `SemaphoreSlim` instances are disposed when the app shuts down. Registered as singleton — disposed at app shutdown automatically by DI container.
- Bulkhead fail-open for unknown service names: logs warning but allows through rather than blocking unknown services that may be added without updating the bulkhead config.

---

## External References

- [ASP.NET Core Rate Limiting — `AddRateLimiter`](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit)
- [Microsoft.Extensions.Http.Resilience — `AddResilienceHandler`](https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience)
- [Polly v8 — Retry strategy (`HttpRetryStrategyOptions`)](https://www.pollydocs.org/strategies/retry.html)
- [design.md — TR-023 (retry exponential backoff external APIs); NFR-016 (circuit breaker); NFR-008 (concurrent users)](../.propel/context/docs/design.md)
- [OWASP A04 — Insecure Design (rate limiting protects enumeration)](https://owasp.org/Top10/A04_2021-Insecure_Design/)

---

## Build Commands

```powershell
# No new NuGet packages required — all packages are built-in .NET 8 or already referenced
dotnet build server/PropelIQ.slnx --no-restore

# Test
dotnet test server/PropelIQ.slnx --no-build --filter "Category=US035_Resilience"
```

---

## Implementation Validation Strategy

- [ ] `IExternalServiceBulkhead.ExecuteAsync("email", ...)` with all 10 permits held → `BulkheadRejectedException` thrown after 5s timeout
- [ ] `IExternalServiceBulkhead.ExecuteAsync("email", ...)` with 9 of 10 held → executes successfully
- [ ] `IExternalServiceBulkhead.ExecuteAsync("unknown-service", ...)` → logs warning; executes operation normally (fail-open)
- [ ] `"sendgrid-http"` named client: mock SendGrid returning HTTP 503 → Polly retries 3× with exponential delays → final exception after all retries exhausted
- [ ] `"pagerduty-http"` named client: mock PagerDuty returning HTTP 429 → retried; response with `Retry-After` header → `UseJitter=true` delays respected
- [ ] Rate limiting `"api-global"`: 101 requests in 10s from same user → 101st returns HTTP 429 with `Retry-After: 1`
- [ ] `GET /api/health` bypasses rate limiting → never returns HTTP 429
- [ ] `AlertNotificationService.SendPagerDutyAsync` uses `IHttpClientFactory.CreateClient("pagerduty-http")` NOT `new HttpClient()`
- [ ] `ExternalServiceBulkhead` implements `IDisposable` — `Dispose()` disposes all `SemaphoreSlim` instances

---

## Implementation Checklist

- [ ] CREATE `ExternalServiceResilienceOptions` — 10 properties with documented defaults; `SectionName` constant
- [ ] CREATE `IExternalServiceBulkhead` + `BulkheadRejectedException` — exception carries `ServiceName` property for structured logging
- [ ] CREATE `ExternalServiceBulkhead` — (a) builds `Dictionary<string, SemaphoreSlim>` at constructor time from options; (b) `WaitAsync(_timeout, ct)` — respect cancellation token; (c) always `Release()` in `finally`; (d) fail-open for unknown names; (e) implements `IDisposable` — disposes all semaphores; registered as `AddSingleton` so DI container disposes on shutdown
- [ ] MODIFY `Program.cs` — (a) `AddHttpClient("sendgrid-http")` + `AddResilienceHandler("sendgrid-retry")` with 3× exponential retry; (b) `AddHttpClient("pagerduty-http")` + `AddResilienceHandler("pagerduty-retry")` same; (c) `AddRateLimiter` with two policies; (d) `app.UseRateLimiter()` BEFORE `UseAuthorization()`; (e) `MapControllers().RequireRateLimiting("api-global")`; (f) `MapHealthChecks(...).DisableRateLimiting()`; (g) singleton bulkhead + options binding
- [ ] MODIFY `AlertNotificationService` (US_034) — add `IHttpClientFactory` constructor parameter; replace `using var http = new HttpClient()` with `_httpClientFactory.CreateClient("pagerduty-http")`; remove hardcoded base URL (set on named client); DO NOT change email or SMS dispatch logic
- [ ] MODIFY `appsettings.json` — add `"ExternalServiceResilience"` section with all defaults

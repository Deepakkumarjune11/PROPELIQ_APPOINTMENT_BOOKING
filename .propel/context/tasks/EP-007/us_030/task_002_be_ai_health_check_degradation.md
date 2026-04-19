# Task - task_002_be_ai_health_check_degradation

## Requirement Reference

- **User Story**: US_030 — Azure OpenAI Client & Gateway Middleware
- **Story Location**: `.propel/context/tasks/EP-007/us_030/us_030.md`
- **Acceptance Criteria**:
  - AC-5: When the Azure OpenAI health check fails, the gateway marks the service as degraded and AI features gracefully degrade (return cached responses or "AI temporarily unavailable" messages) per AIR-S03.
- **Edge Cases**:
  - Circuit breaker opens during a critical clinical workflow: `AiDegradationHandler` checks Redis for the last successful response for the same `featureContext`; if found, returns it with `IsTruncated = false` and a `Warning` log noting "results may be outdated"; if no cache, returns the static degraded message.
  - Health check endpoint itself times out: `AzureOpenAiHealthCheck` wraps the probe in a 2-second cancellation token; timeout → `HealthCheckResult.Degraded("probe timeout")`.

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
| Health Checks | `Microsoft.Extensions.Diagnostics.HealthChecks` | 8.0 LTS (built-in) |
| AI/ML - SDK | `Azure.AI.OpenAI` NuGet | 1.0.x |
| AI/ML - Identity | `Azure.Identity` NuGet | 1.12.x |
| Caching | Upstash Redis (`IDistributedCache`) | Cloud |
| Logging | Serilog | 3.x |
| Testing - Unit | xUnit + Moq | 2.x / 4.x |

> `Microsoft.Extensions.Diagnostics.HealthChecks` is a built-in .NET 8 package — no additional NuGet required. ASP.NET Core health check middleware (`UseHealthChecks` / `MapHealthChecks`) is available without additional packages. The `IDistributedCache` (Upstash Redis) is already registered from existing `Program.cs` setup.

---

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | Yes |
| **AIR Requirements** | AIR-S03 (degradation logging), AIR-O02 (pre-requisite awareness — full circuit breaker in US_031) |
| **AI Pattern** | AI Gateway — health check + graceful degradation |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | `appsettings.json` → `AzureOpenAi:DegradationMessage`, `AzureOpenAi:ResponseCacheTtlSeconds` |
| **Model Provider** | Azure OpenAI GPT-4 Turbo (HIPAA BAA — TR-006, NFR-013) |

### CRITICAL: AI Implementation Requirements

- **MUST** use a **2-second probe timeout** in `AzureOpenAiHealthCheck` to avoid blocking the health endpoint (TR-019 uptime requirement — health check cannot be a single point of failure).
- **MUST** probe with the **cheapest possible call** — a 1-token embedding generation (not a chat completion) to minimize cost and latency during health polling.
- **MUST** cache the last successful `GatewayResponse` per `featureContext` key in Redis with a configurable TTL (default 24h) so degraded mode can serve stale-but-meaningful data.
- **MUST** tag the degraded cached response with `source: "cached"` in the Serilog log so operators can distinguish stale results from live AI results. Never silently serve stale AI data without logging.
- **MUST NOT** block calling code beyond the degradation response path — if degradation handler itself throws, fall through to a hardcoded `GatewayResponse` with the static degraded message rather than propagating the exception.
- **MUST** record the `IAiAvailabilityState.MarkDegraded(reason)` call whenever health check transitions to `Unhealthy` or `Degraded` — US_031 will replace the simple flag with a full Polly circuit breaker; this task establishes the interface contract that US_031 will implement.

---

## Mobile References (Mobile Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **Mobile Impact** | No |
| **Platform Target** | N/A |
| **Min OS Version** | N/A |
| **Mobile Framework** | N/A |

---

## Task Overview

Implement the health check + graceful degradation sub-system for the AI gateway layer, establishing the availability state contract that US_031 (Circuit Breaker) will build upon.

**Architecture of the degradation pipeline:**

```
HTTP Request → AzureOpenAiGateway.ChatCompletionAsync
                         ↓
             IAiAvailabilityState.IsAvailable?
               ├── Yes → Normal GPT-4 call (task_001)
               └── No  → IAiDegradationHandler.GetDegradedResponseAsync(featureContext)
                                   ↓
                        Redis: get last cached GatewayResponse for featureContext
                          ├── Cache hit  → return stale response (IsTruncated=false, logged "cached")
                          └── Cache miss → return static GatewayResponse (Content = degraded message)
```

**Health check probe flow:**
```
GET /health/ai
    → AzureOpenAiHealthCheck.CheckHealthAsync()
        → probe: 1-token embedding via AzureOpenAIClient (2s timeout)
            ├── Latency < 1s, status 2xx → HealthCheckResult.Healthy
            ├── Latency 1–2s or non-critical warning → HealthCheckResult.Degraded
            └── Exception / timeout / non-2xx → HealthCheckResult.Unhealthy
                → IAiAvailabilityState.MarkDegraded(reason)
```

**Response cache (Redis-backed):**
- After every successful `ChatCompletionAsync` call, `AzureOpenAiGateway` writes the response to Redis keyed by `ai_cache:{featureContext}`.
- TTL: configurable via `AzureOpenAi:ResponseCacheTtlSeconds` (default 86400 = 24h).
- This write is fire-and-forget (`_ = CacheResponseAsync(...)`) to avoid adding latency to the happy path.

**US_031 interface seam:**
- `IAiAvailabilityState` is deliberately minimal in this task — just `bool IsAvailable` + `MarkDegraded(string reason)` + `MarkRecovered()`. US_031 replaces the `InMemoryAvailabilityState` implementation with a Polly circuit breaker–driven implementation without changing the interface.

---

## Dependent Tasks

- **task_001_be_azure_openai_gateway_hardening.md** (US_030) — `AzureOpenAiOptions`, `GatewayResponse`, `AzureOpenAiGateway`, `IAiGateway` with new `ChatCompletionAsync` must exist before this task modifies `AzureOpenAiGateway` to inject `IAiAvailabilityState` and `IAiDegradationHandler`.
- **US_031** (next) — `IAiAvailabilityState` is the seam US_031 replaces. Do NOT hard-couple `AzureOpenAiGateway` to `InMemoryAvailabilityState`; always inject via interface.

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Availability/IAiAvailabilityState.cs` | Interface: `bool IsAvailable { get; }`, `void MarkDegraded(string reason)`, `void MarkRecovered()` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Availability/InMemoryAvailabilityState.cs` | Simple in-memory implementation (volatile bool); singleton; replaced by US_031 circuit breaker |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Availability/IAiDegradationHandler.cs` | Interface: `Task<GatewayResponse> GetDegradedResponseAsync(string featureContext, CancellationToken ct)` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Availability/AiDegradationHandler.cs` | Redis cache lookup for last successful response; fallback to static degraded message; logs `source: "cached"` |
| CREATE | `server/src/PropelIQ.Api/HealthCheck/AzureOpenAiHealthCheck.cs` | `IHealthCheck` implementation: 1-token embedding probe with 2s timeout; calls `IAiAvailabilityState.MarkDegraded/MarkRecovered` |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/AzureOpenAiGateway.cs` | Inject `IAiAvailabilityState`, `IAiDegradationHandler`; add degradation guard at top of `ChatCompletionAsync`; add fire-and-forget response cache write on success |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Presentation/ServiceCollectionExtensions.cs` | Register `IAiAvailabilityState → InMemoryAvailabilityState` (singleton), `IAiDegradationHandler → AiDegradationHandler` (scoped) |
| MODIFY | `server/src/PropelIQ.Api/Program.cs` | `services.AddHealthChecks().AddCheck<AzureOpenAiHealthCheck>("azure-openai", tags: new[]{"ai","live"})` + `app.MapHealthChecks("/health/ai", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ai") })` |
| MODIFY | `server/src/PropelIQ.Api/appsettings.json` | Add to existing `"AzureOpenAi"` section: `"DegradationMessage"` and `"ResponseCacheTtlSeconds"` (86400) |

---

## Implementation Plan

### 1. `IAiAvailabilityState` — availability gate interface

```csharp
// ClinicalIntelligence.Application/AI/Availability/IAiAvailabilityState.cs
namespace PropelIQ.ClinicalIntelligence.Application.AI.Availability;

/// <summary>
/// Tracks AI service availability. Consumed by AzureOpenAiGateway to determine
/// whether to attempt live calls or route to degradation handler.
/// US_031 replaces InMemoryAvailabilityState with a Polly circuit-breaker implementation.
/// </summary>
public interface IAiAvailabilityState
{
    bool IsAvailable { get; }
    void MarkDegraded(string reason);
    void MarkRecovered();
}
```

### 2. `InMemoryAvailabilityState` — simple flag (replaced by US_031)

```csharp
// ClinicalIntelligence.Application/AI/Availability/InMemoryAvailabilityState.cs
public sealed class InMemoryAvailabilityState : IAiAvailabilityState
{
    // volatile ensures memory visibility across threads without locking overhead.
    // InMemoryAvailabilityState is registered as singleton — single instance per process.
    private volatile bool _isAvailable = true;

    public bool IsAvailable => _isAvailable;

    public void MarkDegraded(string reason)
    {
        _isAvailable = false;
        // US_031 will add: circuit breaker state transition + Polly state machine here
    }

    public void MarkRecovered()
    {
        _isAvailable = true;
    }
}
```

### 3. `AiDegradationHandler` — cache-first fallback

```csharp
// ClinicalIntelligence.Application/AI/Availability/AiDegradationHandler.cs
public sealed class AiDegradationHandler(
    IDistributedCache cache,
    IOptions<AzureOpenAiOptions> options,
    ILogger<AiDegradationHandler> logger) : IAiDegradationHandler
{
    private const string CacheKeyPrefix = "ai_cache:";

    public async Task<GatewayResponse> GetDegradedResponseAsync(
        string featureContext, CancellationToken ct = default)
    {
        try
        {
            var cacheKey = $"{CacheKeyPrefix}{featureContext}";
            var cached = await cache.GetStringAsync(cacheKey, ct);

            if (cached is not null)
            {
                // Deserialize last successful response (stored as JSON)
                var staleResponse = JsonSerializer.Deserialize<GatewayResponse>(cached)!;

                logger.LogWarning(
                    "AI degraded mode: returning cached response | featureContext={FeatureContext} " +
                    "source=cached — results may be outdated.",
                    featureContext);

                // Return the stale response as-is; callers can check logs for degraded status
                return staleResponse;
            }
        }
        catch (Exception cacheEx)
        {
            // Cache failure must NOT propagate — log and fall through to static message
            logger.LogError(cacheEx,
                "AI degradation cache read failed for featureContext={FeatureContext}. Returning static message.",
                featureContext);
        }

        // Final fallback: static degraded message (no GPT content)
        var msg = options.Value.DegradationMessage.Length > 0
            ? options.Value.DegradationMessage
            : "AI assistance is temporarily unavailable. Please proceed manually or try again shortly.";

        logger.LogWarning(
            "AI degraded mode: returning static degradation message | featureContext={FeatureContext}",
            featureContext);

        return new GatewayResponse(msg, 0, 0, IsTruncated: false, featureContext);
    }
}
```

### 4. `AzureOpenAiHealthCheck` — probe with 2-second timeout

```csharp
// PropelIQ.Api/HealthCheck/AzureOpenAiHealthCheck.cs
public sealed class AzureOpenAiHealthCheck(
    AzureOpenAIClient aiClient,
    IOptions<AzureOpenAiOptions> options,
    IAiAvailabilityState availabilityState,
    ILogger<AzureOpenAiHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        // 2-second hard timeout — health endpoint must respond quickly (TR-019)
        using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        probeCts.CancelAfter(TimeSpan.FromSeconds(2));

        var sw = Stopwatch.StartNew();

        try
        {
            // Cheapest possible probe: 1-token embedding to test connectivity + auth
            var embClient = aiClient.GetEmbeddingClient(options.Value.EmbeddingDeploymentName);
            await embClient.GenerateEmbeddingAsync("health", probeCts.Token);
            sw.Stop();

            if (sw.ElapsedMilliseconds > 1_000)
            {
                // Elevated latency — mark degraded but do NOT mark availability state as failed
                logger.LogWarning(
                    "AzureOpenAI health check: elevated latency {LatencyMs}ms (threshold 1000ms). Marking Degraded.",
                    sw.ElapsedMilliseconds);
                return HealthCheckResult.Degraded($"Elevated latency: {sw.ElapsedMilliseconds}ms");
            }

            availabilityState.MarkRecovered();
            return HealthCheckResult.Healthy($"Latency: {sw.ElapsedMilliseconds}ms");
        }
        catch (OperationCanceledException) when (probeCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            var reason = $"Probe timed out after 2000ms";
            availabilityState.MarkDegraded(reason);
            logger.LogError("AzureOpenAI health check: probe timeout after {LatencyMs}ms", sw.ElapsedMilliseconds);
            return HealthCheckResult.Unhealthy(reason);
        }
        catch (Exception ex)
        {
            sw.Stop();
            var reason = $"Probe failed: {ex.GetType().Name}";
            availabilityState.MarkDegraded(reason);
            logger.LogError(ex, "AzureOpenAI health check: probe failed | latencyMs={LatencyMs}", sw.ElapsedMilliseconds);
            return HealthCheckResult.Unhealthy(reason, ex);
        }
    }
}
```

> **Security note (OWASP A05 — Security Misconfiguration):** The health endpoint at `/health/ai` MUST NOT expose internal system details publicly. Configure `ResponseWriter` to omit the exception message from the JSON output in production. In `Program.cs`, set `HealthCheckOptions.ResponseWriter` to a minimal writer that returns `{"status":"Unhealthy"}` without the exception details — reserve verbose output for internal monitoring only.

### 5. `AzureOpenAiGateway` modifications — degradation guard + response cache write

```csharp
// Add to existing AzureOpenAiGateway constructor parameter list:
private readonly IAiAvailabilityState _availabilityState;
private readonly IAiDegradationHandler _degradationHandler;
private readonly IDistributedCache _cache;
private readonly AzureOpenAiOptions _opts;

// Add at the START of ChatCompletionAsync, before sanitizer call:
if (!_availabilityState.IsAvailable)
{
    logger.LogWarning(
        "AzureOpenAI unavailable — routing to degradation handler | featureContext={FeatureContext} correlationId={CorrelationId}",
        featureContext, correlationId);
    return await _degradationHandler.GetDegradedResponseAsync(featureContext, ct);
}

// Add at the END of ChatCompletionAsync, after building gatewayResponse (fire-and-forget):
_ = CacheSuccessfulResponseAsync(featureContext, gatewayResponse);

// Private fire-and-forget helper (catches all exceptions internally):
private async Task CacheSuccessfulResponseAsync(string featureContext, GatewayResponse response)
{
    try
    {
        var key = $"ai_cache:{featureContext}";
        var json = JsonSerializer.Serialize(response);
        var ttl = TimeSpan.FromSeconds(_opts.ResponseCacheTtlSeconds);
        await _cache.SetStringAsync(key, json, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl
        });
    }
    catch (Exception ex)
    {
        // Non-critical — never let cache write failures affect the main request path
        logger.LogWarning(ex,
            "Failed to cache AI response for featureContext={FeatureContext}. Ignoring.", featureContext);
    }
}
```

### 6. `appsettings.json` additions to `AzureOpenAi` section

```json
"AzureOpenAi": {
  "Endpoint": "https://<your-resource>.openai.azure.com/",
  "InferenceDeploymentName": "gpt-4-turbo",
  "EmbeddingDeploymentName": "text-embedding-3-small",
  "OutputTokenBudget": 4096,
  "DegradationMessage": "AI assistance is temporarily unavailable. Please proceed manually or try again shortly.",
  "ResponseCacheTtlSeconds": 86400,
  "SystemPrompts": { ... }
}
```

### 7. `Program.cs` health check registration

```csharp
// After existing service registrations (AddPatientAccessModule, AddClinicalIntelligenceModule, etc.):
builder.Services
    .AddHealthChecks()
    .AddCheck<AzureOpenAiHealthCheck>(
        name: "azure-openai",
        failureStatus: HealthStatus.Degraded,  // Unhealthy probe = Degraded app status (not hard down)
        tags: ["ai", "live"]);

// In app.MapControllers() / app.UseEndpoints() section:
app.MapHealthChecks("/health/ai", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("ai"),
    // Minimal response writer: omit exception details from public HTTP response (OWASP A05)
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json";
        var result = JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new { name = e.Key, status = e.Value.Status.ToString() })
        });
        await ctx.Response.WriteAsync(result);
    }
});
```

---

## Current Project State

```
server/src/Modules/ClinicalIntelligence/
└── ClinicalIntelligence.Application/
    └── AI/
        ├── AzureOpenAiGateway.cs               ← MODIFY: inject IAiAvailabilityState + IAiDegradationHandler;
        │                                                   add degradation guard + fire-and-forget cache write
        ├── AzureOpenAiOptions.cs               ← MODIFY: add DegradationMessage + ResponseCacheTtlSeconds
        └── Availability/                        ← CREATE all files this task
            ├── IAiAvailabilityState.cs
            ├── InMemoryAvailabilityState.cs
            ├── IAiDegradationHandler.cs
            └── AiDegradationHandler.cs

server/src/Modules/ClinicalIntelligence/
└── ClinicalIntelligence.Presentation/
    └── ServiceCollectionExtensions.cs          ← MODIFY: register IAiAvailabilityState + IAiDegradationHandler

server/src/PropelIQ.Api/
├── HealthCheck/
│   ├── HealthCheckResponse.cs                  ← existing (from initial setup)
│   └── AzureOpenAiHealthCheck.cs               ← CREATE this task
├── Program.cs                                  ← MODIFY: AddHealthChecks + MapHealthChecks
└── appsettings.json                            ← MODIFY: add DegradationMessage + ResponseCacheTtlSeconds
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Availability/IAiAvailabilityState.cs` | `bool IsAvailable`; `MarkDegraded(string)`; `MarkRecovered()` — seam for US_031 circuit breaker |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Availability/InMemoryAvailabilityState.cs` | `volatile bool` implementation; singleton; replaced by US_031 |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Availability/IAiDegradationHandler.cs` | `Task<GatewayResponse> GetDegradedResponseAsync(string featureContext, CancellationToken ct)` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Availability/AiDegradationHandler.cs` | Redis cache lookup for last `GatewayResponse` per featureContext; fallback to static message; logs `source: "cached"` |
| CREATE | `server/src/PropelIQ.Api/HealthCheck/AzureOpenAiHealthCheck.cs` | `IHealthCheck`: 1-token embedding probe with 2s timeout; `MarkDegraded`/`MarkRecovered` on `IAiAvailabilityState` |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/AzureOpenAiGateway.cs` | Inject `IAiAvailabilityState`, `IAiDegradationHandler`; add `if (!_availabilityState.IsAvailable)` guard before GPT call; add fire-and-forget `CacheSuccessfulResponseAsync` after success |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/AzureOpenAiOptions.cs` | Add `DegradationMessage` string + `ResponseCacheTtlSeconds` int (default 86400) |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Presentation/ServiceCollectionExtensions.cs` | `services.AddSingleton<IAiAvailabilityState, InMemoryAvailabilityState>()` + `services.AddScoped<IAiDegradationHandler, AiDegradationHandler>()` |
| MODIFY | `server/src/PropelIQ.Api/Program.cs` | `services.AddHealthChecks().AddCheck<AzureOpenAiHealthCheck>(...)` + `app.MapHealthChecks("/health/ai", ...)` with minimal public response writer (OWASP A05) |
| MODIFY | `server/src/PropelIQ.Api/appsettings.json` | Add `DegradationMessage` + `ResponseCacheTtlSeconds` to existing `"AzureOpenAi"` section |

---

## External References

- [ASP.NET Core Health Checks — Microsoft docs](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks)
- [IHealthCheck interface](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.diagnostics.healthchecks.ihealthcheck)
- [HealthCheckOptions.ResponseWriter](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.diagnostics.healthchecks.healthcheckoptions.responsewriter)
- [IDistributedCache — Redis SetStringAsync/GetStringAsync](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed)
- [OWASP A05 — Security Misconfiguration: exposing stack traces in health endpoints](https://owasp.org/Top10/A05_2021-Security_Misconfiguration/)
- [US_031 circuit breaker — next task that replaces InMemoryAvailabilityState](.propel/context/tasks/EP-007/us_031/)

---

## Build Commands

```powershell
# Restore + build
dotnet restore server/PropelIQ.slnx
dotnet build server/PropelIQ.slnx --no-restore

# Run unit tests
dotnet test server/PropelIQ.slnx --no-build --filter "Category=US030"

# Smoke test health endpoint (local dev)
curl -s http://localhost:5000/health/ai | python -m json.tool
```

---

## Implementation Validation Strategy

- [ ] Unit tests pass (xUnit + Moq)
- [ ] `GET /health/ai` returns `{"status":"Healthy"}` when Azure OpenAI probe succeeds
- [ ] `GET /health/ai` returns `{"status":"Unhealthy"}` when probe throws — exception message NOT in response body (OWASP A05)
- [ ] **[AI Tasks]** Degradation path: mock `IAiAvailabilityState.IsAvailable = false` → `ChatCompletionAsync` returns degradation response without calling `AzureOpenAIClient` (verified by mock assert — `AzureOpenAIClient.GetChatClient` never called)
- [ ] **[AI Tasks]** Cache hit on degradation: seed Redis with `ai_cache:FactExtraction` → `AiDegradationHandler` returns cached response and logs `source=cached`
- [ ] **[AI Tasks]** Cache miss on degradation: empty Redis → handler returns static `DegradationMessage` from options
- [ ] **[AI Tasks]** Cache write failure (Redis unavailable): fire-and-forget `CacheSuccessfulResponseAsync` logs warning but `ChatCompletionAsync` still returns the success response
- [ ] **[AI Tasks]** Health check probe timeout: mock probe to delay 3s → `OperationCanceledException` after 2s → `Unhealthy` result + `MarkDegraded` called

---

## Implementation Checklist

- [ ] CREATE `IAiAvailabilityState` interface (seam for US_031) — `bool IsAvailable`, `MarkDegraded(string reason)`, `MarkRecovered()`
- [ ] CREATE `InMemoryAvailabilityState` (`volatile bool`, singleton) — minimal implementation; comment explicitly marks as "replaced by US_031 circuit breaker"
- [ ] CREATE `IAiDegradationHandler` + `AiDegradationHandler` — Redis `ai_cache:{featureContext}` lookup; fallback to `AzureOpenAiOptions.DegradationMessage`; log `source=cached` on cache hit; catch cache exceptions internally (never propagate)
- [ ] CREATE `AzureOpenAiHealthCheck` — 1-token embedding probe; 2s `CancellationToken` linked source; `MarkDegraded/MarkRecovered` on `IAiAvailabilityState`; minimal `HealthCheckResult` data only (no internal exception details)
- [ ] MODIFY `AzureOpenAiGateway.ChatCompletionAsync` — add availability guard (`if (!_availabilityState.IsAvailable) return degradation`) at the very top (before sanitizer — degradation must be instant); add fire-and-forget `CacheSuccessfulResponseAsync` after building `gatewayResponse`
- [ ] MODIFY `AzureOpenAiOptions.cs` — add `DegradationMessage` (string, default provided) + `ResponseCacheTtlSeconds` (int, default 86400); update `appsettings.json`
- [ ] MODIFY `ServiceCollectionExtensions.cs` + `Program.cs` — register `InMemoryAvailabilityState` singleton, `AiDegradationHandler` scoped, `AzureOpenAiHealthCheck` in `AddHealthChecks()`, `MapHealthChecks("/health/ai")` with OWASP A05-compliant minimal response writer

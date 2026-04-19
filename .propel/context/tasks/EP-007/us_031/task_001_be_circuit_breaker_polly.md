# Task - task_001_be_circuit_breaker_polly

## Requirement Reference

- **User Story**: US_031 — Circuit Breaker, Content Safety & Model Rollback
- **Story Location**: `.propel/context/tasks/EP-007/us_031/us_031.md`
- **Acceptance Criteria**:
  - AC-1: After 5 consecutive AI call failures within 60 seconds, the circuit opens and all subsequent AI requests receive a cached/fallback response for 30 seconds before attempting half-open state per AIR-O02.
  - AC-2: In half-open state, a test request that succeeds closes the circuit and resumes normal traffic; a test request that fails re-opens the circuit for another 30 seconds per AIR-O02.
- **Edge Cases**:
  - Circuit breaker opens during critical clinical workflow: `AiDegradationHandler` (US_030/task_002) serves last-cached `GatewayResponse` for the feature context with a "results may be outdated" warning log. This task only adds the circuit breaker intelligence that drives those state transitions — degradation serving logic is unchanged.

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
| Resilience | Polly (`Polly.Core` + `Microsoft.Extensions.Resilience`) | 8.x |
| DI Integration | `Microsoft.Extensions.Http.Resilience` | 8.x |
| Logging | Serilog | 3.x |
| Testing - Unit | xUnit + Moq | 2.x / 4.x |

> `Microsoft.Extensions.Resilience` provides `AddResiliencePipeline(name, builder => ...)` and `ResiliencePipelineProvider<string>` for named pipeline injection — the correct Polly v8 DI pattern. The `static readonly ResiliencePipeline` used in US_019 and US_030 task_001 is replaced by the DI-registered named pipeline for testability and configurability. Both `Polly.Core` and `Microsoft.Extensions.Resilience` are OSS (MIT/Apache) — satisfy NFR-015.

---

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | Yes |
| **AIR Requirements** | AIR-O02 |
| **AI Pattern** | AI Gateway — resilience / circuit breaker |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | `appsettings.json` → `AzureOpenAi:CircuitBreaker:FailureThreshold` (5), `AzureOpenAi:CircuitBreaker:SamplingWindowSeconds` (60), `AzureOpenAi:CircuitBreaker:BreakDurationSeconds` (30) |
| **Model Provider** | Azure OpenAI GPT-4 Turbo |

### CRITICAL: AI Implementation Requirements

- **MUST** use Polly v8 `ResiliencePipelineBuilder` with `AddCircuitBreaker` (outer) + `AddRetry` (inner) in the **same named pipeline** `"azure-openai"` — this replaces both the `static readonly _pipeline` from US_019 and the `AddResiliencePipeline("azure-openai-retry", ...)` from US_030/task_001. Only one named pipeline `"azure-openai"` must exist after this task.
- **MUST** place the circuit breaker strategy **outer** (added first to builder) and retry strategy **inner** (added second). Execution order: circuit-breaker → retry → actual GPT call. Consequence: the circuit breaker counts one failure per request (after all retries exhausted), not per retry attempt — correct semantics for AC-1 "5 consecutive failures".
- **MUST** wire `OnOpened` callback → `IAiAvailabilityState.MarkDegraded("circuit-breaker-open")` and `OnClosed` callback → `IAiAvailabilityState.MarkRecovered()`. `IAiAvailabilityState` interface is unchanged (US_030 contract).
- **MUST** inject `ResiliencePipelineProvider<string>` into `AzureOpenAiGateway` via constructor to retrieve the named pipeline at runtime — removing all `static readonly` pipeline fields.
- **MUST** set `FailureRatio = 1.0` (all requests in the sampling window must fail before opening — not a percentage threshold) with `MinimumThroughput = 5` to prevent opening on low traffic, per AC-1 "5 consecutive failures".
- **MUST** log circuit state transitions (Opened / HalfOpened / Closed) via Serilog at `LogWarning` (Open/HalfOpen) and `LogInformation` (Closed) — no PHI content logged.

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

Replace the `InMemoryAvailabilityState` implementation (US_030/task_002) and the ad-hoc retry-only pipeline (US_030/task_001) with a single Polly v8 named resilience pipeline `"azure-openai"` that combines a circuit breaker (outer) and retry (inner) strategy. The circuit breaker drives `IAiAvailabilityState` state transitions via its `OnOpened`/`OnClosed` callbacks.

**Pipeline design (Polly v8 execution order):**
```
[Circuit Breaker] → [Retry (3×, 1s/2s/4s, 429+503)] → [GPT call]
```
- Requests flow through the circuit breaker first. If the circuit is open, Polly throws `BrokenCircuitException` immediately (before the retry layer is reached). The `AzureOpenAiGateway.ChatCompletionAsync` fast-path guard (`if (!_availabilityState.IsAvailable)`) catches this at a higher level, so `BrokenCircuitException` should never actually surface to the caller.
- When the circuit is open, after `BreakDuration = 30s`, Polly enters half-open: allows exactly one test request. If that request succeeds → `OnClosed`; if it fails → `OnOpened` again.
- `OnOpened` fires → `IAiAvailabilityState.MarkDegraded(...)`. Next `ChatCompletionAsync` call checks `IsAvailable = false` → routes to `AiDegradationHandler` (AC-1 edge case: cached response with warning).
- `OnClosed` fires → `IAiAvailabilityState.MarkRecovered()`. Next call routes to GPT again.

**`InMemoryAvailabilityState` is NOT deleted** — it remains as the production implementation. US_031 only changes which signals drive `MarkDegraded/MarkRecovered`: previously only the health check called these; now the Polly circuit breaker callbacks also call them. The `volatile bool` state holder is still correct and sufficient.

**DI pipeline migration:**
- Remove `AddResiliencePipeline("azure-openai-retry", ...)` from `ServiceCollectionExtensions.cs`.
- Add `AddResiliencePipeline("azure-openai", ...)` with combined circuit breaker + retry.
- `AzureOpenAiGateway`: remove `static readonly _retryPipeline` field; inject `ResiliencePipelineProvider<string>` via constructor; call `_pipelineProvider.GetPipeline("azure-openai")` inside `ChatCompletionAsync`.

---

## Dependent Tasks

- **task_001_be_azure_openai_gateway_hardening.md** (US_030) — `AddResiliencePipeline("azure-openai-retry", ...)` registered here; US_031 replaces it.
- **task_002_be_ai_health_check_degradation.md** (US_030) — `IAiAvailabilityState`, `InMemoryAvailabilityState` established here; US_031 adds circuit breaker as a second driver of those state transitions.
- **task_002_be_content_safety_model_rollback.md** (US_031, this sprint) — can run in parallel; both tasks modify `AzureOpenAiGateway` but at different insertion points (circuit breaker guard is before GPT call; content safety filter is after GPT call).

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/AzureOpenAiCircuitBreakerOptions.cs` | Options POCO: `FailureThreshold` (int, default 5), `SamplingWindowSeconds` (int, default 60), `BreakDurationSeconds` (int, default 30) |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/AzureOpenAiOptions.cs` | Add `CircuitBreaker` property of type `AzureOpenAiCircuitBreakerOptions` |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/AzureOpenAiGateway.cs` | Remove `static readonly _retryPipeline`; inject `ResiliencePipelineProvider<string>`; replace `_retryPipeline.ExecuteAsync(...)` with `_pipelineProvider.GetPipeline("azure-openai").ExecuteAsync(...)` |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Presentation/ServiceCollectionExtensions.cs` | Remove `AddResiliencePipeline("azure-openai-retry", ...)`; add `AddResiliencePipeline("azure-openai", ...)` with circuit-breaker (outer) + retry (inner); wire `OnOpened/OnClosed/OnHalfOpened` callbacks to `IAiAvailabilityState` |
| MODIFY | `server/src/PropelIQ.Api/appsettings.json` | Add `"AzureOpenAi:CircuitBreaker"` subsection: `FailureThreshold`, `SamplingWindowSeconds`, `BreakDurationSeconds` |

---

## Implementation Plan

### 1. `AzureOpenAiCircuitBreakerOptions` — config POCO

```csharp
// ClinicalIntelligence.Application/AI/AzureOpenAiCircuitBreakerOptions.cs
public sealed class AzureOpenAiCircuitBreakerOptions
{
    /// <summary>Minimum number of consecutive failures within the sampling window to open the circuit (AC-1: 5).</summary>
    public int FailureThreshold { get; set; } = 5;

    /// <summary>Sliding window in seconds during which failures are counted (AC-1: 60s).</summary>
    public int SamplingWindowSeconds { get; set; } = 60;

    /// <summary>Duration in seconds the circuit stays open before entering half-open state (AC-1: 30s).</summary>
    public int BreakDurationSeconds { get; set; } = 30;
}
```

Add to `AzureOpenAiOptions`:
```csharp
/// <summary>Circuit breaker thresholds for the Polly resilience pipeline (AIR-O02).</summary>
public AzureOpenAiCircuitBreakerOptions CircuitBreaker { get; set; } = new();
```

### 2. Combined resilience pipeline in `ServiceCollectionExtensions.cs`

```csharp
// REMOVE existing:
// services.AddResiliencePipeline("azure-openai-retry", builder => { ... });

// ADD replacement (combined circuit breaker + retry):
services.AddResiliencePipeline("azure-openai", (builder, context) =>
{
    var opts = context.ServiceProvider
        .GetRequiredService<IOptions<AzureOpenAiOptions>>().Value;
    var availabilityState = context.ServiceProvider
        .GetRequiredService<IAiAvailabilityState>();
    var logger = context.ServiceProvider
        .GetRequiredService<ILogger<AzureOpenAiGateway>>();

    // Strategy 1 (outer): Circuit Breaker — counts one failure per exhausted request
    builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
    {
        // FailureRatio = 1.0 means ALL requests in the window must fail.
        // MinimumThroughput = FailureThreshold ensures we need at least N failures.
        FailureRatio = 1.0,
        MinimumThroughput = opts.CircuitBreaker.FailureThreshold,
        SamplingDuration = TimeSpan.FromSeconds(opts.CircuitBreaker.SamplingWindowSeconds),
        BreakDuration = TimeSpan.FromSeconds(opts.CircuitBreaker.BreakDurationSeconds),

        // OnOpened: circuit opened — mark availability as degraded (AC-1)
        OnOpened = args =>
        {
            availabilityState.MarkDegraded("circuit-breaker-open");
            logger.LogWarning(
                "AI circuit breaker OPENED after {Failures} failures in {Window}s window. " +
                "Breaking for {Break}s. Reason: {Reason}",
                opts.CircuitBreaker.FailureThreshold,
                opts.CircuitBreaker.SamplingWindowSeconds,
                opts.CircuitBreaker.BreakDurationSeconds,
                args.Outcome.Exception?.GetType().Name ?? "unknown");
            return default;
        },

        // OnHalfOpened: break duration elapsed — allow one test request (AC-2)
        OnHalfOpened = args =>
        {
            logger.LogWarning(
                "AI circuit breaker HALF-OPEN — probing with test request.");
            return default;
        },

        // OnClosed: test request succeeded — resume normal traffic (AC-2)
        OnClosed = args =>
        {
            availabilityState.MarkRecovered();
            logger.LogInformation(
                "AI circuit breaker CLOSED — normal AI operation resumed.");
            return default;
        },

        // ShouldHandle: same failure set as the retry strategy below
        ShouldHandle = new PredicateBuilder()
            .Handle<RequestFailedException>(ex => ex.Status is 429 or 503)
            .Handle<HttpRequestException>()
    });

    // Strategy 2 (inner): Retry — 3 attempts at 1s/2s/4s with Retry-After header support
    builder.AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        DelayGenerator = static args =>
        {
            if (args.Outcome.Exception is RequestFailedException { Status: 429 } rfe
                && rfe.GetRawResponse()?.Headers.TryGetValue("Retry-After", out var ra) == true
                && int.TryParse(ra, out var secs))
                return ValueTask.FromResult<TimeSpan?>(TimeSpan.FromSeconds(Math.Min(secs, 4)));
            return ValueTask.FromResult<TimeSpan?>(
                TimeSpan.FromSeconds(Math.Pow(2, args.AttemptNumber)));
        },
        ShouldHandle = new PredicateBuilder()
            .Handle<RequestFailedException>(ex => ex.Status is 429 or 503)
            .Handle<HttpRequestException>()
    });
});
```

> **Pipeline strategy ordering note (Polly v8):** Strategies execute in the order they are added. Adding circuit breaker first (outer) and retry second (inner) means the circuit breaker sees the result only after all retries are exhausted. This is the correct behaviour: the circuit breaker should count one failure per original AI call, not per retry attempt. If retry were outer, the circuit breaker would open after 5 individual retry attempts (≈ 2 total original requests at 3 retries each), which would be too aggressive.

### 3. `AzureOpenAiGateway` — swap static pipeline for DI pipeline

```csharp
// REMOVE these static fields (were set up in US_019 and US_030 task_001):
// private static readonly ResiliencePipeline _pipeline = ...;         (from US_019)
// private static readonly ResiliencePipeline _retryPipeline = ...;    (from US_030 task_001)

// ADD to constructor parameters:
private readonly ResiliencePipelineProvider<string> _pipelineProvider;

public AzureOpenAiGateway(
    IOptions<AzureOpenAiOptions> options,
    IDistributedCache cache,
    IAuditLogger auditLogger,
    IPromptSanitizer sanitizer,
    IHttpContextAccessor httpContextAccessor,
    IAiAvailabilityState availabilityState,
    IAiDegradationHandler degradationHandler,
    ResiliencePipelineProvider<string> pipelineProvider,  // ← NEW (US_031)
    ILogger<AzureOpenAiGateway> logger,
    PropelIQDbContext db)
{
    // ... assign fields ...
    _pipelineProvider = pipelineProvider;
}

// REPLACE in ChatCompletionAsync — wherever _retryPipeline.ExecuteAsync(...) appears:
// OLD:
//   var completion = await _retryPipeline.ExecuteAsync(async token => { ... }, ct);
// NEW:
var pipeline = _pipelineProvider.GetPipeline("azure-openai");
ChatCompletion completion = await pipeline.ExecuteAsync(async token =>
    await chatClient.CompleteChatAsync([...], options, token), ct);

// REPLACE in GenerateEmbeddingsAsync — wherever _pipeline.ExecuteAsync(...) appears:
// OLD:
//   var response = await _pipeline.ExecuteAsync(async token => { ... }, ct);
// NEW (same named pipeline wraps both inference and embedding calls):
var embeddingPipeline = _pipelineProvider.GetPipeline("azure-openai");
var response = await embeddingPipeline.ExecuteAsync(async token => { ... }, ct);
```

> **`BrokenCircuitException` handling note:** When the circuit is open, calling `_pipelineProvider.GetPipeline("azure-openai").ExecuteAsync(...)` throws `BrokenCircuitException`. However, the `if (!_availabilityState.IsAvailable)` guard (added in US_030/task_002) fires BEFORE `pipeline.ExecuteAsync()` — so `BrokenCircuitException` should never reach the caller in the normal flow. Nonetheless, add a catch for `BrokenCircuitException` in `ChatCompletionAsync` as a safety net that routes to the degradation handler, then logs at `Warning` level.

### 4. `appsettings.json` additions

```json
"AzureOpenAi": {
  "CircuitBreaker": {
    "FailureThreshold": 5,
    "SamplingWindowSeconds": 60,
    "BreakDurationSeconds": 30
  }
  // ... existing Endpoint, InferenceDeploymentName, etc. ...
}
```

---

## Current Project State

```
server/src/Modules/ClinicalIntelligence/
└── ClinicalIntelligence.Application/
    └── AI/
        ├── IAiGateway.cs                         ← no change (US_030 contract)
        ├── AzureOpenAiGateway.cs                 ← MODIFY: remove static pipelines;
        │                                                     inject ResiliencePipelineProvider<string>;
        │                                                     replace .ExecuteAsync calls
        ├── AzureOpenAiOptions.cs                 ← MODIFY: add CircuitBreaker property
        ├── AzureOpenAiCircuitBreakerOptions.cs   ← CREATE this task
        └── Availability/
            ├── IAiAvailabilityState.cs            ← no change (US_030 interface preserved)
            └── InMemoryAvailabilityState.cs       ← no change (still the implementation;
                                                               circuit breaker callbacks drive it)

server/src/Modules/ClinicalIntelligence/
└── ClinicalIntelligence.Presentation/
    └── ServiceCollectionExtensions.cs            ← MODIFY: remove "azure-openai-retry";
                                                              add "azure-openai" combined pipeline

server/src/PropelIQ.Api/
└── appsettings.json                              ← MODIFY: add CircuitBreaker subsection
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/AzureOpenAiCircuitBreakerOptions.cs` | POCO: `FailureThreshold` (5), `SamplingWindowSeconds` (60), `BreakDurationSeconds` (30) |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/AzureOpenAiOptions.cs` | Add `CircuitBreaker` property of type `AzureOpenAiCircuitBreakerOptions` |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/AzureOpenAiGateway.cs` | Remove two `static readonly ResiliencePipeline` fields; inject `ResiliencePipelineProvider<string>`; replace all `.ExecuteAsync()` calls with `_pipelineProvider.GetPipeline("azure-openai").ExecuteAsync()` |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Presentation/ServiceCollectionExtensions.cs` | Remove `AddResiliencePipeline("azure-openai-retry", ...)`; add `AddResiliencePipeline("azure-openai", ...)` — circuit breaker (outer, `OnOpened/OnClosed/OnHalfOpened` callbacks) + retry (inner, 3×, 1s/2s/4s) |
| MODIFY | `server/src/PropelIQ.Api/appsettings.json` | Add `AzureOpenAi:CircuitBreaker` section with `FailureThreshold`, `SamplingWindowSeconds`, `BreakDurationSeconds` |

---

## External References

- [Polly v8 Circuit Breaker strategy docs](https://www.pollydocs.org/strategies/circuit-breaker.html)
- [Polly v8 — strategy ordering (outer/inner)](https://www.pollydocs.org/pipelines/index.html#order-of-strategies)
- [Microsoft.Extensions.Resilience — AddResiliencePipeline DI](https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience)
- [ResiliencePipelineProvider<TKey>](https://www.pollydocs.org/advanced/dependency-injection.html#resiliencepipelineprovider)
- [CircuitBreakerStrategyOptions — OnOpened/OnClosed/OnHalfOpened callbacks](https://www.pollydocs.org/strategies/circuit-breaker.html#defaults)
- [NFR-016 — circuit breaker patterns for external dependencies](../.propel/context/docs/design.md)

---

## Build Commands

```powershell
# Restore + build (verify no static pipeline field errors)
dotnet restore server/PropelIQ.slnx
dotnet build server/PropelIQ.slnx --no-restore

# Run unit tests scoped to US_031
dotnet test server/PropelIQ.slnx --no-build --filter "Category=US031"
```

---

## Implementation Validation Strategy

- [ ] Unit tests pass (xUnit + Moq)
- [ ] **[AI Tasks]** Verify 5 consecutive `RequestFailedException(503)` calls trigger `OnOpened` → `MarkDegraded` — confirmed via unit test with mocked `IAiAvailabilityState`
- [ ] **[AI Tasks]** Verify circuit opens after 5 exhausted retries (not after 5 individual retry attempts) — 5 calls each exhausting 3 retries = 5 circuit-breaker-level failures
- [ ] **[AI Tasks]** After `BreakDuration = 30s` (fast-forward in unit test), verify Polly allows one test request through (half-open); success → `MarkRecovered` called
- [ ] **[AI Tasks]** Half-open test failure → `MarkDegraded` called again, circuit stays open
- [ ] **[AI Tasks]** `static readonly ResiliencePipeline` fields fully removed — build succeeds; no `static` pipeline fields remain in `AzureOpenAiGateway.cs`
- [ ] **[AI Tasks]** Serilog logs contain `"circuit breaker OPENED"` at Warning level on open; `"CLOSED"` at Information level on close — no raw AI content in log messages

---

## Implementation Checklist

- [ ] CREATE `AzureOpenAiCircuitBreakerOptions` POCO (`FailureThreshold = 5`, `SamplingWindowSeconds = 60`, `BreakDurationSeconds = 30`) and add as `CircuitBreaker` property on `AzureOpenAiOptions`
- [ ] MODIFY `ServiceCollectionExtensions.cs` — remove `AddResiliencePipeline("azure-openai-retry", ...)`; add `AddResiliencePipeline("azure-openai", ...)` with circuit breaker outer (FailureRatio=1.0, MinimumThroughput=FailureThreshold, OnOpened/OnClosed/OnHalfOpened callbacks) + retry inner (3×, 1s/2s/4s, Retry-After header); callbacks wire to `IAiAvailabilityState` singleton from DI
- [ ] MODIFY `AzureOpenAiGateway.cs` — remove both `static readonly ResiliencePipeline` fields (US_019 `_pipeline` and US_030 `_retryPipeline`); inject `ResiliencePipelineProvider<string>` via primary constructor; replace all `.ExecuteAsync()` calls with `_pipelineProvider.GetPipeline("azure-openai").ExecuteAsync()`; add safety-net catch for `BrokenCircuitException` → route to degradation handler + log Warning
- [ ] MODIFY `appsettings.json` — add `"AzureOpenAi:CircuitBreaker"` subsection (`FailureThreshold: 5`, `SamplingWindowSeconds: 60`, `BreakDurationSeconds: 30`)
- [ ] Verify: no `static` Polly pipeline fields remain in `AzureOpenAiGateway.cs`; all pipeline accesses go through `_pipelineProvider.GetPipeline("azure-openai")`
- [ ] **[AI Tasks - MANDATORY]** Implement and test circuit breaker before marking task complete
- [ ] **[AI Tasks - MANDATORY]** Verify AIR-O02: 5 consecutive failures → circuit open for 30s → half-open probe → close on success / re-open on failure

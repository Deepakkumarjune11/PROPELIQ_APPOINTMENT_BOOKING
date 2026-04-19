# Task - task_001_be_latency_sla_schema_validation

## Requirement Reference

- **User Story**: US_032 — AI Latency, Schema Validation & Feature Flags
- **Story Location**: `.propel/context/tasks/EP-007/us_032/us_032.md`
- **Acceptance Criteria**:
  - AC-1: Given any AI request is processed, when the response is returned, then the system records end-to-end latency and alerts if p95 latency exceeds 3 seconds per AIR-Q02.
  - AC-2: Given an AI model returns a structured response (JSON), when the response is received, then the gateway validates against the expected JSON schema and rejects malformed responses with a retry per AIR-Q03.
  - AC-3: Given a schema validation failure occurs 3 times consecutively, when the retry budget is exhausted, then the system returns a structured error response and logs the schema violation for model fine-tuning feedback per AIR-Q03.
- **Edge Cases**:
  - P95 SLA consistently breached: Auto-disable non-critical AI features and alert engineering team (implemented in task_002 using `IFeatureFlagService`; this task exposes `GetP95Async` seam and logs the breach event at Error level).
  - Partial schema matches: Strict validation — partial matches are rejected to prevent downstream errors. Any missing required property OR wrong `JsonValueKind` for a typed property → fail.

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
| Caching / Latency Store | Upstash Redis (StackExchange.Redis) | Cloud / 2.8.x |
| JSON Parsing | `System.Text.Json` | Built-in (.NET 8) |
| Logging | Serilog + `IAuditLogger` (US_026) | 3.x / custom |
| Testing — Unit | xUnit + Moq | 2.x / 4.x |

> Schema validation uses `System.Text.Json.JsonDocument` only — no external JSON Schema libraries (satisfies NFR-015 free/OSS constraint). The design.md line 158 explicitly calls for "Custom JSON Schema Validator" using this approach.

---

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | Yes |
| **AIR Requirements** | AIR-Q02, AIR-Q03 |
| **AI Pattern** | AI Gateway — latency observability + structured output schema enforcement |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | `appsettings.json` → `"AiSla"` section (`P95ThresholdMs`, `SampleWindowSize`); per-featureContext schema definitions in `AiSchemaRegistry` (static, code-defined) |
| **Model Provider** | Azure OpenAI GPT-4 Turbo |

### CRITICAL: AI Implementation Requirements

- **MUST** measure latency using `Stopwatch.GetTimestamp()` at the START of `ChatCompletionAsync` (before the feature flag check — latency includes availability guard overhead) through to just before returning the `GatewayResponse`. Record via `ILatencyRecorder.RecordAsync(featureContext, latencyMs, ct)`.
- **MUST** retain last `AiSlaOptions.SampleWindowSize` (default 200) latency samples per featureContext using a Redis list (`RPUSH` + `LTRIM(0, SampleWindowSize-1)`). P95 = LRANGE(0,-1) → parse → sort → take index at `Math.Ceiling(0.95 * count) - 1`.
- **MUST** log at `Error` level when p95 exceeds `AiSlaOptions.P95ThresholdMs` (3000ms default). The alert triggers `IFeatureFlagService.SetFlagAsync` for non-critical features — but `IFeatureFlagService` is defined in task_002. The p95 check here only logs; auto-disable is wired in task_002 by injecting `IFeatureFlagService` into the gateway. The `ILatencyRecorder.GetP95Async(featureContext, ct)` seam is defined here and consumed by both this task and task_002's SLA auto-disable logic.
- **MUST** implement schema validation as an **outer retry loop** in `ChatCompletionAsync` — SEPARATE from the Polly `"azure-openai"` pipeline (which handles HTTP/transient errors). The schema retry loop retries the entire GPT call up to `MaxSchemaAttempts = 3` on semantic schema failures only.
- **Schema validation applies ONLY to feature contexts that have registered schemas** (i.e., `AiSchemaRegistry.TryGetSchema(featureContext, out _)` returns true). If no schema is registered for a featureContext (e.g., `"ConversationalIntake"` returns free-text), skip validation — no retry loop applied.
- **MUST NOT** log the full JSON response content on schema failure (TR-006 PHI risk). Log ONLY: `featureContext`, `attemptNumber`, `errorReason` (property name + kind mismatch), `correlationId`.
- **Schema exhausted (AC-3)**: After 3 failed schema attempts, log a dedicated audit entry (`ActionType = "SchemaValidationExhausted"`) with `correlationId` + `featureContext` + `errorReason`. Return a deterministic `GatewayResponse` with a safe user-facing message from `AiSlaOptions.SchemaErrorMessage`. Do NOT throw — callers should receive a structured response, not an exception.

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

This task adds two orthogonal quality measurement and enforcement mechanisms to `AzureOpenAiGateway.ChatCompletionAsync`:

**1. End-to-End Latency Recording (AC-1, AIR-Q02):**
A Redis-backed sliding-window p95 tracker that measures wall-clock latency from gateway entry to response return (inclusive of content safety checks and schema validation, exclusive of caller's own processing). Measurements stored as a Redis list (`RPUSH` + `LTRIM`) to prevent unbounded growth. P95 computed inline on a `LRANGE` + sort. SLA breach (p95 > 3s) triggers a Serilog `Error` log — the auto-disable signal is consumed by task_002 which injects `IFeatureFlagService` for actual feature disabling.

**2. JSON Schema Validation with Retry (AC-2, AC-3, AIR-Q03, AIR-Q04):**
For feature contexts that return structured JSON (`FactExtraction`, `CodeSuggestion`, `ConflictDetection`), the gateway validates the GPT response against a per-featureContext schema definition before accepting the output. Schema definition is code-registered (not config file) in `AiSchemaRegistry` — a static registry mapping featureContext → `AiSchemaDefinition` (required properties with expected `JsonValueKind`). Validation uses `System.Text.Json.JsonDocument.Parse()` — no external library.

On schema failure, the gateway re-issues the full GPT call (re-executing the Polly pipeline) up to 3 total attempts. After 3 failures: returns `GatewayResponse(SchemaErrorMessage, ...)` with an audit log entry tagged `"SchemaValidationExhausted"` for model fine-tuning signal. The schema violation log is the mechanism by which fine-tuning feedback is surfaced (per AC-3) — actual fine-tuning pipeline integration is out of scope.

---

## Dependent Tasks

- **task_001_be_azure_openai_gateway_hardening.md** (US_030) — `AzureOpenAiGateway`, `GatewayResponse`, `AzureOpenAiOptions`, `IAiGateway` must exist.
- **task_001_be_circuit_breaker_polly.md** (US_031) — `ResiliencePipelineProvider<string>` injection into gateway must exist; the schema retry outer loop wraps the Polly-managed inner call.
- **task_002_be_content_safety_model_rollback.md** (US_031) — `IContentSafetyFilter.EvaluateAsync` called AFTER schema validation succeeds (schema first, then safety filter). Latency timer stops after safety filter returns — latency includes safety check time.
- **task_002_be_feature_flag_service.md** (US_032, this epic) — task_002 injects `IFeatureFlagService` and `ILatencyRecorder` together into the gateway to complete the SLA auto-disable loop.

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Latency/ILatencyRecorder.cs` | `RecordAsync(featureContext, latencyMs, ct)` + `GetP95Async(featureContext, ct) → double` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Latency/RedisLatencyRecorder.cs` | Redis list `ai:latency:{featureContext}` + RPUSH + LTRIM(0, SampleWindowSize-1); `GetP95Async` = LRANGE→sort→p95 index |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Latency/AiSlaOptions.cs` | `P95ThresholdMs` (default 3000), `SampleWindowSize` (default 200), `SchemaErrorMessage` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Schema/IAiSchemaValidator.cs` | `SchemaValidationResult Validate(string jsonContent, string featureContext)` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Schema/SchemaValidationResult.cs` | `record(bool IsValid, string? ErrorReason)` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Schema/AiSchemaDefinition.cs` | `record(IReadOnlyDictionary<string, JsonValueKind> RequiredProperties)` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Schema/AiSchemaRegistry.cs` | Static registry: `FactExtraction` + `CodeSuggestion` + `ConflictDetection` schemas; `TryGetSchema(featureContext, out AiSchemaDefinition?)` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Schema/JsonDocumentSchemaValidator.cs` | Implements `IAiSchemaValidator`; `JsonDocument.Parse()` + property presence + `JsonValueKind` type checks |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/AzureOpenAiGateway.cs` | Inject `ILatencyRecorder`, `IAiSchemaValidator`; add `Stopwatch.GetTimestamp()` timing; schema retry loop (max 3); record latency + p95 log after response |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Presentation/ServiceCollectionExtensions.cs` | Register `ILatencyRecorder → RedisLatencyRecorder` (singleton), `IAiSchemaValidator → JsonDocumentSchemaValidator` (singleton); bind `AiSlaOptions` from `"AiSla"` section |
| MODIFY | `server/src/PropelIQ.Api/appsettings.json` | Add `"AiSla"` section (`P95ThresholdMs`, `SampleWindowSize`, `SchemaErrorMessage`) |

---

## Implementation Plan

### 1. `AiSlaOptions` — configuration POCO

```csharp
// ClinicalIntelligence.Application/AI/Latency/AiSlaOptions.cs
public sealed class AiSlaOptions
{
    public const string SectionName = "AiSla";

    /// <summary>Alert threshold for p95 latency in milliseconds (AIR-Q02: 3 seconds).</summary>
    public int P95ThresholdMs { get; set; } = 3000;

    /// <summary>Sliding window size — number of latency samples retained per featureContext.</summary>
    public int SampleWindowSize { get; set; } = 200;

    /// <summary>User-facing message returned after schema validation is exhausted (3 consecutive failures).</summary>
    public string SchemaErrorMessage { get; set; } =
        "The AI response could not be validated. Please try again or contact support.";
}
```

### 2. `ILatencyRecorder` + `RedisLatencyRecorder`

```csharp
// ILatencyRecorder.cs
public interface ILatencyRecorder
{
    Task RecordAsync(string featureContext, long latencyMs, CancellationToken ct = default);
    Task<double> GetP95Async(string featureContext, CancellationToken ct = default);
}

// RedisLatencyRecorder.cs
public sealed class RedisLatencyRecorder(
    IConnectionMultiplexer redis,
    IOptions<AiSlaOptions> options) : ILatencyRecorder
{
    private string ListKey(string ctx) => $"ai:latency:{ctx}";

    public async Task RecordAsync(string featureContext, long latencyMs, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var key = ListKey(featureContext);
        // Atomic RPUSH + LTRIM in a pipeline (single round-trip)
        var batch = db.CreateBatch();
        _ = batch.ListRightPushAsync(key, latencyMs.ToString());
        _ = batch.ListTrimAsync(key, 0, options.Value.SampleWindowSize - 1);
        batch.Execute();
    }

    public async Task<double> GetP95Async(string featureContext, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var values = await db.ListRangeAsync(ListKey(featureContext));
        if (values.Length == 0) return 0;

        var samples = values
            .Where(v => long.TryParse(v, out _))
            .Select(v => long.Parse((string)v!))
            .OrderBy(x => x)
            .ToArray();

        if (samples.Length == 0) return 0;
        var p95Index = (int)Math.Ceiling(0.95 * samples.Length) - 1;
        return samples[Math.Max(0, Math.Min(p95Index, samples.Length - 1))];
    }
}
```

> **Redis pipeline note:** `RPUSH` + `LTRIM` are issued via `IDatabase.CreateBatch()` to reduce round-trips to a single command flush. The LTRIM ensures the list never grows beyond `SampleWindowSize`, bounding Redis memory per feature context.

### 3. `AiSchemaDefinition` + `AiSchemaRegistry`

```csharp
// AiSchemaDefinition.cs
/// <summary>Describes required top-level properties and their expected JSON value kinds for a featureContext response.</summary>
public sealed record AiSchemaDefinition(
    IReadOnlyDictionary<string, JsonValueKind> RequiredProperties);

// AiSchemaRegistry.cs
/// <summary>Static registry mapping featureContext to its expected JSON schema definition.</summary>
public static class AiSchemaRegistry
{
    private static readonly IReadOnlyDictionary<string, AiSchemaDefinition> _schemas =
        new Dictionary<string, AiSchemaDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["FactExtraction"] = new(new Dictionary<string, JsonValueKind>
            {
                { "facts", JsonValueKind.Array },
                { "documentId", JsonValueKind.String },
                { "extractionVersion", JsonValueKind.String }
            }),
            ["CodeSuggestion"] = new(new Dictionary<string, JsonValueKind>
            {
                { "suggestedCodes", JsonValueKind.Array },
                { "confidence", JsonValueKind.Number },
                { "rationale", JsonValueKind.String }
            }),
            ["ConflictDetection"] = new(new Dictionary<string, JsonValueKind>
            {
                { "conflicts", JsonValueKind.Array },
                { "hasCriticalConflicts", JsonValueKind.True | JsonValueKind.False }
            })
        };

    public static bool TryGetSchema(string featureContext,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out AiSchemaDefinition? schema)
        => _schemas.TryGetValue(featureContext, out schema);
}
```

> **ConflictDetection schema note:** `hasCriticalConflicts` is a boolean field. `JsonValueKind.True | JsonValueKind.False` is a bitwise combination used as a "accept either boolean value" check — the validator treats this as "must be `JsonValueKind.True` OR `JsonValueKind.False`" (see validator implementation below).

### 4. `SchemaValidationResult` + `IAiSchemaValidator` + `JsonDocumentSchemaValidator`

```csharp
// SchemaValidationResult.cs
public sealed record SchemaValidationResult(bool IsValid, string? ErrorReason)
{
    public static SchemaValidationResult Valid() => new(true, null);
    public static SchemaValidationResult Invalid(string reason) => new(false, reason);
}

// IAiSchemaValidator.cs
public interface IAiSchemaValidator
{
    SchemaValidationResult Validate(string jsonContent, string featureContext);
}

// JsonDocumentSchemaValidator.cs
public sealed class JsonDocumentSchemaValidator : IAiSchemaValidator
{
    public SchemaValidationResult Validate(string jsonContent, string featureContext)
    {
        if (!AiSchemaRegistry.TryGetSchema(featureContext, out var schema))
            return SchemaValidationResult.Valid(); // No schema registered → pass-through

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(jsonContent);
        }
        catch (JsonException ex)
        {
            return SchemaValidationResult.Invalid($"JSON parse error: {ex.Message}");
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return SchemaValidationResult.Invalid(
                    $"Root must be Object, got {root.ValueKind}");

            foreach (var (propertyName, expectedKind) in schema.RequiredProperties)
            {
                if (!root.TryGetProperty(propertyName, out var prop))
                    return SchemaValidationResult.Invalid(
                        $"Missing required property '{propertyName}'");

                // Boolean fields are encoded as True|False combined flag
                bool booleanField = (expectedKind & (JsonValueKind.True | JsonValueKind.False)) != 0
                                    && (int)expectedKind <= 3; // JsonValueKind.True=1, False=2
                if (!booleanField && prop.ValueKind != expectedKind)
                    return SchemaValidationResult.Invalid(
                        $"Property '{propertyName}' expected {expectedKind}, got {prop.ValueKind}");

                if (booleanField && prop.ValueKind != JsonValueKind.True && prop.ValueKind != JsonValueKind.False)
                    return SchemaValidationResult.Invalid(
                        $"Property '{propertyName}' expected boolean, got {prop.ValueKind}");
            }
        }

        return SchemaValidationResult.Valid();
    }
}
```

### 5. `AzureOpenAiGateway.ChatCompletionAsync` — latency + schema integration

```csharp
// Inject ILatencyRecorder and IAiSchemaValidator via constructor (additions to US_031 state)

public async Task<GatewayResponse> ChatCompletionAsync(
    string featureContext, string systemPrompt, string userMessage,
    Guid correlationId, CancellationToken ct)
{
    // --- Timing start (AC-1: includes full gateway overhead) ---
    var startTimestamp = Stopwatch.GetTimestamp();

    // [existing] availability guard + feature flag check (task_002)
    // [existing] degradation handler shortcut
    // [existing] prompt sanitizer
    // [existing] deployment resolution
    // [existing] system prompt prepend

    // --- Schema retry loop (AC-2, AC-3) ---
    const int MaxSchemaAttempts = 3;
    GatewayResponse? result = null;
    SchemaValidationResult? lastSchemaResult = null;

    for (int attempt = 1; attempt <= MaxSchemaAttempts; attempt++)
    {
        // [existing] Polly pipeline call → completion object
        var completion = await _pipelineProvider.GetPipeline("azure-openai")
            .ExecuteAsync(async innerCt =>
                await chatClient.CompleteChatAsync(messages, options, innerCt), ct);

        var content = completion.Value.Content[0].Text;
        var inputTokens = completion.Value.Usage.InputTokenCount;
        var outputTokens = completion.Value.Usage.OutputTokenCount;
        var isTruncated = completion.Value.FinishReason == ChatFinishReason.Length;

        // Schema validation
        var schemaResult = _schemaValidator.Validate(content, featureContext);
        if (schemaResult.IsValid)
        {
            result = new GatewayResponse(content, inputTokens, outputTokens, isTruncated, featureContext);
            break;
        }

        lastSchemaResult = schemaResult;
        logger.LogWarning(
            "Schema validation failed | attempt={Attempt}/{Max} featureContext={FeatureContext} " +
            "reason={Reason} correlationId={CorrelationId}",
            attempt, MaxSchemaAttempts, featureContext, schemaResult.ErrorReason, correlationId);
    }

    // AC-3: Retry budget exhausted
    if (result is null)
    {
        await _auditLogger.LogAsync(new AuditLogEntry
        {
            ActionType = "SchemaValidationExhausted",
            TargetEntityId = correlationId.ToString(),
            TargetEntityType = "AiResponse",
            Payload = JsonSerializer.Serialize(new
            {
                featureContext,
                attempts = MaxSchemaAttempts,
                errorReason = lastSchemaResult?.ErrorReason   // property name only — no content
            })
        }, ct);

        // Record latency even on schema-exhausted path
        var exhaustedLatencyMs = Stopwatch.GetElapsedTime(startTimestamp).Milliseconds;
        await RecordAndCheckSlaAsync(featureContext, exhaustedLatencyMs, ct);

        return new GatewayResponse(
            _slaOpts.SchemaErrorMessage, 0, 0, IsTruncated: false, featureContext);
    }

    // [existing] content safety filter (US_031/task_002)
    // [existing] CacheSuccessfulResponseAsync fire-and-forget

    // --- Timing end + latency recording (AC-1) ---
    var latencyMs = (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
    await RecordAndCheckSlaAsync(featureContext, latencyMs, ct);

    return result;
}

private async Task RecordAndCheckSlaAsync(string featureContext, long latencyMs, CancellationToken ct)
{
    await _latencyRecorder.RecordAsync(featureContext, latencyMs, ct);
    var p95 = await _latencyRecorder.GetP95Async(featureContext, ct);

    if (p95 > _slaOpts.P95ThresholdMs)
    {
        logger.LogError(
            "AI SLA breach | p95={P95}ms > threshold={Threshold}ms featureContext={FeatureContext}",
            p95, _slaOpts.P95ThresholdMs, featureContext);
        // NOTE: auto-disable is wired in task_002 by extending RecordAndCheckSlaAsync
        //       to also call IFeatureFlagService.SetFlagAsync for non-critical features
    }
}
```

### 6. `appsettings.json` — `AiSla` section

```json
"AiSla": {
  "P95ThresholdMs": 3000,
  "SampleWindowSize": 200,
  "SchemaErrorMessage": "The AI response could not be validated. Please try again or contact support."
}
```

### 7. DI registrations in `ServiceCollectionExtensions.cs`

```csharp
// Latency recorder — singleton (thread-safe, wraps IConnectionMultiplexer)
services.AddSingleton<ILatencyRecorder, RedisLatencyRecorder>();

// Schema validator — singleton (stateless, pure System.Text.Json parsing)
services.AddSingleton<IAiSchemaValidator, JsonDocumentSchemaValidator>();

// Bind AiSlaOptions
services.Configure<AiSlaOptions>(configuration.GetSection(AiSlaOptions.SectionName));
```

---

## Current Project State

```
server/src/Modules/ClinicalIntelligence/
└── ClinicalIntelligence.Application/
    └── AI/
        ├── AzureOpenAiGateway.cs               ← MODIFY: inject ILatencyRecorder + IAiSchemaValidator;
        │                                                   add Stopwatch timing wrapper;
        │                                                   add schema retry loop (max 3);
        │                                                   add RecordAndCheckSlaAsync helper
        ├── Latency/                             ← CREATE all files this task
        │   ├── ILatencyRecorder.cs
        │   ├── RedisLatencyRecorder.cs
        │   └── AiSlaOptions.cs
        └── Schema/                              ← CREATE all files this task
            ├── IAiSchemaValidator.cs
            ├── SchemaValidationResult.cs
            ├── AiSchemaDefinition.cs
            ├── AiSchemaRegistry.cs
            └── JsonDocumentSchemaValidator.cs

server/src/Modules/ClinicalIntelligence/
└── ClinicalIntelligence.Presentation/
    └── ServiceCollectionExtensions.cs          ← MODIFY: register ILatencyRecorder (singleton),
                                                            IAiSchemaValidator (singleton);
                                                            bind AiSlaOptions

server/src/PropelIQ.Api/
└── appsettings.json                            ← MODIFY: add "AiSla" section
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Latency/ILatencyRecorder.cs` | `RecordAsync(featureContext, latencyMs, ct)` + `GetP95Async(featureContext, ct) → double` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Latency/RedisLatencyRecorder.cs` | Redis list per featureContext; RPUSH + LTRIM; LRANGE + sort for p95 |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Latency/AiSlaOptions.cs` | `P95ThresholdMs` (3000), `SampleWindowSize` (200), `SchemaErrorMessage` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Schema/IAiSchemaValidator.cs` | `SchemaValidationResult Validate(string jsonContent, string featureContext)` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Schema/SchemaValidationResult.cs` | `record(bool IsValid, string? ErrorReason)` + `Valid()` + `Invalid(reason)` factories |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Schema/AiSchemaDefinition.cs` | `record(IReadOnlyDictionary<string, JsonValueKind> RequiredProperties)` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Schema/AiSchemaRegistry.cs` | Static schemas for `FactExtraction`, `CodeSuggestion`, `ConflictDetection`; `TryGetSchema` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Schema/JsonDocumentSchemaValidator.cs` | `JsonDocument.Parse()` + required property presence + `JsonValueKind` type check; boolean field special case |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/AzureOpenAiGateway.cs` | Inject `ILatencyRecorder` + `IAiSchemaValidator`; `Stopwatch.GetTimestamp()` timing; schema retry loop (max 3 attempts); `RecordAndCheckSlaAsync` after response; p95 > threshold → `LogError` |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Presentation/ServiceCollectionExtensions.cs` | `AddSingleton<ILatencyRecorder, RedisLatencyRecorder>()` + `AddSingleton<IAiSchemaValidator, JsonDocumentSchemaValidator>()` + `Configure<AiSlaOptions>(...)` |
| MODIFY | `server/src/PropelIQ.Api/appsettings.json` | Add `"AiSla"` section |

---

## External References

- [System.Text.Json.JsonDocument.Parse — docs.microsoft.com](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsondocument.parse)
- [System.Diagnostics.Stopwatch.GetTimestamp — high-resolution timing](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.stopwatch.gettimestamp)
- [StackExchange.Redis — Batch/Pipeline](https://stackexchange.github.io/StackExchange.Redis/PipelinesMultiplexers.html)
- [design.md — AIR-Q02 p95 < 3s, AIR-Q03 schema validity 98%](../.propel/context/docs/design.md#L80-L82)
- [design.md — Custom JSON Schema Validator guardrail](../.propel/context/docs/design.md#L158)

---

## Build Commands

```powershell
# Restore + build
dotnet restore server/PropelIQ.slnx
dotnet build server/PropelIQ.slnx --no-restore

# Run unit tests (tag filter)
dotnet test server/PropelIQ.slnx --no-build --filter "Category=US032_Latency"
```

---

## Implementation Validation Strategy

- [ ] Unit tests pass (xUnit + Moq)
- [ ] **[AI Tasks]** Schema validation pass: valid `FactExtraction` JSON → `IsValid = true`; JSON missing `"facts"` property → `IsValid = false, ErrorReason = "Missing required property 'facts'"`
- [ ] **[AI Tasks]** Schema validation type mismatch: `"facts": "not-an-array"` → `IsValid = false, ErrorReason` mentions `facts` property
- [ ] **[AI Tasks]** Free-text featureContext (`ConversationalIntake`) with no registered schema → `IsValid = true` (no validation performed)
- [ ] **[AI Tasks]** Schema retry loop: first 2 calls return invalid JSON; 3rd also invalid → `GatewayResponse(SchemaErrorMessage)` returned; audit log entry `"SchemaValidationExhausted"` staged; `IAuditLogger.LogAsync` called exactly once
- [ ] **[AI Tasks]** Schema retry loop: first call invalid, 2nd call valid → result returned immediately; `IAuditLogger.LogAsync` NOT called for exhaustion
- [ ] **[AI Tasks]** Latency recording: `RecordAsync` called once per `ChatCompletionAsync` completion (including schema-exhausted path); mock verifies `IConnectionMultiplexer.GetDatabase().ListRightPushAsync` called with `"ai:latency:FactExtraction"`
- [ ] **[AI Tasks]** P95 SLA breach: mock `GetP95Async` returns 5000 → `LogError` called with p95 > threshold message; `LogWarning` NOT used (must be Error level for SLA alerts)
- [ ] **[AI Tasks]** Schema error response does NOT contain any fragment of GPT response content — audit log payload contains only `featureContext + attempts + errorReason`

---

## Implementation Checklist

- [ ] CREATE `AiSlaOptions` POCO with `P95ThresholdMs=3000`, `SampleWindowSize=200`, `SchemaErrorMessage`; `SectionName = "AiSla"`
- [ ] CREATE `ILatencyRecorder` with `RecordAsync` + `GetP95Async`; `RedisLatencyRecorder` uses Redis list per `featureContext`; RPUSH + LTRIM batch; LRANGE + sort + p95 index
- [ ] CREATE `AiSchemaDefinition` record + `AiSchemaRegistry` static class with schemas for `FactExtraction` (facts array + documentId + extractionVersion), `CodeSuggestion` (suggestedCodes array + confidence number + rationale string), `ConflictDetection` (conflicts array + hasCriticalConflicts boolean)
- [ ] CREATE `IAiSchemaValidator` + `SchemaValidationResult` record + `JsonDocumentSchemaValidator` — `JsonDocument.Parse()` + required property presence check + `JsonValueKind` type validation; boolean field special case (True or False both valid)
- [ ] MODIFY `AzureOpenAiGateway.ChatCompletionAsync` — (a) `Stopwatch.GetTimestamp()` at entry; (b) schema retry loop (max 3 calls, re-executes Polly pipeline each attempt); (c) on schema exhaustion: audit log + return `SchemaErrorMessage` response; (d) `RecordAndCheckSlaAsync` after final return; (e) p95 > `P95ThresholdMs` → `LogError`
- [ ] MODIFY `ServiceCollectionExtensions.cs` — register `ILatencyRecorder` (singleton), `IAiSchemaValidator` (singleton); `Configure<AiSlaOptions>`
- [ ] MODIFY `appsettings.json` — add `"AiSla"` section
- [ ] **[AI Tasks - MANDATORY]** Implement and verify schema retry loop; confirm audit log payload has NO response content; confirm safe error message returned on exhaustion
- [ ] **[AI Tasks - MANDATORY]** Verify AIR-Q02/AIR-Q03: latency recording on all paths (success, schema-exhausted, safety-blocked); p95 SLA breach → Error log; schema exhaustion → `SchemaValidationExhausted` audit entry

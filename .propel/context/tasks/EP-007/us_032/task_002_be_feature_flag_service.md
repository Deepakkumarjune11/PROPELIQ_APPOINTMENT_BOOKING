# Task - task_002_be_feature_flag_service

## Requirement Reference

- **User Story**: US_032 — AI Latency, Schema Validation & Feature Flags
- **Story Location**: `.propel/context/tasks/EP-007/us_032/us_032.md`
- **Acceptance Criteria**:
  - AC-4: Given an AI feature has a feature flag, when the flag is disabled, then the feature is completely hidden from the UI and the API returns a "feature unavailable" response without calling the AI model per AIR-Q04 and TR-025.
  - AC-5: Given a feature flag is toggled, when the change is saved, then it takes effect within 30 seconds across all application instances without restart per TR-025.
  - AC-1 (SLA auto-disable edge case): When p95 latency consistently breaches 3 seconds, the system auto-disables non-critical AI features and alerts the engineering team — this task wires the `IFeatureFlagService.SetFlagAsync` call to the latency SLA breach path established in task_001.
- **Edge Cases**:
  - What if Redis is unavailable when a feature flag is read? → Fall back to `IOptionsMonitor<AiFeatureFlagsOptions>.CurrentValue.Defaults` — if the default says `true`, feature remains enabled; if default says `false`, feature is blocked. This prevents a Redis outage from unintentionally disabling features in production.
  - UI feature hiding (AC-4): "Completely hidden from the UI" — this task enforces at the API level. A separate FE story will consume the feature flag API (`GET /api/v1/admin/ai/features`) to conditionally render UI elements. The API gate is the authoritative enforcement point; UI hiding follows.

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
| Feature Store | Upstash Redis (StackExchange.Redis via `IConnectionMultiplexer`) | Cloud / 2.8.x |
| Config Hot-Reload | `IOptionsMonitor<T>` | Built-in (.NET 8) |
| Exception Handling | ASP.NET Core `IExceptionHandler` (minimal API) | Built-in (.NET 8) |
| JWT Auth | Microsoft.AspNetCore.Authentication.JwtBearer | 8.0.x |
| Logging | Serilog + `IAuditLogger` (US_026) | 3.x / custom |
| Testing — Unit | xUnit + Moq | 2.x / 4.x |

---

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | Yes |
| **AIR Requirements** | AIR-Q04, TR-025 |
| **AI Pattern** | AI Gateway — feature gate enforcement + SLA-triggered auto-disable |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | `appsettings.json` → `"AiFeatureFlags"` section (`Defaults`, `CriticalFeatures`) |
| **Model Provider** | Azure OpenAI GPT-4 Turbo |

### CRITICAL: AI Implementation Requirements

- **MUST** check `IFeatureFlagService.IsEnabledAsync(featureContext, ct)` at the TOP of `AzureOpenAiGateway.ChatCompletionAsync` — BEFORE prompt sanitization, deployment resolution, and any AI call. If disabled, throw `FeatureDisabledException(featureContext)` immediately (zero AI cost path).
- **MUST** read feature flags directly from Redis on every request — NO in-process caching of flag values. Redis GET is sub-millisecond, providing instant propagation. This trivially satisfies the "within 30 seconds" requirement of TR-025 (actual propagation is <1ms, not 30 seconds). The 30-second bound is the SLA upper bound, not the target.
- **MUST** fall back to `IOptionsMonitor<AiFeatureFlagsOptions>.CurrentValue.Defaults[featureName]` (default: `true`) when the Redis key is absent. This ensures Redis cold-start / key eviction does NOT accidentally disable features.
- **MUST** wire the SLA auto-disable from task_001's `RecordAndCheckSlaAsync` helper. Extend the helper to accept `IFeatureFlagService` (injected into the gateway) and call `SetFlagAsync(featureContext, false, ct)` only when: (a) p95 > `P95ThresholdMs`, AND (b) `featureContext` is NOT in `AiFeatureFlagsOptions.CriticalFeatures`. After auto-disabling: log at `Error` level with message including featureContext, p95, and the auto-disable action.
- **MUST** ensure `FeatureDisabledException` → HTTP 503 with RFC 7807 problem-details JSON body: `{"type": "https://propeliq.health/errors/feature-unavailable", "title": "Feature Unavailable", "status": 503, "detail": "The AI feature '{featureName}' is currently disabled.", "featureName": "..."}`. This is the "feature unavailable response" required by AC-4.
- **MUST NOT** return HTTP 404 for a disabled feature — callers must be able to distinguish "feature does not exist" (404) from "feature exists but is disabled" (503).
- **Admin toggle endpoint** MUST validate `featureName` is in the known registered feature set (from `AiFeatureFlagsOptions.Defaults.Keys`) to prevent arbitrary Redis key writes (OWASP A01 — insecure object reference via `featureName` path parameter).

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

This task implements the feature flag system for AI capabilities, enabling zero-downtime toggles and SLA-triggered auto-disable:

**1. Feature Flag Service (AC-4, AC-5):**
`IFeatureFlagService` with `RedisFeatureFlagService` backing. Redis keys follow the pattern `ai:feature:{featureName}:enabled` (value: `"true"` or `"false"`). On every `IsEnabledAsync` call, the service reads from Redis directly (no local cache), falling back to `IOptionsMonitor<AiFeatureFlagsOptions>` defaults when the key is absent. This guarantees propagation is instant across all instances (Redis fan-out), satisfying TR-025 "within 30 seconds" with orders-of-magnitude headroom.

**2. Gateway Feature Gate (AC-4):**
`AzureOpenAiGateway.ChatCompletionAsync` gains an upfront feature flag check. `FeatureDisabledException` is thrown if the flag is off; the ASP.NET Core exception handler translates this to HTTP 503 + RFC 7807 problem-details JSON. No AI model call is made when the feature is disabled — zero-cost enforcement.

**3. SLA Auto-Disable (AC-1 edge case):**
Extends task_001's `RecordAndCheckSlaAsync` helper with the `IFeatureFlagService` injection. When p95 exceeds 3 seconds AND the feature is non-critical (not in `AiFeatureFlagsOptions.CriticalFeatures`), the flag is automatically set to `false` in Redis — immediately preventing further AI calls for that featureContext. This is the "auto-disables non-critical AI features and alerts engineering team" behaviour from the edge case spec.

**4. Admin Feature Management API:**
`GET /api/v1/admin/ai/features` — returns all feature flags and their current states (Redis + defaults).
`POST /api/v1/admin/ai/features/{featureName}/toggle` — admin-only; validates featureName against known set; sets Redis key; audit logs; returns `{"featureName", "enabled"}`.

---

## Dependent Tasks

- **task_001_be_azure_openai_gateway_hardening.md** (US_030) — gateway exists.
- **task_002_be_content_safety_model_rollback.md** (US_031) — `AdminController` base (admin module) must exist.
- **task_001_be_latency_sla_schema_validation.md** (US_032, this epic) — `ILatencyRecorder`, `AiSlaOptions`, `RecordAndCheckSlaAsync` helper must exist in gateway before this task extends it.

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/FeatureFlags/IFeatureFlagService.cs` | `IsEnabledAsync(featureName, ct)` + `SetFlagAsync(featureName, enabled, ct)` + `GetAllFlagsAsync(ct)` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/FeatureFlags/AiFeatureFlagsOptions.cs` | `Dictionary<string, bool> Defaults` + `List<string> CriticalFeatures`; hot-reload via `IOptionsMonitor` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/FeatureFlags/RedisFeatureFlagService.cs` | Redis key `ai:feature:{name}:enabled`; direct read on every call; fallback to `IOptionsMonitor` defaults on key-absent or Redis error |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/FeatureFlags/FeatureDisabledException.cs` | `sealed class FeatureDisabledException(string featureName) : Exception(...)` |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/AzureOpenAiGateway.cs` | Inject `IFeatureFlagService`; add feature flag check at top of `ChatCompletionAsync`; extend `RecordAndCheckSlaAsync` with auto-disable logic |
| MODIFY | `server/src/PropelIQ.Api/Program.cs` | Register `IExceptionHandler` (or `UseExceptionHandler` lambda) to map `FeatureDisabledException` → 503 RFC 7807 |
| MODIFY | `server/src/Modules/Admin/Admin.Presentation/AdminController.cs` | Add `GET /api/v1/admin/ai/features` (list all flags) + `POST /api/v1/admin/ai/features/{featureName}/toggle` (admin-only; validated featureName; audit log) |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Presentation/ServiceCollectionExtensions.cs` | Register `IFeatureFlagService → RedisFeatureFlagService` (singleton); bind `AiFeatureFlagsOptions` from `"AiFeatureFlags"` section |
| MODIFY | `server/src/PropelIQ.Api/appsettings.json` | Add `"AiFeatureFlags"` section (`Defaults` dict, `CriticalFeatures` list) |

---

## Implementation Plan

### 1. `AiFeatureFlagsOptions` — config POCO

```csharp
// ClinicalIntelligence.Application/AI/FeatureFlags/AiFeatureFlagsOptions.cs
public sealed class AiFeatureFlagsOptions
{
    public const string SectionName = "AiFeatureFlags";

    /// <summary>Default enabled state per featureContext when Redis key is absent.</summary>
    public Dictionary<string, bool> Defaults { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ConversationalIntake"] = true,
        ["FactExtraction"] = true,
        ["CodeSuggestion"] = true,
        ["ConflictDetection"] = true,
        ["View360"] = true
    };

    /// <summary>Features that MUST NOT be auto-disabled on SLA breach (e.g., patient-safety-critical features).</summary>
    public List<string> CriticalFeatures { get; set; } = ["ConversationalIntake"];
}
```

### 2. `IFeatureFlagService` + `FeatureDisabledException`

```csharp
// IFeatureFlagService.cs
public interface IFeatureFlagService
{
    /// <summary>Returns true if the AI feature is enabled. Reads from Redis directly — no local cache.</summary>
    Task<bool> IsEnabledAsync(string featureName, CancellationToken ct = default);

    /// <summary>Persists a feature flag update to Redis. Propagation across instances is instant.</summary>
    Task SetFlagAsync(string featureName, bool enabled, CancellationToken ct = default);

    /// <summary>Returns current state of all known feature flags (Redis state merged with defaults).</summary>
    Task<IReadOnlyDictionary<string, bool>> GetAllFlagsAsync(CancellationToken ct = default);
}

// FeatureDisabledException.cs
public sealed class FeatureDisabledException(string featureName)
    : Exception($"AI feature '{featureName}' is currently disabled.")
{
    public string FeatureName { get; } = featureName;
}
```

### 3. `RedisFeatureFlagService` — implementation

```csharp
// RedisFeatureFlagService.cs
public sealed class RedisFeatureFlagService(
    IConnectionMultiplexer redis,
    IOptionsMonitor<AiFeatureFlagsOptions> options,
    ILogger<RedisFeatureFlagService> logger) : IFeatureFlagService
{
    private static string FlagKey(string name) => $"ai:feature:{name}:enabled";

    public async Task<bool> IsEnabledAsync(string featureName, CancellationToken ct)
    {
        try
        {
            var db = redis.GetDatabase();
            var value = await db.StringGetAsync(FlagKey(featureName));

            if (value.HasValue)
                return value.ToString().Equals("true", StringComparison.OrdinalIgnoreCase);

            // Key absent — use config default (fail-open: default true if not specified)
            return options.CurrentValue.Defaults.TryGetValue(featureName, out var defaultVal)
                ? defaultVal
                : true; // Unknown feature = enabled by default (forward-compatible)
        }
        catch (Exception ex)
        {
            // Redis unavailable — fail-open using config default
            logger.LogWarning(ex,
                "Redis unavailable reading feature flag {FeatureName} — using config default",
                featureName);
            return options.CurrentValue.Defaults.TryGetValue(featureName, out var fallback)
                ? fallback
                : true;
        }
    }

    public async Task SetFlagAsync(string featureName, bool enabled, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        await db.StringSetAsync(FlagKey(featureName), enabled ? "true" : "false");
        logger.LogInformation(
            "Feature flag updated: {FeatureName} = {Enabled}", featureName, enabled);
    }

    public async Task<IReadOnlyDictionary<string, bool>> GetAllFlagsAsync(CancellationToken ct)
    {
        var defaults = options.CurrentValue.Defaults;
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, defaultVal) in defaults)
        {
            try
            {
                var db = redis.GetDatabase();
                var value = await db.StringGetAsync(FlagKey(name));
                result[name] = value.HasValue
                    ? value.ToString().Equals("true", StringComparison.OrdinalIgnoreCase)
                    : defaultVal;
            }
            catch
            {
                result[name] = defaultVal; // Redis error → use default
            }
        }

        return result;
    }
}
```

### 4. `AzureOpenAiGateway.ChatCompletionAsync` — feature flag gate (top of method)

```csharp
// ADD at the very top of ChatCompletionAsync, BEFORE any other logic:
if (!await _featureFlagService.IsEnabledAsync(featureContext, ct))
{
    logger.LogInformation(
        "AI feature disabled | featureContext={FeatureContext} correlationId={CorrelationId}",
        featureContext, correlationId);
    throw new FeatureDisabledException(featureContext);
}

// Timing start immediately follows:
var startTimestamp = Stopwatch.GetTimestamp();
// ... (rest of existing logic)
```

### 5. `RecordAndCheckSlaAsync` extension for auto-disable (extend from task_001)

```csharp
// Extend the existing RecordAndCheckSlaAsync helper in AzureOpenAiGateway:
private async Task RecordAndCheckSlaAsync(string featureContext, long latencyMs, CancellationToken ct)
{
    await _latencyRecorder.RecordAsync(featureContext, latencyMs, ct);
    var p95 = await _latencyRecorder.GetP95Async(featureContext, ct);

    if (p95 > _slaOpts.P95ThresholdMs)
    {
        var criticalFeatures = _featureFlagOpts.CurrentValue.CriticalFeatures;

        if (!criticalFeatures.Contains(featureContext, StringComparer.OrdinalIgnoreCase))
        {
            // Auto-disable non-critical AI feature on SLA breach
            await _featureFlagService.SetFlagAsync(featureContext, false, ct);
            logger.LogError(
                "SLA breach AUTO-DISABLE | p95={P95}ms > threshold={Threshold}ms | " +
                "featureContext={FeatureContext} — feature has been disabled automatically. " +
                "Re-enable via POST /api/v1/admin/ai/features/{FeatureContext}/toggle",
                p95, _slaOpts.P95ThresholdMs, featureContext, featureContext);
        }
        else
        {
            logger.LogError(
                "SLA breach CRITICAL feature — NOT auto-disabled | p95={P95}ms > threshold={Threshold}ms " +
                "featureContext={FeatureContext}",
                p95, _slaOpts.P95ThresholdMs, featureContext);
        }
    }
}
```

### 6. Exception handler in `Program.cs` — `FeatureDisabledException` → HTTP 503

```csharp
// In Program.cs, BEFORE app.UseAuthentication():
app.UseExceptionHandler(exceptionApp =>
{
    exceptionApp.Run(async context =>
    {
        var exceptionFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        if (exceptionFeature?.Error is FeatureDisabledException fde)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://propeliq.health/errors/feature-unavailable",
                title = "Feature Unavailable",
                status = 503,
                detail = $"The AI feature '{fde.FeatureName}' is currently disabled.",
                featureName = fde.FeatureName
            });
        }
        // Other exceptions fall through to default handler
    });
});
```

> **Note**: If `Program.cs` already registers a global exception handler (from earlier US stories), extend it with an additional `if/else if` branch for `FeatureDisabledException` rather than replacing the handler.

### 7. Admin endpoints

```csharp
// Add to existing AdminController:

[HttpGet("ai/features")]
[Authorize(Roles = "admin")]
public async Task<IActionResult> GetAiFeatureFlags(CancellationToken ct)
{
    var flags = await _featureFlagService.GetAllFlagsAsync(ct);
    return Ok(new { features = flags });
}

[HttpPost("ai/features/{featureName}/toggle")]
[Authorize(Roles = "admin")]
public async Task<IActionResult> ToggleAiFeature(
    [FromRoute] string featureName,
    [FromBody] ToggleFeatureFlagRequest request,
    CancellationToken ct)
{
    // OWASP A01: Validate featureName is a known registered feature (not arbitrary Redis key write)
    var knownFeatures = _featureFlagOptions.CurrentValue.Defaults.Keys;
    if (!knownFeatures.Contains(featureName, StringComparer.OrdinalIgnoreCase))
        return BadRequest(new { error = "Unknown feature name", featureName });

    await _featureFlagService.SetFlagAsync(featureName, request.Enabled, ct);

    await _auditLogger.LogAsync(new AuditLogEntry
    {
        ActorId = User.GetUserId(),
        ActorType = AuditActorType.Admin,
        ActionType = "AiFeatureFlagToggle",
        TargetEntityId = featureName,
        TargetEntityType = "AiFeatureFlag",
        Payload = JsonSerializer.Serialize(new { featureName, enabled = request.Enabled })
    }, ct);
    await _dbContext.SaveChangesAsync(ct);

    return Ok(new { featureName, enabled = request.Enabled });
}

// Request DTO (in AdminController.cs or adjacent file)
public sealed record ToggleFeatureFlagRequest(bool Enabled);
```

### 8. `appsettings.json` — `AiFeatureFlags` section

```json
"AiFeatureFlags": {
  "Defaults": {
    "ConversationalIntake": true,
    "FactExtraction": true,
    "CodeSuggestion": true,
    "ConflictDetection": true,
    "View360": true
  },
  "CriticalFeatures": ["ConversationalIntake"]
}
```

> `ConversationalIntake` is marked as critical (not auto-disabled on SLA breach) because it is the primary patient-facing feature — disabling it during a latency spike would directly impact patient intake flow.

---

## Current Project State

```
server/src/Modules/ClinicalIntelligence/
└── ClinicalIntelligence.Application/
    └── AI/
        ├── AzureOpenAiGateway.cs               ← MODIFY: inject IFeatureFlagService +
        │                                                   IOptionsMonitor<AiFeatureFlagsOptions>;
        │                                                   feature flag check at top;
        │                                                   extend RecordAndCheckSlaAsync with auto-disable
        └── FeatureFlags/                        ← CREATE all files this task
            ├── IFeatureFlagService.cs
            ├── AiFeatureFlagsOptions.cs
            ├── RedisFeatureFlagService.cs
            └── FeatureDisabledException.cs

server/src/Modules/Admin/
└── Admin.Presentation/
    └── AdminController.cs                      ← MODIFY: add GET /ai/features +
                                                            POST /ai/features/{name}/toggle

server/src/PropelIQ.Api/
├── Program.cs                                  ← MODIFY: register FeatureDisabledException handler
└── appsettings.json                            ← MODIFY: add "AiFeatureFlags" section

server/src/Modules/ClinicalIntelligence/
└── ClinicalIntelligence.Presentation/
    └── ServiceCollectionExtensions.cs          ← MODIFY: register IFeatureFlagService (singleton);
                                                            bind AiFeatureFlagsOptions
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/FeatureFlags/IFeatureFlagService.cs` | `IsEnabledAsync`, `SetFlagAsync`, `GetAllFlagsAsync` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/FeatureFlags/AiFeatureFlagsOptions.cs` | `Dictionary<string, bool> Defaults` + `List<string> CriticalFeatures`; `IOptionsMonitor` hot-reload |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/FeatureFlags/RedisFeatureFlagService.cs` | Redis key `ai:feature:{name}:enabled`; direct Redis read; fail-open fallback to `IOptionsMonitor` defaults on Redis absence or error |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/FeatureFlags/FeatureDisabledException.cs` | `sealed class FeatureDisabledException(string featureName) : Exception`; `FeatureName` property |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/AzureOpenAiGateway.cs` | Inject `IFeatureFlagService` + `IOptionsMonitor<AiFeatureFlagsOptions>`; feature flag check at top of `ChatCompletionAsync`; extend `RecordAndCheckSlaAsync` with non-critical auto-disable + `LogError` |
| MODIFY | `server/src/PropelIQ.Api/Program.cs` | Add `UseExceptionHandler` lambda (or extend existing) — `FeatureDisabledException` → 503 + RFC 7807 JSON |
| MODIFY | `server/src/Modules/Admin/Admin.Presentation/AdminController.cs` | `GET /api/v1/admin/ai/features`; `POST /api/v1/admin/ai/features/{featureName}/toggle` with `featureName` validation, `SetFlagAsync`, audit log |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Presentation/ServiceCollectionExtensions.cs` | `AddSingleton<IFeatureFlagService, RedisFeatureFlagService>()` + `Configure<AiFeatureFlagsOptions>(...)` |
| MODIFY | `server/src/PropelIQ.Api/appsettings.json` | Add `"AiFeatureFlags"` section with 5 default features + `CriticalFeatures` array |

---

## External References

- [IOptionsMonitor<T> — hot-reload configuration pattern, .NET 8](https://learn.microsoft.com/en-us/dotnet/core/extensions/options#ioptionsmonitor)
- [ASP.NET Core UseExceptionHandler with inline handler](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling#useexceptionhandler-with-a-request-handler)
- [RFC 7807 — Problem Details for HTTP APIs](https://datatracker.ietf.org/doc/html/rfc7807)
- [StackExchange.Redis StringGetAsync / StringSetAsync](https://stackexchange.github.io/StackExchange.Redis/Basics.html)
- [OWASP A01 — Broken Access Control: unvalidated resource IDs](https://owasp.org/Top10/A01_2021-Broken_Access_Control/)
- [design.md — TR-025 feature flags; AIR-Q04 schema validity](../.propel/context/docs/design.md)

---

## Build Commands

```powershell
# Restore + build
dotnet restore server/PropelIQ.slnx
dotnet build server/PropelIQ.slnx --no-restore

# Run unit tests (tag filter)
dotnet test server/PropelIQ.slnx --no-build --filter "Category=US032_FeatureFlags"
```

---

## Implementation Validation Strategy

- [ ] Unit tests pass (xUnit + Moq)
- [ ] **[AI Tasks]** Feature flag disabled: `IsEnabledAsync("FactExtraction")` returns `false` → `ChatCompletionAsync` throws `FeatureDisabledException` immediately; `IAzureOpenAIClient.GetChatClient` NOT called (zero AI cost)
- [ ] **[AI Tasks]** Feature flag enabled: `IsEnabledAsync("FactExtraction")` returns `true` → gateway proceeds normally
- [ ] **[AI Tasks]** Redis key absent: `GetAsync("ai:feature:FactExtraction:enabled")` returns null → `IsEnabledAsync` returns config default (`true`); no exception thrown
- [ ] **[AI Tasks]** Redis unavailable (exception): `GetAsync` throws → catch returns config default → `LogWarning` called with Redis error; feature remains enabled
- [ ] **[AI Tasks]** `FeatureDisabledException` → HTTP 503 with JSON body containing `status=503`, `featureName`, `title="Feature Unavailable"`; NOT 404
- [ ] **[AI Tasks]** Admin toggle with unknown `featureName` → HTTP 400 with `{"error": "Unknown feature name", ...}`; `SetFlagAsync` NOT called (OWASP A01 guard)
- [ ] **[AI Tasks]** Admin toggle with valid `featureName` + admin JWT → `SetFlagAsync` called; audit log entry `"AiFeatureFlagToggle"` staged; `SaveChangesAsync` called; 200 returned with `{featureName, enabled}`
- [ ] **[AI Tasks]** Admin toggle with non-admin JWT → HTTP 403 (controller `[Authorize(Roles = "admin")]`)
- [ ] **[AI Tasks]** SLA auto-disable: mock `GetP95Async` returns 5001ms; `featureContext = "FactExtraction"` (non-critical) → `SetFlagAsync("FactExtraction", false)` called; `LogError` called with "AUTO-DISABLE" in message
- [ ] **[AI Tasks]** SLA breach for critical feature (`ConversationalIntake`): `GetP95Async` returns 5001ms → `SetFlagAsync` NOT called; `LogError` called with "CRITICAL feature — NOT auto-disabled"

---

## Implementation Checklist

- [ ] CREATE `AiFeatureFlagsOptions` with `Defaults` dictionary (5 features, all `true`) + `CriticalFeatures` list (`["ConversationalIntake"]`); `SectionName = "AiFeatureFlags"`; hot-reload via `IOptionsMonitor`
- [ ] CREATE `IFeatureFlagService` with `IsEnabledAsync` + `SetFlagAsync` + `GetAllFlagsAsync`; `FeatureDisabledException` with `FeatureName` property
- [ ] CREATE `RedisFeatureFlagService` — Redis key `ai:feature:{name}:enabled`; direct `StringGetAsync` on every call; fallback to `IOptionsMonitor` defaults on key-absent; catch Redis exceptions and fall-open to config default; log Warning on Redis error
- [ ] MODIFY `AzureOpenAiGateway.ChatCompletionAsync` — (a) `IFeatureFlagService.IsEnabledAsync(featureContext)` check at TOP (before timing start); (b) throw `FeatureDisabledException(featureContext)` if disabled; (c) inject `IOptionsMonitor<AiFeatureFlagsOptions>`; (d) extend `RecordAndCheckSlaAsync` to check `CriticalFeatures` and call `SetFlagAsync` on breach for non-critical features + `LogError`
- [ ] MODIFY `Program.cs` — add/extend `UseExceptionHandler` to map `FeatureDisabledException` → 503 + RFC 7807 `application/problem+json` response body
- [ ] MODIFY `AdminController` — `GET /ai/features` (admin; returns all flag states); `POST /ai/features/{featureName}/toggle` (admin; validates featureName in known set; `SetFlagAsync`; audit log `"AiFeatureFlagToggle"`; `SaveChangesAsync`)
- [ ] MODIFY `ServiceCollectionExtensions.cs` — `AddSingleton<IFeatureFlagService, RedisFeatureFlagService>()` + `Configure<AiFeatureFlagsOptions>(...)`
- [ ] MODIFY `appsettings.json` — add `"AiFeatureFlags"` section
- [ ] **[AI Tasks - MANDATORY]** Implement and test feature flag gate in gateway; verify zero AI cost on disabled flag; verify 503 + RFC 7807 response
- [ ] **[AI Tasks - MANDATORY]** Verify TR-025: flag toggle propagates within <1ms (Redis read on next request); verify SLA auto-disable correctly skips CriticalFeatures; verify admin featureName validation (OWASP A01)

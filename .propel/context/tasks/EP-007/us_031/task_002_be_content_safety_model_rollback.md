# Task - task_002_be_content_safety_model_rollback

## Requirement Reference

- **User Story**: US_031 — Circuit Breaker, Content Safety & Model Rollback
- **Story Location**: `.propel/context/tasks/EP-007/us_031/us_031.md`
- **Acceptance Criteria**:
  - AC-3: After the AI model generates a response, the content safety filter scans for PHI leakage, harmful content, and hallucinated medical advice and blocks responses failing any check per AIR-O03 (mapped as AIR-S04 in design.md: content filtering requirement).
  - AC-4: When a content safety violation is detected, the system returns a safe generic response, logs the violation with the SHA256 hash of the original response (not the content), and increments a safety violation counter per AIR-O03.
  - AC-5: When an admin triggers model rollback, the system switches to the previous model deployment within 60 seconds without service interruption per AIR-O04 (mapped as AIR-O03 in design.md: model version rollback within 1 hour).
- **Edge Cases**:
  - Multilingual responses: `ContentSafetyFilter` normalizes to Unicode NFC before pattern matching; character-class regex patterns (not word-boundary English assumptions) for PHI detection; harmful content keyword list includes transliterations of top clinical advisory terms in Spanish, French, Portuguese — configurable via `ContentSafetyOptions.AdditionalHarmKeywords`.

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
| Caching | Upstash Redis (`IDistributedCache`) | Cloud |
| Hashing | `System.Security.Cryptography.SHA256` | Built-in (.NET 8) |
| Logging | Serilog + `IAuditLogger` (US_026) | 3.x / custom |
| Testing - Unit | xUnit + Moq | 2.x / 4.x |

> All content safety logic uses built-in `System.Text.RegularExpressions` and `System.Security.Cryptography` — no paid third-party dependencies (NFR-015). The design.md notes Azure OpenAI Content Safety as a potential backing service (Assumption 4), but the AC scope here is a custom filter layer in front of that; Azure Content Safety can be layered on top in a future US without changing this interface.

---

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | Yes |
| **AIR Requirements** | AIR-O03, AIR-S04 |
| **AI Pattern** | AI Gateway — output safety filter + model version management |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | `appsettings.json` → `ContentSafety:PhiPatterns`, `ContentSafety:HarmKeywords`, `ContentSafety:MedicalAdvicePatterns`; hot-reload via `IOptionsMonitor<ContentSafetyOptions>` |
| **Model Provider** | Azure OpenAI GPT-4 Turbo |

### CRITICAL: AI Implementation Requirements

- **MUST** invoke `IContentSafetyFilter.EvaluateAsync(response.Content)` AFTER the GPT call succeeds and AFTER `IsTruncated` is determined but BEFORE `CacheSuccessfulResponseAsync` writes to Redis. A blocked response MUST NOT be cached as a "successful" response (would serve unsafe stale content in degradation mode).
- **MUST** log the SHA256 hash of the response content (`Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(response.Content)))`) in the audit payload — NEVER the raw response content (HIPAA + OWASP A04).
- **MUST** increment Redis counter `ai:safety_violations:{featureContext}` atomically using `IDatabase.StringIncrementAsync(key)` (Upstash Redis via `IConnectionMultiplexer`). This is a separate operation from `IDistributedCache` — use `IConnectionMultiplexer` directly for atomic increment.
- **MUST** return a safe static `GatewayResponse` on violation (content from `ContentSafetyOptions.SafeResponseMessage`). Never return any fragment of the blocked AI response to the caller.
- **MUST** use `IOptionsMonitor<ContentSafetyOptions>` (not `IOptions<>`) so safety patterns are hot-reloadable from `appsettings.json` without redeploy — consistent with `IOptionsMonitor` convention established in US_029.
- **MUST NOT** block the content safety filter evaluation path with `await` on audit log save if it adds >10ms — stage the audit log entry via `IAuditLogger.LogAsync` (fire-and-forget save is acceptable for the counter increment; the audit log itself must be saved atomically by the caller's next `SaveChangesAsync`).
- **Model rollback MUST** operate entirely through Redis key updates — no `appsettings.json` file writes, no application restarts. All in-flight requests at the time of rollback continue with the old deployment name (they already have the `chatClient` reference). Only new requests after the Redis key update use the rolled-back deployment.

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

Implement two independent gateway capabilities that both post-process AI responses:

**1. Content Safety Filter (AC-3, AC-4):**
A three-layer output filter applied to every `GatewayResponse.Content` before it leaves the gateway:
- **Layer 1 — PHI leakage detection**: regex patterns for SSN (`\d{3}[-\s]\d{2}[-\s]\d{4}`), 10-digit phone numbers, email addresses, date-of-birth date patterns with year hints (e.g., `19\d{2}` or `20\d{2}` adjacent to month/day patterns). These patterns detect PHI that the LLM may have hallucinated or reproduced from training data.
- **Layer 2 — Harmful content detection**: keyword blocklist including clinical terms associated with self-harm instructions, explicit violence, and substance misuse advice. Language-agnostic character-class patterns.
- **Layer 3 — Medical advice hallucination detection**: patterns catching direct prescriptive advice (e.g., "you should take", "I recommend you take", "your diagnosis is", "you have [disease]") when the `featureContext` is NOT `"ConversationalIntake"` (intake context explicitly uses directive phrasing — excluded from this check).

On violation, the filter returns a `ContentSafetyViolation` record. `AzureOpenAiGateway` handles violations by: staging audit log, incrementing Redis counter, returning safe static response, and NOT caching the blocked response.

**2. Model Version Rollback (AC-5):**
A Redis-backed deployment override mechanism allowing zero-downtime model switching:
- `IModelVersionService`: `ActivateDeploymentAsync(name, ct)` — sets `ai:deployment:inference:previous` to current, then `ai:deployment:inference:current` to new name. `RollbackAsync(ct)` — swaps current back to previous. `GetActiveDeploymentAsync(ct)` — reads `ai:deployment:inference:current`; falls back to `AzureOpenAiOptions.InferenceDeploymentName` if key absent.
- `AzureOpenAiGateway.ChatCompletionAsync` calls `GetActiveDeploymentAsync(ct)` each request (Redis read < 1ms) to get the live deployment name before constructing `chatClient`.
- Admin endpoint `POST /api/v1/admin/ai/deployment/rollback` — requires `[Authorize(Roles = "admin")]`; calls `IModelVersionService.RollbackAsync(ct)`; returns 200 with `{"activeDeployment": "<name>"}`.
- "Within 60 seconds without service interruption": Redis key update propagates to all instances within milliseconds; no restarts needed; in-flight requests complete on old deployment (their `chatClient` is already constructed).

---

## Dependent Tasks

- **task_001_be_azure_openai_gateway_hardening.md** (US_030) — `AzureOpenAiGateway.ChatCompletionAsync`, `GatewayResponse`, `AzureOpenAiOptions` must exist.
- **task_002_be_ai_health_check_degradation.md** (US_030) — `CacheSuccessfulResponseAsync` (fire-and-forget cache write) must exist in `AzureOpenAiGateway` — content safety filter inserts BEFORE this call.
- **task_001_be_circuit_breaker_polly.md** (US_031) — can proceed in parallel; task_001 modifies the area around the GPT call (wraps with pipeline); this task modifies the area after the GPT call (content safety) and the deployment name resolution at the start.
- **task_002_be_user_lifecycle_api.md** (US_025) — `AdminController` with JWT `[Authorize(Roles = "admin")]` must exist as the parent for the new rollback endpoint.

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Safety/IContentSafetyFilter.cs` | `Task<ContentSafetyViolation?> EvaluateAsync(string content, string featureContext, CancellationToken ct)` — returns null if safe |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Safety/ContentSafetyViolation.cs` | `record(SafetyViolationType ViolationType, string PatternId, string ResponseHash)` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Safety/ContentSafetyOptions.cs` | `IOptionsMonitor`-backed POCO: `PhiPatterns`, `HarmKeywords`, `MedicalAdvicePatterns`, `SafeResponseMessage`, `AdditionalHarmKeywords`; `ExcludedFeatureContextsForMedicalAdvice` list (default `["ConversationalIntake"]`) |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Safety/ContentSafetyFilter.cs` | 3-layer filter: PHI regex + harm keyword + medical advice (context-excluded); Unicode NFC pre-normalize; `RegexMatchTimeoutException` → treat as Safe (ReDoS guard); `IOptionsMonitor<ContentSafetyOptions>` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/ModelVersion/IModelVersionService.cs` | `Task ActivateDeploymentAsync(string name, ct)`; `Task RollbackAsync(ct)`; `Task<string> GetActiveDeploymentAsync(ct)` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/ModelVersion/RedisModelVersionService.cs` | Redis keys `ai:deployment:inference:current` + `ai:deployment:inference:previous`; `GetActiveDeploymentAsync` reads current key, falls back to `AzureOpenAiOptions.InferenceDeploymentName` |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/AzureOpenAiGateway.cs` | Inject `IContentSafetyFilter`, `IConnectionMultiplexer`, `IModelVersionService`; (a) read `GetActiveDeploymentAsync()` before constructing `chatClient`; (b) after GPT call, invoke `EvaluateAsync(content, featureContext)` before cache write; on violation: stage audit log + Redis INCR + return safe response (no cache) |
| MODIFY | `server/src/Modules/Admin/Admin.Presentation/AdminController.cs` | Add `POST /api/v1/admin/ai/deployment/rollback` — `[Authorize(Roles = "admin")]`; calls `IModelVersionService.RollbackAsync(ct)`; returns `{"activeDeployment": "<name>"}` |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Presentation/ServiceCollectionExtensions.cs` | Register `IContentSafetyFilter → ContentSafetyFilter` (transient); `IModelVersionService → RedisModelVersionService` (singleton); bind `ContentSafetyOptions` from `"ContentSafety"` config section |
| MODIFY | `server/src/PropelIQ.Api/appsettings.json` | Add `"ContentSafety"` section with PHI patterns, harm keywords, medical advice patterns, safe response message |

---

## Implementation Plan

### 1. `ContentSafetyOptions` — config POCO

```csharp
// ClinicalIntelligence.Application/AI/Safety/ContentSafetyOptions.cs
public sealed class ContentSafetyOptions
{
    public const string SectionName = "ContentSafety";

    public List<SafetyPatternEntry> PhiPatterns { get; set; } = [];
    public List<SafetyPatternEntry> HarmKeywords { get; set; } = [];
    public List<SafetyPatternEntry> MedicalAdvicePatterns { get; set; } = [];

    /// <summary>Feature contexts where medical advice patterns are NOT applied (e.g., ConversationalIntake uses directive phrasing legitimately).</summary>
    public List<string> ExcludedFeatureContextsForMedicalAdvice { get; set; } = ["ConversationalIntake"];

    public string SafeResponseMessage { get; set; } =
        "I'm unable to provide that response. Please consult your clinical team or try again.";

    /// <summary>Additional harm keywords for multilingual support — appended to HarmKeywords at runtime.</summary>
    public List<string> AdditionalHarmKeywords { get; set; } = [];
}

public sealed record SafetyPatternEntry(
    string Id,           // e.g. "PHI-001"
    string Pattern,      // regex
    string Description   // for audit log (not the content — the label only)
);
```

### 2. `ContentSafetyViolation` + `SafetyViolationType`

```csharp
public enum SafetyViolationType { PhiLeakage, HarmfulContent, MedicalAdviceHallucination }

public sealed record ContentSafetyViolation(
    SafetyViolationType ViolationType,
    string PatternId,
    string ResponseHash  // SHA256 hex of response content — NOT the content itself
);
```

### 3. `ContentSafetyFilter` — 3-layer implementation

```csharp
public sealed class ContentSafetyFilter(
    IOptionsMonitor<ContentSafetyOptions> options) : IContentSafetyFilter
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(50);

    public Task<ContentSafetyViolation?> EvaluateAsync(
        string content, string featureContext, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(content))
            return Task.FromResult<ContentSafetyViolation?>(null);

        var opts = options.CurrentValue;

        // Pre-normalize: Unicode NFC (multilingual bypass defense, consistent with US_029)
        var normalized = content.Normalize(NormalizationForm.FormC);
        var responseHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));

        // Layer 1: PHI leakage patterns
        foreach (var entry in opts.PhiPatterns)
        {
            if (MatchesSafely(normalized, entry.Pattern))
                return Task.FromResult<ContentSafetyViolation?>(
                    new(SafetyViolationType.PhiLeakage, entry.Id, responseHash));
        }

        // Layer 2: Harmful content keywords
        foreach (var entry in opts.HarmKeywords)
        {
            if (MatchesSafely(normalized, entry.Pattern))
                return Task.FromResult<ContentSafetyViolation?>(
                    new(SafetyViolationType.HarmfulContent, entry.Id, responseHash));
        }

        // Layer 3: Medical advice hallucination (context-excluded per ExcludedFeatureContextsForMedicalAdvice)
        if (!opts.ExcludedFeatureContextsForMedicalAdvice.Contains(featureContext))
        {
            foreach (var entry in opts.MedicalAdvicePatterns)
            {
                if (MatchesSafely(normalized, entry.Pattern))
                    return Task.FromResult<ContentSafetyViolation?>(
                        new(SafetyViolationType.MedicalAdviceHallucination, entry.Id, responseHash));
            }
        }

        return Task.FromResult<ContentSafetyViolation?>(null); // Safe
    }

    private static bool MatchesSafely(string input, string pattern)
    {
        try
        {
            return Regex.IsMatch(input, pattern,
                RegexOptions.IgnoreCase | RegexOptions.Singleline,
                matchTimeout: RegexTimeout);
        }
        catch (RegexMatchTimeoutException)
        {
            // ReDoS guard — treat timeout as no-match (safe), log via caller
            return false;
        }
    }
}
```

### 4. `AzureOpenAiGateway.ChatCompletionAsync` — content safety insertion point

```csharp
// After existing GPT call and IsTruncated detection, BEFORE CacheSuccessfulResponseAsync:

var violation = await _contentSafetyFilter.EvaluateAsync(content, featureContext, ct);

if (violation is not null)
{
    // AC-4: Stage audit log with response HASH not content (AIR-S03 + OWASP A04)
    await _auditLogger.LogAsync(new AuditLogEntry
    {
        ActorId = _currentActorId,
        ActorType = _currentActorType,
        ActionType = "ContentSafetyViolation",
        TargetEntityId = correlationId.ToString(),
        TargetEntityType = "AiResponse",
        IpAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
        Payload = JsonSerializer.Serialize(new
        {
            violationType = violation.ViolationType.ToString(),
            patternId = violation.PatternId,
            responseHash = violation.ResponseHash,    // SHA256 — not content
            featureContext
        })
    }, ct);

    // AC-4: Atomic Redis counter increment
    var counterKey = $"ai:safety_violations:{featureContext}";
    await _connectionMultiplexer.GetDatabase()
        .StringIncrementAsync(counterKey);

    logger.LogWarning(
        "AI content safety violation | type={ViolationType} patternId={PatternId} " +
        "featureContext={FeatureContext} correlationId={CorrelationId} responseHash={Hash}",
        violation.ViolationType, violation.PatternId, featureContext, correlationId,
        violation.ResponseHash);  // hash is safe to log

    // Return safe static response — NEVER return any part of the blocked content
    return new GatewayResponse(
        _opts.ContentSafety.SafeResponseMessage,
        inputTokens, outputTokens,
        IsTruncated: false,
        featureContext);
    // NOTE: CacheSuccessfulResponseAsync NOT called here — blocked response must not be cached
}

// Only reaches here if content is safe — then cache write proceeds
_ = CacheSuccessfulResponseAsync(featureContext, gatewayResponse);
```

> **Note on `IConnectionMultiplexer` vs `IDistributedCache`:** `IDistributedCache` does not expose `StringIncrementAsync`. The `IConnectionMultiplexer` (StackExchange.Redis) is already registered in `Program.cs` (established in the original infrastructure setup). Injecting it here adds no new dependency — it is the same Redis connection used by the distributed cache under the hood. Only the `IDatabase.StringIncrementAsync` operation is used for the counter; all other Redis operations continue to use `IDistributedCache`.

### 5. `IModelVersionService` + `RedisModelVersionService`

```csharp
// ClinicalIntelligence.Application/AI/ModelVersion/IModelVersionService.cs
public interface IModelVersionService
{
    Task ActivateDeploymentAsync(string deploymentName, CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
    Task<string> GetActiveDeploymentAsync(CancellationToken ct = default);
}

// ClinicalIntelligence.Application/AI/ModelVersion/RedisModelVersionService.cs
public sealed class RedisModelVersionService(
    IConnectionMultiplexer redis,
    IOptions<AzureOpenAiOptions> options,
    ILogger<RedisModelVersionService> logger) : IModelVersionService
{
    private const string CurrentKey = "ai:deployment:inference:current";
    private const string PreviousKey = "ai:deployment:inference:previous";

    public async Task ActivateDeploymentAsync(string deploymentName, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var current = await db.StringGetAsync(CurrentKey);
        if (current.HasValue)
            await db.StringSetAsync(PreviousKey, current); // preserve for rollback
        await db.StringSetAsync(CurrentKey, deploymentName);
        logger.LogInformation(
            "AI deployment activated: {DeploymentName} (previous: {Previous})",
            deploymentName, current.HasValue ? (string)current! : options.Value.InferenceDeploymentName);
    }

    public async Task RollbackAsync(CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var previous = await db.StringGetAsync(PreviousKey);
        if (!previous.HasValue)
        {
            // No previous stored — fall back to config default
            await db.StringSetAsync(CurrentKey, options.Value.InferenceDeploymentName);
            logger.LogWarning(
                "AI deployment rollback: no previous deployment in Redis, reverting to config default {Default}",
                options.Value.InferenceDeploymentName);
            return;
        }
        var current = await db.StringGetAsync(CurrentKey);
        await db.StringSetAsync(PreviousKey, current); // swap
        await db.StringSetAsync(CurrentKey, previous);
        logger.LogInformation(
            "AI deployment rolled back to: {RolledBack} (was: {Was})",
            (string)previous!, current.HasValue ? (string)current! : "unknown");
    }

    public async Task<string> GetActiveDeploymentAsync(CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var current = await db.StringGetAsync(CurrentKey);
        return current.HasValue
            ? (string)current!
            : options.Value.InferenceDeploymentName; // config default fallback
    }
}
```

### 6. `AzureOpenAiGateway` — deployment name resolution

```csharp
// Replace inside ChatCompletionAsync (top of method, after availability guard):
// OLD: var chatClient = client.GetChatClient(_opts.InferenceDeploymentName);
// NEW: read active deployment from Redis each call (sub-millisecond Redis GET)
var activeDeployment = await _modelVersionService.GetActiveDeploymentAsync(ct);
var chatClient = aiClient.GetChatClient(activeDeployment);
```

### 7. Admin rollback endpoint

```csharp
// Add to existing AdminController (Admin.Presentation):

[HttpPost("ai/deployment/rollback")]
[Authorize(Roles = "admin")]
public async Task<IActionResult> RollbackAiDeployment(CancellationToken ct)
{
    await _modelVersionService.RollbackAsync(ct);
    var active = await _modelVersionService.GetActiveDeploymentAsync(ct);

    await _auditLogger.LogAsync(new AuditLogEntry
    {
        ActorId = User.GetUserId(),        // from JWT claim
        ActorType = AuditActorType.Admin,
        ActionType = "AiDeploymentRollback",
        TargetEntityId = "AiGateway",
        TargetEntityType = "DeploymentConfiguration",
        Payload = JsonSerializer.Serialize(new { activeDeployment = active })
    }, ct);
    await _dbContext.SaveChangesAsync(ct);

    return Ok(new { activeDeployment = active });
}
```

### 8. `appsettings.json` — `ContentSafety` section

```json
"ContentSafety": {
  "SafeResponseMessage": "I'm unable to provide that response. Please consult your clinical team or try again.",
  "ExcludedFeatureContextsForMedicalAdvice": ["ConversationalIntake"],
  "PhiPatterns": [
    { "Id": "PHI-001", "Pattern": "\\b\\d{3}[\\s\\-]\\d{2}[\\s\\-]\\d{4}\\b", "Description": "US Social Security Number format" },
    { "Id": "PHI-002", "Pattern": "\\b(\\+1[\\s\\-]?)?\\(?\\d{3}\\)?[\\s\\-]\\d{3}[\\s\\-]\\d{4}\\b", "Description": "US phone number" },
    { "Id": "PHI-003", "Pattern": "\\b[A-Za-z0-9._%+\\-]+@[A-Za-z0-9.\\-]+\\.[A-Za-z]{2,}\\b", "Description": "Email address" },
    { "Id": "PHI-004", "Pattern": "\\b(19|20)\\d{2}[\\-\\/](0?[1-9]|1[0-2])[\\-\\/](0?[1-9]|[12]\\d|3[01])\\b", "Description": "Date of birth pattern" }
  ],
  "HarmKeywords": [
    { "Id": "HARM-001", "Pattern": "how\\s+to\\s+(overdose|end\\s+(your|my)\\s+life|harm\\s+(yourself|myself))", "Description": "Self-harm instruction" },
    { "Id": "HARM-002", "Pattern": "(buy|obtain|acquire)\\s+(illegal|controlled)\\s+(drug|substance)", "Description": "Illegal substance acquisition" }
  ],
  "MedicalAdvicePatterns": [
    { "Id": "MADV-001", "Pattern": "you\\s+should\\s+take\\s+[A-Za-z]+", "Description": "Prescriptive medication advice" },
    { "Id": "MADV-002", "Pattern": "I\\s+recommend\\s+(you\\s+)?(take|stop|start)\\s+[A-Za-z]+", "Description": "Prescriptive recommendation" },
    { "Id": "MADV-003", "Pattern": "your\\s+diagnosis\\s+is|you\\s+have\\s+(cancer|diabetes|hypertension|[A-Za-z]+\\s+disease)", "Description": "Diagnosis statement" }
  ],
  "AdditionalHarmKeywords": []
}
```

---

## Current Project State

```
server/src/Modules/ClinicalIntelligence/
└── ClinicalIntelligence.Application/
    └── AI/
        ├── AzureOpenAiGateway.cs               ← MODIFY: inject IContentSafetyFilter,
        │                                                   IConnectionMultiplexer, IModelVersionService;
        │                                                   add deployment resolution at top;
        │                                                   add safety filter after GPT call
        ├── Safety/                              ← CREATE all files this task
        │   ├── IContentSafetyFilter.cs
        │   ├── ContentSafetyViolation.cs
        │   ├── ContentSafetyOptions.cs
        │   └── ContentSafetyFilter.cs
        └── ModelVersion/                        ← CREATE all files this task
            ├── IModelVersionService.cs
            └── RedisModelVersionService.cs

server/src/Modules/Admin/
└── Admin.Presentation/
    └── AdminController.cs                      ← MODIFY: add POST /ai/deployment/rollback

server/src/Modules/ClinicalIntelligence/
└── ClinicalIntelligence.Presentation/
    └── ServiceCollectionExtensions.cs          ← MODIFY: register IContentSafetyFilter (transient),
                                                            IModelVersionService (singleton)

server/src/PropelIQ.Api/
└── appsettings.json                            ← MODIFY: add "ContentSafety" section
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Safety/IContentSafetyFilter.cs` | `Task<ContentSafetyViolation?> EvaluateAsync(string content, string featureContext, ct)` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Safety/ContentSafetyViolation.cs` | `record(SafetyViolationType, PatternId, ResponseHash)` + `SafetyViolationType` enum |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Safety/ContentSafetyOptions.cs` | Options POCO with `PhiPatterns`, `HarmKeywords`, `MedicalAdvicePatterns`, `SafeResponseMessage`, `ExcludedFeatureContextsForMedicalAdvice`; bound via `IOptionsMonitor` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Safety/ContentSafetyFilter.cs` | 3-layer filter (PHI + harm + medical advice); Unicode NFC pre-normalize; ReDoS 50ms timeout guard; `IOptionsMonitor<ContentSafetyOptions>` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/ModelVersion/IModelVersionService.cs` | `ActivateDeploymentAsync`, `RollbackAsync`, `GetActiveDeploymentAsync` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/ModelVersion/RedisModelVersionService.cs` | Redis keys `ai:deployment:inference:current` / `previous`; config fallback in `GetActiveDeploymentAsync` |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/AzureOpenAiGateway.cs` | Inject `IContentSafetyFilter`, `IConnectionMultiplexer`, `IModelVersionService`; read `GetActiveDeploymentAsync()` before `chatClient` creation; invoke `EvaluateAsync()` post-GPT pre-cache; on violation: audit log + Redis INCR + return safe response |
| MODIFY | `server/src/Modules/Admin/Admin.Presentation/AdminController.cs` | Add `[HttpPost("ai/deployment/rollback")]` — admin-only; calls `RollbackAsync()`; audit logs the rollback; returns `{"activeDeployment": "..."}` |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Presentation/ServiceCollectionExtensions.cs` | `services.AddTransient<IContentSafetyFilter, ContentSafetyFilter>()` + `services.AddSingleton<IModelVersionService, RedisModelVersionService>()` + `services.Configure<ContentSafetyOptions>(config.GetSection("ContentSafety"))` |
| MODIFY | `server/src/PropelIQ.Api/appsettings.json` | Add `"ContentSafety"` section with PHI patterns (4), harm keywords (2), medical advice patterns (3), safe message, excluded contexts |

---

## External References

- [System.Security.Cryptography.SHA256 — HashData](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.sha256.hashdata)
- [StackExchange.Redis IDatabase.StringIncrementAsync](https://stackexchange.github.io/StackExchange.Redis/Basics.html)
- [OWASP A04 — Insecure Design: AI response logging](https://owasp.org/Top10/A04_2021-Insecure_Design/)
- [HIPAA PHI Safe Harbor de-identification standard](https://www.hhs.gov/hipaa/for-professionals/privacy/special-topics/de-identification/index.html)
- [IOptionsMonitor hot-reload pattern — established in US_029](../.propel/context/tasks/EP-006/us_029/task_001_ai_prompt_safety_rag_acl.md)
- [design.md — AIR-S04 content filtering](../.propel/context/docs/design.md)

---

## Build Commands

```powershell
# Restore + build
dotnet restore server/PropelIQ.slnx
dotnet build server/PropelIQ.slnx --no-restore

# Run unit tests
dotnet test server/PropelIQ.slnx --no-build --filter "Category=US031"
```

---

## Implementation Validation Strategy

- [ ] Unit tests pass (xUnit + Moq)
- [ ] **[AI Tasks]** PHI pattern test: response containing `"SSN: 123-45-6789"` → `ContentSafetyViolation(PhiLeakage, "PHI-001", hash)` returned; safe generic message returned to caller
- [ ] **[AI Tasks]** Medical advice pattern (FactExtraction context): response containing `"You should take metformin"` → blocked; same pattern in ConversationalIntake context → NOT blocked
- [ ] **[AI Tasks]** Audit log payload: confirm `responseHash` is SHA256 hex string; confirm raw `content` string NOT present in payload
- [ ] **[AI Tasks]** Redis counter: `StringIncrementAsync("ai:safety_violations:FactExtraction")` called once per violation — verified via mock assert on `IConnectionMultiplexer.GetDatabase()`
- [ ] **[AI Tasks]** Blocked response NOT cached: mock `IDistributedCache.SetStringAsync` must NOT be called when violation is detected
- [ ] **[AI Tasks]** Model rollback: `RollbackAsync` sets `current` ← `previous`; `GetActiveDeploymentAsync` returns the rolled-back name; rollback when no `previous` key → returns config default
- [ ] **[AI Tasks]** Admin rollback endpoint: `POST /api/v1/admin/ai/deployment/rollback` with non-admin JWT → 403; with admin JWT → 200 + `{"activeDeployment": "..."}`

---

## Implementation Checklist

- [ ] CREATE `ContentSafetyOptions` (IOptionsMonitor-bound, 5 properties including `ExcludedFeatureContextsForMedicalAdvice`); `SafetyPatternEntry` record; `SafetyViolationType` enum; `ContentSafetyViolation` record with `ResponseHash` field
- [ ] CREATE `ContentSafetyFilter` — 3 layers (PHI regex, harm keywords, medical advice with context exclusion); Unicode NFC pre-normalize; `RegexMatchTimeoutException` → false (safe, not thrown); `IOptionsMonitor.CurrentValue` for hot-reload
- [ ] CREATE `IModelVersionService` + `RedisModelVersionService` — Redis keys `ai:deployment:inference:current`/`previous`; `RollbackAsync` swaps current↔previous; `GetActiveDeploymentAsync` falls back to `AzureOpenAiOptions.InferenceDeploymentName` when key absent
- [ ] MODIFY `AzureOpenAiGateway.ChatCompletionAsync` — (a) resolve deployment via `_modelVersionService.GetActiveDeploymentAsync(ct)` before `GetChatClient()`; (b) after GPT call, invoke `_contentSafetyFilter.EvaluateAsync(content, featureContext, ct)`; on violation: stage audit log + `StringIncrementAsync(counter)` + return `GatewayResponse(safeMessage)` WITHOUT calling `CacheSuccessfulResponseAsync`
- [ ] MODIFY `AdminController` — add `POST /api/v1/admin/ai/deployment/rollback` (admin-only); call `_modelVersionService.RollbackAsync(ct)`; audit log `"AiDeploymentRollback"` with `activeDeployment` in payload; return 200 + deployment name
- [ ] MODIFY `ServiceCollectionExtensions.cs` — register `IContentSafetyFilter` (transient), `IModelVersionService` (singleton), bind `ContentSafetyOptions` from `"ContentSafety"` section
- [ ] MODIFY `appsettings.json` — add `"ContentSafety"` section with 4 PHI patterns, 2 harm patterns, 3 medical advice patterns, `SafeResponseMessage`, `ExcludedFeatureContextsForMedicalAdvice`
- [ ] **[AI Tasks - MANDATORY]** Implement and test content safety filter; verify SHA256 hash in audit log; verify safe response returned on violation
- [ ] **[AI Tasks - MANDATORY]** Verify AIR-O03/AIR-S04: all three violation types blocked; multilingual normalize; blocked responses NOT cached; admin rollback operates within 60s (Redis propagation instant)

# Task - task_001_be_azure_openai_gateway_hardening

## Requirement Reference

- **User Story**: US_030 — Azure OpenAI Client & Gateway Middleware
- **Story Location**: `.propel/context/tasks/EP-007/us_030/us_030.md`
- **Acceptance Criteria**:
  - AC-1: Gateway authenticates with Azure OpenAI using managed identity credentials and routes to the configured model endpoint (GPT-4 Turbo for inference, text-embedding-3-small for embeddings) per TR-006 and TR-007.
  - AC-2: Gateway enforces max 4096 output tokens per request and appends system prompts appropriate to the feature context per AIR-O01.
  - AC-3: Gateway retries up to 3 times with exponential backoff (1s, 2s, 4s) on transient errors (HTTP 429, 503) per AIR-S03.
  - AC-4: Request/response metadata (model, tokens_used, latency_ms, status) is logged for observability without logging PHI content per TR-006.
- **Edge Cases**:
  - Token budget exceeded mid-response: `MaxOutputTokenCount` cap on the Azure OpenAI request causes the model to truncate; gateway detects `FinishReason = Length`, sets `GatewayResponse.IsTruncated = true`, logs the truncation event.
  - Model version changes: `AzureOpenAiOptions.InferenceDeploymentName` and `EmbeddingDeploymentName` are configuration-driven (bound from `appsettings.json`); updating the setting + redeploying switches the model without code changes.

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
| AI/ML - LLM | Azure OpenAI GPT-4 Turbo | API version 2024-02-01 |
| AI/ML - SDK | `Azure.AI.OpenAI` NuGet | 1.0.x |
| AI/ML - Identity | `Azure.Identity` NuGet | 1.12.x |
| Resilience | Polly (`Polly.Core` / `Microsoft.Extensions.Http.Resilience`) | 8.x |
| Token Counting | SharpToken | 1.0.x |
| Logging | Serilog | 3.x |
| Testing - Unit | xUnit + Moq | 2.x / 4.x |

> `Azure.Identity` provides `DefaultAzureCredential` for managed identity authentication. In development, it falls back to VS/VS Code/Azure CLI credentials. In Azure App Service with system-assigned managed identity, no configuration change is required. All libraries are OSS/free — satisfies NFR-015. `Polly.Core` 8.x is the Polly v8 API (breaking change from v7); use `ResiliencePipelineBuilder` not the v7 `Policy.HandleResult()` API.

---

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | Yes |
| **AIR Requirements** | AIR-O01, AIR-S03 |
| **AI Pattern** | AI Gateway — authentication, routing, token budget, retry, observability |
| **Prompt Template Path** | `.propel/context/prompts/` (system prompts per feature context; config-driven, not hardcoded) |
| **Guardrails Config** | `appsettings.json` → `AzureOpenAi:OutputTokenBudget` (4096), `AzureOpenAi:SystemPrompts` dictionary |
| **Model Provider** | Azure OpenAI GPT-4 Turbo (HIPAA BAA — TR-006, NFR-013) |

### CRITICAL: AI Implementation Requirements

- **MUST** use `DefaultAzureCredential` (not `AzureKeyCredential`) for all Azure OpenAI calls per TR-006 managed identity requirement. The Azure OpenAI resource must have the app's managed identity assigned the `Cognitive Services OpenAI User` role.
- **MUST** enforce `MaxOutputTokenCount = 4096` on every `ChatCompletionsOptions` / `EmbeddingsOptions` call. If `FinishReason == CompletionsFinishReason.Length`, set `GatewayResponse.IsTruncated = true` and log at `Warning` level.
- **MUST** NOT log raw prompt text, user input, or model response content in observability logs (TR-006 PHI risk). Log ONLY: `model`, `inputTokens`, `outputTokens`, `latencyMs`, `status` (`Success`/`Truncated`/`RetryExhausted`/`RateLimited`), `featureContext`.
- **MUST** separate inference model deployment (`gpt-4-turbo`) and embedding model deployment (`text-embedding-3-small`) in `AzureOpenAiOptions` — routed to different `AzureOpenAIClient` instances or via deployment name parameter per AC-1.
- **MUST** implement Polly retry using `ResiliencePipelineBuilder` (Polly v8 API) — NOT the deprecated v7 `Policy.HandleResult()` syntax. Retry on `HttpRequestException` (503), `Azure.RequestFailedException` with status 429 or 503.
- **MUST** respect `Retry-After` header on HTTP 429 responses — use Polly's `OnRetry` callback to extract the header and delay for the indicated duration (up to 4s max; do not wait indefinitely).
- **MUST** retain the existing `GenerateEmbeddingsAsync` + `IsCircuitOpen` contract (US_019 callers use these). Only ADDITIVE changes to `IAiGateway`.

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

Harden the existing `AzureOpenAiGateway` implementation (provisionally created in US_019/task_002 and US_020/task_001) to production-ready standards for EP-007.

**Scope of changes to existing gateway:**
1. **Auth upgrade**: Replace `AzureKeyCredential(_options.ApiKey)` with `DefaultAzureCredential()` on both the embeddings client and the chat completions client per TR-006.
2. **Model routing config**: Extract deployment names and endpoint to `AzureOpenAiOptions` POCO (bound from `appsettings.json`). Separate keys for inference (`InferenceDeploymentName`) and embeddings (`EmbeddingDeploymentName`).
3. **Output token budget**: Enforce `MaxOutputTokenCount = 4096` on `ChatCompletionsOptions`. Detect `FinishReason.Length` and mark `GatewayResponse.IsTruncated`.
4. **System prompt appending**: Accept `FeatureContext` string (e.g., `"FactExtraction"`, `"CodeSuggestion"`, `"ConversationalIntake"`) and prepend the matching system prompt from `AzureOpenAiOptions.SystemPrompts` dictionary before the caller's `systemPrompt` parameter.
5. **Retry policy**: Polly v8 `ResiliencePipeline` with 3 retries at 1s/2s/4s delays; triggers on 429 and 503; respects `Retry-After` header.
6. **Observability logging**: Serilog structured log after every gateway call (success, truncated, or final failure) using `LogInformation`/`LogWarning`/`LogError` scoped properties — zero PHI.

**`IAiGateway` interface changes**: The interface gains `Task<GatewayResponse> ChatCompletionAsync(string featureContext, string systemPrompt, string userMessage, Guid correlationId, CancellationToken ct)`. The old `ChatCompletionAsync` from `IChatCompletionGateway` (US_020) is preserved as a compatibility shim that delegates to the new signature.

---

## Dependent Tasks

- **task_002_ai_embedding_pipeline.md** (US_019) — `IAiGateway`, `AzureOpenAiGateway` must exist as starting point for modifications.
- **task_001_ai_rag_extraction_job.md** (US_020) — `IChatCompletionGateway.ChatCompletionAsync` must exist; US_030 refactors it to the new `GatewayResponse` return type.
- **task_001_ai_prompt_safety_rag_acl.md** (US_029) — `IPromptSanitizer` is injected into `AzureOpenAiGateway`; this task must complete before US_030 modifies the same file.

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/AzureOpenAiOptions.cs` | Options POCO: `Endpoint`, `InferenceDeploymentName`, `EmbeddingDeploymentName`, `OutputTokenBudget` (int, default 4096), `SystemPrompts` dictionary keyed by feature context |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/GatewayResponse.cs` | Result record: `(string Content, int InputTokens, int OutputTokens, bool IsTruncated, string FeatureContext)` |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/IAiGateway.cs` | Add `Task<GatewayResponse> ChatCompletionAsync(string featureContext, string systemPrompt, string userMessage, Guid correlationId, CancellationToken ct)` |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/AzureOpenAiGateway.cs` | Replace `AzureKeyCredential` with `DefaultAzureCredential`; add `IOptions<AzureOpenAiOptions>`; implement `GatewayResponse ChatCompletionAsync`; enforce 4096 output token cap; system prompt appending; Polly v8 retry pipeline; Serilog observability logging |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/IChatCompletionGateway.cs` | Update return type of `ChatCompletionAsync` to `Task<GatewayResponse>`; retain signature for backward compatibility with US_020 callers |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Presentation/ServiceCollectionExtensions.cs` | Bind `AzureOpenAiOptions` from `"AzureOpenAi"` config section; register `AzureOpenAIClient` with `DefaultAzureCredential`; configure Polly retry pipeline via `AddResiliencePipeline` |
| MODIFY | `server/src/PropelIQ.Api/appsettings.json` | Add `"AzureOpenAi"` section: `Endpoint`, `InferenceDeploymentName`, `EmbeddingDeploymentName`, `OutputTokenBudget`, `SystemPrompts` dictionary |
| MODIFY | `server/src/PropelIQ.Api/appsettings.Development.json` | Mirror `"AzureOpenAi"` section with dev deployment names; `OutputTokenBudget` may be lower for cost control in dev |

---

## Implementation Plan

### 1. `AzureOpenAiOptions` — configuration POCO

```csharp
// ClinicalIntelligence.Application/AI/AzureOpenAiOptions.cs
public sealed class AzureOpenAiOptions
{
    public const string SectionName = "AzureOpenAi";

    /// <summary>Azure OpenAI resource endpoint URI.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>GPT-4 Turbo deployment name in the Azure OpenAI resource.</summary>
    public string InferenceDeploymentName { get; set; } = "gpt-4-turbo";

    /// <summary>text-embedding-3-small deployment name in the Azure OpenAI resource.</summary>
    public string EmbeddingDeploymentName { get; set; } = "text-embedding-3-small";

    /// <summary>Maximum output tokens per chat completion request (AIR-O01 + AC-2).</summary>
    public int OutputTokenBudget { get; set; } = 4096;

    /// <summary>System prompts keyed by feature context string.</summary>
    public Dictionary<string, string> SystemPrompts { get; set; } = new()
    {
        ["FactExtraction"] = "You are a clinical fact extraction assistant operating within a HIPAA-compliant healthcare platform. Extract structured clinical facts only. Do not infer beyond the document content.",
        ["CodeSuggestion"] = "You are a medical coding assistant operating within a HIPAA-compliant healthcare platform. Suggest ICD-10 and CPT codes based only on documented facts provided.",
        ["ConversationalIntake"] = "You are a patient intake assistant. Ask clear, empathetic questions to gather intake information. Do not provide medical advice."
    };
}
```

### 2. `GatewayResponse` — result record

```csharp
// ClinicalIntelligence.Application/AI/GatewayResponse.cs
public sealed record GatewayResponse(
    string Content,
    int InputTokens,
    int OutputTokens,
    bool IsTruncated,
    string FeatureContext
)
{
    public static GatewayResponse Truncated(string content, int inputTokens, int outputTokens, string ctx)
        => new(content, inputTokens, outputTokens, IsTruncated: true, ctx);

    public static GatewayResponse Success(string content, int inputTokens, int outputTokens, string ctx)
        => new(content, inputTokens, outputTokens, IsTruncated: false, ctx);
}
```

### 3. `IAiGateway` additions

```csharp
// ADD to existing IAiGateway interface (additive — existing GenerateEmbeddingsAsync + IsCircuitOpen retained)
public interface IAiGateway
{
    // --- Existing (US_019) ---
    Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(
        IReadOnlyList<string> inputs, Guid documentId, CancellationToken ct = default);

    bool IsCircuitOpen { get; }

    // --- New (US_030) ---

    /// <summary>
    /// Executes a chat completion with managed identity auth, token budget enforcement (AIR-O01),
    /// system prompt appending, retry policy (AC-3), and observability logging (AC-4).
    /// Does NOT log raw prompt/response content per TR-006.
    /// </summary>
    Task<GatewayResponse> ChatCompletionAsync(
        string featureContext,
        string systemPrompt,
        string userMessage,
        Guid correlationId,
        CancellationToken ct = default);
}
```

> **Backward compatibility:** `IChatCompletionGateway.ChatCompletionAsync(systemPrompt, userMessage, documentId, ct)` (from US_020) becomes a shim method in `AzureOpenAiGateway` that calls `((IAiGateway)this).ChatCompletionAsync("FactExtraction", systemPrompt, userMessage, documentId, ct)` and returns `result.Content`. The `IChatCompletionGateway` return type is updated to `Task<GatewayResponse>` — US_020 callers need updating to use `result.Content`.

### 4. `AzureOpenAiGateway` — managed identity + Polly v8 retry

```csharp
public sealed class AzureOpenAiGateway(
    IOptions<AzureOpenAiOptions> options,
    IDistributedCache cache,
    IAuditLogger auditLogger,
    IPromptSanitizer sanitizer,        // injected per US_029
    IHttpContextAccessor httpContextAccessor,
    ILogger<AzureOpenAiGateway> logger,
    PropelIQDbContext db)
    : IAiGateway, IChatCompletionGateway
{
    private readonly AzureOpenAiOptions _opts = options.Value;

    // Managed identity — DefaultAzureCredential auto-selects:
    //   Production: system-assigned managed identity on Azure App Service
    //   Development: Azure CLI / Visual Studio credentials
    // REQUIRES: Azure OpenAI resource has "Cognitive Services OpenAI User" role assigned to app identity
    private static readonly DefaultAzureCredential _credential = new();

    // Polly v8 retry pipeline (AC-3): 3 retries, 1s/2s/4s exponential backoff
    // Triggers on HttpRequestException (503) and RequestFailedException (429, 503)
    private static readonly ResiliencePipeline _retryPipeline =
        new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                DelayGenerator = static args =>
                {
                    // Respect Retry-After header on 429; otherwise exponential backoff
                    if (args.Outcome.Exception is RequestFailedException rfe
                        && rfe.Status == 429
                        && rfe.GetRawResponse()?.Headers.TryGetValue("Retry-After", out var retryAfter) == true
                        && int.TryParse(retryAfter, out var waitSeconds))
                    {
                        var wait = TimeSpan.FromSeconds(Math.Min(waitSeconds, 4));
                        return ValueTask.FromResult<TimeSpan?>(wait);
                    }
                    // 1s, 2s, 4s
                    return ValueTask.FromResult<TimeSpan?>(TimeSpan.FromSeconds(Math.Pow(2, args.AttemptNumber)));
                },
                ShouldHandle = new PredicateBuilder()
                    .Handle<RequestFailedException>(ex => ex.Status is 429 or 503)
                    .Handle<HttpRequestException>()
            })
            .Build();

    public async Task<GatewayResponse> ChatCompletionAsync(
        string featureContext,
        string systemPrompt,
        string userMessage,
        Guid correlationId,
        CancellationToken ct = default)
    {
        // AIR-S01 — sanitize (US_029 pre-check; sanitizer throws PromptInjectionBlockedException if Blocked)
        var sanitized = sanitizer.Evaluate(userMessage);
        if (sanitized.Verdict == SanitizationVerdict.Blocked)
        {
            await AuditAndSaveBlockedAsync(correlationId, sanitized, ct);
            throw new PromptInjectionBlockedException(sanitized.MatchedPatternId);
        }

        // Append context-appropriate system prompt (AC-2)
        var contextSystemPrompt = _opts.SystemPrompts.TryGetValue(featureContext, out var sp)
            ? $"{sp}\n\n{systemPrompt}"
            : systemPrompt;

        var client = new AzureOpenAIClient(new Uri(_opts.Endpoint), _credential);
        var chatClient = client.GetChatClient(_opts.InferenceDeploymentName);

        var sw = Stopwatch.StartNew();
        GatewayResponse gatewayResponse;

        try
        {
            ChatCompletion completion = await _retryPipeline.ExecuteAsync(async token =>
                await chatClient.CompleteChatAsync(
                    [
                        new SystemChatMessage(contextSystemPrompt),
                        new UserChatMessage(sanitized.NormalizedInput)
                    ],
                    new ChatCompletionOptions
                    {
                        MaxOutputTokenCount = _opts.OutputTokenBudget, // AC-2: 4096 max
                        Temperature = 0f,
                        ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
                    },
                    token),
                ct);

            sw.Stop();

            bool isTruncated = completion.FinishReason == ChatFinishReason.Length;
            string content = completion.Content[0].Text;
            int inputTokens = completion.Usage?.InputTokenCount ?? 0;
            int outputTokens = completion.Usage?.OutputTokenCount ?? 0;

            // AC-4: Observability logging — NO raw content logged (TR-006 PHI protection)
            var logLevel = isTruncated ? LogLevel.Warning : LogLevel.Information;
            logger.Log(logLevel,
                "AzureOpenAI {Status} | model={Model} feature={Feature} correlationId={CorrelationId} " +
                "inputTokens={InputTokens} outputTokens={OutputTokens} latencyMs={LatencyMs}",
                isTruncated ? "Truncated" : "Success",
                _opts.InferenceDeploymentName, featureContext, correlationId,
                inputTokens, outputTokens, sw.ElapsedMilliseconds);

            if (isTruncated)
            {
                logger.LogWarning(
                    "AIR-O01 output token budget ({Budget}) reached for correlationId={CorrelationId}. " +
                    "Response marked truncated.",
                    _opts.OutputTokenBudget, correlationId);
            }

            gatewayResponse = isTruncated
                ? GatewayResponse.Truncated(content, inputTokens, outputTokens, featureContext)
                : GatewayResponse.Success(content, inputTokens, outputTokens, featureContext);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            logger.LogError(ex,
                "AzureOpenAI RetryExhausted | model={Model} feature={Feature} correlationId={CorrelationId} " +
                "latencyMs={LatencyMs}",
                _opts.InferenceDeploymentName, featureContext, correlationId, sw.ElapsedMilliseconds);
            throw;
        }

        return gatewayResponse;
    }

    // ... existing GenerateEmbeddingsAsync (updated to use DefaultAzureCredential) ...
    // Replace: new EmbeddingsClient(new Uri(_opts.Endpoint), new AzureKeyCredential(_opts.ApiKey))
    // With:    new AzureOpenAIClient(new Uri(_opts.Endpoint), _credential).GetEmbeddingClient(_opts.EmbeddingDeploymentName)
}
```

> **Security note (OWASP A02 — Cryptographic Failures):** Removing `AzureKeyCredential` eliminates the API key from configuration entirely. No secret rotation schedule needed. Managed identity credentials are issued and rotated by Azure AD automatically. The `_credential` field is `static readonly` — one `DefaultAzureCredential` instance per gateway class is correct and avoids the overhead of re-initializing the credential chain on every call.

### 5. `appsettings.json` — `AzureOpenAi` section

```json
"AzureOpenAi": {
  "Endpoint": "https://<your-resource>.openai.azure.com/",
  "InferenceDeploymentName": "gpt-4-turbo",
  "EmbeddingDeploymentName": "text-embedding-3-small",
  "OutputTokenBudget": 4096,
  "SystemPrompts": {
    "FactExtraction": "You are a clinical fact extraction assistant operating within a HIPAA-compliant healthcare platform. Extract structured clinical facts only. Do not infer beyond the document content.",
    "CodeSuggestion": "You are a medical coding assistant operating within a HIPAA-compliant healthcare platform. Suggest ICD-10 and CPT codes based only on documented facts provided.",
    "ConversationalIntake": "You are a patient intake assistant. Ask clear, empathetic questions to gather intake information. Do not provide medical advice."
  }
}
```

> **NOTE:** Remove any existing `AzureOpenAi:ApiKey` key if present — managed identity makes it obsolete. `Endpoint` is not a secret (it is the resource URL, not a credential). Do NOT add `Endpoint` to GitHub Encrypted Secrets. The `ConfigurationValidator.cs` (US_027) should NOT require `AzureOpenAi:ApiKey` — remove that check if it exists.

### 6. `ServiceCollectionExtensions.cs` additions

```csharp
// In AddClinicalIntelligenceModule(IServiceCollection services, IConfiguration config):

services.Configure<AzureOpenAiOptions>(config.GetSection(AzureOpenAiOptions.SectionName));

// AzureOpenAIClient is registered as singleton (one client per app instance)
// DefaultAzureCredential is thread-safe and handles token refresh internally
services.AddSingleton(_ =>
{
    var opts = config.GetSection(AzureOpenAiOptions.SectionName).Get<AzureOpenAiOptions>()!;
    return new AzureOpenAIClient(new Uri(opts.Endpoint), new DefaultAzureCredential());
});

// Polly retry pipeline for Azure OpenAI (separate from the circuit breaker added by US_031)
services.AddResiliencePipeline("azure-openai-retry", builder =>
{
    builder.AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        DelayGenerator = static args =>
        {
            if (args.Outcome.Exception is RequestFailedException { Status: 429 } rfe
                && rfe.GetRawResponse()?.Headers.TryGetValue("Retry-After", out var ra) == true
                && int.TryParse(ra, out var secs))
                return ValueTask.FromResult<TimeSpan?>(TimeSpan.FromSeconds(Math.Min(secs, 4)));
            return ValueTask.FromResult<TimeSpan?>(TimeSpan.FromSeconds(Math.Pow(2, args.AttemptNumber)));
        },
        ShouldHandle = new PredicateBuilder()
            .Handle<RequestFailedException>(ex => ex.Status is 429 or 503)
            .Handle<HttpRequestException>()
    });
});
```

---

## Current Project State

```
server/src/Modules/ClinicalIntelligence/
└── ClinicalIntelligence.Application/
    └── AI/
        ├── IAiGateway.cs               ← MODIFY: add ChatCompletionAsync(featureContext,...) overload
        ├── AzureOpenAiGateway.cs       ← MODIFY: DefaultAzureCredential, output token budget,
        │                                           system prompt appending, Polly v8 retry, Serilog
        ├── IChatCompletionGateway.cs   ← MODIFY: return type → Task<GatewayResponse>
        ├── AzureOpenAiOptions.cs       ← CREATE this task
        ├── GatewayResponse.cs          ← CREATE this task
        └── Sanitization/               ← exists from US_029
            └── IPromptSanitizer.cs

server/src/Modules/ClinicalIntelligence/
└── ClinicalIntelligence.Presentation/
    └── ServiceCollectionExtensions.cs  ← MODIFY: bind AzureOpenAiOptions; register AzureOpenAIClient;
                                                   configure Polly retry pipeline

server/src/PropelIQ.Api/
├── appsettings.json                    ← MODIFY: add "AzureOpenAi" section; remove ApiKey if present
└── appsettings.Development.json        ← MODIFY: mirror AzureOpenAi section (dev deployment names)
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/AzureOpenAiOptions.cs` | Options POCO: `Endpoint`, `InferenceDeploymentName`, `EmbeddingDeploymentName`, `OutputTokenBudget` (4096), `SystemPrompts` dictionary |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/GatewayResponse.cs` | `record(Content, InputTokens, OutputTokens, IsTruncated, FeatureContext)` with `Success()` and `Truncated()` factory methods |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/IAiGateway.cs` | Add `Task<GatewayResponse> ChatCompletionAsync(featureContext, systemPrompt, userMessage, correlationId, ct)` |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/AzureOpenAiGateway.cs` | Replace `AzureKeyCredential` with `DefaultAzureCredential`; add `IOptions<AzureOpenAiOptions>`; implement new `ChatCompletionAsync` with 4096 output cap + system prompt + Polly v8 retry + Serilog; update `GenerateEmbeddingsAsync` to use `DefaultAzureCredential` |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/IChatCompletionGateway.cs` | Update return type to `Task<GatewayResponse>`; retain signature; backward compat shim in `AzureOpenAiGateway` |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Presentation/ServiceCollectionExtensions.cs` | Bind `AzureOpenAiOptions`; register `AzureOpenAIClient` singleton with `DefaultAzureCredential`; `AddResiliencePipeline("azure-openai-retry", ...)` |
| MODIFY | `server/src/PropelIQ.Api/appsettings.json` | Add `"AzureOpenAi"` section (endpoint, deployment names, output budget, system prompts); remove `ApiKey` if present |
| MODIFY | `server/src/PropelIQ.Api/appsettings.Development.json` | Mirror `"AzureOpenAi"` section with dev deployment names |

---

## External References

- [Azure.AI.OpenAI 1.0 release notes — breaking changes from preview](https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/openai/Azure.AI.OpenAI/CHANGELOG.md)
- [Azure.Identity DefaultAzureCredential authentication flow](https://learn.microsoft.com/en-us/dotnet/api/azure.identity.defaultazurecredential)
- [Assign Azure OpenAI RBAC role to managed identity](https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/managed-identity)
- [Polly v8 migration guide from v7](https://www.pollydocs.org/migration-v8.html)
- [Polly v8 RetryStrategyOptions — DelayGenerator](https://www.pollydocs.org/strategies/retry.html)
- [ChatCompletionsOptions.MaxOutputTokenCount — Azure SDK](https://learn.microsoft.com/en-us/dotnet/api/azure.ai.openai.chatcompletionsoptions.maxtokens)
- [OWASP A02 — Cryptographic Failures: secrets in config](https://owasp.org/Top10/A02_2021-Cryptographic_Failures/)

---

## Build Commands

```powershell
# Restore + build (verify DefaultAzureCredential compilation)
dotnet restore server/PropelIQ.slnx
dotnet build server/PropelIQ.slnx --no-restore

# Run unit tests
dotnet test server/PropelIQ.slnx --no-build --filter "Category=US030"
```

---

## Implementation Validation Strategy

- [ ] Unit tests pass (xUnit + Moq)
- [ ] **[AI Tasks]** `DefaultAzureCredential` instantiates without exception in local dev (Azure CLI login required for local test)
- [ ] **[AI Tasks]** Polly retry fires 3 times on mocked `RequestFailedException(429)` — verified by unit test counting retry attempts
- [ ] **[AI Tasks]** Retry-After header respected: mock 429 with `Retry-After: 2` header → Polly waits ~2s (assert delay ≥ 1.8s with tolerance)
- [ ] **[AI Tasks]** `MaxOutputTokenCount = 4096` set on every `ChatCompletionsOptions` — verified by inspecting captured options in mock
- [ ] **[AI Tasks]** `FinishReason.Length` → `IsTruncated = true` in returned `GatewayResponse`
- [ ] **[AI Tasks]** Serilog logs contain `model`, `inputTokens`, `outputTokens`, `latencyMs` — do NOT contain any substring from `userMessage` or `systemPrompt`
- [ ] **[AI Tasks]** Existing `GenerateEmbeddingsAsync` callers (US_019, US_020) compile unchanged — no regression

---

## Implementation Checklist

- [ ] CREATE `AzureOpenAiOptions` POCO with `Endpoint`, `InferenceDeploymentName`, `EmbeddingDeploymentName`, `OutputTokenBudget` (default 4096), `SystemPrompts` dictionary (3 built-in feature contexts)
- [ ] CREATE `GatewayResponse` record with `IsTruncated` flag; `Success()` + `Truncated()` factory methods
- [ ] MODIFY `IAiGateway` — add `ChatCompletionAsync(featureContext, systemPrompt, userMessage, correlationId, ct)` returning `Task<GatewayResponse>`; retain existing members (additive only)
- [ ] MODIFY `AzureOpenAiGateway` — replace ALL `AzureKeyCredential` usages with `DefaultAzureCredential`; inject `IOptions<AzureOpenAiOptions>`; implement new `ChatCompletionAsync` with system prompt prepend, `MaxOutputTokenCount`, Polly v8 retry pipeline, `IsTruncated` detection, Serilog observability (no PHI)
- [ ] MODIFY `IChatCompletionGateway.ChatCompletionAsync` return type to `Task<GatewayResponse>`; add backward-compat shim in `AzureOpenAiGateway` delegating old signature to new (US_020 callers updated to use `.Content`)
- [ ] MODIFY `ServiceCollectionExtensions.cs` — bind `AzureOpenAiOptions`; register `AzureOpenAIClient` singleton with `DefaultAzureCredential`; configure `AddResiliencePipeline("azure-openai-retry", ...)` with Polly v8 API
- [ ] MODIFY `appsettings.json` + `appsettings.Development.json` — add `"AzureOpenAi"` section; remove `ApiKey` entry if it exists; confirm `ConfigurationValidator.cs` does not require `AzureOpenAi:ApiKey`
- [ ] **[AI Tasks - MANDATORY]** Implement and test guardrails (retry policy, token budget) before marking task complete
- [ ] **[AI Tasks - MANDATORY]** Verify AIR-O01 requirements: 4096 output token limit enforced; truncated responses marked with `IsTruncated = true`

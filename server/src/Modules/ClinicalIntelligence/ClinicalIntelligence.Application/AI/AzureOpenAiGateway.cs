using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using ClinicalIntelligence.Application.AI.Availability;
using ClinicalIntelligence.Application.AI.Exceptions;
using ClinicalIntelligence.Application.AI.FeatureFlags;
using ClinicalIntelligence.Application.AI.Latency;
using ClinicalIntelligence.Application.AI.ModelVersion;
using ClinicalIntelligence.Application.AI.Safety;
using ClinicalIntelligence.Application.AI.Schema;
using ClinicalIntelligence.Application.AI.Sanitization;
using ClinicalIntelligence.Application.Infrastructure;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using OpenAI.Embeddings;
using Polly.CircuitBreaker;
using Polly.Registry;
using StackExchange.Redis;

namespace ClinicalIntelligence.Application.AI;

/// <summary>
/// Azure OpenAI implementation of <see cref="IAiGateway"/> and <see cref="IChatCompletionGateway"/>.
///
/// AI requirements enforced:
/// - AIR-O01: Rejects batches / chat prompts whose estimated token count exceeds 8,000.
/// - AIR-O02: Polly v8 circuit breaker opens after 5 consecutive embedding failures; stays
///            open for 30 s; <see cref="IsCircuitOpen"/> signals callers to route to
///            <c>ManualReview</c>.
/// - AIR-O04: Redis cache keyed by <c>SHA256(chunk_text)</c> with 7-day TTL.
/// - AIR-S03: Every API call is logged with structured metadata only — no PHI/PII.
/// - US_030 / AC-1: Managed identity (<see cref="DefaultAzureCredential"/>) — no API key in config.
/// - US_030 / AC-2: <c>MaxOutputTokenCount</c> enforced from <see cref="AzureOpenAiOptions.OutputTokenBudget"/>.
/// - US_030 / AC-3: Polly v8 retry pipeline — 3 attempts, 1 s / 2 s / 4 s back-off, respects
///                  <c>Retry-After</c> header ≤ 4 s on 429 / 503.
/// - US_030 / AC-4: IsTruncated=true when FinishReason=Length; all log fields are non-PHI.
///
/// Thread-safety: registered as singleton. Volatile <c>_circuitOpen</c> flag is written
/// only in circuit-breaker callbacks and read by any thread.
/// </summary>
public sealed class AzureOpenAiGateway : IChatCompletionGateway
{
    // ── Constants ───────────────────────────────────────────────
    private const int MaxBatchSize          = 15;    // 15 × 512 tokens ≤ 8,000 (AIR-O01)
    private const int TokenBudgetPerRequest = 8_000; // AIR-O01
    private const int CharsPerToken         = 4;     // rough estimate: 4 chars ≈ 1 token

    // ── Managed identity credential — one instance per process (thread-safe) ─────
    private static readonly DefaultAzureCredential ManagedIdentityCredential = new();

    // ── Fields ───────────────────────────────────────────────────────────────────
    private readonly AzureOpenAIClient?                          _azureClient;
    private readonly EmbeddingClient?                            _embeddingClient;
    private readonly IAiEmbeddingCache                           _cache;
    private readonly IPromptSanitizer                            _sanitizer;
    private readonly ILogger<AzureOpenAiGateway>                 _logger;
    private readonly AzureOpenAiOptions                          _opts;
    private readonly bool                                        _isConfigured;
    private readonly IAiAvailabilityState                        _availabilityState;
    private readonly IAiDegradationHandler                       _degradationHandler;
    private readonly IDistributedCache                           _responseCache;
    private readonly ResiliencePipelineProvider<string>          _pipelineProvider;
    private readonly IContentSafetyFilter                        _contentSafetyFilter;
    private readonly IConnectionMultiplexer                      _connectionMultiplexer;
    private readonly IModelVersionService                        _modelVersionService;
    private readonly IOptionsMonitor<ContentSafetyOptions>       _safetyOptions;
    private readonly IAiSchemaValidator                          _schemaValidator;
    private readonly ILatencyRecorder                            _latencyRecorder;
    private readonly IOptions<AiSlaOptions>                      _slaOptions;
    private readonly IFeatureFlagService                         _featureFlagService;
    private readonly IOptionsMonitor<AiFeatureFlagsOptions>      _featureFlagOptions;

    /// <inheritdoc />
    /// Reflects <see cref="IAiAvailabilityState.IsAvailable"/> which is driven by the
    /// Polly circuit breaker <c>OnOpened</c>/<c>OnClosed</c> callbacks (US_031).
    public bool IsCircuitOpen => !_availabilityState.IsAvailable;

    public AzureOpenAiGateway(
        IOptions<AzureOpenAIEmbeddingOptions> embeddingOptions,
        IOptions<AzureOpenAiOptions>          openAiOptions,
        IAiEmbeddingCache                     cache,
        IPromptSanitizer                      sanitizer,
        IAiAvailabilityState                  availabilityState,
        IAiDegradationHandler                 degradationHandler,
        IDistributedCache                     responseCache,
        ResiliencePipelineProvider<string>    pipelineProvider,
        IContentSafetyFilter                  contentSafetyFilter,
        IConnectionMultiplexer                connectionMultiplexer,
        IModelVersionService                  modelVersionService,
        IOptionsMonitor<ContentSafetyOptions> safetyOptions,
        IAiSchemaValidator                    schemaValidator,
        ILatencyRecorder                                      latencyRecorder,
        IOptions<AiSlaOptions>                                slaOptions,
        IFeatureFlagService                                   featureFlagService,
        IOptionsMonitor<AiFeatureFlagsOptions>                featureFlagOptions,
        ILogger<AzureOpenAiGateway>                           logger)
    {
        _cache                 = cache;
        _sanitizer             = sanitizer;
        _logger                = logger;
        _opts                  = openAiOptions.Value;
        _availabilityState     = availabilityState;
        _degradationHandler    = degradationHandler;
        _responseCache         = responseCache;
        _pipelineProvider      = pipelineProvider;
        _contentSafetyFilter   = contentSafetyFilter;
        _connectionMultiplexer = connectionMultiplexer;
        _modelVersionService   = modelVersionService;
        _safetyOptions         = safetyOptions;
        _schemaValidator       = schemaValidator;
        _latencyRecorder       = latencyRecorder;
        _slaOptions            = slaOptions;
        _featureFlagService    = featureFlagService;
        _featureFlagOptions    = featureFlagOptions;

        var embOpts = embeddingOptions.Value;

        // Configured when Endpoint is set — no API key required (US_030 / AC-1 / OWASP A02)
        _isConfigured = !string.IsNullOrWhiteSpace(embOpts.Endpoint);

        if (_isConfigured)
        {
            if (!Uri.TryCreate(embOpts.Endpoint, UriKind.Absolute, out var uri))
                throw new InvalidOperationException("AzureOpenAI:Endpoint is not a valid absolute URI.");

            _azureClient     = new AzureOpenAIClient(uri, ManagedIdentityCredential);
            _embeddingClient = _azureClient.GetEmbeddingClient(embOpts.EmbeddingDeploymentName);
        }

    }

    // ── Embedding generation ──────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(
        IReadOnlyList<string> inputs,
        Guid                  documentId,
        CancellationToken     ct = default)
    {
        if (inputs.Count > MaxBatchSize)
            throw new ArgumentException(
                $"Batch size {inputs.Count} exceeds maximum {MaxBatchSize}.", nameof(inputs));

        var results          = new float[inputs.Count][];
        var cacheMissIndices = new List<int>(inputs.Count);

        // AIR-O04: Redis cache check per chunk (keyed by SHA256(chunk_text))
        for (int i = 0; i < inputs.Count; i++)
        {
            var cached = await _cache.GetAsync(BuildCacheKey(inputs[i]), ct);
            if (cached is not null)
                results[i] = cached;
            else
                cacheMissIndices.Add(i);
        }

        if (cacheMissIndices.Count == 0)
            return results;  // Full cache hit — no API call

        // AIR-O01: Estimated token budget guard
        var estimatedTokens = cacheMissIndices.Sum(i => inputs[i].Length / CharsPerToken);
        if (estimatedTokens > TokenBudgetPerRequest)
            throw new TokenBudgetExceededException(
                $"Estimated token count {estimatedTokens} exceeds AIR-O01 limit of {TokenBudgetPerRequest}.");

        if (!_isConfigured || _embeddingClient is null)
        {
            _logger.LogWarning(
                "AzureOpenAiGateway: embedding API not configured for document {DocumentId}; returning zero vectors.",
                documentId);
            foreach (var i in cacheMissIndices)
                results[i] = new float[1536];
            return results;
        }

        var apiInputs = cacheMissIndices.Select(i => inputs[i]).ToList();

        // Call Azure OpenAI via shared Polly resilience pipeline — circuit breaker (outer) + retry (inner) (US_031, AIR-O02)
        var apiVectors = await _pipelineProvider.GetPipeline("azure-openai")
            .ExecuteAsync<IReadOnlyList<float[]>>(async token =>
        {
            var response = await _embeddingClient.GenerateEmbeddingsAsync(apiInputs, null, token);
            return (IReadOnlyList<float[]>)response.Value
                .OrderBy(e => e.Index)
                .Select(e => e.ToFloats().ToArray())
                .ToList();
        }, ct);

        // AIR-S03: Structured audit log — no chunk text (PHI) in payload
        _logger.LogInformation(
            "EmbeddingsGenerated: DocumentId={DocumentId} ChunkCount={ChunkCount} EstimatedTokens={EstimatedTokens}",
            documentId, cacheMissIndices.Count, estimatedTokens);

        // AIR-O04: Populate results and cache with 7-day TTL
        for (int j = 0; j < cacheMissIndices.Count; j++)
        {
            var i = cacheMissIndices[j];
            results[i] = apiVectors[j];
            await _cache.SetAsync(BuildCacheKey(inputs[i]), results[i], TimeSpan.FromDays(7), ct);
        }

        return results;
    }

    // ── Chat completion (US_030 hardened path) ────────────────────────────────────

    /// <inheritdoc />
    public async Task<GatewayResponse> ChatCompletionAsync(
        string            featureContext,
        string            systemPrompt,
        string            userMessage,
        Guid              correlationId,
        CancellationToken ct = default)
    {
        // AC-4 / AIR-Q04 / TR-025: Feature flag gate — zero AI cost path (checked before ALL other logic)
        if (!await _featureFlagService.IsEnabledAsync(featureContext, ct).ConfigureAwait(false))
        {
            _logger.LogInformation(
                "AI feature disabled | featureContext={FeatureContext} correlationId={CorrelationId}",
                featureContext, correlationId);
            throw new FeatureDisabledException(featureContext);
        }

        // AC-1 / AIR-Q02: Latency measurement starts at gateway entry — includes full overhead
        var startTimestamp = Stopwatch.GetTimestamp();

        // US_030 / AC-5: Degradation guard — route to handler immediately if service unavailable.
        // Must be BEFORE sanitizer call so degradation is instant (no unnecessary compute).
        if (!_availabilityState.IsAvailable)
        {
            _logger.LogWarning(
                "AzureOpenAI unavailable — routing to degradation handler | " +
                "featureContext={FeatureContext} correlationId={CorrelationId}",
                featureContext, correlationId);
            return await _degradationHandler.GetDegradedResponseAsync(featureContext, ct)
                .ConfigureAwait(false);
        }

        // AIR-S04 / US_029: Evaluate user message through the prompt injection pipeline
        var sanitizationResult = _sanitizer.Evaluate(userMessage);

        if (sanitizationResult.Verdict == SanitizationVerdict.Blocked)
        {
            _logger.LogWarning(
                "PromptInjectionBlocked: CorrelationId={CorrelationId} Feature={Feature} PatternId={PatternId}",
                correlationId, featureContext, sanitizationResult.MatchedPatternId);
            throw new PromptInjectionBlockedException(sanitizationResult.MatchedPatternId);
        }

        if (sanitizationResult.Verdict == SanitizationVerdict.FlaggedForReview)
        {
            _logger.LogWarning(
                "PromptInjectionFlagged: CorrelationId={CorrelationId} Feature={Feature} PatternId={PatternId} — forwarding normalised input.",
                correlationId, featureContext, sanitizationResult.MatchedPatternId);
            userMessage = sanitizationResult.NormalizedInput;
        }

        // Prepend feature-level system prompt from configuration (AC-1)
        var featureSystemPrompt = _opts.SystemPrompts.TryGetValue(featureContext, out var fp)
            ? fp + "\n\n" + systemPrompt
            : systemPrompt;

        // AIR-O01: Enforce combined token budget before calling API
        var estimatedInputTokens = (featureSystemPrompt.Length + userMessage.Length) / CharsPerToken;
        if (estimatedInputTokens > TokenBudgetPerRequest)
            throw new TokenBudgetExceededException(
                $"Chat completion estimated token count {estimatedInputTokens} exceeds AIR-O01 limit of {TokenBudgetPerRequest}.");

        if (!_isConfigured || _azureClient is null)
        {
            _logger.LogWarning(
                "AzureOpenAiGateway: chat completion API not configured for correlation {CorrelationId}; returning empty JSON array.",
                correlationId);
            return GatewayResponse.Success("[]", 0, 0, featureContext);
        }

        var deploymentName = await _modelVersionService.GetActiveDeploymentAsync(ct)
            .ConfigureAwait(false);

        var chatClient = _azureClient.GetChatClient(deploymentName);

        // Prepare messages and options once — the schema retry loop reuses the same inputs
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(featureSystemPrompt),
            new UserChatMessage(userMessage),
        };

        var chatOptions = new ChatCompletionOptions
        {
            Temperature         = 0f,                          // deterministic (AIR-Q04)
            MaxOutputTokenCount = _opts.OutputTokenBudget,     // US_030 / AC-2
            ResponseFormat      = ChatResponseFormat.CreateJsonObjectFormat(),
        };

        // ── Schema retry loop (US_032, AC-2, AC-3, AIR-Q03) ────────────────────────────────────────────
        // Outer loop retries the full Polly-managed GPT call up to MaxSchemaAttempts on
        // semantic schema failures. Polly (inner) handles HTTP/transient errors independently.
        const int MaxSchemaAttempts = 3;
        GatewayResponse? result            = null;
        SchemaValidationResult? lastSchema = null;
        int finalInputTokens               = 0;
        int finalOutputTokens              = 0;

        for (int attempt = 1; attempt <= MaxSchemaAttempts; attempt++)
        {
            ChatCompletion completion;
            try
            {
                completion = await _pipelineProvider.GetPipeline("azure-openai")
                    .ExecuteAsync<ChatCompletion>(async token =>
                    {
                        var raw = await chatClient.CompleteChatAsync(messages, chatOptions, token)
                            .ConfigureAwait(false);
                        return raw.Value;
                    }, ct).ConfigureAwait(false);
            }
            catch (BrokenCircuitException bcEx)
            {
                // Safety-net: circuit opened between the IsAvailable guard and ExecuteAsync.
                // Record latency on this path so p95 tracks all gateway entry durations.
                var bcMs = (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
                await RecordAndCheckSlaAsync(featureContext, bcMs, ct).ConfigureAwait(false);
                _logger.LogWarning(bcEx,
                    "AI circuit breaker open — routing to degradation handler | " +
                    "featureContext={FeatureContext} correlationId={CorrelationId}",
                    featureContext, correlationId);
                return await _degradationHandler.GetDegradedResponseAsync(featureContext, ct)
                    .ConfigureAwait(false);
            }

            var content       = completion.Content.Count > 0 ? completion.Content[0].Text : string.Empty;
            finalInputTokens  = completion.Usage?.InputTokenCount  ?? 0;
            finalOutputTokens = completion.Usage?.OutputTokenCount ?? 0;
            var isTruncated   = completion.FinishReason == ChatFinishReason.Length;

            // AC-2 / AIR-Q03: Schema validation (applies only to contexts with registered schemas)
            var schemaResult = _schemaValidator.Validate(content, featureContext);
            if (schemaResult.IsValid)
            {
                result = isTruncated
                    ? GatewayResponse.Truncated(content, finalInputTokens, finalOutputTokens, featureContext)
                    : GatewayResponse.Success(content,   finalInputTokens, finalOutputTokens, featureContext);
                break;
            }

            lastSchema = schemaResult;
            _logger.LogWarning(
                "SchemaValidationFailed | attempt={Attempt}/{Max} featureContext={FeatureContext} " +
                "reason={Reason} correlationId={CorrelationId}",
                attempt, MaxSchemaAttempts, featureContext, schemaResult.ErrorReason, correlationId);
        }

        // ── AC-3: Schema exhausted ─────────────────────────────────────────────────────────────────
        if (result is null)
        {
            // Audit log at Error — payload contains property names only, NEVER response content (TR-006)
            _logger.LogError(
                "SchemaValidationExhausted | featureContext={FeatureContext} attempts={Attempts} " +
                "errorReason={ErrorReason} correlationId={CorrelationId}",
                featureContext, MaxSchemaAttempts, lastSchema?.ErrorReason, correlationId);

            var exhaustedMs = (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
            await RecordAndCheckSlaAsync(featureContext, exhaustedMs, ct).ConfigureAwait(false);

            // Blocked response MUST NOT be cached (would serve invalid content in degradation)
            return new GatewayResponse(
                Content:        _slaOptions.Value.SchemaErrorMessage,
                InputTokens:    0,
                OutputTokens:   0,
                IsTruncated:    false,
                FeatureContext: featureContext);
        }

        // AC-4: Structured observability log — no content/PHI, only safe metadata
        _logger.LogInformation(
            "ChatCompletionCompleted: Feature={Feature} CorrelationId={CorrelationId} Model={Model} " +
            "InputTokens={InputTokens} OutputTokens={OutputTokens} IsTruncated={IsTruncated}",
            featureContext, correlationId, deploymentName,
            finalInputTokens, finalOutputTokens, result.IsTruncated);

        // US_031 / AC-3: Content safety filter — AFTER schema validation, BEFORE caching (AIR-O03, AIR-S04)
        // A blocked response MUST NOT be cached as a "successful" degradation fallback.
        var violation = await _contentSafetyFilter
            .EvaluateAsync(result.Content, featureContext, ct)
            .ConfigureAwait(false);

        if (violation is not null)
        {
            // Log SHA256 hash only — NEVER raw content (HIPAA + OWASP A04 + AIR-S03)
            _logger.LogWarning(
                "AI content safety violation | type={ViolationType} patternId={PatternId} " +
                "featureContext={FeatureContext} correlationId={CorrelationId} responseHash={Hash}",
                violation.ViolationType, violation.PatternId,
                featureContext, correlationId, violation.ResponseHash);

            await _connectionMultiplexer.GetDatabase()
                .StringIncrementAsync($"ai:safety_violations:{featureContext}")
                .ConfigureAwait(false);

            var safetyMs = (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
            await RecordAndCheckSlaAsync(featureContext, safetyMs, ct).ConfigureAwait(false);

            // Return safe static response — blocked content MUST NOT reach the caller
            return new GatewayResponse(
                Content:        _safetyOptions.CurrentValue.SafeResponseMessage,
                InputTokens:    finalInputTokens,
                OutputTokens:   finalOutputTokens,
                IsTruncated:    false,
                FeatureContext: featureContext);
            // NOTE: CacheSuccessfulResponseAsync intentionally NOT called here
        }

        // US_030 / AC-5: Cache safe response for degraded-mode fallback (fire-and-forget).
        // Intentionally not awaited — cache write failure MUST NOT affect the main response path.
        _ = CacheSuccessfulResponseAsync(featureContext, result);

        // AC-1 / AIR-Q02: Record end-to-end latency (includes schema validation + safety filter time)
        var latencyMs = (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
        await RecordAndCheckSlaAsync(featureContext, latencyMs, ct).ConfigureAwait(false);

        return result;
    }

    // ── Backward-compat shim (IChatCompletionGateway — existing callers) ─────────

    /// <inheritdoc />
    public Task<GatewayResponse> ChatCompletionAsync(
        string            systemPrompt,
        string            userMessage,
        Guid              documentId,
        CancellationToken ct = default)
        => ChatCompletionAsync("FactExtraction", systemPrompt, userMessage, documentId, ct);

    // ── Helpers ───────────────────────────────────────────────────────────────────

    // SHA-256 key prevents PHI from appearing in Redis key space (OWASP A02)
    private static string BuildCacheKey(string input)
        => $"emb:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)))}";

    /// <summary>
    /// Fire-and-forget helper: writes the last successful <see cref="GatewayResponse"/> to
    /// Redis so the <see cref="IAiDegradationHandler"/> can serve it during outages (US_030 / AC-5).
    /// All exceptions are caught and logged — a cache write failure MUST NOT propagate.
    /// </summary>
    private async Task CacheSuccessfulResponseAsync(string featureContext, GatewayResponse response)
    {
        try
        {
            var key  = $"ai_cache:{featureContext}";
            var json = JsonSerializer.Serialize(response);
            var ttl  = TimeSpan.FromSeconds(_opts.ResponseCacheTtlSeconds > 0
                ? _opts.ResponseCacheTtlSeconds
                : 86_400);

            await _responseCache.SetStringAsync(key, json, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl,
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Non-critical — cache failure must never break the main response pipeline.
            _logger.LogWarning(ex,
                "Failed to cache AI response for featureContext={FeatureContext}. Ignoring.",
                featureContext);
        }
    }

    // ── Latency + SLA breach helper (US_032, AC-1, AIR-Q02) ───────────────────────────────

    /// <summary>
    /// Records a latency sample and checks whether the p95 sliding window exceeds
    /// <see cref="AiSlaOptions.P95ThresholdMs"/>. Logs at <c>Error</c> on breach.
    /// The auto-disable hook (task_002 <c>IFeatureFlagService</c>) will extend this method.
    /// </summary>
    private async Task RecordAndCheckSlaAsync(string featureContext, long latencyMs, CancellationToken ct)
    {
        await _latencyRecorder.RecordAsync(featureContext, latencyMs, ct).ConfigureAwait(false);
        var p95 = await _latencyRecorder.GetP95Async(featureContext, ct).ConfigureAwait(false);

        if (p95 > _slaOptions.Value.P95ThresholdMs)
        {
            var criticalFeatures = _featureFlagOptions.CurrentValue.CriticalFeatures;

            if (!criticalFeatures.Contains(featureContext, StringComparer.OrdinalIgnoreCase))
            {
                // AC-1 / TR-025: Auto-disable non-critical feature on persistent SLA breach.
                // Next request to this feature will receive FeatureDisabledException → 503.
                await _featureFlagService.SetFlagAsync(featureContext, false, ct)
                    .ConfigureAwait(false);

                _logger.LogError(
                    "SLA breach AUTO-DISABLE | p95={P95}ms > threshold={Threshold}ms | " +
                    "featureContext={FeatureContext} — feature disabled automatically. " +
                    "Re-enable via POST /api/v1/admin/ai/features/{FeatureContextRoute}/toggle",
                    (long)p95, _slaOptions.Value.P95ThresholdMs, featureContext, featureContext);
            }
            else
            {
                _logger.LogError(
                    "SLA breach CRITICAL feature — NOT auto-disabled | " +
                    "p95={P95}ms > threshold={Threshold}ms featureContext={FeatureContext}",
                    (long)p95, _slaOptions.Value.P95ThresholdMs, featureContext);
            }
        }
    }
}

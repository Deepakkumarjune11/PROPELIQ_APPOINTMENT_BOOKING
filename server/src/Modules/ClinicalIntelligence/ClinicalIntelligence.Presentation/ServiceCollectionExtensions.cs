using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using ClinicalIntelligence.Application.AI;
using ClinicalIntelligence.Application.AI.Access;
using ClinicalIntelligence.Application.AI.Availability;
using ClinicalIntelligence.Application.AI.FeatureFlags;
using ClinicalIntelligence.Application.AI.Latency;
using ClinicalIntelligence.Application.AI.ModelVersion;
using ClinicalIntelligence.Application.AI.Safety;
using ClinicalIntelligence.Application.AI.Schema;
using ClinicalIntelligence.Application.AI.Sanitization;
using ClinicalIntelligence.Application.Documents.Jobs;
using ClinicalIntelligence.Application.Documents.Queries.GetPatientDocuments;
using ClinicalIntelligence.Application.Documents.Services;
using ClinicalIntelligence.Application.Infrastructure;
using ClinicalIntelligence.Data.Repositories;
using ClinicalIntelligence.Data.Services;
using ClinicalIntelligence.Presentation.ExceptionHandling;
using ClinicalIntelligence.Presentation.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace ClinicalIntelligence.Presentation;

/// <summary>
/// Registers all ClinicalIntelligence module services into the DI container.
/// Called once from Program.cs during application startup.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddClinicalIntelligenceModule(
        this IServiceCollection services,
        IConfiguration          configuration)
    {
        // ── MediatR ───────────────────────────────────────────────────────
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(GetPatientDocumentsHandler).Assembly));

        // ── Repositories ─────────────────────────────────────────────────
        // Scoped: wraps PropelIQDbContext which is also scoped.
        services.AddScoped<IClinicalDocumentRepository, ClinicalDocumentRepository>();

        // Real implementation: DocumentChunkEmbeddingRepository handles both
        // IEmbeddingChunkRepository and IChunkStagingService (us_019/task_003).
        services.AddScoped<DocumentChunkEmbeddingRepository>();
        services.AddScoped<IEmbeddingChunkRepository>(sp =>
            sp.GetRequiredService<DocumentChunkEmbeddingRepository>());

        // ── File storage ─────────────────────────────────────────────────
        // Singleton: no mutable state; each StoreAsync call creates its own FileStream.
        services.AddSingleton<IFileStorageService, LocalFileStorageService>();

        // ── PDF + Chunking services (US_019/task_001) ─────────────────────
        services.AddScoped<PdfTextExtractor>();
        services.AddScoped<DocumentChunker>();

        services.AddScoped<IChunkStagingService>(sp =>
            sp.GetRequiredService<DocumentChunkEmbeddingRepository>());

        // ── AI Gateway (US_019/task_002) ──────────────────────────────────
        // Embedding cache — adapts shared IConnectionMultiplexer (registered by Program.cs).
        services.AddSingleton<IAiEmbeddingCache, RedisEmbeddingCacheAdapter>();

        // Azure OpenAI embedding options (same AzureOpenAI section as PatientAccess).
        services.Configure<AzureOpenAIEmbeddingOptions>(
            configuration.GetSection(AzureOpenAIEmbeddingOptions.SectionName));

        // ── Azure OpenAI hardened gateway options (US_030 / AC-1) ─────────
        // Separate config section "AzureOpenAi" (camelCase) for the inference gateway.
        // Managed identity (DefaultAzureCredential) — no API key stored in config (OWASP A02).
        services.Configure<AzureOpenAiOptions>(
            configuration.GetSection(AzureOpenAiOptions.SectionName));

        // Shared AzureOpenAIClient singleton — DefaultAzureCredential resolves via MSI /
        // Workload Identity / local developer credential chain at runtime.
        services.AddSingleton<AzureOpenAIClient>(sp =>
        {
            var openAiOpts = configuration
                .GetSection(AzureOpenAiOptions.SectionName)
                .Get<AzureOpenAiOptions>();

            if (openAiOpts is null || string.IsNullOrWhiteSpace(openAiOpts.Endpoint))
            {
                // Not configured in this environment — gateway will log a warning on each call
                return new AzureOpenAIClient(
                    new Uri("https://placeholder.openai.azure.com/"),
                    new Azure.AzureKeyCredential("placeholder"));
            }

            return new AzureOpenAIClient(
                new Uri(openAiOpts.Endpoint),
                new DefaultAzureCredential());
        });

        // ── Prompt Safety (US_029/task_001) ───────────────────────────────
        // Singleton: stateless pipeline; IOptionsMonitor provides hot-reload.
        services.Configure<PromptInjectionOptions>(
            configuration.GetSection(PromptInjectionOptions.SectionName));
        services.AddSingleton<IPromptSanitizer, PromptSanitizer>();

        // ── AI Availability & Degradation (US_030/task_002, AC-5) ─────────
        // InMemoryAvailabilityState: volatile bool singleton — replaced by US_031 circuit breaker.
        services.AddSingleton<IAiAvailabilityState, InMemoryAvailabilityState>();
        // AiDegradationHandler: singleton (safe — IDistributedCache/Redis is also registered as singleton
        // via AddStackExchangeRedisCache; avoids captive dependency with the gateway singleton).
        services.AddSingleton<IAiDegradationHandler, AiDegradationHandler>();
        // ── Content Safety Filter (US_031/task_002, AIR-O03, AIR-S04) ──────────────────
        // Transient: stateless 3-layer regex filter; IOptionsMonitor provides hot-reload.
        services.Configure<ContentSafetyOptions>(
            configuration.GetSection(ContentSafetyOptions.SectionName));
        services.AddTransient<IContentSafetyFilter, ContentSafetyFilter>();

        // ── Model Version Service (US_031/task_002, AC-5, AIR-O04) ────────────────────
        // Singleton: Redis connection is thread-safe; deployment name reads are sub-millisecond.
        services.AddSingleton<IModelVersionService, RedisModelVersionService>();

        // ── AI Latency Recorder + SLA Options (US_032/task_001, AC-1, AIR-Q02) ──────────
        // Singleton: wraps IConnectionMultiplexer (thread-safe); all operations are Redis I/O.
        services.Configure<AiSlaOptions>(configuration.GetSection(AiSlaOptions.SectionName));
        services.AddSingleton<ILatencyRecorder, RedisLatencyRecorder>();

        // ── AI Schema Validator (US_032/task_001, AC-2, AIR-Q03) ───────────────────
        // Singleton: stateless (uses static AiSchemaRegistry + System.Text.Json parsing).
        services.AddSingleton<IAiSchemaValidator, JsonDocumentSchemaValidator>();

        // ── AI Feature Flags (US_032/task_002, AC-4, AC-5, TR-025) ─────────────────
        // Singleton: IConnectionMultiplexer is thread-safe; direct Redis read per request.
        services.Configure<AiFeatureFlagsOptions>(
            configuration.GetSection(AiFeatureFlagsOptions.SectionName));
        services.AddSingleton<IFeatureFlagService, RedisFeatureFlagService>();

        // ── Polly v8 resilience pipeline: circuit breaker (outer) + retry (inner) (US_031, AIR-O02) ──
        // The builder action is lazy — it executes on the first GetPipeline("azure-openai") call.
        // Execution order: [CircuitBreaker] → [Retry 3×] → [GPT/embedding call]
        // Circuit breaker is outer: counts one failure per original request (after all retries exhausted).
        services.AddResiliencePipeline("azure-openai", (builder, context) =>
        {
            var opts = context.ServiceProvider
                .GetRequiredService<IOptions<AzureOpenAiOptions>>().Value;
            var availabilityState = context.ServiceProvider
                .GetRequiredService<IAiAvailabilityState>();
            var logger = context.ServiceProvider
                .GetRequiredService<ILogger<AzureOpenAiGateway>>();

            // Strategy 1 (outer): Circuit Breaker (AIR-O02, AC-1)
            // FailureRatio=1.0 + MinimumThroughput=FailureThreshold:
            //   ALL N requests in the sampling window must fail before the circuit opens.
            builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio      = 1.0,
                MinimumThroughput = opts.CircuitBreaker.FailureThreshold,
                SamplingDuration  = TimeSpan.FromSeconds(opts.CircuitBreaker.SamplingWindowSeconds),
                BreakDuration     = TimeSpan.FromSeconds(opts.CircuitBreaker.BreakDurationSeconds),
                ShouldHandle      = new PredicateBuilder()
                    .Handle<RequestFailedException>(ex => ex.Status is 429 or 503)
                    .Handle<HttpRequestException>(),

                // OnOpened: mark AI as degraded — subsequent ChatCompletionAsync calls route to AiDegradationHandler (AC-1)
                OnOpened = args =>
                {
                    availabilityState.MarkDegraded("circuit-breaker-open");
                    logger.LogWarning(
                        "AI circuit breaker OPENED after {FailureThreshold} failures in {Window}s. " +
                        "Breaking for {Break}s. Exception: {ExceptionType}",
                        opts.CircuitBreaker.FailureThreshold,
                        opts.CircuitBreaker.SamplingWindowSeconds,
                        opts.CircuitBreaker.BreakDurationSeconds,
                        args.Outcome.Exception?.GetType().Name ?? "unknown");
                    return default;
                },

                // OnHalfOpened: break duration elapsed — Polly allows one test request through (AC-2)
                OnHalfOpened = _ =>
                {
                    logger.LogWarning(
                        "AI circuit breaker HALF-OPEN — probing with single test request.");
                    return default;
                },

                // OnClosed: test request succeeded — resume normal AI traffic (AC-2)
                OnClosed = _ =>
                {
                    availabilityState.MarkRecovered();
                    logger.LogInformation(
                        "AI circuit breaker CLOSED — normal AI operation resumed.");
                    return default;
                },
            });

            // Strategy 2 (inner): Retry — 3 attempts, Retry-After header aware, exponential fallback (US_030/AC-3)
            // Inner placement: the circuit breaker counts one failure per original request, not per retry attempt.
            builder.AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                ShouldHandle     = new PredicateBuilder()
                    .Handle<RequestFailedException>(ex => ex.Status is 429 or 503)
                    .Handle<HttpRequestException>(),
                DelayGenerator   = static args =>
                {
                    if (args.Outcome.Exception is RequestFailedException { Status: 429 } rfEx)
                    {
                        var resp = rfEx.GetRawResponse();
                        if (resp is not null
                            && resp.Headers.TryGetValue("Retry-After", out var retryAfterStr)
                            && int.TryParse(retryAfterStr, out var secs)
                            && secs is >= 1 and <= 4)
                            return ValueTask.FromResult<TimeSpan?>(TimeSpan.FromSeconds(secs));
                    }

                    // Exponential back-off: attempt 0 → 1 s, 1 → 2 s, 2 → 4 s
                    return ValueTask.FromResult<TimeSpan?>(
                        TimeSpan.FromSeconds(Math.Pow(2, args.AttemptNumber)));
                },
            });
        });

        // Gateway — singleton: Polly circuit breaker state must survive across job invocations.
        // Registered as both IAiGateway and IChatCompletionGateway (same singleton instance).
        services.AddSingleton<AzureOpenAiGateway>();
        services.AddSingleton<IAiGateway>(sp => sp.GetRequiredService<AzureOpenAiGateway>());
        services.AddSingleton<IChatCompletionGateway>(sp => sp.GetRequiredService<AzureOpenAiGateway>());

        // Document similarity search (AC-3, TR-015).
        services.AddScoped<IDocumentSearchService, DocumentSearchService>();

        // RAG access filter — scoped: wraps PropelIQDbContext (US_029/task_001, AIR-S02).
        services.AddScoped<IRagAccessFilter, RagAccessFilter>();

        // Context assembler: re-rank + 3,000-token window assembly (AIR-R03, AIR-R04).
        services.AddScoped<ContextAssembler>();

        // Fact persistence — threshold split, PHI encryption, status transition, 360-view trigger (US_020/task_002).
        services.AddScoped<IFactPersistenceService, FactPersistenceService>();

        // 360-view assembler — semantic de-duplication with cosine similarity (US_021, AIR-003).
        services.AddScoped<PatientView360Assembler>();

        // 360-view upsert service — DB access, decrypt, assemble, encrypt, optimistic-concurrency upsert (US_021).
        services.AddScoped<IPatientView360UpsertService, PatientView360UpsertService>();

        // Conflict detection — embedding-based conflict scan after 360-view assembly (US_022, AIR-004).
        services.AddScoped<IConflictDetectionService, ConflictDetectionService>();

        // Code suggestion persistence — load + decrypt patient facts, soft-delete + insert suggestions (US_023).
        services.AddScoped<ICodeSuggestionPersistenceService, CodeSuggestionPersistenceService>();

        // ── Exception handling ────────────────────────────────────────────
        services.AddExceptionHandler<ClinicalIntelligenceExceptionHandler>();

        // ── Hangfire jobs ─────────────────────────────────────────────────
        // Transient: new instance per job invocation; avoids context contamination.
        services.AddTransient<DocumentExtractionJob>();
        services.AddTransient<EmbeddingGenerationJob>();
        services.AddTransient<FactExtractionJob>();
        services.AddTransient<PatientView360UpdateJob>();
        services.AddTransient<ConflictDetectionJob>();
        services.AddTransient<CodeSuggestionJob>();

        return services;
    }
}


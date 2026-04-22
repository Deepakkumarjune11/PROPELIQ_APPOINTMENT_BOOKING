using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClinicalIntelligence.Application.AI.Availability;

/// <summary>
/// Redis-backed implementation of <see cref="IAiDegradationHandler"/> (US_030 / AC-5).
///
/// Degradation pipeline:
/// <list type="number">
///   <item>Look up <c>ai_cache:{featureContext}</c> in Redis for the last successful response.</item>
///   <item>On cache hit: deserialise and return the stale <see cref="GatewayResponse"/>;
///       log <c>source=cached</c> per AIR-S03.</item>
///   <item>On cache miss or Redis error: return a static <see cref="GatewayResponse"/> using
///       <see cref="AzureOpenAiOptions.DegradationMessage"/>; log <c>source=static</c>.</item>
/// </list>
///
/// This handler NEVER propagates exceptions — all Redis errors are caught internally
/// so degradation cannot cause a secondary failure in the calling pipeline.
///
/// Registered as <c>Scoped</c> because <see cref="IDistributedCache"/> is scoped in some
/// ASP.NET Core configurations and Hangfire jobs resolve a fresh scope per invocation.
/// </summary>
public sealed class AiDegradationHandler : IAiDegradationHandler
{
    internal const string CacheKeyPrefix = "ai_cache:";

    private readonly IDistributedCache             _cache;
    private readonly AzureOpenAiOptions            _opts;
    private readonly ILogger<AiDegradationHandler> _logger;

    public AiDegradationHandler(
        IDistributedCache              cache,
        IOptions<AzureOpenAiOptions>   options,
        ILogger<AiDegradationHandler>  logger)
    {
        _cache  = cache;
        _opts   = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<GatewayResponse> GetDegradedResponseAsync(
        string featureContext, CancellationToken ct = default)
    {
        try
        {
            var cacheKey = $"{CacheKeyPrefix}{featureContext}";
            var cached   = await _cache.GetStringAsync(cacheKey, ct);

            if (cached is not null)
            {
                var staleResponse = JsonSerializer.Deserialize<GatewayResponse>(cached);
                if (staleResponse is not null)
                {
                    // AIR-S03: always log when serving stale data so operators can audit.
                    // NEVER silently serve stale AI content without a visible log entry.
                    _logger.LogWarning(
                        "AI degraded mode: returning cached response | featureContext={FeatureContext} " +
                        "source=cached — results may be outdated.",
                        featureContext);

                    return staleResponse;
                }
            }
        }
        catch (Exception cacheEx)
        {
            // Redis unavailable or deserialisation failure — MUST NOT propagate (task AC-5 edge case).
            _logger.LogError(cacheEx,
                "AI degradation cache read failed for featureContext={FeatureContext}. " +
                "Falling through to static degradation message.",
                featureContext);
        }

        // Final fallback: static message — guaranteed non-null path (OWASP A05: no exception exposure).
        var msg = !string.IsNullOrWhiteSpace(_opts.DegradationMessage)
            ? _opts.DegradationMessage
            : "AI assistance is temporarily unavailable. Please proceed manually or try again shortly.";

        _logger.LogWarning(
            "AI degraded mode: returning static degradation message | featureContext={FeatureContext} source=static",
            featureContext);

        return new GatewayResponse(msg, InputTokens: 0, OutputTokens: 0, IsTruncated: false, FeatureContext: featureContext);
    }
}

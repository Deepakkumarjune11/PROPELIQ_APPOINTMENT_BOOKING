using System.Text.Json;
using ClinicalIntelligence.Application.Infrastructure;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ClinicalIntelligence.Presentation.Services;

/// <summary>
/// Implements <see cref="IAiEmbeddingCache"/> using the shared <see cref="IConnectionMultiplexer"/>
/// registered by <c>PropelIQ.Api/Program.cs</c> (Upstash Redis, TR-004).
///
/// Follows the same graceful-degradation pattern as <c>RedisCacheService</c>:
/// <see cref="RedisConnectionException"/> is caught and logged as a warning rather than
/// rethrown — the AI gateway falls back to calling Azure OpenAI on cache miss.
///
/// JSON-serialises <c>float[]</c> to avoid binary encoding incompatibilities across
/// .NET versions and Redis client libraries.
/// </summary>
public sealed class RedisEmbeddingCacheAdapter : IAiEmbeddingCache
{
    private readonly IConnectionMultiplexer               _redis;
    private readonly ILogger<RedisEmbeddingCacheAdapter>  _logger;

    public RedisEmbeddingCacheAdapter(
        IConnectionMultiplexer              redis,
        ILogger<RedisEmbeddingCacheAdapter> logger)
    {
        _redis  = redis;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<float[]?> GetAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var db    = _redis.GetDatabase();
            var value = await db.StringGetAsync(key).ConfigureAwait(false);

            if (!value.HasValue) return null;

            return JsonSerializer.Deserialize<float[]>(value!);
        }
        catch (RedisConnectionException)
        {
            _logger.LogWarning("EmbeddingCache miss (Redis unavailable) for key {Key}.", key);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task SetAsync(string key, float[] vector, TimeSpan expiry, CancellationToken ct = default)
    {
        try
        {
            var db   = _redis.GetDatabase();
            var json = JsonSerializer.Serialize(vector);
            await db.StringSetAsync(key, json, expiry).ConfigureAwait(false);
        }
        catch (RedisConnectionException)
        {
            _logger.LogWarning("EmbeddingCache set skipped (Redis unavailable) for key {Key}.", key);
        }
    }
}

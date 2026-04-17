using System.Text.Json;
using PatientAccess.Application.Infrastructure;
using StackExchange.Redis;

namespace PropelIQ.Api.Infrastructure.Caching;

/// <summary>
/// Redis-backed implementation of <see cref="ICacheService"/> using
/// <see cref="IConnectionMultiplexer"/>. All methods catch
/// <see cref="RedisConnectionException"/> and log a structured warning rather than
/// rethrowing — enabling graceful fallback to direct database queries per TR-004 edge case.
/// </summary>
public sealed class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger)
    {
        _redis   = redis;
        _logger  = logger;
    }

    /// <inheritdoc/>
    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        try
        {
            var db    = _redis.GetDatabase();
            var value = await db.StringGetAsync(key).ConfigureAwait(false);

            if (!value.HasValue)
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(value!);
        }
        catch (RedisConnectionException)
        {
            _logger.LogWarning("Cache miss (Redis unavailable) for key {Key}", key);
            return default;
        }
    }

    /// <inheritdoc/>
    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        try
        {
            var db   = _redis.GetDatabase();
            var json = JsonSerializer.Serialize(value);
            if (expiry.HasValue)
                await db.StringSetAsync(key, json, expiry.Value).ConfigureAwait(false);
            else
                await db.StringSetAsync(key, json).ConfigureAwait(false);
        }
        catch (RedisConnectionException)
        {
            _logger.LogWarning("Cache set skipped (Redis unavailable) for key {Key}", key);
        }
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(key).ConfigureAwait(false);
        }
        catch (RedisConnectionException)
        {
            _logger.LogWarning("Cache remove skipped (Redis unavailable) for key {Key}", key);
        }
    }
}

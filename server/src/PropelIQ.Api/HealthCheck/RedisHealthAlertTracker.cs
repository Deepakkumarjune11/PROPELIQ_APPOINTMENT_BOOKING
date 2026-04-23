using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace PropelIQ.Api.HealthCheck;

/// <summary>
/// Redis-backed <see cref="IHealthAlertTracker"/> implementation.
/// Key pattern: <c>hc:failures:{checkName}</c> — auto-expires after 2 minutes to clear stale
/// failure counts when a service recovers and the Hangfire job interval lapses.
/// All Redis calls degrade gracefully when Redis is unavailable.
/// </summary>
public sealed class RedisHealthAlertTracker(
    IConnectionMultiplexer redis,
    ILogger<RedisHealthAlertTracker> logger) : IHealthAlertTracker
{
    private static string FailureKey(string name) => $"hc:failures:{name}";

    public async Task<long> IncrementFailureAsync(string checkName, CancellationToken ct = default)
    {
        try
        {
            var db = redis.GetDatabase();
            var count = await db.StringIncrementAsync(FailureKey(checkName)).ConfigureAwait(false);
            await db.KeyExpireAsync(FailureKey(checkName), TimeSpan.FromMinutes(2)).ConfigureAwait(false);
            return count;
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Redis unavailable — failure counter for '{CheckName}' not persisted", checkName);
            return 1;
        }
    }

    public async Task ResetAsync(string checkName, CancellationToken ct = default)
    {
        try
        {
            await redis.GetDatabase().KeyDeleteAsync(FailureKey(checkName)).ConfigureAwait(false);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Redis unavailable — failure counter reset for '{CheckName}' skipped", checkName);
        }
    }

    public async Task<long> GetConsecutiveFailuresAsync(string checkName, CancellationToken ct = default)
    {
        try
        {
            var val = await redis.GetDatabase().StringGetAsync(FailureKey(checkName)).ConfigureAwait(false);
            return val.HasValue && long.TryParse(val, out var n) ? n : 0;
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Redis unavailable — returning 0 for failure count of '{CheckName}'", checkName);
            return 0;
        }
    }
}

using StackExchange.Redis;

namespace PropelIQ.Api.HealthCheck;

/// <summary>
/// Redis-backed <see cref="IHealthAlertTracker"/> implementation.
/// Key pattern: <c>hc:failures:{checkName}</c> — auto-expires after 2 minutes to clear stale
/// failure counts when a service recovers and the Hangfire job interval lapses.
/// </summary>
public sealed class RedisHealthAlertTracker(IConnectionMultiplexer redis) : IHealthAlertTracker
{
    private static string FailureKey(string name) => $"hc:failures:{name}";

    public async Task<long> IncrementFailureAsync(string checkName, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var count = await db.StringIncrementAsync(FailureKey(checkName)).ConfigureAwait(false);
        // 2-minute TTL — auto-clears stale failure counts between polling intervals
        await db.KeyExpireAsync(FailureKey(checkName), TimeSpan.FromMinutes(2)).ConfigureAwait(false);
        return count;
    }

    public Task ResetAsync(string checkName, CancellationToken ct = default)
        => redis.GetDatabase().KeyDeleteAsync(FailureKey(checkName));

    public async Task<long> GetConsecutiveFailuresAsync(string checkName, CancellationToken ct = default)
    {
        var val = await redis.GetDatabase().StringGetAsync(FailureKey(checkName)).ConfigureAwait(false);
        return val.HasValue && long.TryParse(val, out var n) ? n : 0;
    }
}

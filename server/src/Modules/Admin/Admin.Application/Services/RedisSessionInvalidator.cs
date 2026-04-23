using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Admin.Application.Services;

/// <summary>
/// Stores a per-user invalidation flag in Redis.
/// Key: <c>session_invalidated:{userId}</c>; TTL: 15 minutes (JWT max lifetime).
/// Checked in <c>OnTokenValidated</c> to reject tokens for disabled/role-changed users.
/// Degrades gracefully when Redis is unavailable — operations are logged as warnings
/// rather than propagating <see cref="RedisConnectionException"/> (NFR-009).
/// </summary>
public sealed class RedisSessionInvalidator(
    IDistributedCache cache,
    ILogger<RedisSessionInvalidator> logger) : ISessionInvalidator
{
    private static string UserKey(Guid userId) => $"session_invalidated:{userId}";

    /// <inheritdoc/>
    public async Task InvalidateSessionAsync(Guid userId, CancellationToken ct)
    {
        try
        {
            await cache.SetStringAsync(
                UserKey(userId),
                "1",
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
                },
                ct);
        }
        catch (RedisConnectionException ex)
        {
            // Redis unavailable — session invalidation skipped. The user's current JWT
            // will remain valid until its natural 15-min expiry (acceptable degraded state).
            logger.LogWarning(ex, "Redis unavailable; session invalidation skipped for user {UserId}", userId);
        }
        catch (Exception ex) when (ex is RedisTimeoutException or RedisServerException)
        {
            logger.LogWarning(ex, "Redis error; session invalidation skipped for user {UserId}", userId);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsUserInvalidatedAsync(Guid userId, CancellationToken ct)
    {
        try
        {
            return await cache.GetStringAsync(UserKey(userId), ct) is not null;
        }
        catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException or RedisServerException)
        {
            // Redis unavailable — assume token is valid so legitimate users are not locked out.
            logger.LogWarning(ex, "Redis unavailable; token invalidation check skipped for user {UserId}", userId);
            return false;
        }
    }
}

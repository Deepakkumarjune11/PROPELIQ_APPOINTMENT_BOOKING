using Microsoft.Extensions.Caching.Distributed;

namespace Admin.Application.Services;

/// <summary>
/// Stores a per-user invalidation flag in Redis.
/// Key: <c>session_invalidated:{userId}</c>; TTL: 15 minutes (JWT max lifetime).
/// Checked in <c>OnTokenValidated</c> to reject tokens for disabled/role-changed users.
/// </summary>
public sealed class RedisSessionInvalidator(IDistributedCache cache) : ISessionInvalidator
{
    private static string UserKey(Guid userId) => $"session_invalidated:{userId}";

    /// <inheritdoc/>
    public Task InvalidateSessionAsync(Guid userId, CancellationToken ct) =>
        cache.SetStringAsync(
            UserKey(userId),
            "1",
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
            },
            ct);

    /// <inheritdoc/>
    public async Task<bool> IsUserInvalidatedAsync(Guid userId, CancellationToken ct) =>
        await cache.GetStringAsync(UserKey(userId), ct) is not null;
}

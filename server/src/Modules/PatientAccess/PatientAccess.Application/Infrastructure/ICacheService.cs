namespace PatientAccess.Application.Infrastructure;

/// <summary>
/// Abstraction for distributed caching operations.
/// Lives in the Application layer so domain/application services depend only on the
/// interface, not on a Redis-specific implementation — enabling clean testing via mocks.
/// </summary>
public interface ICacheService
{
    /// <summary>Retrieves a cached value by key. Returns <c>null</c> on cache miss or Redis unavailability.</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);

    /// <summary>Stores a value in the cache with an optional TTL. Silently no-ops when Redis is unavailable.</summary>
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default);

    /// <summary>Removes a key from the cache. Silently no-ops when Redis is unavailable.</summary>
    Task RemoveAsync(string key, CancellationToken ct = default);
}

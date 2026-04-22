namespace PropelIQ.Api.Infrastructure.Caching;

/// <summary>
/// Configurable TTLs and window sizes for slot and provider schedule caching (US_035, AC-3).
/// Bound from <c>appsettings.json → "Cache"</c> section.
/// </summary>
public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    /// <summary>TTL for slot availability cache per provider per date (seconds). Default 5 minutes.</summary>
    public int SlotAvailabilityTtlSeconds { get; set; } = 300;

    /// <summary>TTL for provider weekly schedule cache (seconds). Default 60 minutes.</summary>
    public int ProviderScheduleTtlSeconds { get; set; } = 3_600;

    /// <summary>Rolling window size for cache hit/miss ratio counters (seconds). Default 5 minutes.</summary>
    public int HitRatioWindowSeconds { get; set; } = 300;
}

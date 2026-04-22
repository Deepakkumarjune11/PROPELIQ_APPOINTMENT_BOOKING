namespace PropelIQ.Api.Infrastructure.Caching;

/// <summary>
/// Typed cache contract for slot availability and provider schedule data (US_035, AC-3).
/// Wraps <see cref="PatientAccess.Application.Infrastructure.ICacheService"/> with explicit
/// Redis key conventions, configurable TTLs, and cache hit ratio tracking.
///
/// Redis key patterns:
/// <list type="bullet">
///   <item><c>slots:availability:{staffId}:{date:yyyy-MM-dd}</c> — available slots for a provider on a date</item>
///   <item><c>provider:schedule:{staffId}:week:{year}-{week:D2}</c> — provider weekly schedule</item>
///   <item><c>cache:hits:slots</c> / <c>cache:misses:slots</c> — rolling counters (TTL = HitRatioWindowSeconds)</item>
/// </list>
/// </summary>
public interface ISlotCacheService
{
    /// <summary>Returns cached available slots for <paramref name="staffId"/> on <paramref name="date"/>, or <c>null</c> on cache miss.</summary>
    Task<IReadOnlyList<AvailableSlotDto>?> GetAvailableSlotsAsync(
        Guid staffId, DateOnly date, CancellationToken ct = default);

    /// <summary>Stores available slots for <paramref name="staffId"/> on <paramref name="date"/> with configured TTL.</summary>
    Task SetAvailableSlotsAsync(
        Guid staffId, DateOnly date, IReadOnlyList<AvailableSlotDto> slots,
        CancellationToken ct = default);

    /// <summary>Removes the slot availability cache entry for <paramref name="staffId"/> on <paramref name="date"/>. Call on booking or cancellation.</summary>
    Task InvalidateAvailableSlotsAsync(
        Guid staffId, DateOnly date, CancellationToken ct = default);

    /// <summary>Returns the cached provider schedule for the given ISO week, or <c>null</c> on cache miss.</summary>
    Task<ProviderScheduleDto?> GetProviderScheduleAsync(
        Guid staffId, int isoWeekNumber, int year, CancellationToken ct = default);

    /// <summary>Stores the provider weekly schedule with configured TTL.</summary>
    Task SetProviderScheduleAsync(
        Guid staffId, int isoWeekNumber, int year, ProviderScheduleDto schedule,
        CancellationToken ct = default);

    /// <summary>Removes all cached weekly schedule entries for <paramref name="staffId"/> via SCAN. Call on provider schedule change.</summary>
    Task InvalidateProviderScheduleAsync(
        Guid staffId, CancellationToken ct = default);

    /// <summary>
    /// Returns the slot cache hit ratio % within the rolling <see cref="CacheOptions.HitRatioWindowSeconds"/> window.
    /// Returns <c>null</c> if no data has been recorded yet.
    /// </summary>
    Task<double?> GetHitRatioAsync(CancellationToken ct = default);
}

/// <summary>Minimal read-side projection of a slot, used for cache serialization.</summary>
public sealed record AvailableSlotDto(DateTime SlotDatetime, Guid StaffId, bool IsAvailable);

/// <summary>Provider weekly schedule cache entry.</summary>
public sealed record ProviderScheduleDto(Guid StaffId, IReadOnlyList<DayScheduleDto> Days);

/// <summary>Single-day schedule for a provider.</summary>
public sealed record DayScheduleDto(DateOnly Date, IReadOnlyList<AvailableSlotDto> Slots);

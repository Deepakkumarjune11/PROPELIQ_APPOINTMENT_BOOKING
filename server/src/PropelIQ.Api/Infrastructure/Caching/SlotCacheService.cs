using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PatientAccess.Application.Infrastructure;
using StackExchange.Redis;

namespace PropelIQ.Api.Infrastructure.Caching;

/// <summary>
/// Redis-backed <see cref="ISlotCacheService"/> that wraps <see cref="ICacheService"/>
/// with slot-specific key conventions, configurable TTLs, and INCR-based hit/miss counters.
///
/// Cache invalidation:
/// <list type="bullet">
///   <item>Booking / cancellation → call <see cref="InvalidateAvailableSlotsAsync"/> for the affected date</item>
///   <item>Provider schedule change → call <see cref="InvalidateProviderScheduleAsync"/> to SCAN-delete all weekly keys</item>
/// </list>
/// </summary>
public sealed class SlotCacheService(
    ICacheService cache,
    IConnectionMultiplexer redis,
    IOptions<CacheOptions> opts,
    ILogger<SlotCacheService> logger) : ISlotCacheService
{
    private static string SlotKey(Guid staffId, DateOnly date)
        => $"slots:availability:{staffId}:{date:yyyy-MM-dd}";

    private static string ScheduleKey(Guid staffId, int week, int year)
        => $"provider:schedule:{staffId}:week:{year}-{week:D2}";

    private const string HitCounterKey  = "cache:hits:slots";
    private const string MissCounterKey = "cache:misses:slots";

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AvailableSlotDto>?> GetAvailableSlotsAsync(
        Guid staffId, DateOnly date, CancellationToken ct = default)
    {
        var result = await cache.GetAsync<List<AvailableSlotDto>>(SlotKey(staffId, date), ct)
            .ConfigureAwait(false);

        var db  = redis.GetDatabase();
        var ttl = TimeSpan.FromSeconds(opts.Value.HitRatioWindowSeconds);

        if (result is not null)
        {
            await db.StringIncrementAsync(HitCounterKey).ConfigureAwait(false);
            await db.KeyExpireAsync(HitCounterKey, ttl, CommandFlags.FireAndForget)
                .ConfigureAwait(false);
        }
        else
        {
            await db.StringIncrementAsync(MissCounterKey).ConfigureAwait(false);
            await db.KeyExpireAsync(MissCounterKey, ttl, CommandFlags.FireAndForget)
                .ConfigureAwait(false);
        }

        return result;
    }

    /// <inheritdoc/>
    public Task SetAvailableSlotsAsync(
        Guid staffId, DateOnly date, IReadOnlyList<AvailableSlotDto> slots,
        CancellationToken ct = default)
        => cache.SetAsync(
            SlotKey(staffId, date),
            slots,
            TimeSpan.FromSeconds(opts.Value.SlotAvailabilityTtlSeconds),
            ct);

    /// <inheritdoc/>
    public Task InvalidateAvailableSlotsAsync(
        Guid staffId, DateOnly date, CancellationToken ct = default)
        => cache.RemoveAsync(SlotKey(staffId, date), ct);

    /// <inheritdoc/>
    public async Task<ProviderScheduleDto?> GetProviderScheduleAsync(
        Guid staffId, int isoWeekNumber, int year, CancellationToken ct = default)
        => await cache.GetAsync<ProviderScheduleDto>(
            ScheduleKey(staffId, isoWeekNumber, year), ct)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public Task SetProviderScheduleAsync(
        Guid staffId, int isoWeekNumber, int year, ProviderScheduleDto schedule,
        CancellationToken ct = default)
        => cache.SetAsync(
            ScheduleKey(staffId, isoWeekNumber, year),
            schedule,
            TimeSpan.FromSeconds(opts.Value.ProviderScheduleTtlSeconds),
            ct);

    /// <inheritdoc/>
    public async Task InvalidateProviderScheduleAsync(
        Guid staffId, CancellationToken ct = default)
    {
        var db      = redis.GetDatabase();
        var server  = redis.GetServer(redis.GetEndPoints()[0]);
        var pattern = $"provider:schedule:{staffId}:*";
        var deleted = 0;

        // KeysAsync uses cursor-based SCAN — non-blocking, safe for production
        await foreach (var key in server.KeysAsync(pattern: pattern))
        {
            await db.KeyDeleteAsync(key).ConfigureAwait(false);
            deleted++;
        }

        logger.LogDebug(
            "Invalidated {Count} schedule cache entries for staff {StaffId}",
            deleted, staffId);
    }

    /// <inheritdoc/>
    public async Task<double?> GetHitRatioAsync(CancellationToken ct = default)
    {
        var db     = redis.GetDatabase();
        var hits   = await db.StringGetAsync(HitCounterKey).ConfigureAwait(false);
        var misses = await db.StringGetAsync(MissCounterKey).ConfigureAwait(false);

        if (!hits.HasValue && !misses.HasValue)
            return null;

        var h     = hits.HasValue   && long.TryParse(hits,   out var hv) ? hv : 0L;
        var m     = misses.HasValue && long.TryParse(misses, out var mv) ? mv : 0L;
        var total = h + m;

        return total == 0 ? null : Math.Round((double)h / total * 100.0, 2);
    }
}

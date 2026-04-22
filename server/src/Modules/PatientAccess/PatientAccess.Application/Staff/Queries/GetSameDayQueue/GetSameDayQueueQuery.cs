using MediatR;
using Microsoft.Extensions.Logging;
using PatientAccess.Application.Infrastructure;
using PatientAccess.Application.Repositories;
using PatientAccess.Application.Staff.Dtos;

namespace PatientAccess.Application.Staff.Queries.GetSameDayQueue;

/// <summary>
/// Returns today's same-day queue ordered by queue_position (US_017, AC-1, AC-4).
/// Cache strategy: Redis hit → return; miss → DB query → rebuild with 30s TTL.
/// </summary>
public sealed record GetSameDayQueueQuery : IRequest<IReadOnlyList<QueueEntryDto>>;

/// <summary>
/// Handles <see cref="GetSameDayQueueQuery"/>.
/// Redis-first cache read (30s TTL) with DB fallback on miss (NFR-001, AC-4).
/// </summary>
public sealed class GetSameDayQueueHandler
    : IRequestHandler<GetSameDayQueueQuery, IReadOnlyList<QueueEntryDto>>
{
    private const string CacheKey = "staff:queue:today";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    private readonly IQueueRepository                   _repo;
    private readonly ICacheService                      _cache;
    private readonly ILogger<GetSameDayQueueHandler>    _logger;

    public GetSameDayQueueHandler(
        IQueueRepository                repo,
        ICacheService                   cache,
        ILogger<GetSameDayQueueHandler> logger)
    {
        _repo   = repo;
        _cache  = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<QueueEntryDto>> Handle(
        GetSameDayQueueQuery query,
        CancellationToken    cancellationToken)
    {
        // ── Redis cache hit (AC-4) ─────────────────────────────────────────
        var cached = await _cache.GetAsync<List<QueueEntryDto>>(CacheKey, cancellationToken);
        if (cached is not null)
        {
            _logger.LogDebug("SameDayQueue: cache hit ({Count} entries).", cached.Count);
            return cached;
        }

        // ── Cache miss — query DB and rebuild ─────────────────────────────
        var entries = await _repo.GetTodayQueueAsync(cancellationToken);

        await _cache.SetAsync(CacheKey, entries, CacheTtl, cancellationToken);

        _logger.LogDebug("SameDayQueue: cache miss — fetched {Count} entries from DB.", entries.Count);

        return entries;
    }
}

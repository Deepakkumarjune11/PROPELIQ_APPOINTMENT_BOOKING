using MediatR;
using Microsoft.Extensions.Logging;
using PatientAccess.Application.Infrastructure;
using PatientAccess.Application.Repositories;
using PatientAccess.Application.Services;

namespace PatientAccess.Application.Queries.GetAvailability;

/// <summary>
/// Handles <see cref="GetAvailabilityQuery"/> using a cache-aside pattern:
/// <list type="number">
///   <item>Check Redis cache (60-second TTL) — AC-2.</item>
///   <item>On cache miss, query DB via <see cref="IAvailabilityRepository"/> — AC-3.</item>
///   <item>Populate cache, return results.</item>
/// </list>
/// When Redis is unavailable, <see cref="ICacheService.GetAsync{T}"/> returns <c>null</c>;
/// the handler transparently falls back to the DB without surfacing an error — AC-1 edge case.
/// </summary>
public sealed class GetAvailabilityHandler
    : IRequestHandler<GetAvailabilityQuery, IReadOnlyList<AvailabilitySlotDto>>
{
    private const int CacheTtlSeconds = 60; // Matches AC-2 (60 s Redis TTL)
    private const string CacheKeyPrefix = "availability";

    private readonly ICacheService _cache;
    private readonly IAvailabilityRepository _repo;
    private readonly INoShowRiskScoringService _riskScorer;
    private readonly ILogger<GetAvailabilityHandler> _logger;

    public GetAvailabilityHandler(
        ICacheService cache,
        IAvailabilityRepository repo,
        INoShowRiskScoringService riskScorer,
        ILogger<GetAvailabilityHandler> logger)
    {
        _cache       = cache;
        _repo        = repo;
        _riskScorer  = riskScorer;
        _logger      = logger;
    }

    public async Task<IReadOnlyList<AvailabilitySlotDto>> Handle(
        GetAvailabilityQuery request,
        CancellationToken cancellationToken)
    {
        // Defensive guard — controller validates first, but handler must never throw.
        if (request.EndDate < request.StartDate)
        {
            _logger.LogWarning(
                "GetAvailabilityQuery received with EndDate {EndDate} before StartDate {StartDate}. Returning empty.",
                request.EndDate, request.StartDate);
            return Array.Empty<AvailabilitySlotDto>();
        }

        var cacheKey = BuildCacheKey(request.StartDate, request.EndDate);

        // ── AC-2: Cache hit path ────────────────────────────────────────────
        var cached = await _cache.GetAsync<IReadOnlyList<AvailabilitySlotDto>>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            _logger.LogDebug("Cache hit for key {CacheKey}", cacheKey);
            return cached;
        }

        // ── AC-3: Cache miss — query database ──────────────────────────────
        _logger.LogDebug("Cache miss for key {CacheKey}. Querying database.", cacheKey);
        var slots = await _repo.GetAvailableSlotsAsync(request.StartDate, request.EndDate, cancellationToken);

        var dtos = slots
            .Select(s =>
            {
                var risk = _riskScorer.CalculateSchedulingRisk(s.SlotDatetime);
                return new AvailabilitySlotDto(
                    s.Id,
                    s.SlotDatetime.ToString("o"),
                    // Fall back to generic provider label when the slot has no assigned clinician.
                    s.Provider ?? "General Practice",
                    s.VisitType ?? "in-person",
                    s.Location,
                    s.DurationMinutes,
                    // Prefer a pre-stored score on the entity; fall back to the live calculation.
                    s.NoShowRiskScore ?? risk.Score,
                    risk.ContributingFactors,
                    risk.IsPartialScoring);
            })
            .ToList();

        // Populate cache — silently no-ops when Redis is unavailable (ICacheService contract)
        await _cache.SetAsync(cacheKey, dtos, TimeSpan.FromSeconds(CacheTtlSeconds), cancellationToken);

        return dtos;
    }

    /// <summary>
    /// Builds a deterministic, lowercase cache key.
    /// Format: <c>availability:yyyy-MM-dd:yyyy-MM-dd</c>
    /// </summary>
    private static string BuildCacheKey(DateOnly start, DateOnly end) =>
        $"{CacheKeyPrefix}:{start:yyyy-MM-dd}:{end:yyyy-MM-dd}";
}

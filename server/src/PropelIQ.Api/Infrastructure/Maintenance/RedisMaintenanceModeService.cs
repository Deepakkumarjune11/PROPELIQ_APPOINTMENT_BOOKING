using StackExchange.Redis;
using Microsoft.Extensions.Logging;

namespace PropelIQ.Api.Infrastructure.Maintenance;

/// <summary>
/// Redis-backed <see cref="IMaintenanceModeService"/> implementation.
///
/// Keys:
/// <list type="bullet">
///   <item><c>maintenance:active</c> — present = maintenance is on; deleted = off</item>
///   <item><c>maintenance:started_at</c> — ISO 8601 UTC start timestamp (24h TTL safety)</item>
///   <item><c>maintenance:estimated_minutes</c> — operator-provided ETA</item>
/// </list>
///
/// Fault tolerance: if Redis is unreachable, <see cref="IsActiveAsync"/> and
/// <see cref="GetStatusAsync"/> fail-open (return "not in maintenance") so that
/// a Redis outage never blocks the entire API. Write operations (<see cref="ActivateAsync"/>/
/// <see cref="DeactivateAsync"/>) propagate exceptions — admins see the error explicitly.
/// </summary>
public sealed class RedisMaintenanceModeService(
    IConnectionMultiplexer redis,
    ILogger<RedisMaintenanceModeService> logger)
    : IMaintenanceModeService
{
    private const string ActiveKey    = "maintenance:active";
    private const string StartedKey   = "maintenance:started_at";
    private const string EstimatedKey = "maintenance:estimated_minutes";

    public async Task<bool> IsActiveAsync(CancellationToken ct = default)
    {
        try
        {
            return await redis.GetDatabase().KeyExistsAsync(ActiveKey).ConfigureAwait(false);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex,
                "Redis unavailable when checking maintenance mode — assuming inactive (fail-open)");
            return false;
        }
    }

    public async Task ActivateAsync(int estimatedMinutes, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var batch = db.CreateBatch();
        _ = batch.StringSetAsync(ActiveKey, "1");
        _ = batch.StringSetAsync(
            StartedKey,
            DateTime.UtcNow.ToString("O"),   // ISO 8601 round-trip
            TimeSpan.FromHours(24));          // Safety TTL — auto-clears after 24 h
        _ = batch.StringSetAsync(EstimatedKey, estimatedMinutes.ToString());
        batch.Execute();
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task DeactivateAsync(CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var batch = db.CreateBatch();
        _ = batch.KeyDeleteAsync(ActiveKey);
        _ = batch.KeyDeleteAsync(StartedKey);
        _ = batch.KeyDeleteAsync(EstimatedKey);
        batch.Execute();
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task<MaintenanceStatus> GetStatusAsync(CancellationToken ct = default)
    {
        try
        {
            var db = redis.GetDatabase();
            var isActive = await db.KeyExistsAsync(ActiveKey).ConfigureAwait(false);

            if (!isActive)
                return new MaintenanceStatus(false, null, 0);

            var startedVal   = await db.StringGetAsync(StartedKey).ConfigureAwait(false);
            var estimatedVal = await db.StringGetAsync(EstimatedKey).ConfigureAwait(false);

            DateTime? startedAt = null;
            if (startedVal.HasValue &&
                DateTime.TryParse(
                    startedVal,
                    null,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out var dt))
            {
                startedAt = dt;
            }

            var estimated = 0;
            if (estimatedVal.HasValue && int.TryParse(estimatedVal, out var e))
                estimated = e;

            return new MaintenanceStatus(true, startedAt, estimated);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex,
                "Redis unavailable when reading maintenance status — returning inactive (fail-open)");
            return new MaintenanceStatus(false, null, 0);
        }
    }
}

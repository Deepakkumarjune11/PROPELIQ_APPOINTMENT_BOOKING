using Admin.Application.Analytics.Dto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PatientAccess.Data;
using StackExchange.Redis;

namespace Admin.Application.Analytics;

/// <summary>
/// PostgreSQL + Redis implementation of <see cref="IMetricsQueryService"/> (US_033, FR-018).
///
/// KPI and trend queries use EF Core <c>SqlQueryRaw</c> against pre-aggregated materialized
/// views (created by task_003). System health reads live Redis stats and a DB connection count.
///
/// Materialized view dependency: queries fail at runtime (not compile time) until task_003
/// creates <c>mv_daily_kpi</c>, <c>mv_daily_appointment_volumes</c>, etc.
/// The freshness query is wrapped in try/catch to degrade gracefully before task_003 runs.
///
/// Thread-safety: scoped lifetime — one instance per request.
/// </summary>
public sealed class PostgresMetricsQueryService(
    PropelIQDbContext         db,
    IConnectionMultiplexer    redis,
    ILogger<PostgresMetricsQueryService> logger) : IMetricsQueryService
{
    // Feature contexts whose latency data populates p50/p95/p99 API latency chips (AC-4).
    // ConversationalIntake is the primary patient-facing feature; used as the representative context.
    private static readonly string[] LatencyFeatureContexts =
        ["ConversationalIntake", "FactExtraction", "CodeSuggestion", "ConflictDetection"];

    // ── KPI (AC-1) ──────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<KpiMetricsDto> GetKpiAsync(
        DateOnly startDate, DateOnly endDate, CancellationToken ct = default)
    {
        KpiRow? rows = null;
        try
        {
            // POCO for SqlQueryRaw column binding (column names match property names exactly)
            rows = await db.Database.SqlQueryRaw<KpiRow>(
                """
                SELECT
                    COALESCE(SUM(appointment_count), 0)::int      AS "AppointmentCount",
                    COALESCE(AVG(noshow_rate),        0)::float8   AS "NoShowRate",
                    COALESCE(AVG(avg_wait_time_min),  0)::float8   AS "AvgWaitTimeMin",
                    COALESCE(AVG(ai_acceptance_rate), 0)::float8   AS "AiAcceptanceRate"
                FROM mv_daily_kpi
                WHERE metric_date BETWEEN {0} AND {1}
                """,
                startDate, endDate)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // mv_daily_kpi does not exist yet (task_003 pending) — degrade gracefully.
            logger.LogWarning(ex, "mv_daily_kpi unavailable — returning zero KPI values");
        }

        var (staleSec, lastRefreshed) = await GetFreshnessAsync("mv_daily_kpi", ct)
            .ConfigureAwait(false);

        return new KpiMetricsDto(
            rows?.AppointmentCount  ?? 0,
            rows?.NoShowRate        ?? 0,
            rows?.AvgWaitTimeMin    ?? 0,
            rows?.AiAcceptanceRate  ?? 0,
            staleSec,
            lastRefreshed);
    }

    // ── Trends (AC-3) ───────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<TrendDataDto> GetTrendsAsync(
        DateOnly startDate, DateOnly endDate, CancellationToken ct = default)
    {
        List<DailyRow> daily = [];
        List<WeeklyRow> weekly = [];
        List<ThroughputRow> throughput = [];

        try
        {
            var dailyTask = db.Database.SqlQueryRaw<DailyRow>(
                """
                SELECT
                    metric_date::text               AS "Date",
                    appointment_count::int          AS "Count"
                FROM mv_daily_appointment_volumes
                WHERE metric_date BETWEEN {0} AND {1}
                ORDER BY metric_date
                """,
                startDate, endDate)
                .ToListAsync(ct);

            var weeklyTask = db.Database.SqlQueryRaw<WeeklyRow>(
                """
                SELECT
                    week_label                      AS "Week",
                    noshow_rate::float8             AS "NoShowRate",
                    ai_latency_p95_ms::float8       AS "AiLatencyP95Ms"
                FROM mv_weekly_noshow_rates
                WHERE week_start_date >= {0} AND week_start_date <= {1}
                ORDER BY week_start_date
                """,
                startDate, endDate)
                .ToListAsync(ct);

            var throughputTask = db.Database.SqlQueryRaw<ThroughputRow>(
                """
                SELECT
                    processing_status               AS "Status",
                    document_count::int             AS "Count"
                FROM mv_document_processing_throughput
                WHERE metric_date BETWEEN {0} AND {1}
                """,
                startDate, endDate)
                .ToListAsync(ct);

            await Task.WhenAll(dailyTask, weeklyTask, throughputTask).ConfigureAwait(false);

            daily      = dailyTask.Result;
            weekly     = weeklyTask.Result;
            throughput = throughputTask.Result;
        }
        catch (Exception ex)
        {
            // Materialized views do not exist yet (task_003 pending) — degrade gracefully.
            logger.LogWarning(ex, "Trend materialized views unavailable — returning empty trend data");
        }

        return new TrendDataDto(
            DailyVolumes: daily
                .Select(r => new DailyVolumeEntry(r.Date, r.Count))
                .ToList(),
            WeeklyTrends: weekly
                .Select(r => new WeeklyTrendEntry(r.Week, r.NoShowRate, r.AiLatencyP95Ms))
                .ToList(),
            DocumentThroughput: throughput
                .Select(r => new DocumentThroughputEntry(r.Status, r.Count))
                .ToList());
    }

    // ── System Health (AC-4) ─────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<SystemHealthDto> GetSystemHealthAsync(
        bool aiGatewayAvailable, CancellationToken ct = default)
    {
        var latencyTask  = ComputeApiLatencyPercentilesAsync(ct);
        var dbPoolTask   = EstimateDbPoolUsageAsync(ct);
        var cacheHitTask = GetCacheHitRatioPctAsync(ct);

        await Task.WhenAll(latencyTask, dbPoolTask, cacheHitTask).ConfigureAwait(false);

        var (p50, p95, p99) = latencyTask.Result;
        var aiStatus = aiGatewayAvailable ? "Available" : "Degraded";

        return new SystemHealthDto(
            ApiLatencyP50Ms:   p50,
            ApiLatencyP95Ms:   p95,
            ApiLatencyP99Ms:   p99,
            DbPoolUsagePct:    dbPoolTask.Result,
            CacheHitRatioPct:  cacheHitTask.Result,
            AiGatewayStatus:   aiStatus);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads all retained latency samples across known feature contexts and computes
    /// combined p50/p95/p99 percentiles. Falls back to (0, 0, 0) on Redis error.
    /// Redis key pattern: <c>ai:latency:{featureContext}</c> (written by RedisLatencyRecorder).
    /// </summary>
    private async Task<(double P50, double P95, double P99)> ComputeApiLatencyPercentilesAsync(
        CancellationToken ct)
    {
        try
        {
            var redisDb = redis.GetDatabase();
            var allSamples = new List<double>();

            foreach (var context in LatencyFeatureContexts)
            {
                var key    = $"ai:latency:{context}";
                var values = await redisDb.ListRangeAsync(key).ConfigureAwait(false);
                foreach (var v in values)
                {
                    if (double.TryParse(v.ToString(), out var ms))
                        allSamples.Add(ms);
                }
            }

            if (allSamples.Count == 0)
                return (0, 0, 0);

            allSamples.Sort();
            var count = allSamples.Count;
            return (
                P50: allSamples[Math.Max(0, (int)Math.Ceiling(0.50 * count) - 1)],
                P95: allSamples[Math.Max(0, (int)Math.Ceiling(0.95 * count) - 1)],
                P99: allSamples[Math.Max(0, (int)Math.Ceiling(0.99 * count) - 1)]);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read AI latency samples from Redis — returning 0 for all percentiles");
            return (0, 0, 0);
        }
    }

    /// <summary>
    /// Estimates DB connection pool usage by counting active connections for this database
    /// via <c>pg_stat_activity</c> and expressing it as a percentage of the configured max.
    /// Falls back to 0 on error.
    /// </summary>
    private async Task<int> EstimateDbPoolUsageAsync(CancellationToken ct)
    {
        try
        {
            var rows = await db.Database.SqlQueryRaw<ActiveConnectionRow>(
                """
                SELECT
                    count(*)::int          AS "ActiveConnections",
                    (SELECT setting::int FROM pg_settings WHERE name = 'max_connections')::int
                                           AS "MaxConnections"
                FROM pg_stat_activity
                WHERE datname = current_database()
                """)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (rows is null || rows.MaxConnections == 0)
                return 0;

            return Math.Min(100, (int)Math.Round(100.0 * rows.ActiveConnections / rows.MaxConnections));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to query pg_stat_activity — DB pool usage unavailable");
            return 0;
        }
    }

    /// <summary>
    /// Computes the Redis cache hit ratio from <c>INFO stats</c> by parsing
    /// <c>keyspace_hits</c> and <c>keyspace_misses</c>. Falls back to 0 on error.
    /// </summary>
    private async Task<int> GetCacheHitRatioPctAsync(CancellationToken ct)
    {
        try
        {
            var redisDb    = redis.GetDatabase();
            var infoString = (string?)await redisDb.ExecuteAsync("INFO", "stats")
                .ConfigureAwait(false);

            if (string.IsNullOrEmpty(infoString))
                return 0;

            var hits   = ParseInfoLong(infoString, "keyspace_hits");
            var misses = ParseInfoLong(infoString, "keyspace_misses");
            var total  = hits + misses;

            if (total == 0) return 0;

            return (int)Math.Round(100.0 * hits / total);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read Redis INFO stats — cache hit ratio unavailable");
            return 0;
        }
    }

    private static long ParseInfoLong(string info, string key)
    {
        var marker = key + ":";
        var idx = info.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return 0;

        var start = idx + marker.Length;
        var end   = info.IndexOfAny(['\r', '\n'], start);
        var slice = end < 0 ? info[start..] : info[start..end];

        return long.TryParse(slice.Trim(), out var val) ? val : 0;
    }

    /// <summary>
    /// Reads the <c>mv_last_refresh</c> metadata table to determine data freshness.
    /// Returns (999, UtcNow) gracefully when the table does not yet exist (pre-task_003).
    /// </summary>
    private async Task<(int StaleSeconds, DateTimeOffset LastRefreshed)> GetFreshnessAsync(
        string viewName, CancellationToken ct)
    {
        try
        {
            var row = await db.Database.SqlQueryRaw<FreshnessRow>(
                """
                SELECT last_refreshed_at AS "LastRefreshedAt"
                FROM mv_last_refresh
                WHERE view_name = {0}
                """,
                viewName)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (row is null)
                return (999, DateTimeOffset.UtcNow);

            var stale = (int)(DateTimeOffset.UtcNow - row.LastRefreshedAt).TotalSeconds;
            return (Math.Max(0, stale), row.LastRefreshedAt);
        }
        catch
        {
            // mv_last_refresh does not exist yet (task_003 pending) — degrade gracefully
            return (999, DateTimeOffset.UtcNow);
        }
    }

    // ── Internal POCO types for SqlQueryRaw column binding ───────────────────
    // Properties must match SQL column aliases (case-insensitive) — EF Core 8 binding.

    private sealed class KpiRow
    {
        public int AppointmentCount { get; set; }
        public double NoShowRate { get; set; }
        public double AvgWaitTimeMin { get; set; }
        public double AiAcceptanceRate { get; set; }
    }

    private sealed class DailyRow
    {
        public string Date { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    private sealed class WeeklyRow
    {
        public string Week { get; set; } = string.Empty;
        public double NoShowRate { get; set; }
        public double AiLatencyP95Ms { get; set; }
    }

    private sealed class ThroughputRow
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    private sealed class FreshnessRow
    {
        public DateTimeOffset LastRefreshedAt { get; set; }
    }

    private sealed class ActiveConnectionRow
    {
        public int ActiveConnections { get; set; }
        public int MaxConnections { get; set; }
    }
}

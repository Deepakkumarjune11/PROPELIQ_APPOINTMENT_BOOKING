using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PatientAccess.Data;

namespace Admin.Application.Analytics;

/// <summary>
/// Hangfire recurring job that refreshes all analytics materialized views using
/// <c>REFRESH MATERIALIZED VIEW CONCURRENTLY</c> and stamps <c>mv_last_refresh</c>.
///
/// CONCURRENTLY allows reads to proceed during refresh (no exclusive table lock).
/// Registered as an hourly recurring job in startup (US_033, AC-2).
/// </summary>
[AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
[DisableConcurrentExecution(timeoutInSeconds: 300)]
public sealed class RefreshMetricsMaterializedViewsJob(
    PropelIQDbContext db,
    ILogger<RefreshMetricsMaterializedViewsJob> logger)
{
    private static readonly string[] Views =
    [
        "mv_daily_appointment_volumes",
        "mv_weekly_noshow_rates",
        "mv_daily_kpi",
        "mv_document_processing_throughput",
    ];

    public async Task ExecuteAsync(CancellationToken ct)
    {
        foreach (var view in Views)
        {
            try
            {
                // CONCURRENTLY: no exclusive lock; reads proceed during refresh (PG 15).
                // View name is from a static allow-list — not user input; no injection risk.
#pragma warning disable EF1002
                await db.Database.ExecuteSqlRawAsync(
                    $"REFRESH MATERIALIZED VIEW CONCURRENTLY {view}", ct)
                    .ConfigureAwait(false);
#pragma warning restore EF1002

                await db.Database.ExecuteSqlRawAsync(
                    """
                    INSERT INTO mv_last_refresh (view_name, last_refreshed_at)
                    VALUES ({0}, NOW() AT TIME ZONE 'UTC')
                    ON CONFLICT (view_name) DO UPDATE
                        SET last_refreshed_at = EXCLUDED.last_refreshed_at
                    """,
                    view)
                    .ConfigureAwait(false);

                logger.LogInformation("Materialized view refreshed: {View}", view);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to refresh materialized view: {View}", view);
                throw; // Allow Hangfire AutomaticRetry to handle
            }
        }
    }
}

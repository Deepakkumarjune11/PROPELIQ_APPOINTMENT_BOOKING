using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PatientAccess.Data;

namespace PropelIQ.Api.HealthCheck;

/// <summary>
/// Verifies the Hangfire background job processor is alive by querying <c>hangfire.servers</c>.
/// Returns <see cref="HealthCheckResult.Degraded"/> if no active server row is found (job processor
/// stopped) and <see cref="HealthCheckResult.Unhealthy"/> on a query exception.
/// 3-second timeout prevents blocking the 5-second health check deadline (AC-1).
/// </summary>
public sealed class HangfireHealthCheck(PropelIQDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(3));

        try
        {
#pragma warning disable EF1002
            var count = await db.Database
                .SqlQueryRaw<int>("SELECT COUNT(*)::INT FROM hangfire.servers")
                .FirstOrDefaultAsync(cts.Token)
                .ConfigureAwait(false);
#pragma warning restore EF1002

            return count > 0
                ? HealthCheckResult.Healthy($"Hangfire: {count} active server(s)")
                : HealthCheckResult.Degraded("Hangfire: no active servers found");
        }
        catch (OperationCanceledException)
        {
            return HealthCheckResult.Degraded("Hangfire: health check timed out (3s)");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Hangfire: probe failed", ex);
        }
    }
}

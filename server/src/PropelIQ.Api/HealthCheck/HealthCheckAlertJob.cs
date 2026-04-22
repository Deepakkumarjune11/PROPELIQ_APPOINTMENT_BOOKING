using Hangfire;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PropelIQ.Api.HealthCheck;

/// <summary>
/// Hangfire recurring job that polls ASP.NET Core health checks every 30 seconds and dispatches
/// alerts when a service fails consecutively <see cref="HealthAlertOptions.ConsecutiveFailureThreshold"/>
/// times (AC-2 — alert after 2 consecutive failures = ~60 seconds).
///
/// <see cref="AutomaticRetryAttribute"/> is set to 0 — stale health check results from a retry
/// are meaningless and would artificially inflate failure counters.
/// <see cref="DisableConcurrentExecutionAttribute"/> prevents overlap with the next 30-second run.
/// </summary>
[AutomaticRetry(Attempts = 0)]
[DisableConcurrentExecution(timeoutInSeconds: 30)]
public sealed class HealthCheckAlertJob(
    HealthCheckService healthCheckService,
    IHealthAlertTracker tracker,
    IAlertNotificationService notifier,
    IOptions<HealthAlertOptions> opts,
    ILogger<HealthCheckAlertJob> logger)
{
    public async Task ExecuteAsync(CancellationToken ct)
    {
        var report = await healthCheckService.CheckHealthAsync(ct).ConfigureAwait(false);
        var threshold = opts.Value.ConsecutiveFailureThreshold;

        foreach (var (name, entry) in report.Entries)
        {
            if (entry.Status != HealthStatus.Healthy)
            {
                var failures = await tracker.IncrementFailureAsync(name, ct).ConfigureAwait(false);
                logger.LogWarning(
                    "Health check '{CheckName}' is {Status} (consecutive failures: {Count})",
                    name, entry.Status, failures);

                if (failures >= threshold)
                {
                    await notifier.SendAlertAsync(
                        name,
                        entry.Status.ToString(),
                        entry.Description ?? entry.Exception?.Message ?? "No detail available",
                        ct)
                        .ConfigureAwait(false);
                }
            }
            else
            {
                await tracker.ResetAsync(name, ct).ConfigureAwait(false);
            }
        }
    }
}

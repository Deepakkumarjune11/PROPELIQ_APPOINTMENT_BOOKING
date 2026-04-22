namespace PropelIQ.Api.HealthCheck;

/// <summary>
/// Tracks consecutive health check failures per service in Redis.
/// Used by <see cref="HealthCheckAlertJob"/> to determine when to fire an alert.
/// </summary>
public interface IHealthAlertTracker
{
    /// <summary>Increments the consecutive failure counter for <paramref name="checkName"/> and returns the new count.</summary>
    Task<long> IncrementFailureAsync(string checkName, CancellationToken ct = default);

    /// <summary>Resets (deletes) the failure counter for <paramref name="checkName"/>.</summary>
    Task ResetAsync(string checkName, CancellationToken ct = default);

    /// <summary>Returns the current consecutive failure count; 0 if no failures recorded.</summary>
    Task<long> GetConsecutiveFailuresAsync(string checkName, CancellationToken ct = default);
}

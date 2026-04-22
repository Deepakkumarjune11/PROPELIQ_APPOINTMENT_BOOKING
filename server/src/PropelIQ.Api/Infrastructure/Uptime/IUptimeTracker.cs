namespace PropelIQ.Api.Infrastructure.Uptime;

/// <summary>
/// Records and queries service downtime intervals for SLA uptime reporting (US_034, AC-5).
/// </summary>
public interface IUptimeTracker
{
    /// <summary>Marks the start of a downtime incident for <paramref name="service"/>. Idempotent — will not overwrite an already-open incident.</summary>
    Task RecordDowntimeStartAsync(string service, CancellationToken ct = default);

    /// <summary>Closes the current open downtime incident for <paramref name="service"/> and persists the interval.</summary>
    Task RecordDowntimeEndAsync(string service, CancellationToken ct = default);

    /// <summary>Computes monthly uptime % from recorded intervals for the given <paramref name="service"/>, <paramref name="year"/>, and <paramref name="month"/>.</summary>
    Task<UptimeReport> GetMonthlyUptimeAsync(string service, int year, int month, CancellationToken ct = default);
}

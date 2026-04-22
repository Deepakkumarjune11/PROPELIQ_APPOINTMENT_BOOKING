namespace PropelIQ.Api.Infrastructure.Uptime;

/// <summary>Monthly uptime SLA report for a single service (US_034, AC-5).</summary>
public sealed record UptimeReport(
    string Service,
    int Year,
    int Month,
    double UptimePercent,
    double TotalDowntimeMinutes,
    int IncidentCount);

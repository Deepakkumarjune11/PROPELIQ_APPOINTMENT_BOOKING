namespace Admin.Application.Analytics.Dto;

/// <summary>KPI snapshot for the analytics dashboard (US_033, AC-1, FR-018).</summary>
public sealed record KpiMetricsDto(
    int AppointmentCount,
    double NoShowRate,
    double AvgWaitTimeMin,
    double AiAcceptanceRate,
    int DataFreshnessSec,
    DateTimeOffset LastRefreshedAt);

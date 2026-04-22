using Admin.Application.Analytics.Dto;

namespace Admin.Application.Analytics;

/// <summary>
/// Queries operational metrics for the analytics dashboard (US_033, FR-018, TR-017).
///
/// KPI and trend queries run against pre-aggregated materialized views (task_003) to satisfy
/// the 2-second response time requirement (AC-2, NFR-018) without hitting live OLTP tables.
/// System health metrics are sampled live on each request.
/// </summary>
public interface IMetricsQueryService
{
    /// <summary>
    /// Returns a KPI snapshot for the given date range from the <c>mv_daily_kpi</c>
    /// materialized view (AC-1).
    /// </summary>
    Task<KpiMetricsDto> GetKpiAsync(DateOnly startDate, DateOnly endDate, CancellationToken ct = default);

    /// <summary>
    /// Returns time-series trend data for the given date range from the
    /// <c>mv_daily_appointment_volumes</c>, <c>mv_weekly_noshow_rates</c>, and
    /// <c>mv_document_processing_throughput</c> materialized views (AC-3).
    /// </summary>
    Task<TrendDataDto> GetTrendsAsync(DateOnly startDate, DateOnly endDate, CancellationToken ct = default);

    /// <summary>
    /// Returns live infrastructure health metrics sampled at request time (AC-4, TR-017).
    /// </summary>
    /// <param name="aiGatewayAvailable">
    /// Current AI gateway availability from <c>IAiAvailabilityState.IsAvailable</c>.
    /// Passed by the controller to avoid a cross-module dependency on
    /// <c>ClinicalIntelligence.Application</c>.
    /// </param>
    Task<SystemHealthDto> GetSystemHealthAsync(bool aiGatewayAvailable, CancellationToken ct = default);
}

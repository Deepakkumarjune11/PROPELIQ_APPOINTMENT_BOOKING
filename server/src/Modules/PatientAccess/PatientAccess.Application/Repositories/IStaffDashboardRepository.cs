using PatientAccess.Application.Staff.Dtos;

namespace PatientAccess.Application.Repositories;

/// <summary>
/// Data-layer contract for the staff dashboard summary aggregate (SCR-010).
/// </summary>
public interface IStaffDashboardRepository
{
    /// <summary>
    /// Returns walk-in counts, queue length, verification pending, and critical conflicts
    /// for today (UTC). All counts are computed in parallel.
    /// </summary>
    Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
}

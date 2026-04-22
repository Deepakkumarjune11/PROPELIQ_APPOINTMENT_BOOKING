using MediatR;
using Microsoft.Extensions.Logging;
using PatientAccess.Application.Repositories;
using PatientAccess.Application.Staff.Dtos;

namespace PatientAccess.Application.Staff.Queries.GetDashboardSummary;

/// <summary>
/// Handles <see cref="GetDashboardSummaryQuery"/> — delegates to
/// <see cref="IStaffDashboardRepository"/> which executes 4 aggregate COUNT queries.
/// </summary>
public sealed class GetDashboardSummaryHandler
    : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    private readonly IStaffDashboardRepository              _repo;
    private readonly ILogger<GetDashboardSummaryHandler>    _logger;

    public GetDashboardSummaryHandler(
        IStaffDashboardRepository           repo,
        ILogger<GetDashboardSummaryHandler> logger)
    {
        _repo   = repo;
        _logger = logger;
    }

    public async Task<DashboardSummaryDto> Handle(
        GetDashboardSummaryQuery query,
        CancellationToken        cancellationToken)
    {
        var summary = await _repo.GetSummaryAsync(cancellationToken);

        _logger.LogDebug(
            "DashboardSummary: walkIns={WalkIns} queue={Queue} pending={Pending} conflicts={Conflicts}",
            summary.WalkInsToday, summary.QueueLength, summary.VerificationPending, summary.CriticalConflicts);

        return summary;
    }
}

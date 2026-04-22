using MediatR;
using PatientAccess.Application.Staff.Dtos;

namespace PatientAccess.Application.Staff.Queries.GetDashboardSummary;

/// <summary>
/// Returns aggregate counts for the staff dashboard (US_016, SCR-010).
/// </summary>
public sealed record GetDashboardSummaryQuery : IRequest<DashboardSummaryDto>;

using MediatR;
using PatientAccess.Application.Patients.Dtos;

namespace PatientAccess.Application.Patients.Queries.SearchPatients;

/// <summary>
/// Returns up to 10 patients whose email or phone contains <see cref="Query"/>
/// (case-insensitive partial match) — US_016, AC-2.
/// </summary>
/// <param name="Query">Search term (min 2 characters enforced by controller).</param>
public sealed record SearchPatientsQuery(string Query)
    : IRequest<IReadOnlyList<PatientSearchResultDto>>;

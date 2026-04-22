using MediatR;
using Microsoft.Extensions.Logging;
using PatientAccess.Application.Patients.Dtos;
using PatientAccess.Application.Repositories;

namespace PatientAccess.Application.Patients.Queries.SearchPatients;

/// <summary>
/// Handles <see cref="SearchPatientsQuery"/>.
/// Delegates to <see cref="IPatientStaffRepository"/> which applies
/// case-insensitive partial matching on email and phone (top 10).
/// </summary>
public sealed class SearchPatientsHandler
    : IRequestHandler<SearchPatientsQuery, IReadOnlyList<PatientSearchResultDto>>
{
    private readonly IPatientStaffRepository           _repo;
    private readonly ILogger<SearchPatientsHandler>    _logger;

    public SearchPatientsHandler(
        IPatientStaffRepository        repo,
        ILogger<SearchPatientsHandler> logger)
    {
        _repo   = repo;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PatientSearchResultDto>> Handle(
        SearchPatientsQuery request,
        CancellationToken   cancellationToken)
    {
        if (request.Query.Trim().Length < 2)
            return Array.Empty<PatientSearchResultDto>();

        var results = await _repo.SearchByEmailOrPhoneAsync(request.Query.Trim(), cancellationToken);

        _logger.LogDebug(
            "SearchPatients: query='{Query}' found={Count}",
            request.Query, results.Count);

        return results;
    }
}

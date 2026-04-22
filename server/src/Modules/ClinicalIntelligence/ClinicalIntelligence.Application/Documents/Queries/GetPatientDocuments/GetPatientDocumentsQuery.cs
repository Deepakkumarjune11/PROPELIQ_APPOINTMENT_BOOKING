using ClinicalIntelligence.Application.Documents.Dtos;
using ClinicalIntelligence.Application.Infrastructure;
using MediatR;

namespace ClinicalIntelligence.Application.Documents.Queries.GetPatientDocuments;

/// <summary>
/// Returns the authenticated patient's clinical document list (US_018, AC-3).
/// Filters by ownership and excludes soft-deleted records.
/// </summary>
/// <param name="PatientId">JWT <c>sub</c> claim — ownership enforced in repository (OWASP A01).</param>
public sealed record GetPatientDocumentsQuery(Guid PatientId) : IRequest<IReadOnlyList<ClinicalDocumentDto>>;

/// <summary>
/// Handles <see cref="GetPatientDocumentsQuery"/> by delegating to <see cref="IClinicalDocumentRepository"/>.
/// </summary>
public sealed class GetPatientDocumentsHandler
    : IRequestHandler<GetPatientDocumentsQuery, IReadOnlyList<ClinicalDocumentDto>>
{
    private readonly IClinicalDocumentRepository _repo;

    public GetPatientDocumentsHandler(IClinicalDocumentRepository repo)
    {
        _repo = repo;
    }

    public Task<IReadOnlyList<ClinicalDocumentDto>> Handle(
        GetPatientDocumentsQuery query,
        CancellationToken        cancellationToken)
    {
        return _repo.GetPatientDocumentsAsync(query.PatientId, cancellationToken);
    }
}

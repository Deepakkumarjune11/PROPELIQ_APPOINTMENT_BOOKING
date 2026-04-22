using ClinicalIntelligence.Application.Exceptions;
using ClinicalIntelligence.Application.Infrastructure;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ClinicalIntelligence.Application.Documents.Commands.DeleteDocument;

/// <summary>
/// Soft-deletes a clinical document belonging to the authenticated patient (US_018, AC-3).
/// </summary>
/// <param name="DocumentId">Document to delete.</param>
/// <param name="PatientId">JWT <c>sub</c> claim — ownership enforced in repository (OWASP A01).</param>
public sealed record DeleteDocumentCommand(Guid DocumentId, Guid PatientId) : IRequest;

/// <summary>
/// Handles <see cref="DeleteDocumentCommand"/>.
/// Delegates ownership check, soft-delete, and AuditLog write to <see cref="IClinicalDocumentRepository"/>.
/// Physical file is NOT deleted — retained for audit per DR-013.
/// </summary>
public sealed class DeleteDocumentHandler : IRequestHandler<DeleteDocumentCommand>
{
    private readonly IClinicalDocumentRepository _repo;
    private readonly ILogger<DeleteDocumentHandler> _logger;

    public DeleteDocumentHandler(
        IClinicalDocumentRepository    repo,
        ILogger<DeleteDocumentHandler> logger)
    {
        _repo   = repo;
        _logger = logger;
    }

    public async Task Handle(DeleteDocumentCommand command, CancellationToken cancellationToken)
    {
        // Throws NotFoundException (404) or ForbiddenException (403) on failure
        await _repo.DeleteDocumentAsync(command.DocumentId, command.PatientId, cancellationToken);

        _logger.LogInformation(
            "Document {DocumentId} soft-deleted by patient {PatientId}.",
            command.DocumentId, command.PatientId);
    }
}

using ClinicalIntelligence.Application.Documents.Commands.DeleteDocument;
using ClinicalIntelligence.Application.Documents.Commands.UploadDocument;
using ClinicalIntelligence.Application.Documents.Dtos;
using ClinicalIntelligence.Application.Documents.Queries.GetPatientDocuments;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ClinicalIntelligence.Presentation.Controllers;

/// <summary>
/// REST endpoints for patient document management (US_018).
///
/// Authentication: JWT Bearer, <c>Patient</c> role only (OWASP A01).
/// Base route: <c>api/v1/documents</c>
/// </summary>
[ApiController]
[Authorize(Roles = "Patient")]
[Route("api/v1/documents")]
public sealed class DocumentsController : ControllerBase
{
    /// <summary>25 MB + overhead for multipart boundary (FR-010).</summary>
    private const long MaxFileSizeBytes = 25L * 1024 * 1024;

    private readonly IMediator _mediator;

    public DocumentsController(IMediator mediator) => _mediator = mediator;

    // ── POST api/v1/documents/upload ────────────────────────────────────────

    /// <summary>
    /// Uploads a single PDF clinical document (US_018, AC-2, AC-3, AC-4, AC-5).
    ///
    /// Server-side validation (OWASP A05 — do not trust Content-Type alone):
    /// - File size ≤ 25 MB (FR-010)
    /// - PDF magic bytes: first 4 bytes == %PDF (0x25 0x50 0x44 0x46)
    /// </summary>
    /// <param name="file">PDF file from the multipart form.</param>
    /// <param name="encounterId">Optional appointment/encounter GUID association.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>
    ///   <c>201 Created</c> with <see cref="ClinicalDocumentDto"/>.<br/>
    ///   <c>422 Unprocessable Entity</c> for invalid file type or size.<br/>
    ///   <c>403 Forbidden</c> for non-Patient roles.
    /// </returns>
    [HttpPost("upload")]
    [RequestSizeLimit(26_214_400)] // 25 MB + 1 MB overhead for multipart boundary
    [RequestFormLimits(MultipartBodyLengthLimit = 26_214_400)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ClinicalDocumentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Upload(
        [FromForm] UploadDocumentFormModel request,
        CancellationToken cancellationToken)
    {
        var file        = request.File;
        var encounterId = request.EncounterId;
        // ── Validate size (cheap check first — FR-010) ────────────────────
        if (file.Length > MaxFileSizeBytes)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title  = "Unprocessable Entity",
                Detail = "File size must be under 25MB. Please select a smaller file.",
            });
        }

        // ── Validate PDF magic bytes (OWASP A05 — spoof-proof type check) ─
        await using var stream = file.OpenReadStream();
        var header = new byte[4];
        var read   = await stream.ReadAsync(header.AsMemory(0, 4), cancellationToken);

        if (read < 4 ||
            header[0] != 0x25 ||
            header[1] != 0x50 ||
            header[2] != 0x44 ||
            header[3] != 0x46)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title  = "Unprocessable Entity",
                Detail = "Unsupported file type. Only PDF files are accepted.",
            });
        }

        // Reset stream position to beginning so the handler can store the full file
        stream.Seek(0, SeekOrigin.Begin);

        var patientId = GetPatientId();

        var command = new UploadDocumentCommand(
            PatientId:       patientId,
            EncounterId:     encounterId,
            OriginalFileName: file.FileName,
            FileSizeBytes:   file.Length,
            FileStream:      stream);

        var result = await _mediator.Send(command, cancellationToken);

        return CreatedAtAction(nameof(GetDocuments), null, result);
    }

    // ── GET api/v1/documents ────────────────────────────────────────────────

    /// <summary>
    /// Returns the authenticated patient's document list, ordered by upload date descending (US_018, AC-3).
    /// Patient ownership is enforced via the JWT <c>sub</c> claim — no cross-patient leakage (OWASP A01).
    /// </summary>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>
    ///   <c>200 OK</c> with <see cref="ClinicalDocumentDto"/> array.<br/>
    ///   <c>403 Forbidden</c> for non-Patient roles.
    /// </returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ClinicalDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDocuments(CancellationToken cancellationToken)
    {
        var query  = new GetPatientDocumentsQuery(GetPatientId());
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    // ── DELETE api/v1/documents/{id} ────────────────────────────────────────

    /// <summary>
    /// Soft-deletes a document owned by the authenticated patient (US_018, AC-3).
    /// Physical file is retained for audit per DR-013.
    /// </summary>
    /// <param name="id">Document GUID.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>
    ///   <c>204 No Content</c> on success.<br/>
    ///   <c>404 Not Found</c> if document does not exist.<br/>
    ///   <c>403 Forbidden</c> if the document belongs to another patient or for non-Patient roles.
    /// </returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DeleteDocumentCommand(id, GetPatientId());
        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private Guid GetPatientId()
    {
        return Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var id)
            ? id
            : Guid.Empty;
    }
}

/// <summary>
/// Form model for <c>POST api/v1/documents/upload</c>.
/// Wrapping IFormFile in a class is required by Swashbuckle 6.x when mixing
/// file and non-file [FromForm] parameters on the same action.
/// </summary>
public sealed class UploadDocumentFormModel
{
    /// <summary>The PDF file to upload (max 25 MB).</summary>
    public IFormFile File { get; init; } = null!;

    /// <summary>Optional appointment/encounter association.</summary>
    public Guid? EncounterId { get; init; }
}

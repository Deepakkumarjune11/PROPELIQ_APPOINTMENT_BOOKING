namespace ClinicalIntelligence.Application.Documents.Models;

/// <summary>
/// Input DTO passed to <see cref="Services.PatientView360Assembler.DeduplicateAsync"/>.
///
/// The caller (in the Data layer) is responsible for decrypting <c>FactText</c> before
/// mapping to this DTO, so the assembler never touches ciphertext and requires no
/// Data Protection dependency.
/// </summary>
/// <param name="Id">Unique identifier of the source <c>ExtractedFact</c>.</param>
/// <param name="DocumentId">GUID of the source <c>ClinicalDocument</c>.</param>
/// <param name="FactType">String name matching <c>PatientAccess.Domain.Enums.FactType</c>.</param>
/// <param name="PlainTextValue">Decrypted clinical value — PHI; never log this field.</param>
/// <param name="ConfidenceScore">AI extraction confidence score 0.0–1.0.</param>
/// <param name="SourceCharOffset">Character offset in source document; nullable (AIR-006).</param>
/// <param name="SourceCharLength">Character span in source document; nullable (AIR-006).</param>
public record FactForAssemblyDto(
    Guid    Id,
    Guid    DocumentId,
    string  FactType,
    string  PlainTextValue,
    float   ConfidenceScore,
    int?    SourceCharOffset,
    int?    SourceCharLength);

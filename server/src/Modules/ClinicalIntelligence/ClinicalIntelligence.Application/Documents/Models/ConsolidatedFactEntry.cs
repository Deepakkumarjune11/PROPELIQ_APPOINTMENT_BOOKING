namespace ClinicalIntelligence.Application.Documents.Models;

/// <summary>
/// Source reference for a single extracted fact — character-level citation (AIR-006).
/// </summary>
/// <param name="DocumentId">GUID of the source <c>ClinicalDocument</c>.</param>
/// <param name="DocumentName">Original filename — display use only; no PHI.</param>
/// <param name="SourceCharOffset">Character offset in source text; nullable when unresolved.</param>
/// <param name="SourceCharLength">Character span in source text; nullable when unresolved.</param>
public record FactSourceRef(
    Guid    DocumentId,
    string  DocumentName,
    int?    SourceCharOffset,
    int?    SourceCharLength);

/// <summary>
/// A single de-duplicated clinical fact in the patient 360-view (FR-012, AIR-003).
///
/// Used as:
/// - The element type of the JSONB <c>ConsolidatedFacts</c> column in <c>PatientView360</c>
///   (serialised/deserialised by <c>PatientView360UpdateJob</c>).
/// - The response body element of <c>GET /api/v1/patients/{id}/360-view</c>.
///
/// <b>PHI note</b>: <c>Value</c> contains decrypted clinical text. The JSONB column is
/// encrypted as a whole via <c>IDataProtector</c> before being stored (DR-015). This record
/// is only materialised in memory after decryption.
/// </summary>
/// <param name="FactType">String name matching <c>PatientAccess.Domain.Enums.FactType</c>.</param>
/// <param name="Value">Decrypted plain-text clinical value (e.g., "Blood Pressure: 120/80").</param>
/// <param name="ConfidenceScore">Highest confidence score among merged source facts.</param>
/// <param name="Sources">All contributing source references (union across merged duplicates).</param>
public record ConsolidatedFactEntry(
    string                       FactType,
    string                       Value,
    float                        ConfidenceScore,
    IReadOnlyList<FactSourceRef> Sources);

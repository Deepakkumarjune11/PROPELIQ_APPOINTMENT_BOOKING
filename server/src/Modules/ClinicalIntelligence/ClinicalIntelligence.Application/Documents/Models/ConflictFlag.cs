namespace ClinicalIntelligence.Application.Documents.Models;

/// <summary>
/// A single source value involved in a detected clinical conflict (US_022, AIR-004).
///
/// PHI field: <c>Value</c> contains decrypted plain-text clinical data. This record is only
/// materialised in memory; the enclosing <see cref="ConflictFlag"/> is encrypted before
/// being stored in <c>PatientView360.ConflictFlags</c> (DR-015).
/// </summary>
/// <param name="DocumentId">GUID of the source <c>ClinicalDocument</c> contributing this value.</param>
/// <param name="DocumentName">Original filename — display only; no PHI.</param>
/// <param name="Value">Decrypted plain-text clinical value (PHI — only in memory after decrypt).</param>
/// <param name="ConfidenceScore">AI extraction confidence score for this fact.</param>
/// <param name="SourceCharOffset">Character offset in the source document text; nullable.</param>
public record ConflictSource(
    Guid    DocumentId,
    string  DocumentName,
    string  Value,
    float   ConfidenceScore,
    int?    SourceCharOffset);

/// <summary>
/// A detected conflict between two clinical fact values of the same <c>FactType</c>
/// sourced from different documents (US_022, AIR-004).
///
/// Stored as an individually-encrypted JSON element within the <c>conflict_flags</c>
/// <c>text[]</c> PostgreSQL column of <c>PatientView360</c>. Each array element holds
/// exactly one encrypted <c>ConflictFlag</c> blob, so <c>ConflictFlags.Length</c> gives
/// the total conflict count without requiring decryption (DR-015 / performance).
///
/// Cosine similarity below 0.70 between two same-FactType values across different
/// source documents triggers conflict creation (AIR-004). Circuit-open fallback uses
/// case-insensitive string-inequality (AIR-O02).
/// </summary>
/// <param name="ConflictId">Unique identifier for this conflict item.</param>
/// <param name="FactType">String name matching <c>PatientAccess.Domain.Enums.FactType</c>.</param>
/// <param name="Sources">The two conflicting source values (always exactly 2 entries).</param>
public record ConflictFlag(
    Guid                          ConflictId,
    string                        FactType,
    IReadOnlyList<ConflictSource> Sources);

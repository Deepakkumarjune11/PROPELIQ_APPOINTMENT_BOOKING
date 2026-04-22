namespace ClinicalIntelligence.Application.Documents.Dtos;

/// <summary>
/// API response record for a clinical document entry (US_018, AC-3).
/// ExtractionStatus is returned as a lowercase snake_case string to match the FE
/// <c>ExtractionStatus</c> union type: 'queued' | 'processing' | 'completed' | 'manual_review' | 'failed'.
/// </summary>
/// <param name="DocumentId">Document GUID — used as the delete/download key.</param>
/// <param name="OriginalFileName">Original client-supplied filename (sanitised display value).</param>
/// <param name="FileSizeBytes">File size in bytes.</param>
/// <param name="UploadedAt">UTC timestamp of upload (ISO 8601 serialised by JSON).</param>
/// <param name="ExtractionStatus">Lowercase snake_case status string.</param>
/// <param name="EncounterId">Associated appointment GUID, or null for standalone uploads.</param>
public sealed record ClinicalDocumentDto(
    Guid      DocumentId,
    string    OriginalFileName,
    long      FileSizeBytes,
    DateTime  UploadedAt,
    string    ExtractionStatus,
    Guid?     EncounterId);

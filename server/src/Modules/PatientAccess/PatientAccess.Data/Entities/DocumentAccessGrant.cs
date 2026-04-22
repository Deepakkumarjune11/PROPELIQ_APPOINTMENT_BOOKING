namespace PatientAccess.Data.Entities;

/// <summary>
/// Explicit per-document access grant for RAG retrieval access control (AIR-S02, OWASP A01).
/// Immutable after creation — no update operations permitted in application code.
/// Revocation is supported via DELETE; append-only in this sprint.
/// </summary>
public sealed class DocumentAccessGrant
{
    public Guid Id { get; private set; }

    /// <summary>FK to clinical_documents.id (ClinicalIntelligence domain).</summary>
    public Guid DocumentId { get; private set; }

    /// <summary>Staff member ID (grantee_type = 'staff') or department identifier (grantee_type = 'dept').</summary>
    public Guid GranteeId { get; private set; }

    /// <summary>'staff' = individual grant; 'dept' = department-level grant (reserved for future use).</summary>
    public string GranteeType { get; private set; } = default!;

    public DateTimeOffset GrantedAt { get; private set; }

    // Required by EF Core — no public setters
    private DocumentAccessGrant() { }

    /// <summary>
    /// Factory method — the only way to create a valid grant.
    /// Validates <paramref name="granteeType"/> against the permitted set ('staff' | 'dept').
    /// </summary>
    public static DocumentAccessGrant Create(Guid documentId, Guid granteeId, string granteeType)
    {
        if (granteeType is not ("staff" or "dept"))
            throw new ArgumentException("grantee_type must be 'staff' or 'dept'.", nameof(granteeType));

        return new DocumentAccessGrant
        {
            Id         = Guid.NewGuid(),
            DocumentId = documentId,
            GranteeId  = granteeId,
            GranteeType = granteeType,
            GrantedAt  = DateTimeOffset.UtcNow,
        };
    }
}

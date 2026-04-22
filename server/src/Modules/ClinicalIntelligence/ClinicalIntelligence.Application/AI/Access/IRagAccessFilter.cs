namespace ClinicalIntelligence.Application.AI.Access;

/// <summary>
/// Determines which document IDs a given actor is authorised to include in a RAG
/// similarity search (AIR-S02, OWASP A01).
///
/// Role semantics:
/// <list type="bullet">
///   <item><term>Patient</term><description>May only search their own uploaded documents.</description></item>
///   <item><term>Staff</term><description>Access governed by department assignment and explicit grants (US_029/task_002).</description></item>
///   <item><term>Admin / System</term><description>Unrestricted — returns <c>null</c>.</description></item>
///   <item><term>Unknown</term><description>Fail-closed — returns an empty list.</description></item>
/// </list>
///
/// A <c>null</c> return value means <em>no document-level filter</em> — the caller applies
/// only the standard similarity threshold.  An empty <see cref="IReadOnlyList{T}"/> means
/// <em>no documents are accessible</em> and the caller MUST return an empty result set without
/// contacting the AI gateway.
/// </summary>
public interface IRagAccessFilter
{
    /// <summary>
    /// Returns the set of authorised document IDs for <paramref name="actorId"/> with
    /// <paramref name="actorRole"/>, or <c>null</c> when no document-level restriction applies.
    /// </summary>
    /// <param name="actorId">Principal identifier (patient Guid or staff Guid).</param>
    /// <param name="actorRole">JWT role claim, e.g. <c>"Patient"</c>, <c>"Staff"</c>, <c>"Admin"</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Authorised document IDs, or <c>null</c> for unrestricted access,
    /// or an empty list when access is denied (fail-closed).
    /// </returns>
    Task<IReadOnlyList<Guid>?> GetAuthorizedDocumentIdsAsync(
        Guid              actorId,
        string            actorRole,
        CancellationToken ct = default);
}

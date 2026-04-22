namespace PatientAccess.Application.Repositories;

/// <summary>
/// Persistence contract for AI prompt audit records (AIR-S03, DR-012 HIPAA retention).
/// Implemented in PatientAccess.Data to keep Application free of EF Core references.
/// </summary>
public interface IAIPromptLogRepository
{
    /// <summary>
    /// Appends a sanitised audit record for a single AI inference call.
    /// Request and response summaries must contain ONLY metadata (counts, flags) — NO PHI content.
    /// </summary>
    Task LogAsync(AIPromptLogEntry entry, CancellationToken ct = default);
}

/// <summary>
/// Sanitised metadata for one AI inference call — NO patient message content (AIR-S01 / DR-012).
/// </summary>
/// <param name="ModelProvider">LLM provider identifier, e.g. "AzureOpenAI".</param>
/// <param name="DeploymentName">Deployment or model version identifier.</param>
/// <param name="RequestSummary">Non-PHI summary: turn count and estimated token count only.</param>
/// <param name="ResponseSummary">Non-PHI summary: isComplete flag and response word count only.</param>
/// <param name="IsComplete">Whether the AI indicated all intake questions were answered.</param>
public sealed record AIPromptLogEntry(
    string ModelProvider,
    string DeploymentName,
    string RequestSummary,
    string ResponseSummary,
    bool IsComplete);

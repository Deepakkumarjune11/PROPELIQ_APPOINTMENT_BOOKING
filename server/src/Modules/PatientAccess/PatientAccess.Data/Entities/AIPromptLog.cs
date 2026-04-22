namespace PatientAccess.Data.Entities;

/// <summary>
/// Immutable audit record for every Azure OpenAI inference call (AIR-S03, DR-012 HIPAA retention).
/// IMPORTANT: RequestSummary and ResponseSummary store ONLY metadata (counts and flags).
/// Patient message content (PHI) is NEVER persisted here (AIR-S01).
/// No FK to Patient intentional — avoids PHI linkage in the audit table.
/// </summary>
public class AIPromptLog
{
    public Guid Id { get; set; }

    /// <summary>UTC timestamp of the inference call.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>LLM provider identifier, e.g. "AzureOpenAI".</summary>
    public string ModelProvider { get; set; } = default!;

    /// <summary>Deployment or model version, e.g. "gpt-4-turbo".</summary>
    public string DeploymentName { get; set; } = default!;

    /// <summary>
    /// Sanitised request metadata — NO PHI. Format: "turns:{n} est_tokens:{n}".
    /// </summary>
    public string RequestSummary { get; set; } = default!;

    /// <summary>
    /// Sanitised response metadata — NO PHI. Format: "isComplete:{bool} words:{n}".
    /// </summary>
    public string ResponseSummary { get; set; } = default!;

    /// <summary>Whether the AI indicated all intake questions were answered in this turn.</summary>
    public bool IsComplete { get; set; }
}

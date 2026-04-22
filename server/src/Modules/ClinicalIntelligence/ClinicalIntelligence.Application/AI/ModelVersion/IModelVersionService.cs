namespace ClinicalIntelligence.Application.AI.ModelVersion;

/// <summary>
/// Redis-backed model deployment version manager (US_031, AC-5, AIR-O04).
///
/// Allows zero-downtime model switching: Redis key updates propagate across all
/// instances within milliseconds — no application restart required.
/// In-flight requests complete on the previously-resolved deployment name.
/// </summary>
public interface IModelVersionService
{
    /// <summary>
    /// Activates a new deployment by name. Persists the current deployment to the
    /// <c>previous</c> Redis key to enable rollback.
    /// </summary>
    /// <param name="deploymentName">Azure OpenAI deployment name to make active.</param>
    /// <param name="ct">Cancellation token.</param>
    Task ActivateDeploymentAsync(string deploymentName, CancellationToken ct = default);

    /// <summary>
    /// Rolls back to the previous deployment stored in Redis.
    /// If no previous deployment exists, resets to the configured default from
    /// <c>AzureOpenAiOptions.InferenceDeploymentName</c>.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task RollbackAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns the currently active deployment name.
    /// Reads the Redis <c>current</c> key; falls back to
    /// <c>AzureOpenAiOptions.InferenceDeploymentName</c> when the key is absent.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task<string> GetActiveDeploymentAsync(CancellationToken ct = default);
}

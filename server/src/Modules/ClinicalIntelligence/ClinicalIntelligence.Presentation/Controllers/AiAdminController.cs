using System.Security.Claims;
using ClinicalIntelligence.Application.AI.FeatureFlags;
using ClinicalIntelligence.Application.AI.ModelVersion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClinicalIntelligence.Presentation.Controllers;

/// <summary>
/// Admin-only endpoints for AI model version management and feature flag management
/// (US_031, US_032, AC-4, AC-5, AIR-O04, TR-025).
///
/// Endpoints:
///   <c>POST /api/v1/admin/ai/deployment/rollback</c> — rolls back the active Azure OpenAI
///   deployment to the previously activated version via Redis key update.
///   <c>GET  /api/v1/admin/ai/features</c>            — lists all AI feature flags and their current state.
///   <c>POST /api/v1/admin/ai/features/{featureName}/toggle</c> — enables or disables a named
///   AI feature flag; featureName validated against the registered set (OWASP A01).
///
/// All endpoints require the <c>Admin</c> role (OWASP A01 — Broken Access Control).
/// </summary>
[ApiController]
[Route("api/v1/admin/ai")]
[Authorize(Roles = "Admin")]
public sealed class AiAdminController(
    IModelVersionService                   modelVersionService,
    IFeatureFlagService                    featureFlagService,
    IOptionsMonitor<AiFeatureFlagsOptions> featureFlagOptions,
    ILogger<AiAdminController>             logger)
    : ControllerBase
{
    private Guid ActorId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : Guid.Empty;

    // ── POST /api/v1/admin/ai/deployment/rollback ────────────────────────────

    /// <summary>
    /// Rolls back the active Azure OpenAI inference deployment to the previously activated version.
    /// Returns the deployment name now active after rollback.
    /// </summary>
    /// <remarks>
    /// The rollback operates by swapping Redis keys — no application restart required.
    /// If no previous deployment is stored in Redis, the endpoint reverts to the
    /// <c>AzureOpenAiOptions.InferenceDeploymentName</c> configuration default.
    /// </remarks>
    [HttpPost("deployment/rollback")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RollbackDeployment(CancellationToken ct)
    {
        await modelVersionService.RollbackAsync(ct).ConfigureAwait(false);

        var active = await modelVersionService.GetActiveDeploymentAsync(ct).ConfigureAwait(false);

        // Structured audit log: actor ID + rolled-back deployment name (no PHI — safe to log)
        logger.LogInformation(
            "AiDeploymentRollback completed | actorId={ActorId} activeDeployment={ActiveDeployment}",
            ActorId, active);

        return Ok(new { activeDeployment = active });
    }

    // ── GET /api/v1/admin/ai/features ───────────────────────────────────────

    /// <summary>
    /// Returns the current enabled/disabled state of all registered AI feature flags.
    /// Each flag is resolved from Redis first; falls back to the configured default when absent.
    /// </summary>
    [HttpGet("features")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAiFeatureFlags(CancellationToken ct)
    {
        var flags = await featureFlagService.GetAllFlagsAsync(ct).ConfigureAwait(false);
        return Ok(new { features = flags });
    }

    // ── POST /api/v1/admin/ai/features/{featureName}/toggle ─────────────────

    /// <summary>
    /// Enables or disables a named AI feature flag.
    /// The change is propagated instantly to all application instances via Redis.
    /// </summary>
    /// <param name="featureName">
    /// The feature context name to toggle. Must be one of the known registered features
    /// (validated against <c>AiFeatureFlagsOptions.Defaults.Keys</c> — OWASP A01).
    /// </param>
    /// <param name="request">Body containing the desired <c>enabled</c> state.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("features/{featureName}/toggle")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ToggleAiFeature(
        [FromRoute] string              featureName,
        [FromBody]  ToggleFeatureRequest request,
        CancellationToken               ct)
    {
        // OWASP A01: Validate featureName against the registered feature set.
        // Prevents arbitrary Redis key writes via crafted path parameter.
        var knownFeatures = featureFlagOptions.CurrentValue.Defaults.Keys;
        if (!knownFeatures.Contains(featureName, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { error = "Unknown feature name", featureName });

        await featureFlagService.SetFlagAsync(featureName, request.Enabled, ct)
            .ConfigureAwait(false);

        logger.LogInformation(
            "AiFeatureFlagToggle | actorId={ActorId} featureName={FeatureName} enabled={Enabled}",
            ActorId, featureName, request.Enabled);

        return Ok(new { featureName, enabled = request.Enabled });
    }
}

/// <summary>Request body for <c>POST /api/v1/admin/ai/features/{featureName}/toggle</c>.</summary>
public sealed record ToggleFeatureRequest(bool Enabled);

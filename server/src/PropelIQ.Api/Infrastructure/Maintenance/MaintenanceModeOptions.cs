namespace PropelIQ.Api.Infrastructure.Maintenance;

/// <summary>
/// Configuration for <see cref="MaintenanceModeMiddleware"/>.
/// Bound from <c>appsettings.json → "MaintenanceMode"</c>.
/// </summary>
public sealed class MaintenanceModeOptions
{
    public const string SectionName = "MaintenanceMode";

    /// <summary>
    /// URL path prefixes that bypass maintenance mode.
    /// Health check and admin maintenance endpoints must always remain reachable
    /// for monitoring and recovery (AC-3, OWASP A05 — no monitoring blind spot).
    /// </summary>
    public string[] ExemptPaths { get; set; } =
    [
        "/api/health",
        "/api/v1/admin/maintenance",
        "/swagger",
    ];

    public string DefaultMessage { get; set; } =
        "System is under planned maintenance. Please try again shortly.";
}

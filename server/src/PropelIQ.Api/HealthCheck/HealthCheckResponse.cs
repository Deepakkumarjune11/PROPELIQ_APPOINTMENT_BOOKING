namespace PropelIQ.Api.HealthCheck;

/// <summary>
/// Response body returned by <c>GET /api/health</c>.
/// </summary>
/// <param name="Status">Top-level health status — "Healthy", "Degraded", or "Unhealthy".</param>
/// <param name="Environment">Hosting environment name (e.g. "Development", "Production").</param>
/// <param name="Version">Assembly informational version of <c>PropelIQ.Api</c>.</param>
/// <param name="Timestamp">UTC timestamp of when the health check was evaluated.</param>
/// <param name="Checks">Per-dependency check results keyed by check name (e.g. "postgresql", "redis").</param>
public sealed record HealthCheckResponse(
    string Status,
    string Environment,
    string Version,
    DateTime Timestamp,
    IReadOnlyDictionary<string, string> Checks
);

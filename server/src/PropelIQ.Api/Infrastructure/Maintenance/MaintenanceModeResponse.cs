namespace PropelIQ.Api.Infrastructure.Maintenance;

/// <summary>
/// JSON body returned with HTTP 503 responses during maintenance mode (AC-4).
/// Includes a <c>Retry-After</c> response header of 300 seconds.
/// </summary>
public sealed record MaintenanceModeResponse(
    string Message,
    DateTime? StartedAtUtc,
    int EstimatedMinutes);

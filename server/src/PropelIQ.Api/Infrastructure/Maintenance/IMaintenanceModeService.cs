namespace PropelIQ.Api.Infrastructure.Maintenance;

/// <summary>
/// Manages platform-wide maintenance mode state backed by Redis.
/// State persists across app restarts and is visible to all API instances.
/// </summary>
public interface IMaintenanceModeService
{
    Task<bool> IsActiveAsync(CancellationToken ct = default);
    Task ActivateAsync(int estimatedMinutes, CancellationToken ct = default);
    Task DeactivateAsync(CancellationToken ct = default);
    Task<MaintenanceStatus> GetStatusAsync(CancellationToken ct = default);
}

/// <summary>Snapshot of the current maintenance mode state.</summary>
public sealed record MaintenanceStatus(
    bool IsActive,
    DateTime? StartedAtUtc,
    int EstimatedMinutes);

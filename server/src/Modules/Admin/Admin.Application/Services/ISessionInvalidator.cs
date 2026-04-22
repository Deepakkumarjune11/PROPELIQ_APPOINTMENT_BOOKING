namespace Admin.Application.Services;

/// <summary>
/// Manages user-scoped session invalidation in Redis.
/// Used after role changes and account disabling to force re-authentication (UC-006 AC-3, AC-4).
/// </summary>
public interface ISessionInvalidator
{
    /// <summary>
    /// Marks all sessions for <paramref name="userId"/> as invalidated in Redis
    /// with a 15-minute TTL (matching the JWT max lifetime per NFR-005).
    /// </summary>
    Task InvalidateSessionAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Returns <c>true</c> if the user's session has been invalidated via
    /// <see cref="InvalidateSessionAsync"/>; checked in <c>OnTokenValidated</c>.
    /// </summary>
    Task<bool> IsUserInvalidatedAsync(Guid userId, CancellationToken ct);
}

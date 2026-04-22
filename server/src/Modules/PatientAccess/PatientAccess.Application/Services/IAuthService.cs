using PatientAccess.Application.DTOs;

namespace PatientAccess.Application.Services;

/// <summary>
/// Handles JWT-based authentication for Staff, Admin, and Patient principals.
/// Implementations must enforce OWASP A07: generic error responses that do not
/// distinguish user-not-found from wrong-password.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Validates credentials against Staff, Admin, or Patient entities and issues a 15-minute JWT.
    /// Returns <c>null</c> for any authentication failure (generic, per OWASP A07).
    /// </summary>
    Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken ct);

    /// <summary>
    /// Validates the current token (allowing a 30-second grace window past expiry),
    /// atomically blacklists it in Redis, and issues a new 15-minute JWT.
    /// Returns <c>null</c> if the token is invalid or expired beyond the grace window.
    /// </summary>
    Task<AuthResponse?> RefreshAsync(string currentToken, CancellationToken ct);

    /// <summary>
    /// Blacklists the supplied token in Redis with a TTL equal to its remaining lifetime.
    /// Redis failures are logged as warnings but do not prevent logout from succeeding
    /// (fail-open for availability; security maintained by the 15-minute TTL).
    /// </summary>
    Task LogoutAsync(string token, CancellationToken ct);
}

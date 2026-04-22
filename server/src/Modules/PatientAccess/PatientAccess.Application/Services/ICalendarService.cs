namespace PatientAccess.Application.Services;

/// <summary>
/// Calendar provider integration contract.
/// Implemented separately for Google Calendar (Authorization Code flow via Google OAuth 2.0)
/// and Outlook Calendar (Authorization Code flow via Microsoft Identity Platform v2).
/// </summary>
public interface ICalendarService
{
    /// <summary>
    /// Builds the OAuth 2.0 authorization URL.
    /// The <paramref name="appointmentId"/> is encoded as the <c>state</c> parameter —
    /// not as a redirect URL — to prevent open redirect vulnerabilities (OWASP A01).
    /// </summary>
    /// <param name="appointmentId">Appointment to link the calendar event to.</param>
    /// <returns>Full OAuth authorization URL; the FE redirects the browser to this URL.</returns>
    string GetAuthorizationUrl(Guid appointmentId);

    /// <summary>
    /// Exchanges the authorization code for an access token and creates a calendar event.
    /// </summary>
    /// <param name="authorizationCode">One-time authorization code from the OAuth callback.</param>
    /// <param name="appointmentId">Appointment to create the event for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    ///   Thrown when the token exchange or calendar API call fails.
    ///   The caller (<see cref="CalendarController"/>) catches this and redirects to the error page.
    /// </exception>
    Task CreateEventAsync(string authorizationCode, Guid appointmentId, CancellationToken ct = default);
}

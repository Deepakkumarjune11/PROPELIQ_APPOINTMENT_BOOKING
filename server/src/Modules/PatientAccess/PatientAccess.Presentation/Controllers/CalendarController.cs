using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PatientAccess.Application.Services;

namespace PatientAccess.Presentation.Controllers;

/// <summary>
/// Exposes the OAuth 2.0 calendar sync flow for Google and Outlook (TR-012, AC-2, AC-3).
/// Base route: <c>api/v1/calendar</c>
///
/// Security notes (OWASP):
/// - A01 (Open Redirect): Redirect targets in <see cref="Callback"/> are hardcoded paths;
///   they are NEVER derived from query parameters or the OAuth <c>state</c> value.
/// - A02 (Credential Exposure): OAuth client secrets are sourced from IConfiguration only;
///   they are never logged or included in error responses.
/// - A03 (Injection): <paramref name="provider"/> is validated against an allowlist before use.
/// - A01 (Access Control): <see cref="InitSync"/> requires a valid auth token; <see cref="Callback"/>
///   is intentionally unauthenticated so the OAuth provider can complete the redirect flow.
/// </summary>
[ApiController]
[Route("api/v1/calendar")]
public sealed class CalendarController : ControllerBase
{
    private static readonly HashSet<string> AllowedProviders =
        new(StringComparer.OrdinalIgnoreCase) { "google", "outlook" };

    private readonly ICalendarService             _googleService;
    private readonly ICalendarService             _outlookService;
    private readonly ILogger<CalendarController>  _logger;

    public CalendarController(
        [FromKeyedServices("google")]  ICalendarService googleService,
        [FromKeyedServices("outlook")] ICalendarService outlookService,
        ILogger<CalendarController>                     logger)
    {
        _googleService  = googleService;
        _outlookService = outlookService;
        _logger         = logger;
    }

    /// <summary>
    /// Builds and returns the OAuth 2.0 authorization URL for the requested calendar provider.
    /// The front-end redirects the browser to the returned <c>authUrl</c> to begin the consent flow.
    /// </summary>
    /// <param name="provider"><c>google</c> or <c>outlook</c>.</param>
    /// <param name="appointmentId">Appointment to create the calendar event for.</param>
    /// <returns>
    ///   <c>200 OK</c> with <c>{ authUrl: "..." }</c>.<br/>
    ///   <c>400 Bad Request</c> when <paramref name="provider"/> is not recognised.<br/>
    ///   <c>401 Unauthorized</c> when the request is not authenticated.
    /// </returns>
    [HttpGet("{provider}/init")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult InitSync(
        [FromRoute] string provider,
        [FromQuery] Guid   appointmentId)
    {
        if (!AllowedProviders.Contains(provider))
        {
            return Problem(
                detail:     $"Unknown calendar provider '{provider}'. Allowed: google, outlook.",
                statusCode: StatusCodes.Status400BadRequest,
                title:      "Invalid provider");
        }

        var service = ResolveService(provider);
        var authUrl = service.GetAuthorizationUrl(appointmentId);
        return Ok(new { authUrl });
    }

    /// <summary>
    /// OAuth 2.0 callback endpoint.  The provider redirects here after the patient grants consent.
    /// Exchanges the authorization code for an access token and creates the calendar event.
    /// Redirects back to the front-end confirmation page regardless of outcome.
    /// </summary>
    /// <remarks>
    /// This endpoint is intentionally NOT protected with <c>[Authorize]</c> — the OAuth provider
    /// cannot supply auth headers in its redirect.  The <paramref name="code"/> is one-time-use
    /// and is consumed immediately (OWASP A01/A02).
    /// </remarks>
    /// <param name="provider"><c>google</c> or <c>outlook</c>.</param>
    /// <param name="code">One-time authorization code from the OAuth provider.</param>
    /// <param name="state">
    ///   The <c>appointmentId</c> encoded as the OAuth state parameter.
    ///   Validated by parsing as <see cref="Guid"/> — not used as a redirect URL (OWASP A01).
    /// </param>
    /// <param name="ct">Request cancellation token.</param>
    [HttpGet("{provider}/callback")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public async Task<IActionResult> Callback(
        [FromRoute] string            provider,
        [FromQuery] string?           code,
        [FromQuery] Guid              state,
        CancellationToken             ct)
    {
        // Guard: validate provider and code before calling external APIs
        if (!AllowedProviders.Contains(provider) || string.IsNullOrWhiteSpace(code))
        {
            _logger.LogWarning(
                "Calendar callback rejected — invalid provider '{Provider}' or missing code.", provider);
            return Redirect("/appointments/confirmation?calendarError=true");
        }

        try
        {
            var service = ResolveService(provider);
            await service.CreateEventAsync(code, state, ct);

            // Hardcoded redirect target — not derived from request parameters (OWASP A01)
            return Redirect($"/appointments/confirmation?calendarSynced={provider.ToLowerInvariant()}");
        }
        catch (Exception ex)
        {
            // Log the full exception but do NOT include OAuth code or access token (OWASP A02)
            _logger.LogError(ex,
                "Calendar sync failed for provider {Provider} AppointmentId {AppointmentId}.",
                provider, state);

            // Hardcoded error redirect — not derived from request parameters (OWASP A01)
            return Redirect("/appointments/confirmation?calendarError=true");
        }
    }

    private ICalendarService ResolveService(string provider) =>
        provider.Equals("google", StringComparison.OrdinalIgnoreCase)
            ? _googleService
            : _outlookService;
}

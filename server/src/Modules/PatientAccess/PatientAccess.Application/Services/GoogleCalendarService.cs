using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PatientAccess.Application.Configuration;
using PatientAccess.Application.Repositories;

namespace PatientAccess.Application.Services;

/// <summary>
/// Google Calendar integration using the OAuth 2.0 Authorization Code flow (TR-012, AC-2, AC-3).
/// Uses raw <see cref="HttpClient"/> via <see cref="IHttpClientFactory"/> — no Google SDK dependency.
/// OAuth client secret is read from <see cref="CalendarOptions"/> at execution time (OWASP A02).
/// </summary>
public sealed class GoogleCalendarService : ICalendarService
{
    private const string TokenEndpoint   = "https://oauth2.googleapis.com/token";
    private const string CalendarApiBase = "https://www.googleapis.com/calendar/v3/calendars/primary/events";
    private const string EventScope      = "https://www.googleapis.com/auth/calendar.events";
    private const string EventSummary    = "Medical Appointment \u2014 PropelIQ";
    private const string EventDescription = "Medical appointment";

    private readonly IHttpClientFactory             _httpClientFactory;
    private readonly CalendarOptions                _options;
    private readonly ICalendarAppointmentRepository _appointmentRepo;
    private readonly ILogger<GoogleCalendarService> _logger;

    public GoogleCalendarService(
        IHttpClientFactory             httpClientFactory,
        IOptions<CalendarOptions>      options,
        ICalendarAppointmentRepository appointmentRepo,
        ILogger<GoogleCalendarService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options           = options.Value;
        _appointmentRepo   = appointmentRepo;
        _logger            = logger;
    }

    /// <inheritdoc />
    public string GetAuthorizationUrl(Guid appointmentId)
    {
        var g = _options.Google;
        return "https://accounts.google.com/o/oauth2/v2/auth"
               + $"?response_type=code"
               + $"&client_id={Uri.EscapeDataString(g.ClientId)}"
               + $"&redirect_uri={Uri.EscapeDataString(g.RedirectUri)}"
               + $"&scope={Uri.EscapeDataString(EventScope)}"
               + $"&state={appointmentId:D}"
               + "&access_type=offline";
    }

    /// <inheritdoc />
    public async Task CreateEventAsync(
        string authorizationCode,
        Guid   appointmentId,
        CancellationToken ct = default)
    {
        var g = _options.Google;

        // 1. Exchange authorization code for access token (one-time use — must not be retried with same code)
        using var httpClient = _httpClientFactory.CreateClient();

        var tokenParams = new Dictionary<string, string>
        {
            ["code"]          = authorizationCode,
            ["client_id"]     = g.ClientId,
            ["client_secret"] = g.ClientSecret,
            ["redirect_uri"]  = g.RedirectUri,
            ["grant_type"]    = "authorization_code",
        };

        using var tokenContent  = new FormUrlEncodedContent(tokenParams);
        using var tokenResponse = await httpClient.PostAsync(TokenEndpoint, tokenContent, ct);

        if (!tokenResponse.IsSuccessStatusCode)
        {
            var errorBody = await tokenResponse.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Google token exchange failed with status {(int)tokenResponse.StatusCode}. Body omitted for security.");
        }

        var tokenData = await tokenResponse.Content
            .ReadFromJsonAsync<GoogleTokenResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Empty token response from Google.");

        // 2. Resolve appointment details (no PHI included in the calendar event — AC-3)
        var data = await _appointmentRepo.GetCalendarDataAsync(appointmentId, ct);
        if (data is null)
        {
            _logger.LogWarning(
                "Appointment {AppointmentId} not found; Google Calendar event not created.", appointmentId);
            return;
        }

        // 3. Create calendar event via Google Calendar API v3
        var eventBody = new
        {
            summary     = EventSummary,
            description = EventDescription,
            location    = _options.ClinicLocation,
            start       = new { dateTime = data.SlotDatetime.ToString("o"), timeZone = "UTC" },
            end         = new { dateTime = data.SlotDatetime.AddHours(1).ToString("o"), timeZone = "UTC" },
        };

        using var eventClient = _httpClientFactory.CreateClient();
        eventClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenData.AccessToken);

        using var eventResponse = await eventClient.PostAsJsonAsync(CalendarApiBase, eventBody, ct);

        if (!eventResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Google Calendar API returned {(int)eventResponse.StatusCode}. Body omitted for security.");
        }

        _logger.LogInformation(
            "Google Calendar event created for appointment {AppointmentId}.", appointmentId);
    }

    private sealed class GoogleTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;
    }
}

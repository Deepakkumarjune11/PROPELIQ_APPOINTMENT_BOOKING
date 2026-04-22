using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PatientAccess.Application.Configuration;
using PatientAccess.Application.Repositories;

namespace PatientAccess.Application.Services;

/// <summary>
/// Microsoft Outlook Calendar integration using the Microsoft Identity Platform v2
/// OAuth 2.0 Authorization Code flow and Microsoft Graph API (TR-012, AC-2, AC-3).
/// Uses raw <see cref="HttpClient"/> via <see cref="IHttpClientFactory"/> — no Graph SDK dependency.
/// OAuth client secret is read from <see cref="CalendarOptions"/> at execution time (OWASP A02).
/// </summary>
public sealed class OutlookCalendarService : ICalendarService
{
    private const string GraphEventsEndpoint = "https://graph.microsoft.com/v1.0/me/events";
    private const string EventSummary        = "Medical Appointment \u2014 PropelIQ";
    private const string EventDescription    = "Medical appointment";
    private const string GraphScope          = "https://graph.microsoft.com/Calendars.ReadWrite";

    private readonly IHttpClientFactory              _httpClientFactory;
    private readonly CalendarOptions                 _options;
    private readonly ICalendarAppointmentRepository  _appointmentRepo;
    private readonly ILogger<OutlookCalendarService> _logger;

    public OutlookCalendarService(
        IHttpClientFactory              httpClientFactory,
        IOptions<CalendarOptions>       options,
        ICalendarAppointmentRepository  appointmentRepo,
        ILogger<OutlookCalendarService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options           = options.Value;
        _appointmentRepo   = appointmentRepo;
        _logger            = logger;
    }

    /// <inheritdoc />
    public string GetAuthorizationUrl(Guid appointmentId)
    {
        var o       = _options.Outlook;
        var tenantId = string.IsNullOrWhiteSpace(o.TenantId) ? "common" : o.TenantId;

        return $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/authorize"
               + $"?response_type=code"
               + $"&client_id={Uri.EscapeDataString(o.ClientId)}"
               + $"&redirect_uri={Uri.EscapeDataString(o.RedirectUri)}"
               + $"&scope={Uri.EscapeDataString(GraphScope)}"
               + $"&state={appointmentId:D}";
    }

    /// <inheritdoc />
    public async Task CreateEventAsync(
        string authorizationCode,
        Guid   appointmentId,
        CancellationToken ct = default)
    {
        var o        = _options.Outlook;
        var tenantId = string.IsNullOrWhiteSpace(o.TenantId) ? "common" : o.TenantId;
        var tokenUrl = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token";

        // 1. Exchange authorization code for access token
        using var httpClient = _httpClientFactory.CreateClient();

        var tokenParams = new Dictionary<string, string>
        {
            ["code"]          = authorizationCode,
            ["client_id"]     = o.ClientId,
            ["client_secret"] = o.ClientSecret,
            ["redirect_uri"]  = o.RedirectUri,
            ["grant_type"]    = "authorization_code",
            ["scope"]         = GraphScope,
        };

        using var tokenContent  = new FormUrlEncodedContent(tokenParams);
        using var tokenResponse = await httpClient.PostAsync(tokenUrl, tokenContent, ct);

        if (!tokenResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Outlook token exchange failed with status {(int)tokenResponse.StatusCode}. Body omitted for security.");
        }

        var tokenData = await tokenResponse.Content
            .ReadFromJsonAsync<OutlookTokenResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Empty token response from Microsoft Identity Platform.");

        // 2. Resolve appointment details (no PHI included in the calendar event — AC-3)
        var data = await _appointmentRepo.GetCalendarDataAsync(appointmentId, ct);
        if (data is null)
        {
            _logger.LogWarning(
                "Appointment {AppointmentId} not found; Outlook Calendar event not created.", appointmentId);
            return;
        }

        // 3. Create calendar event via Microsoft Graph POST /me/events
        // Microsoft Graph uses ISO 8601 without offset in dateTime; timeZone is separate.
        var eventBody = new
        {
            subject  = EventSummary,
            body     = new { contentType = "Text", content = EventDescription },
            start    = new { dateTime = data.SlotDatetime.ToString("yyyy-MM-ddTHH:mm:ss"), timeZone = "UTC" },
            end      = new { dateTime = data.SlotDatetime.AddHours(1).ToString("yyyy-MM-ddTHH:mm:ss"), timeZone = "UTC" },
            location = new { displayName = _options.ClinicLocation },
        };

        using var eventClient = _httpClientFactory.CreateClient();
        eventClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenData.AccessToken);

        using var eventResponse = await eventClient.PostAsJsonAsync(GraphEventsEndpoint, eventBody, ct);

        if (!eventResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Microsoft Graph returned {(int)eventResponse.StatusCode}. Body omitted for security.");
        }

        _logger.LogInformation(
            "Outlook Calendar event created for appointment {AppointmentId}.", appointmentId);
    }

    private sealed class OutlookTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;
    }
}

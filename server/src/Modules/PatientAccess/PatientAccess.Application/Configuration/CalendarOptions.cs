namespace PatientAccess.Application.Configuration;

/// <summary>
/// OAuth 2.0 credentials and clinic metadata for Google and Outlook calendar sync (TR-012).
/// All credential values are empty strings in <c>appsettings.json</c>; they must be supplied
/// via environment variables / Azure Key Vault in production (OWASP A02).
/// </summary>
public sealed class CalendarOptions
{
    public const string SectionName = "Calendar";

    public GoogleCalendarConfig Google { get; set; } = new();
    public OutlookCalendarConfig Outlook { get; set; } = new();

    /// <summary>Displayed in the calendar event location field (AC-3).</summary>
    public string ClinicLocation { get; set; } = "PropelIQ Health Clinic";
}

/// <summary>Google OAuth 2.0 client configuration (Authorization Code flow).</summary>
public sealed class GoogleCalendarConfig
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    /// <summary>
    /// Fixed redirect URI registered in the Google Cloud Console.
    /// Example: <c>https://api.propeliq.com/api/v1/calendar/google/callback</c>
    /// OWASP A01: never taken from request parameters.
    /// </summary>
    public string RedirectUri { get; set; } = string.Empty;
}

/// <summary>Microsoft Identity Platform OAuth 2.0 client configuration (Authorization Code flow).</summary>
public sealed class OutlookCalendarConfig
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    /// <summary>
    /// Azure AD tenant ID. <c>"common"</c> allows personal + work/school accounts.
    /// </summary>
    public string TenantId { get; set; } = "common";
    /// <summary>
    /// Fixed redirect URI registered in the Azure App Registration.
    /// OWASP A01: never taken from request parameters.
    /// </summary>
    public string RedirectUri { get; set; } = string.Empty;
}

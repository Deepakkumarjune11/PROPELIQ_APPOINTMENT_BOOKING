namespace PatientAccess.Application.Configuration;

/// <summary>
/// Bound from <c>appsettings.json:Twilio</c>.
/// Account credentials must be supplied via environment variables in production (OWASP A02).
/// </summary>
public sealed class TwilioOptions
{
    public const string SectionName = "Twilio";

    public string AccountSid { get; set; } = string.Empty;
    public string AuthToken { get; set; } = string.Empty;
    public string FromNumber { get; set; } = string.Empty;
}

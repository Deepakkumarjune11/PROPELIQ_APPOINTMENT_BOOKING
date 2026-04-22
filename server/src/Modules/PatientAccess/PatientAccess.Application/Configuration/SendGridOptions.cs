namespace PatientAccess.Application.Configuration;

/// <summary>
/// Bound from <c>appsettings.json:SendGrid</c>.
/// API key must be supplied via environment variables in production (OWASP A02).
/// </summary>
public sealed class SendGridOptions
{
    public const string SectionName = "SendGrid";

    public string ApiKey { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
}

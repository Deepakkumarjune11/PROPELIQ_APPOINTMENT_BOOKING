namespace PropelIQ.Api.HealthCheck;

/// <summary>
/// Configuration POCO for health alert notification channels (US_034, AC-2).
/// Bound from <c>appsettings.json → "HealthAlert"</c> section.
///
/// SECURITY (OWASP A02): Sensitive keys (<see cref="EmailAlertChannel.SendGridApiKey"/>,
/// <see cref="SmsAlertChannel.AccountSid"/>, <see cref="SmsAlertChannel.AuthToken"/>,
/// <see cref="PagerDutyAlertChannel.RoutingKey"/>) are intentionally left empty here.
/// Inject them via environment variables or Azure Key Vault at deployment time — never
/// store secrets in source-controlled configuration files.
/// </summary>
public sealed class HealthAlertOptions
{
    public const string SectionName = "HealthAlert";

    public int ConsecutiveFailureThreshold { get; set; } = 2;
    public int AlertDeduplicationMinutes { get; set; } = 5;

    public EmailAlertChannel Email { get; set; } = new();
    public SmsAlertChannel Sms { get; set; } = new();
    public PagerDutyAlertChannel PagerDuty { get; set; } = new();
}

public sealed class EmailAlertChannel
{
    public bool Enabled { get; set; }
    public string ToAddress { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string SubjectPrefix { get; set; } = "[PropelIQ ALERT]";
    public string SendGridApiKey { get; set; } = string.Empty; // inject from env/Key Vault — not in appsettings.json
}

public sealed class SmsAlertChannel
{
    public bool Enabled { get; set; }
    public string ToNumber { get; set; } = string.Empty;
    public string FromNumber { get; set; } = string.Empty;
    public string AccountSid { get; set; } = string.Empty;   // inject from env/Key Vault
    public string AuthToken { get; set; } = string.Empty;    // inject from env/Key Vault
}

public sealed class PagerDutyAlertChannel
{
    public bool Enabled { get; set; }
    public string RoutingKey { get; set; } = string.Empty;   // inject from env/Key Vault
    public string Severity { get; set; } = "critical";       // critical | error | warning | info
}

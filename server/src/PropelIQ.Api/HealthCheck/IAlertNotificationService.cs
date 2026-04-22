namespace PropelIQ.Api.HealthCheck;

/// <summary>
/// Dispatches health alert notifications across configured channels
/// (email via SendGrid, SMS via Twilio, PagerDuty Events API v2).
/// </summary>
public interface IAlertNotificationService
{
    /// <summary>
    /// Sends an alert for <paramref name="checkName"/> (if not suppressed by the deduplication window).
    /// </summary>
    Task SendAlertAsync(string checkName, string status, string detail, CancellationToken ct = default);
}

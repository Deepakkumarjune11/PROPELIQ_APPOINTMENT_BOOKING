using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;
using StackExchange.Redis;
using Twilio;
using Twilio.Rest.Api.V2010.Account;

namespace PropelIQ.Api.HealthCheck;

/// <summary>
/// Dispatches health alerts to enabled channels: SendGrid email, Twilio SMS, PagerDuty Events API v2.
///
/// Deduplication: Redis key <c>hc:alert:sent:{checkName}</c> with <see cref="HealthAlertOptions.AlertDeduplicationMinutes"/>
/// TTL prevents alert storms (at most one alert per dedup window per check name).
///
/// Each channel dispatch is wrapped in a try/catch so a failure in one channel never prevents
/// others from firing.
///
/// SECURITY (OWASP A02): Secrets read exclusively from <see cref="IOptionsMonitor{T}"/> (hot-reloadable);
/// never hardcoded. Empty secret strings result in channel being skipped gracefully.
/// </summary>
public sealed class AlertNotificationService(
    IOptionsMonitor<HealthAlertOptions> opts,
    IConnectionMultiplexer redis,
    IHttpClientFactory httpClientFactory,
    ILogger<AlertNotificationService> logger) : IAlertNotificationService
{
    private static string DedupeKey(string name) => $"hc:alert:sent:{name}";

    public async Task SendAlertAsync(
        string checkName, string status, string detail, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        if (await db.KeyExistsAsync(DedupeKey(checkName)).ConfigureAwait(false))
        {
            logger.LogDebug("Alert suppressed (dedup active) for check '{CheckName}'", checkName);
            return;
        }

        var o = opts.CurrentValue;
        var message = $"[PropelIQ] Service alert: {checkName} is {status}. {detail}";

        // Fire enabled channels independently; one failure must NOT prevent the others
        if (o.Email.Enabled) await SendEmailAsync(o.Email, checkName, message, ct).ConfigureAwait(false);
        if (o.Sms.Enabled)   await SendSmsAsync(o.Sms, message, ct).ConfigureAwait(false);
        if (o.PagerDuty.Enabled) await SendPagerDutyAsync(o.PagerDuty, checkName, status, detail, ct).ConfigureAwait(false);

        // Stamp dedup key regardless of whether channels were enabled — prevents re-dispatch
        // for the dedup window even if all channels were disabled at alert time but might be re-enabled
        await db.StringSetAsync(
            DedupeKey(checkName),
            "1",
            TimeSpan.FromMinutes(o.AlertDeduplicationMinutes))
            .ConfigureAwait(false);
    }

    private async Task SendEmailAsync(
        EmailAlertChannel cfg, string checkName, string message, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cfg.SendGridApiKey))
        {
            logger.LogWarning("SendGrid alert skipped — SendGridApiKey not configured");
            return;
        }

        try
        {
            var client = new SendGridClient(cfg.SendGridApiKey);
            var msg = new SendGridMessage
            {
                From = new EmailAddress(cfg.FromAddress),
                Subject = $"{cfg.SubjectPrefix} {checkName} {message[..Math.Min(message.Length, 50)]}",
                PlainTextContent = message,
            };
            msg.AddTo(new EmailAddress(cfg.ToAddress));
            var response = await client.SendEmailAsync(msg, ct).ConfigureAwait(false);
            if ((int)response.StatusCode >= 400)
                logger.LogWarning("SendGrid alert returned {StatusCode} for check '{CheckName}'",
                    response.StatusCode, checkName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Email alert dispatch failed for check '{CheckName}'", checkName);
        }
    }

    private async Task SendSmsAsync(SmsAlertChannel cfg, string message, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cfg.AccountSid) || string.IsNullOrWhiteSpace(cfg.AuthToken))
        {
            logger.LogWarning("Twilio SMS alert skipped — AccountSid or AuthToken not configured");
            return;
        }

        try
        {
            TwilioClient.Init(cfg.AccountSid, cfg.AuthToken);
            var body = message[..Math.Min(message.Length, 160)]; // SMS 160-char limit
            await MessageResource.CreateAsync(
                to: new Twilio.Types.PhoneNumber(cfg.ToNumber),
                from: new Twilio.Types.PhoneNumber(cfg.FromNumber),
                body: body)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SMS alert dispatch failed");
        }
    }

    private async Task SendPagerDutyAsync(
        PagerDutyAlertChannel cfg, string checkName, string status, string detail, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cfg.RoutingKey))
        {
            logger.LogWarning("PagerDuty alert skipped — RoutingKey not configured");
            return;
        }

        try
        {
            // PagerDuty Events API v2 — named HTTP client "pagerduty-http" with Polly retry (US_035, TR-023)
            var http = httpClientFactory.CreateClient("pagerduty-http");
            var payload = JsonSerializer.Serialize(new
            {
                routing_key = cfg.RoutingKey,
                event_action = "trigger",
                payload = new
                {
                    summary = $"PropelIQ: {checkName} is {status}",
                    severity = cfg.Severity,
                    source = "propeliq-health-check",
                    custom_details = new { checkName, status, detail },
                },
            });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            await http.PostAsync("/v2/enqueue", content, ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PagerDuty alert dispatch failed for check '{CheckName}'", checkName);
        }
    }
}

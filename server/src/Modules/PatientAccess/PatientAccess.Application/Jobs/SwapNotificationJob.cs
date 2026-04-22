using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PatientAccess.Application.Configuration;
using PatientAccess.Application.Repositories;
using PatientAccess.Domain.Enums;
using SendGrid;
using SendGrid.Helpers.Mail;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace PatientAccess.Application.Jobs;

/// <summary>
/// Hangfire job that notifies a patient via SMS and email after their appointment has
/// been successfully swapped to the preferred slot (US_015, AC-4).
///
/// Retry policy: up to 3 attempts with progressive delays so transient provider outages
/// do not permanently block delivery (TR-023).  Delivery outcome is always written to
/// <c>communication_log</c> for staff visibility, even on failure (FR-007 extension 5a).
/// </summary>
[AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 10, 30, 90 })]
public sealed class SwapNotificationJob
{
    private readonly ICommunicationLogRepository        _communicationLogRepo;
    private readonly TwilioOptions                      _twilioOptions;
    private readonly SendGridOptions                    _sendGridOptions;
    private readonly ILogger<SwapNotificationJob>       _logger;

    public SwapNotificationJob(
        ICommunicationLogRepository         communicationLogRepo,
        IOptions<TwilioOptions>             twilioOptions,
        IOptions<SendGridOptions>           sendGridOptions,
        ILogger<SwapNotificationJob>        logger)
    {
        _communicationLogRepo = communicationLogRepo;
        _twilioOptions        = twilioOptions.Value;
        _sendGridOptions      = sendGridOptions.Value;
        _logger               = logger;
    }

    /// <summary>
    /// Sends an SMS and an email confirming the slot swap.
    /// Parameters are JSON-serializable (no CancellationToken) because Hangfire persists them.
    /// </summary>
    /// <param name="patientId">Patient record ID — used for CommunicationLog FK.</param>
    /// <param name="appointmentId">Appointment record ID — used for CommunicationLog FK.</param>
    /// <param name="phone">E.164 destination phone number.</param>
    /// <param name="email">Destination email address.</param>
    /// <param name="patientName">Patient display name for the message body.</param>
    /// <param name="newSlotDatetime">UTC datetime of the newly assigned slot.</param>
    public async Task Execute(
        Guid     patientId,
        Guid     appointmentId,
        string   phone,
        string   email,
        string   patientName,
        DateTime newSlotDatetime)
    {
        var localDisplay = newSlotDatetime.ToString("dddd, MMMM d, yyyy h:mm tt") + " UTC";

        await SendSmsAsync(patientId, appointmentId, phone, localDisplay);
        await SendEmailAsync(patientId, appointmentId, email, patientName, localDisplay);
    }

    // ── SMS ───────────────────────────────────────────────────────────────────

    private async Task SendSmsAsync(
        Guid   patientId,
        Guid   appointmentId,
        string phone,
        string displayDatetime)
    {
        var status = CommunicationStatus.Failed;

        try
        {
            if (string.IsNullOrWhiteSpace(_twilioOptions.AccountSid) ||
                string.IsNullOrWhiteSpace(_twilioOptions.AuthToken)  ||
                string.IsNullOrWhiteSpace(_twilioOptions.FromNumber))
            {
                _logger.LogWarning(
                    "Twilio credentials not configured; skipping SMS for appointment {AppointmentId}.",
                    appointmentId);
                return;
            }

            TwilioClient.Init(_twilioOptions.AccountSid, _twilioOptions.AuthToken);

            await MessageResource.CreateAsync(
                body: $"Good news! Your appointment has been moved to your preferred slot: {displayDatetime}. Reply STOP to opt out.",
                from: new PhoneNumber(_twilioOptions.FromNumber),
                to:   new PhoneNumber(phone));

            status = CommunicationStatus.Sent;

            _logger.LogInformation(
                "SwapNotificationJob: SMS sent for appointment {AppointmentId}.", appointmentId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "SwapNotificationJob: SMS delivery failed for appointment {AppointmentId}.", appointmentId);
            throw;
        }
        finally
        {
            await _communicationLogRepo.AddAsync(new CommunicationLogEntry(
                PatientId:     patientId,
                AppointmentId: appointmentId,
                Channel:       CommunicationChannel.SMS,
                Status:        status,
                AttemptCount:  1));
        }
    }

    // ── Email ──────────────────────────────────────────────────────────────────

    private async Task SendEmailAsync(
        Guid   patientId,
        Guid   appointmentId,
        string email,
        string patientName,
        string displayDatetime)
    {
        var status = CommunicationStatus.Failed;

        try
        {
            if (string.IsNullOrWhiteSpace(_sendGridOptions.ApiKey))
            {
                _logger.LogWarning(
                    "SendGrid API key not configured; skipping email for appointment {AppointmentId}.",
                    appointmentId);
                return;
            }

            var client = new SendGridClient(_sendGridOptions.ApiKey);
            var msg    = new SendGridMessage();

            msg.SetFrom(_sendGridOptions.FromEmail, "PropelIQ");
            msg.AddTo(email);
            msg.SetSubject("Your Appointment Has Been Moved – PropelIQ");
            msg.AddContent(
                MimeType.Html,
                $"<p>Hi {System.Net.WebUtility.HtmlEncode(patientName)},</p>" +
                "<p>Great news! Your appointment has been automatically moved to your preferred time slot.</p>" +
                $"<p><strong>New Date &amp; Time:</strong> {System.Net.WebUtility.HtmlEncode(displayDatetime)}</p>" +
                "<p>If you have any questions, please contact our office. Thank you for using PropelIQ.</p>");

            var response = await client.SendEmailAsync(msg);

            if ((int)response.StatusCode >= 200 && (int)response.StatusCode < 300)
            {
                status = CommunicationStatus.Sent;
                _logger.LogInformation(
                    "SwapNotificationJob: email sent for appointment {AppointmentId}.", appointmentId);
            }
            else
            {
                _logger.LogWarning(
                    "SwapNotificationJob: SendGrid returned {StatusCode} for appointment {AppointmentId}.",
                    response.StatusCode,
                    appointmentId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "SwapNotificationJob: email delivery failed for appointment {AppointmentId}.", appointmentId);
            throw;
        }
        finally
        {
            await _communicationLogRepo.AddAsync(new CommunicationLogEntry(
                PatientId:     patientId,
                AppointmentId: appointmentId,
                Channel:       CommunicationChannel.Email,
                Status:        status,
                AttemptCount:  1));
        }
    }
}

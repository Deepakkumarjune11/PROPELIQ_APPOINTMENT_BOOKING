using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PatientAccess.Application.Configuration;
using PatientAccess.Application.Repositories;
using PatientAccess.Domain.Enums;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace PatientAccess.Application.Jobs;

/// <summary>
/// Hangfire background job that sends an SMS appointment reminder via Twilio (FR-007).
/// Retried once on failure per the edge-case spec; the final outcome is recorded in
/// <c>communication_log</c> so staff can see delivery failures (FR-007 extension 5a).
/// </summary>
[AutomaticRetry(Attempts = 1)]
public sealed class SendReminderSmsJob
{
    private readonly ICommunicationLogRepository _communicationLogRepo;
    private readonly TwilioOptions               _twilioOptions;
    private readonly ILogger<SendReminderSmsJob> _logger;

    public SendReminderSmsJob(
        ICommunicationLogRepository             communicationLogRepo,
        IOptions<TwilioOptions>                 twilioOptions,
        ILogger<SendReminderSmsJob>             logger)
    {
        _communicationLogRepo = communicationLogRepo;
        _twilioOptions        = twilioOptions.Value;
        _logger               = logger;
    }

    /// <summary>
    /// Sends an SMS reminder.  Parameter types must all be JSON-serializable
    /// (no CancellationToken) as Hangfire serializes them to the job store.
    /// </summary>
    /// <param name="patientId">Patient record ID (for CommunicationLog FK).</param>
    /// <param name="appointmentId">Appointment record ID (for CommunicationLog FK).</param>
    /// <param name="phoneNumber">E.164-formatted destination phone number.</param>
    /// <param name="appointmentSummary">Human-readable appointment time string for the SMS body.</param>
    public async Task Execute(
        Guid   patientId,
        Guid   appointmentId,
        string phoneNumber,
        string appointmentSummary)
    {
        var status = CommunicationStatus.Failed;

        try
        {
            // Credentials are read at job execution time (not captured in the closure)
            // so rotation does not require re-enqueuing jobs (OWASP A02).
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
                body: $"Reminder: Your appointment is scheduled for {appointmentSummary}. Reply STOP to opt out.",
                from: new PhoneNumber(_twilioOptions.FromNumber),
                to:   new PhoneNumber(phoneNumber));

            status = CommunicationStatus.Sent;

            _logger.LogInformation(
                "SMS reminder sent for appointment {AppointmentId}.", appointmentId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "SMS delivery failed for appointment {AppointmentId}; status recorded as Failed.",
                appointmentId);
            throw; // Re-throw so Hangfire's AutomaticRetry policy fires
        }
        finally
        {
            // Always write the log entry (including on failure) so staff visibility is maintained
            await _communicationLogRepo.AddAsync(new CommunicationLogEntry(
                PatientId:     patientId,
                AppointmentId: appointmentId,
                Channel:       CommunicationChannel.SMS,
                Status:        status,
                AttemptCount:  1));
        }
    }
}

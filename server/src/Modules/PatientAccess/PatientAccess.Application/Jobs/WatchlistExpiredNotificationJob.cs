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
/// Hangfire job that notifies a patient when their preferred watchlist slot has expired —
/// i.e., the preferred datetime passed without the slot becoming available (US_015, AC-5).
///
/// Retry policy: 1 attempt matches the SMS reminder pattern (FR-007).
/// Outcome is recorded in <c>communication_log</c> for staff visibility.
/// </summary>
[AutomaticRetry(Attempts = 1)]
public sealed class WatchlistExpiredNotificationJob
{
    private readonly ICommunicationLogRepository              _communicationLogRepo;
    private readonly TwilioOptions                            _twilioOptions;
    private readonly ILogger<WatchlistExpiredNotificationJob> _logger;

    public WatchlistExpiredNotificationJob(
        ICommunicationLogRepository               communicationLogRepo,
        IOptions<TwilioOptions>                   twilioOptions,
        ILogger<WatchlistExpiredNotificationJob>  logger)
    {
        _communicationLogRepo = communicationLogRepo;
        _twilioOptions        = twilioOptions.Value;
        _logger               = logger;
    }

    /// <summary>
    /// Sends an SMS informing the patient that their watchlist slot is no longer available.
    /// Parameters are JSON-serializable (no CancellationToken) because Hangfire persists them.
    /// </summary>
    /// <param name="patientId">Patient record ID — used for CommunicationLog FK.</param>
    /// <param name="appointmentId">Appointment record ID — used for CommunicationLog FK.</param>
    /// <param name="phone">E.164 destination phone number.</param>
    /// <param name="email">Destination email address (reserved for future email variant).</param>
    /// <param name="patientName">Patient display name for the message body.</param>
    /// <param name="expiredSlotDatetime">UTC datetime of the preferred slot that expired.</param>
    public async Task Execute(
        Guid     patientId,
        Guid     appointmentId,
        string   phone,
        string   email,
        string   patientName,
        DateTime expiredSlotDatetime)
    {
        var status       = CommunicationStatus.Failed;
        var displayDate  = expiredSlotDatetime.ToString("MMMM d, yyyy") + " UTC";

        try
        {
            if (string.IsNullOrWhiteSpace(_twilioOptions.AccountSid) ||
                string.IsNullOrWhiteSpace(_twilioOptions.AuthToken)  ||
                string.IsNullOrWhiteSpace(_twilioOptions.FromNumber))
            {
                _logger.LogWarning(
                    "Twilio credentials not configured; skipping expiry SMS for appointment {AppointmentId}.",
                    appointmentId);
                return;
            }

            TwilioClient.Init(_twilioOptions.AccountSid, _twilioOptions.AuthToken);

            await MessageResource.CreateAsync(
                body: $"Your preferred slot for {displayDate} is no longer available and has been removed from your watchlist. Your current appointment remains unchanged. Reply STOP to opt out.",
                from: new PhoneNumber(_twilioOptions.FromNumber),
                to:   new PhoneNumber(phone));

            status = CommunicationStatus.Sent;

            _logger.LogInformation(
                "WatchlistExpiredNotificationJob: expiry SMS sent for appointment {AppointmentId}.", appointmentId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "WatchlistExpiredNotificationJob: SMS delivery failed for appointment {AppointmentId}.", appointmentId);
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
}

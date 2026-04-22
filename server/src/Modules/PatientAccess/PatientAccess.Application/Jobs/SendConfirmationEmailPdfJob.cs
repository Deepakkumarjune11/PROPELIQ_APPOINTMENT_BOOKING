using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PatientAccess.Application.Configuration;
using PatientAccess.Application.Repositories;
using PatientAccess.Application.Services;
using PatientAccess.Domain.Enums;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace PatientAccess.Application.Jobs;

/// <summary>
/// Hangfire background job that generates a PDF appointment confirmation via PDFsharp,
/// attaches it to a SendGrid email, delivers it to the patient, and persists both the
/// delivery outcome and the PDF bytes to <c>communication_log</c> (TR-014, FR-007).
/// Exponential backoff: retried up to 3 times with 10 s / 30 s / 90 s delays (TR-023).
/// </summary>
[AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 10, 30, 90 })]
public sealed class SendConfirmationEmailPdfJob
{
    private readonly IPdfGenerationService              _pdfService;
    private readonly ICommunicationLogRepository        _communicationLogRepo;
    private readonly SendGridOptions                    _sendGridOptions;
    private readonly ILogger<SendConfirmationEmailPdfJob> _logger;

    public SendConfirmationEmailPdfJob(
        IPdfGenerationService                   pdfService,
        ICommunicationLogRepository             communicationLogRepo,
        IOptions<SendGridOptions>               sendGridOptions,
        ILogger<SendConfirmationEmailPdfJob>    logger)
    {
        _pdfService           = pdfService;
        _communicationLogRepo = communicationLogRepo;
        _sendGridOptions      = sendGridOptions.Value;
        _logger               = logger;
    }

    /// <summary>
    /// Generates a PDF and sends it as an email attachment.
    /// Parameter types must all be JSON-serializable (no CancellationToken)
    /// as Hangfire serializes them to the job store.
    /// </summary>
    /// <param name="patientId">Patient record ID (for CommunicationLog FK).</param>
    /// <param name="appointmentId">Appointment record ID (for CommunicationLog FK).</param>
    /// <param name="email">Destination email address.</param>
    /// <param name="details">Full appointment details for the PDF and email body.</param>
    public async Task Execute(
        Guid                         patientId,
        Guid                         appointmentId,
        string                       email,
        AppointmentConfirmationDetails details)
    {
        byte[]? pdfBytes = null;
        var status       = CommunicationStatus.Failed;

        try
        {
            if (string.IsNullOrWhiteSpace(_sendGridOptions.ApiKey))
            {
                _logger.LogWarning(
                    "SendGrid API key not configured; skipping email for appointment {AppointmentId}.",
                    appointmentId);
                return;
            }

            // Generate PDF — synchronous, PDFsharp is not async (TR-014)
            pdfBytes = _pdfService.Generate(details);

            var client = new SendGridClient(_sendGridOptions.ApiKey);
            var msg    = new SendGridMessage();

            msg.SetFrom(_sendGridOptions.FromEmail, "PropelIQ");
            msg.AddTo(email);
            msg.SetSubject("Your Appointment Confirmation – PropelIQ");
            msg.AddContent(
                MimeType.Html,
                $"<p>Hi {System.Net.WebUtility.HtmlEncode(details.PatientName)},</p>" +
                "<p>Your appointment has been confirmed. Please find your PDF confirmation attached.</p>" +
                $"<p><strong>Date:</strong> {System.Net.WebUtility.HtmlEncode(details.SlotDatetime.ToString("dddd, MMMM d, yyyy h:mm tt"))}</p>" +
                $"<p><strong>Provider:</strong> {System.Net.WebUtility.HtmlEncode(details.ProviderName)}</p>");

            msg.AddAttachment(
                filename:    "confirmation.pdf",
                base64Content: Convert.ToBase64String(pdfBytes),
                type:        "application/pdf");

            var response = await client.SendEmailAsync(msg);

            if ((int)response.StatusCode is < 200 or >= 300)
            {
                var body = await response.Body.ReadAsStringAsync();
                throw new InvalidOperationException(
                    $"SendGrid returned {(int)response.StatusCode}: {body}");
            }

            status = CommunicationStatus.Sent;

            _logger.LogInformation(
                "Confirmation email sent to {EmailHash} for appointment {AppointmentId}.",
                HashEmailForLog(email), appointmentId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Confirmation email failed for appointment {AppointmentId}.",
                appointmentId);
            throw; // Re-throw so Hangfire's AutomaticRetry policy fires
        }
        finally
        {
            // Persist log + PDF bytes so the GET /pdf endpoint can serve them (TR-014)
            await _communicationLogRepo.AddAsync(new CommunicationLogEntry(
                PatientId:     patientId,
                AppointmentId: appointmentId,
                Channel:       CommunicationChannel.Email,
                Status:        status,
                AttemptCount:  1,
                PdfBytes:      status == CommunicationStatus.Sent ? pdfBytes : null));
        }
    }

    /// <summary>
    /// Returns a one-way hash of the email address suitable for structured logs (OWASP A09 / PHI).
    /// </summary>
    private static string HashEmailForLog(string email)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(email));
        return Convert.ToHexString(bytes)[..8];
    }
}

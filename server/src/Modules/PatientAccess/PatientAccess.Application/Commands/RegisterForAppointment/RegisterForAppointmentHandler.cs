using Hangfire;
using MediatR;
using Microsoft.Extensions.Logging;
using PatientAccess.Application.Infrastructure;
using PatientAccess.Application.Jobs;
using PatientAccess.Application.Repositories;
using PatientAccess.Application.Services;

namespace PatientAccess.Application.Commands.RegisterForAppointment;

/// <summary>
/// Handles appointment registration in a single database transaction (FR-002, AC-3):
/// <list type="number">
///   <item>Gets slot datetime for risk scoring (lightweight point lookup).</item>
///   <item>Calls insurance soft-validation (non-blocking).</item>
///   <item>Calculates full no-show risk score using all available signals (AC-1).</item>
///   <item>Delegates the transactional upsert + slot-booking + risk score + audit-log to
///     <see cref="IAppointmentRegistrationRepository"/>, which also converts
///     <c>DbUpdateConcurrencyException</c> to <see cref="Exceptions.SlotAlreadyBookedException"/> (AC-4).</item>
/// </list>
/// Insurance validation exceptions are caught and recorded as <c>pending</c> — the booking
/// is NOT rolled back when the validation service is unavailable.
/// </summary>
public sealed class RegisterForAppointmentHandler
    : IRequestHandler<RegisterForAppointmentCommand, RegisterForAppointmentResponse>
{
    private const decimal HighRiskThreshold = 0.70m;

    private readonly IAppointmentRegistrationRepository _repository;
    private readonly IInsuranceValidationService        _insuranceService;
    private readonly INoShowRiskScoringService           _riskScorer;
    private readonly IBackgroundJobClient                _backgroundJobClient;
    private readonly ILogger<RegisterForAppointmentHandler> _logger;

    public RegisterForAppointmentHandler(
        IAppointmentRegistrationRepository repository,
        IInsuranceValidationService        insuranceService,
        INoShowRiskScoringService           riskScorer,
        IBackgroundJobClient                backgroundJobClient,
        ILogger<RegisterForAppointmentHandler> logger)
    {
        _repository          = repository;
        _insuranceService    = insuranceService;
        _riskScorer          = riskScorer;
        _backgroundJobClient = backgroundJobClient;
        _logger              = logger;
    }

    public async Task<RegisterForAppointmentResponse> Handle(
        RegisterForAppointmentCommand cmd,
        CancellationToken ct)
    {
        // ── a. Get slot datetime for risk scoring (lightweight point lookup) ─
        var slotDatetime = await _repository.GetSlotDatetimeAsync(cmd.SlotId, ct);

        // ── b. Insurance soft-validation (non-blocking) ─────────────────────
        var validationResult = InsuranceValidationResult.Pending;
        try
        {
            validationResult = await _insuranceService.ValidateAsync(
                cmd.InsuranceProvider, cmd.InsuranceMemberId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Insurance validation service unavailable for slot {SlotId}; status recorded as pending.",
                cmd.SlotId);
        }

        var insuranceStatus = validationResult switch
        {
            InsuranceValidationResult.Pass         => "pass",
            InsuranceValidationResult.PartialMatch => "partial-match",
            InsuranceValidationResult.Fail         => "fail",
            _                                       => "pending",
        };

        // ── c. No-show risk scoring (AC-1) ───────────────────────────────────
        // intakeCompleted = false: intake is step 4; registration is step 3 in the booking flow.
        // IsPartialScoring will be true, which is acceptable per US_013 edge case.
        var riskResult = _riskScorer.CalculateFullRisk(
            slotDatetime,
            validationResult,
            intakeCompleted: false);

        if (riskResult.IsPartialScoring)
        {
            // PHI-safe: only appointment slot ID and signal count logged (AIR-S01)
            _logger.LogWarning(
                "[PartialScoring] Slot {SlotId} scored with {SignalCount} signals; intake not yet completed.",
                cmd.SlotId, riskResult.ContributingFactors.Count);
        }

        // ── d. Transactional registration (AC-3 atomicity) ───────────────────
        // NoShowRiskScore is set inside the repository before SaveChangesAsync,
        // ensuring it is persisted in the same transaction as patient upsert + slot booking.
        // DbUpdateConcurrencyException (xmin mismatch) is caught in the repository
        // and re-thrown as SlotAlreadyBookedException → controller returns 409 (AC-4).
        var data = await _repository.RegisterAsync(
            slotId:            cmd.SlotId,
            email:             cmd.Email,
            name:              cmd.Name,
            dob:               cmd.Dob,
            phone:             cmd.Phone,
            insuranceProvider: cmd.InsuranceProvider,
            insuranceMemberId: cmd.InsuranceMemberId,
            insuranceStatus:   insuranceStatus,
            noShowRiskScore:   riskResult.Score,
            ct:                ct);

        _logger.LogInformation(
            "Appointment {SlotId} booked for patient {PatientId} (insuranceStatus={InsuranceStatus} riskScore={RiskScore})",
            cmd.SlotId, data.PatientId, insuranceStatus, riskResult.Score);

        // ── e. Enqueue background notification jobs (AC-1 / TR-009) ────────────────────
        // Jobs are enqueued AFTER SaveChangesAsync so foreign keys are satisfied.
        // Enqueueing failures do NOT roll back the booking (notifications are best-effort at this point).
        try
        {
            var confirmationDetails = new AppointmentConfirmationDetails
            {
                AppointmentId = cmd.SlotId,
                PatientId     = data.PatientId,
                PatientName   = cmd.Name,
                PatientEmail  = cmd.Email,
                PatientPhone  = cmd.Phone,
                SlotDatetime  = slotDatetime,
                ProviderName  = "PropelIQ Provider",
                ClinicName    = "PropelIQ Clinic",
                ClinicPhone   = "N/A",
            };

            var appointmentSummary =
                $"{slotDatetime:dddd, MMMM d, yyyy} at {slotDatetime:h:mm tt}";

            // Immediate confirmation email + PDF — critical queue (TR-009)
            _backgroundJobClient.Create(
                Hangfire.Common.Job.FromExpression<SendConfirmationEmailPdfJob>(
                    j => j.Execute(data.PatientId, cmd.SlotId, cmd.Email, confirmationDetails)),
                new Hangfire.States.EnqueuedState("critical"));

            // Immediate confirmation SMS — critical queue (TR-009)
            _backgroundJobClient.Create(
                Hangfire.Common.Job.FromExpression<SendReminderSmsJob>(
                    j => j.Execute(data.PatientId, cmd.SlotId, cmd.Phone, appointmentSummary)),
                new Hangfire.States.EnqueuedState("critical"));

            // 24-hour reminder SMS — default queue, scheduled to fire 24 h before the appointment
            var reminderFireAt = slotDatetime.AddHours(-24);
            if (reminderFireAt > DateTime.UtcNow)
            {
                _backgroundJobClient.Create(
                    Hangfire.Common.Job.FromExpression<SendReminderSmsJob>(
                        j => j.Execute(data.PatientId, cmd.SlotId, cmd.Phone, "Reminder: " + appointmentSummary)),
                    new Hangfire.States.ScheduledState(reminderFireAt));
            }
        }
        catch (Exception ex)
        {
            // Log and swallow: the booking committed successfully; notification failure is non-blocking
            _logger.LogError(ex,
                "Failed to enqueue notification jobs for appointment {SlotId}.",
                cmd.SlotId);
        }

        return new RegisterForAppointmentResponse(
            data.PatientId,
            cmd.SlotId,
            insuranceStatus,
            riskResult.Score,
            riskResult.Score > HighRiskThreshold,
            riskResult.ContributingFactors);
    }
}


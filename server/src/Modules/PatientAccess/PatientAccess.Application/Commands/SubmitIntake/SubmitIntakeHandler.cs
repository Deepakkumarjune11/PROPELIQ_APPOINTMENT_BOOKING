using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using PatientAccess.Application.Exceptions;
using PatientAccess.Application.Repositories;
using PatientAccess.Domain.Enums;

namespace PatientAccess.Application.Commands.SubmitIntake;

/// <summary>
/// Handles <see cref="SubmitIntakeCommand"/>:
/// <list type="number">
///   <item>Validates the patient exists.</item>
///   <item>Parses the intake mode string to <see cref="IntakeMode"/>.</item>
///   <item>Serialises the answers dictionary to JSON.</item>
///   <item>Delegates persistence + audit-log to <see cref="IIntakeSubmissionRepository"/>.</item>
/// </list>
/// PHI guard (DR-015): raw answers are never emitted to logs — only question count is recorded.
/// </summary>
public sealed class SubmitIntakeHandler : IRequestHandler<SubmitIntakeCommand, SubmitIntakeResponse>
{
    private readonly IIntakeSubmissionRepository _repository;
    private readonly ILogger<SubmitIntakeHandler> _logger;

    public SubmitIntakeHandler(
        IIntakeSubmissionRepository repository,
        ILogger<SubmitIntakeHandler> logger)
    {
        _repository = repository;
        _logger     = logger;
    }

    public async Task<SubmitIntakeResponse> Handle(
        SubmitIntakeCommand cmd,
        CancellationToken cancellationToken)
    {
        // a. Validate patient exists
        var patientExists = await _repository.PatientExistsAsync(cmd.PatientId, cancellationToken);
        if (!patientExists)
            throw new NotFoundException($"Patient {cmd.PatientId} was not found.");

        // b. Parse intake mode — case-insensitive to be tolerant of client casing
        if (!Enum.TryParse<IntakeMode>(cmd.Mode, ignoreCase: true, out var mode))
            throw new ArgumentException($"Unrecognised intake mode '{cmd.Mode}'.", nameof(cmd.Mode));

        // c. Serialise answers — repository receives the pre-serialised string so that
        //    the EF Core ValueConverter (PHI encryption, DR-015) can operate on a plain string.
        var answersJson = JsonSerializer.Serialize(cmd.Answers);

        // d. Persist via repository (IntakeResponse insert + AuditLog — no wrapping transaction
        //    needed because both rows belong to the same aggregate root unit of work).
        var intakeResponseId = await _repository.SubmitIntakeAsync(
            cmd.PatientId, mode, answersJson, cancellationToken);

        // DR-015 / PHI guard: log only question count, never answer content.
        _logger.LogInformation(
            "Intake submitted. Mode={Mode} QuestionCount={Count}",
            cmd.Mode, cmd.Answers.Count);

        return new SubmitIntakeResponse(intakeResponseId);
    }
}

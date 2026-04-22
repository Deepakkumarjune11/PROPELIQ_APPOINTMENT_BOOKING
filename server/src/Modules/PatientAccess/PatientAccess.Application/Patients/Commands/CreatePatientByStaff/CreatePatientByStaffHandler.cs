using MediatR;
using Microsoft.Extensions.Logging;
using PatientAccess.Application.Patients.Dtos;
using PatientAccess.Application.Repositories;

namespace PatientAccess.Application.Patients.Commands.CreatePatientByStaff;

/// <summary>
/// Handles <see cref="CreatePatientByStaffCommand"/> — delegates to
/// <see cref="IPatientStaffRepository"/> which enforces email uniqueness,
/// writes the AuditLog, and saves in a single transaction (DR-008).
/// </summary>
public sealed class CreatePatientByStaffHandler
    : IRequestHandler<CreatePatientByStaffCommand, PatientSearchResultDto>
{
    private readonly IPatientStaffRepository               _repo;
    private readonly ILogger<CreatePatientByStaffHandler>  _logger;

    public CreatePatientByStaffHandler(
        IPatientStaffRepository              repo,
        ILogger<CreatePatientByStaffHandler> logger)
    {
        _repo   = repo;
        _logger = logger;
    }

    public async Task<PatientSearchResultDto> Handle(
        CreatePatientByStaffCommand command,
        CancellationToken           cancellationToken)
    {
        var result = await _repo.CreatePatientAsync(command, cancellationToken);

        _logger.LogInformation(
            "Staff {StaffId} created patient {PatientId} ({Email}).",
            command.StaffId, result.Id, result.Email);

        return result;
    }
}

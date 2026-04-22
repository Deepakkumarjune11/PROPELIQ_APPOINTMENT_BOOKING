using PatientAccess.Application.Patients.Dtos;
using PatientAccess.Application.Patients.Commands.CreatePatientByStaff;

namespace PatientAccess.Application.Repositories;

/// <summary>
/// Data-layer contract for staff-initiated patient search and creation (FR-008).
/// </summary>
public interface IPatientStaffRepository
{
    /// <summary>
    /// Searches patients by partial email or phone (case-insensitive, top 10).
    /// </summary>
    Task<IReadOnlyList<PatientSearchResultDto>> SearchByEmailOrPhoneAsync(
        string            query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a minimal patient profile initiated by staff (DR-008).
    /// Throws <c>ConflictException</c> when the email already exists.
    /// </summary>
    Task<PatientSearchResultDto> CreatePatientAsync(
        CreatePatientByStaffCommand command,
        CancellationToken           cancellationToken = default);
}

using MediatR;
using PatientAccess.Application.Patients.Dtos;

namespace PatientAccess.Application.Patients.Commands.CreatePatientByStaff;

/// <summary>
/// Creates a minimal patient profile from the staff walk-in booking flow (US_016, AC-3).
/// Distinct from patient self-registration to prevent permission confusion.
/// </summary>
/// <param name="FullName">Patient full name (required).</param>
/// <param name="Email">Patient email — must be unique across active patients (409 on duplicate).</param>
/// <param name="Phone">Patient phone number (E.164 format).</param>
/// <param name="StaffId">Acting staff member's ID — written to AuditLog.</param>
public sealed record CreatePatientByStaffCommand(
    string FullName,
    string Email,
    string Phone,
    Guid   StaffId
) : IRequest<PatientSearchResultDto>;

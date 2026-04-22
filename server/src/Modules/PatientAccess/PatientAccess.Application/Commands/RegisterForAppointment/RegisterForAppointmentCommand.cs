using MediatR;

namespace PatientAccess.Application.Commands.RegisterForAppointment;

/// <summary>
/// Books the specified available appointment slot for a patient.
/// Creates the patient record if no matching email exists (AC-1, AC-4).
/// All DB writes execute within a single transaction (FR-002).
/// </summary>
/// <param name="SlotId">Appointment slot to book.</param>
/// <param name="Email">Patient email — used as natural key for upsert (AC-4).</param>
/// <param name="Name">Patient full name.</param>
/// <param name="Dob">Patient date of birth.</param>
/// <param name="Phone">Patient contact phone number.</param>
/// <param name="InsuranceProvider">Optional insurance provider name.</param>
/// <param name="InsuranceMemberId">Optional insurance member ID.</param>
public sealed record RegisterForAppointmentCommand(
    Guid     SlotId,
    string   Email,
    string   Name,
    DateOnly Dob,
    string   Phone,
    string?  InsuranceProvider,
    string?  InsuranceMemberId
) : IRequest<RegisterForAppointmentResponse>;

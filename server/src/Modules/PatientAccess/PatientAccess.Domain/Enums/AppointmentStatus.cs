namespace PatientAccess.Domain.Enums;

/// <summary>Appointment lifecycle states — DR-002.</summary>
public enum AppointmentStatus
{
    Booked,
    Arrived,
    Completed,
    Cancelled,
    NoShow
}

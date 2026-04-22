namespace PatientAccess.Domain.Enums;

/// <summary>Appointment lifecycle states — DR-002.</summary>
public enum AppointmentStatus
{
    /// <summary>Slot is open and not yet claimed by any patient.</summary>
    Available,
    Booked,
    Arrived,
    Completed,
    Cancelled,
    NoShow,
    /// <summary>Patient has been called into the examination room (US_017).</summary>
    InRoom,
    /// <summary>Patient left before being seen — removed from active queue view (US_017).</summary>
    Left
}

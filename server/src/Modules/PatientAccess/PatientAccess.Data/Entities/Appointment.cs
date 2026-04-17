using PatientAccess.Domain.Enums;

namespace PatientAccess.Data.Entities;

public class Appointment
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public DateTime SlotDatetime { get; set; }
    public AppointmentStatus Status { get; set; }
    public Guid? PreferredSlotId { get; set; }
    public decimal? NoShowRiskScore { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    // Navigation
    public Patient Patient { get; set; } = null!;
    public Appointment? PreferredSlot { get; set; }
}

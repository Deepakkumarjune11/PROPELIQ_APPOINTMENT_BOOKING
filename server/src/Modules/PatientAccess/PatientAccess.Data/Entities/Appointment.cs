using PatientAccess.Domain.Enums;

namespace PatientAccess.Data.Entities;

public class Appointment
{
    public Guid Id { get; set; }
    /// <summary>
    /// Null for pre-allocated Available slots that have not yet been claimed by a patient.
    /// Populated when the slot is booked (Status transitions from Available to Booked).
    /// </summary>
    public Guid? PatientId { get; set; }
    public DateTime SlotDatetime { get; set; }
    public AppointmentStatus Status { get; set; }
    public Guid? PreferredSlotId { get; set; }
    public decimal? NoShowRiskScore { get; set; }

    /// <summary>Clinician or practitioner name shown on the slot card (e.g. "Dr. Sarah Chen").</summary>
    public string? Provider { get; set; }
    /// <summary>"in-person" or "telehealth" — drives the visit-type icon on the slot card.</summary>
    public string? VisitType { get; set; }
    /// <summary>Free-form location string (clinic address or "Telehealth").</summary>
    public string? Location { get; set; }
    /// <summary>Appointment duration in minutes (e.g. 30).</summary>
    public int? DurationMinutes { get; set; }

    /// <summary>True when this appointment was booked as a same-day walk-in by staff (US_016).</summary>
    public bool IsWalkIn { get; set; }

    /// <summary>
    /// Position in the same-day walk-in queue (1-based). Null for scheduled appointments.
    /// Assigned atomically within a SERIALIZABLE transaction to prevent gaps/duplicates (US_016, AC-4).
    /// </summary>
    public int? QueuePosition { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    // Navigation
    public Patient? Patient { get; set; }
    public Appointment? PreferredSlot { get; set; }

    /// <summary>
    /// Encapsulates the preferred-slot mutation so that external code does not
    /// set <see cref="PreferredSlotId"/> directly (domain encapsulation, DR-002).
    /// Pass <c>null</c> to clear the watchlist entry after a swap or expiry.
    /// </summary>
    public void SetPreferredSlot(Guid? preferredSlotId)
    {
        PreferredSlotId = preferredSlotId;
    }

    /// <summary>
    /// Marks this appointment as a same-day walk-in and assigns its queue position (US_016, AC-4).
    /// Call this when a same-day slot is available.
    /// </summary>
    /// <param name="queuePosition">1-based queue position assigned atomically within a SERIALIZABLE transaction.</param>
    public void SetAsWalkIn(int queuePosition)
    {
        IsWalkIn      = true;
        QueuePosition = queuePosition;
    }

    /// <summary>
    /// Assigns a wait-queue position when no same-day slot is available (US_016, AC-5).
    /// <see cref="IsWalkIn"/> remains <c>true</c>; <see cref="QueuePosition"/> reflects wait order.
    /// </summary>
    /// <param name="position">1-based position in the wait queue.</param>
    public void SetWaitQueuePosition(int position)
    {
        QueuePosition = position;
    }

    /// <summary>
    /// Transitions the appointment to a new operational status (US_017, AC-3).
    /// Allowed at this endpoint: <c>Arrived</c>, <c>InRoom</c>, <c>Left</c>.
    /// </summary>
    public void UpdateStatus(AppointmentStatus newStatus)
    {
        Status    = newStatus;
        UpdatedAt = DateTime.UtcNow;
    }
}

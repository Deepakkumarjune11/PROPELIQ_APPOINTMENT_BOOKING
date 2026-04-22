using PatientAccess.Domain.Enums;

namespace PatientAccess.Data.Entities;

/// <summary>
/// Audit record for every outbound communication attempt (SMS/email).
/// Stores the generated PDF bytes for the email channel so the
/// GET /api/v1/appointments/{id}/pdf endpoint can serve them without regenerating (TR-014).
/// </summary>
public class CommunicationLog
{
    public Guid Id { get; set; }

    /// <summary>Patient the message was sent to.</summary>
    public Guid PatientId { get; set; }

    /// <summary>Appointment the communication is about.</summary>
    public Guid AppointmentId { get; set; }

    /// <summary>Delivery channel: SMS or Email.</summary>
    public CommunicationChannel Channel { get; set; }

    /// <summary>Delivery outcome: Sent or Failed.</summary>
    public CommunicationStatus Status { get; set; }

    /// <summary>Number of delivery attempts made (including retries).</summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Raw PDF bytes generated for email confirmations.
    /// Nullable — only set when <see cref="Channel"/> is <see cref="CommunicationChannel.Email"/>
    /// and delivery succeeded.  Stored as <c>bytea</c> in PostgreSQL.
    /// </summary>
    public byte[]? PdfBytes { get; set; }

    /// <summary>UTC timestamp when the log entry was created.</summary>
    public DateTime CreatedAt { get; set; }

    // Navigation
    public Patient Patient { get; set; } = null!;
    public Appointment Appointment { get; set; } = null!;
}

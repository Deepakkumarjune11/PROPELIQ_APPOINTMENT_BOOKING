using PatientAccess.Domain.Enums;

namespace PatientAccess.Application.Repositories;

/// <summary>
/// Input model for writing a communication log entry.
/// Defined in Application so job classes do not reference the Data layer directly.
/// </summary>
/// <param name="PatientId">Patient the message was sent to.</param>
/// <param name="AppointmentId">Appointment the communication relates to.</param>
/// <param name="Channel">Delivery channel: SMS or Email.</param>
/// <param name="Status">Delivery outcome: Sent or Failed.</param>
/// <param name="AttemptCount">Number of attempts made.</param>
/// <param name="PdfBytes">Optional PDF bytes (email channel only).</param>
public sealed record CommunicationLogEntry(
    Guid                 PatientId,
    Guid                 AppointmentId,
    CommunicationChannel Channel,
    CommunicationStatus  Status,
    int                  AttemptCount,
    byte[]?              PdfBytes = null);

/// <summary>
/// Repository contract for communication audit log operations.
/// </summary>
public interface ICommunicationLogRepository
{
    /// <summary>
    /// Persists a new <see cref="CommunicationLogEntry"/> row.
    /// </summary>
    Task AddAsync(CommunicationLogEntry entry, CancellationToken ct = default);

    /// <summary>
    /// Returns the raw PDF bytes from the most recent successful email confirmation for
    /// the given appointment, or <see langword="null"/> if the PDF job has not yet run.
    /// </summary>
    Task<byte[]?> GetConfirmationPdfBytesAsync(Guid appointmentId, CancellationToken ct = default);
}

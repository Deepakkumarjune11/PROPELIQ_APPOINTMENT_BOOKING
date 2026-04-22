namespace PatientAccess.Application.Exceptions;

/// <summary>
/// Thrown when an appointment slot is claimed by a concurrent booking between
/// the patient's selection and commit (AC-4, US_013).
/// Originated from <c>DbUpdateConcurrencyException</c> (xmin mismatch) in the Data layer.
/// Converted to <c>409 Conflict</c> by <c>AppointmentsController</c>.
/// </summary>
public sealed class SlotAlreadyBookedException : Exception
{
    public Guid SlotId { get; }

    public SlotAlreadyBookedException(Guid slotId)
        : base($"Slot {slotId} is no longer available.")
    {
        SlotId = slotId;
    }
}

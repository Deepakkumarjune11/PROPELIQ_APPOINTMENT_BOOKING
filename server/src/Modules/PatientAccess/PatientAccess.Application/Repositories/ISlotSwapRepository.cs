using PatientAccess.Application.Jobs;

namespace PatientAccess.Application.Repositories;

/// <summary>
/// Data record describing a watchlist entry — all fields needed by <c>SlotSwapService</c>
/// to evaluate swap eligibility without returning EF Core entity objects.
/// </summary>
/// <param name="AppointmentId">The patient's booked appointment to potentially swap.</param>
/// <param name="PatientId">Owner of the appointment — used for audit log and notification dispatch.</param>
/// <param name="CurrentSlotDatetime">UTC datetime of the currently booked slot.</param>
/// <param name="PreferredSlotId">FK of the preferred (watchlisted) slot.</param>
/// <param name="PreferredSlotDatetime">UTC datetime of the preferred slot.</param>
/// <param name="PatientPhone">E.164 patient phone for SMS notification.</param>
/// <param name="PatientEmail">Patient email for email notification.</param>
/// <param name="PatientName">Patient display name for notification body.</param>
public sealed record WatchlistEntry(
    Guid     AppointmentId,
    Guid     PatientId,
    DateTime CurrentSlotDatetime,
    Guid     PreferredSlotId,
    DateTime PreferredSlotDatetime,
    string   PatientPhone,
    string   PatientEmail,
    string   PatientName);

/// <summary>
/// Repository contract for atomic slot-swap operations (US_015, AC-3).
/// Implemented in PatientAccess.Data with SERIALIZABLE transactions and row-level locks.
/// </summary>
public interface ISlotSwapRepository
{
    /// <summary>
    /// Returns all active watchlist entries — appointments where <c>PreferredSlotId IS NOT NULL</c>.
    /// Includes patient contact details needed for post-swap notifications.
    /// </summary>
    Task<IReadOnlyList<WatchlistEntry>> GetActiveWatchlistEntriesAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Attempts to atomically swap the appointment to the preferred slot.
    /// Executes within a SERIALIZABLE transaction with a pessimistic row-lock on the appointment.
    /// </summary>
    /// <returns>
    ///   <see cref="SlotSwapResult.Swapped"/> — swap committed.<br/>
    ///   <see cref="SlotSwapResult.SlotStillTaken"/> — preferred slot still occupied; no change.<br/>
    ///   <see cref="SlotSwapResult.SlotExpired"/> — preferred datetime in the past; watchlist cleared.<br/>
    ///   <see cref="SlotSwapResult.NotOnWatchlist"/> — appointment no longer has a preferred slot (idempotent guard).
    /// </returns>
    Task<SlotSwapResult> TryAtomicSwapAsync(
        Guid appointmentId,
        Guid patientId,
        CancellationToken ct = default);
}

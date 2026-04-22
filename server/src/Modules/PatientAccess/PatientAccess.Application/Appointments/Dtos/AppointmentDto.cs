using PatientAccess.Domain.Enums;

namespace PatientAccess.Application.Appointments.Dtos;

/// <summary>
/// Response DTO for a patient's appointment record (US_015, AC-4).
/// Includes <see cref="PreferredSlotDatetime"/> for watchlist status display on SCR-008.
/// </summary>
/// <param name="Id">Appointment row identifier.</param>
/// <param name="SlotDatetime">ISO-8601 UTC datetime of the booked slot, e.g. "2026-04-20T09:00:00Z".</param>
/// <param name="ProviderName">Display name of the provider. Null when entity lacks provider data.</param>
/// <param name="ProviderId">Provider identifier for slot-availability lookups. Null when entity lacks provider data.</param>
/// <param name="VisitType">Visit type description (e.g., "In-person", "Telehealth"). Null when not stored.</param>
/// <param name="Status">Current appointment lifecycle status.</param>
/// <param name="PreferredSlotDatetime">
///   ISO-8601 UTC datetime of the patient's watchlist preferred slot,
///   or <see langword="null"/> when not enrolled on the watchlist.
/// </param>
public sealed record AppointmentDto(
    Guid              Id,
    string            SlotDatetime,
    string?           ProviderName,
    Guid?             ProviderId,
    string?           VisitType,
    AppointmentStatus Status,
    string?           PreferredSlotDatetime);

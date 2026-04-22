namespace PatientAccess.Application.Repositories;

/// <summary>
/// Lightweight data record returned by <see cref="IAvailabilityRepository"/>.
/// Keeps the Application layer independent of EF Core entity types.
/// </summary>
/// <param name="Id">Appointment row identifier.</param>
/// <param name="SlotDatetime">UTC datetime of the appointment slot.</param>
/// <param name="NoShowRiskScore">Model-derived no-show probability (0–1), or <c>null</c> if unscored.</param>
public sealed record AvailabilitySlotData(
    Guid Id,
    DateTime SlotDatetime,
    decimal? NoShowRiskScore);

/// <summary>
/// Repository contract for availability slot queries.
/// Defined in the Application layer so handlers depend on the abstraction, not EF Core.
/// Implemented in PatientAccess.Data (EF Core) — consumed via DI.
/// </summary>
public interface IAvailabilityRepository
{
    /// <summary>
    /// Returns all available slots whose <c>SlotDatetime</c> falls within
    /// [startDate, endDate] (inclusive, UTC day boundaries).
    /// </summary>
    Task<IReadOnlyList<AvailabilitySlotData>> GetAvailableSlotsAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken ct = default);
}

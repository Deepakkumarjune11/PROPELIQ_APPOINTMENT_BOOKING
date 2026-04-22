using Hangfire;
using Microsoft.Extensions.Logging;
using PatientAccess.Application.Repositories;

namespace PatientAccess.Application.Jobs;

/// <summary>
/// Application-layer service that orchestrates slot-swap attempts for a single watchlist entry.
/// Delegates the atomic database work to <see cref="ISlotSwapRepository.TryAtomicSwapAsync"/>;
/// enqueues SMS/email notifications outside the transaction so a notification failure never
/// rolls back a committed swap (US_015, AC-3).
/// </summary>
public sealed class SlotSwapService
{
    private readonly ISlotSwapRepository             _slotSwapRepo;
    private readonly IBackgroundJobClient            _jobClient;
    private readonly ILogger<SlotSwapService>        _logger;

    public SlotSwapService(
        ISlotSwapRepository          slotSwapRepo,
        IBackgroundJobClient         jobClient,
        ILogger<SlotSwapService>     logger)
    {
        _slotSwapRepo = slotSwapRepo;
        _jobClient    = jobClient;
        _logger       = logger;
    }

    /// <summary>
    /// Processes a single watchlist entry.  The method is intentionally synchronous at the
    /// orchestration level — each entry is resolved sequentially by the polling job so that
    /// competing Hangfire workers never race on the same entry (the DB lock is the final guard).
    /// </summary>
    public async Task ProcessEntryAsync(
        WatchlistEntry    entry,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "SlotSwapService: attempting swap for appointment {AppointmentId} to preferred slot {PreferredSlotId}.",
            entry.AppointmentId,
            entry.PreferredSlotId);

        SlotSwapResult result;

        try
        {
            result = await _slotSwapRepo.TryAtomicSwapAsync(entry.AppointmentId, entry.PatientId, ct);
        }
        catch (Exception ex)
        {
            // Log and surface — the caller (SlotSwapJob) will handle retry / alerting.
            _logger.LogError(
                ex,
                "SlotSwapService: unexpected error during atomic swap for appointment {AppointmentId}.",
                entry.AppointmentId);
            throw;
        }

        _logger.LogInformation(
            "SlotSwapService: appointment {AppointmentId} swap result = {Result}.",
            entry.AppointmentId,
            result);

        switch (result)
        {
            case SlotSwapResult.Swapped:
                // Enqueue notification AFTER commit so failure does not affect DB state.
                _jobClient.Enqueue<SwapNotificationJob>(j => j.Execute(
                    entry.PatientId,
                    entry.AppointmentId,
                    entry.PatientPhone,
                    entry.PatientEmail,
                    entry.PatientName,
                    entry.PreferredSlotDatetime));
                break;

            case SlotSwapResult.SlotExpired:
                _jobClient.Enqueue<WatchlistExpiredNotificationJob>(j => j.Execute(
                    entry.PatientId,
                    entry.AppointmentId,
                    entry.PatientPhone,
                    entry.PatientEmail,
                    entry.PatientName,
                    entry.PreferredSlotDatetime));
                break;

            case SlotSwapResult.SlotStillTaken:
            case SlotSwapResult.NotOnWatchlist:
                // No notification — watchlist preserved (StillTaken) or already cleared by another worker.
                break;
        }
    }
}

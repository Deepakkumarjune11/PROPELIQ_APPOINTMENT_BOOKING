using Hangfire;
using Microsoft.Extensions.Logging;
using PatientAccess.Application.Repositories;

namespace PatientAccess.Application.Jobs;

/// <summary>
/// Hangfire recurring job that polls the preferred-slot watchlist every 5 minutes and
/// attempts atomic slot swaps for every active watchlist entry (US_015, AC-3).
///
/// Concurrency guard: <see cref="DisableConcurrentExecutionAttribute"/> with a 240-second
/// timeout ensures only one instance runs at a time across all Hangfire workers.  The
/// SERIALIZABLE transaction + row-level FOR UPDATE lock inside <c>SlotSwapRepository</c>
/// is the final correctness guard for concurrent execution edge cases (AC-5).
/// </summary>
[DisableConcurrentExecution(timeoutInSeconds: 240)]
public sealed class SlotSwapJob
{
    private readonly ISlotSwapRepository      _slotSwapRepo;
    private readonly SlotSwapService          _swapService;
    private readonly ILogger<SlotSwapJob>     _logger;

    public SlotSwapJob(
        ISlotSwapRepository  slotSwapRepo,
        SlotSwapService      swapService,
        ILogger<SlotSwapJob> logger)
    {
        _slotSwapRepo = slotSwapRepo;
        _swapService  = swapService;
        _logger       = logger;
    }

    /// <summary>
    /// Entry point invoked by Hangfire's recurring job scheduler.
    /// Loads all watchlist entries and processes each sequentially so that the
    /// SERIALIZABLE transactions do not contend on rows within a single run.
    /// </summary>
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var entries = await _slotSwapRepo.GetActiveWatchlistEntriesAsync(ct);

        _logger.LogInformation(
            "SlotSwapJob: found {Count} active watchlist entries.",
            entries.Count);

        foreach (var entry in entries)
        {
            if (ct.IsCancellationRequested)
                break;

            try
            {
                await _swapService.ProcessEntryAsync(entry, ct);
            }
            catch (Exception ex)
            {
                // Log and continue — a failure for one appointment must not block others.
                _logger.LogError(
                    ex,
                    "SlotSwapJob: unhandled error for appointment {AppointmentId}; continuing.",
                    entry.AppointmentId);
            }
        }

        _logger.LogInformation("SlotSwapJob: run complete.");
    }
}

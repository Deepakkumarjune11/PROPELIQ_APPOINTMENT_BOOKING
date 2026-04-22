using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PropelIQ.Api.Infrastructure.Resilience;

/// <summary>
/// <see cref="SemaphoreSlim"/>-backed implementation of <see cref="IExternalServiceBulkhead"/>.
///
/// One semaphore per named service is created at construction time from
/// <see cref="ExternalServiceResilienceOptions"/>. Each <c>ExecuteAsync</c> call:
/// <list type="number">
///   <item>Attempts <c>WaitAsync(_timeout, ct)</c> — respects caller's cancellation token.</item>
///   <item>On timeout: logs warning and throws <see cref="BulkheadRejectedException"/>.</item>
///   <item>On success: executes the operation, always <c>Release()</c> in <c>finally</c>.</item>
///   <item>Unknown service names: logs warning, executes without throttling (fail-open, AC-4).</item>
/// </list>
/// </summary>
public sealed class ExternalServiceBulkhead(
    IOptions<ExternalServiceResilienceOptions> opts,
    ILogger<ExternalServiceBulkhead> logger) : IExternalServiceBulkhead, IDisposable
{
    private readonly Dictionary<string, SemaphoreSlim> _semaphores = BuildSemaphores(opts.Value);
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(opts.Value.BulkheadTimeoutSeconds);

    private static Dictionary<string, SemaphoreSlim> BuildSemaphores(
        ExternalServiceResilienceOptions o) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["azure-openai"] = new SemaphoreSlim(o.AzureOpenAiMaxConcurrent, o.AzureOpenAiMaxConcurrent),
            ["email"]        = new SemaphoreSlim(o.EmailMaxConcurrent,        o.EmailMaxConcurrent),
            ["sms"]          = new SemaphoreSlim(o.SmsMaxConcurrent,          o.SmsMaxConcurrent),
            ["pagerduty"]    = new SemaphoreSlim(o.PagerDutyMaxConcurrent,    o.PagerDutyMaxConcurrent),
        };

    /// <inheritdoc/>
    public async Task<T> ExecuteAsync<T>(
        string serviceName,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken ct = default)
    {
        if (!_semaphores.TryGetValue(serviceName, out var semaphore))
        {
            // Unknown service — fail-open rather than blocking unexpected integrations (AC-4)
            logger.LogWarning(
                "Bulkhead: unknown service '{Service}' — bypassing throttle. " +
                "Add it to ExternalServiceResilienceOptions to enable concurrency control.",
                serviceName);
            return await operation(ct).ConfigureAwait(false);
        }

        var acquired = await semaphore.WaitAsync(_timeout, ct).ConfigureAwait(false);
        if (!acquired)
        {
            logger.LogWarning(
                "Bulkhead: concurrency limit reached for '{Service}' " +
                "(timeout {TimeoutSeconds}s, available={Available})",
                serviceName, _timeout.TotalSeconds, semaphore.CurrentCount);
            throw new BulkheadRejectedException(serviceName);
        }

        try
        {
            return await operation(ct).ConfigureAwait(false);
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync(
        string serviceName,
        Func<CancellationToken, Task> operation,
        CancellationToken ct = default)
    {
        await ExecuteAsync<bool>(serviceName, async innerCt =>
        {
            await operation(innerCt).ConfigureAwait(false);
            return true;
        }, ct).ConfigureAwait(false);
    }

    /// <summary>Disposes all <see cref="SemaphoreSlim"/> instances. Called by the DI container at app shutdown.</summary>
    public void Dispose()
    {
        foreach (var s in _semaphores.Values)
            s.Dispose();
    }
}

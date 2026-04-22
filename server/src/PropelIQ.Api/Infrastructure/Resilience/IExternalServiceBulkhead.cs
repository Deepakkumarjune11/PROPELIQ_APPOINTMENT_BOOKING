namespace PropelIQ.Api.Infrastructure.Resilience;

/// <summary>
/// Provides bulkhead isolation for named external dependencies using a
/// <see cref="System.Threading.SemaphoreSlim"/>-backed concurrency gate.
///
/// Named slots: <c>azure-openai</c>, <c>email</c>, <c>sms</c>, <c>pagerduty</c>.
/// Unknown service names fail-open (logged + bypassed) per AC-4 guidance.
///
/// Register as singleton: <c>builder.Services.AddSingleton&lt;IExternalServiceBulkhead, ExternalServiceBulkhead&gt;()</c>
/// The DI container disposes the singleton on application shutdown, releasing all semaphore handles.
/// </summary>
public interface IExternalServiceBulkhead
{
    /// <summary>
    /// Executes <paramref name="operation"/> with bulkhead protection for the named service.
    /// </summary>
    /// <exception cref="BulkheadRejectedException">
    /// Thrown when the semaphore timeout (<see cref="ExternalServiceResilienceOptions.BulkheadTimeoutSeconds"/>) elapses.
    /// </exception>
    Task<T> ExecuteAsync<T>(
        string serviceName,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken ct = default);

    /// <inheritdoc cref="ExecuteAsync{T}"/>
    Task ExecuteAsync(
        string serviceName,
        Func<CancellationToken, Task> operation,
        CancellationToken ct = default);
}

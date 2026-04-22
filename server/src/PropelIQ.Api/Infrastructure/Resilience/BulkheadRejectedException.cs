namespace PropelIQ.Api.Infrastructure.Resilience;

/// <summary>
/// Thrown when a bulkhead semaphore timeout elapses, indicating the named external service
/// has exceeded its configured maximum concurrent call limit (US_035, AC-4).
/// Callers should catch this and apply a graceful fallback.
/// </summary>
public sealed class BulkheadRejectedException(string serviceName)
    : Exception($"Bulkhead rejected call to '{serviceName}': concurrency limit reached")
{
    /// <summary>Name of the external service whose concurrency limit was exceeded.</summary>
    public string ServiceName { get; } = serviceName;
}

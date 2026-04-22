using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ClinicalIntelligence.Application.AI.Latency;

/// <summary>
/// Redis-backed sliding-window latency recorder for AI SLA monitoring (US_032, AIR-Q02).
///
/// Storage pattern per featureContext:
///   Redis key  : <c>ai:latency:{featureContext}</c>
///   Redis type : List (append-right)
///   Bound      : LTRIM to <see cref="AiSlaOptions.SampleWindowSize"/> entries after each RPUSH
///
/// RPUSH + LTRIM are issued via a Redis batch (single round-trip) to bound per-request overhead.
/// P95 computation: LRANGE(0,-1) → sort ascending → index at ⌈0.95 × count⌉ − 1.
///
/// Thread-safety: <see cref="IConnectionMultiplexer"/> is thread-safe; safe for singleton lifetime.
/// </summary>
public sealed class RedisLatencyRecorder(
    IConnectionMultiplexer redis,
    IOptions<AiSlaOptions> options) : ILatencyRecorder
{
    private string ListKey(string featureContext) => $"ai:latency:{featureContext}";

    /// <inheritdoc />
    public async Task RecordAsync(string featureContext, long latencyMs, CancellationToken ct = default)
    {
        var db  = redis.GetDatabase();
        var key = ListKey(featureContext);

        // Batch RPUSH + LTRIM into a single Redis pipeline round-trip
        var batch    = db.CreateBatch();
        var pushTask = batch.ListRightPushAsync(key, latencyMs.ToString());
        var trimTask = batch.ListTrimAsync(key, 0, options.Value.SampleWindowSize - 1);
        batch.Execute();

        await Task.WhenAll(pushTask, trimTask).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<double> GetP95Async(string featureContext, CancellationToken ct = default)
    {
        var db     = redis.GetDatabase();
        var values = await db.ListRangeAsync(ListKey(featureContext)).ConfigureAwait(false);

        if (values.Length == 0) return 0;

        var samples = values
            .Where(v => v.HasValue)
            .Select(v => (string)v!)
            .Where(s => long.TryParse(s, out _))
            .Select(long.Parse)
            .OrderBy(x => x)
            .ToArray();

        if (samples.Length == 0) return 0;

        // p95 index: ⌈0.95 × count⌉ − 1, clamped to valid range
        var p95Index = (int)Math.Ceiling(0.95 * samples.Length) - 1;
        return samples[Math.Max(0, Math.Min(p95Index, samples.Length - 1))];
    }
}

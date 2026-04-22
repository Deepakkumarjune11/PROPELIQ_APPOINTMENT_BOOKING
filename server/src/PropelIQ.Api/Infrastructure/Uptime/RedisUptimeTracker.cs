using System.Text.Json;
using StackExchange.Redis;

namespace PropelIQ.Api.Infrastructure.Uptime;

/// <summary>
/// Redis-backed <see cref="IUptimeTracker"/> implementation.
///
/// Key patterns:
/// <list type="bullet">
///   <item><c>uptime:active_start:{service}</c> — ISO 8601 UTC timestamp of the currently-open downtime incident; absent when no incident is active</item>
///   <item><c>uptime:downtime:{service}:{year}-{month:D2}</c> — Redis list of JSON intervals: <c>{ "start": "...", "end": "..." }</c>; retained ~13 months (400-day TTL)</item>
/// </list>
/// </summary>
public sealed class RedisUptimeTracker(IConnectionMultiplexer redis) : IUptimeTracker
{
    private static string StartKey(string svc)
        => $"uptime:active_start:{svc}";

    private static string IntervalKey(string svc, int year, int month)
        => $"uptime:downtime:{svc}:{year}-{month:D2}";

    /// <inheritdoc/>
    public async Task RecordDowntimeStartAsync(string service, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        // When.NotExists prevents overwriting an already-open incident start time
        await db.StringSetAsync(
            StartKey(service),
            DateTime.UtcNow.ToString("O"),
            when: When.NotExists)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task RecordDowntimeEndAsync(string service, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var startVal = await db.StringGetAsync(StartKey(service)).ConfigureAwait(false);
        if (!startVal.HasValue)
            return; // No open incident to close

        var now      = DateTime.UtcNow;
        var interval = JsonSerializer.Serialize(new
        {
            start = startVal.ToString(),
            end   = now.ToString("O"),
        });

        var key = IntervalKey(service, now.Year, now.Month);
        await db.ListRightPushAsync(key, interval).ConfigureAwait(false);
        await db.KeyExpireAsync(key, TimeSpan.FromDays(400)).ConfigureAwait(false); // ~13-month retention
        await db.KeyDeleteAsync(StartKey(service)).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<UptimeReport> GetMonthlyUptimeAsync(
        string service, int year, int month, CancellationToken ct = default)
    {
        var db      = redis.GetDatabase();
        var key     = IntervalKey(service, year, month);
        var entries = await db.ListRangeAsync(key, 0, -1).ConfigureAwait(false);

        var totalDowntimeSeconds = 0.0;
        foreach (var entry in entries)
        {
            using var doc  = JsonDocument.Parse(entry.ToString());
            var root       = doc.RootElement;
            if (DateTime.TryParse(
                    root.GetProperty("start").GetString(),
                    null,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out var start) &&
                DateTime.TryParse(
                    root.GetProperty("end").GetString(),
                    null,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out var end))
            {
                totalDowntimeSeconds += (end - start).TotalSeconds;
            }
        }

        var daysInMonth       = DateTime.DaysInMonth(year, month);
        var totalMonthSeconds = daysInMonth * 24.0 * 3600.0;
        var uptimePercent     = Math.Max(
            0.0,
            (totalMonthSeconds - totalDowntimeSeconds) / totalMonthSeconds * 100.0);

        return new UptimeReport(
            Service:              service,
            Year:                 year,
            Month:                month,
            UptimePercent:        Math.Round(uptimePercent, 4),
            TotalDowntimeMinutes: Math.Round(totalDowntimeSeconds / 60.0, 2),
            IncidentCount:        entries.Length);
    }
}

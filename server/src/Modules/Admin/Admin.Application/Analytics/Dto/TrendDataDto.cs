namespace Admin.Application.Analytics.Dto;

/// <summary>One day's appointment count (US_033, AC-3).</summary>
public sealed record DailyVolumeEntry(string Date, int Count);

/// <summary>One week's no-show rate and AI p95 latency (US_033, AC-3).</summary>
public sealed record WeeklyTrendEntry(string Week, double NoShowRate, double AiLatencyP95Ms);

/// <summary>Document processing count by status (US_033, AC-3).</summary>
public sealed record DocumentThroughputEntry(string Status, int Count);

/// <summary>Time-series trend data for the analytics dashboard (US_033, AC-3, FR-018).</summary>
public sealed record TrendDataDto(
    IReadOnlyList<DailyVolumeEntry> DailyVolumes,
    IReadOnlyList<WeeklyTrendEntry> WeeklyTrends,
    IReadOnlyList<DocumentThroughputEntry> DocumentThroughput);

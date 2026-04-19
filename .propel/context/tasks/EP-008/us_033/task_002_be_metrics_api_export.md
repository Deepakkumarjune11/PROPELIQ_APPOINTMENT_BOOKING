# Task - task_002_be_metrics_api_export

## Requirement Reference

- **User Story**: US_033 — Operational Metrics Dashboard & Reporting
- **Story Location**: `.propel/context/tasks/EP-008/us_033/us_033.md`
- **Acceptance Criteria**:
  - AC-1: KPI endpoint returns today's appointment count, no-show rate, average wait time, and AI suggestion acceptance rate per FR-018.
  - AC-2: All endpoints accept `startDate`/`endDate` query parameters; responses return within 2 seconds; backed by pre-aggregated materialized views (task_003).
  - AC-3: Trend endpoints return daily appointment volumes, weekly no-show rates, document processing throughput, and AI response latency p95 per FR-018.
  - AC-4: System health endpoint returns API response time p50/p95/p99, database connection pool usage, cache hit ratios, and AI gateway status per TR-017.
  - AC-5: Export endpoint generates PDF (PDFSharp per TR-014, OSS) or CSV of the currently displayed metrics and date range per FR-018.
- **Edge Cases**:
  - Delayed metrics: Include `dataFreshnessSec` and `lastRefreshedAt` in every response — computed from the `mv_last_refresh` metadata table updated by the Hangfire refresh job (task_003).
  - High-load: All trend/KPI queries run against materialized views, not live OLTP tables. Export runs against the same materialized views with a 30-second server-side timeout guard.

---

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | No |
| **Figma URL** | N/A |
| **Wireframe Status** | N/A |

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Backend | .NET 8 ASP.NET Core | 8.0 LTS |
| ORM | EF Core | 8.0 |
| Database | PostgreSQL 15 | 15.x |
| PDF Generation | PdfSharpCore (OSS) | 1.3.x (satisfies TR-014 + NFR-015) |
| Redis | StackExchange.Redis (IConnectionMultiplexer) | 2.8.x |
| Auth | JWT Bearer | 8.0.x |
| Logging | Serilog | 3.x |

> **PDF library choice**: `PdfSharpCore` (OSS MIT fork of PDFsharp for .NET Core) satisfies TR-014 ("PDFSharp or similar OSS library") and NFR-015 (no paid dependency). CSV generation uses built-in `StringBuilder` — no additional package needed.

---

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |

---

## Task Overview

Implement the `AnalyticsController` under `PropelIQ.Api` with four endpoint groups:

1. **`GET /api/v1/analytics/kpi`** — Returns today's KPI snapshot (from materialized view `mv_daily_kpi` or direct query for today). Parameters: `startDate`, `endDate`. Response: `{ appointmentCount, noShowRate, avgWaitTimeMin, aiAcceptanceRate, dataFreshnessSec, lastRefreshedAt }`.

2. **`GET /api/v1/analytics/trends`** — Returns time-series data for charts. Parameters: `startDate`, `endDate`. Response: `{ dailyVolumes[], weeklyTrends[], documentThroughput[] }`. Reads from `mv_daily_appointment_volumes` and `mv_weekly_noshow_rates` materialized views (task_003).

3. **`GET /api/v1/analytics/system-health`** — Returns live infrastructure metrics sampled at request time (NOT from materialized views — live reads). Sources: Redis latency lists via `ILatencyRecorder.GetP95Async` (US_032), Npgsql pool stats via `NpgsqlDataSource`, Redis `INFO` command for cache hit ratio, `IAiAvailabilityState.IsAvailable`.

4. **`GET /api/v1/analytics/export`** — Streams PDF or CSV. Parameters: `format` (pdf|csv), `startDate`, `endDate`. No PHI in export — operational metrics only (appointment counts, rates, latencies — no patient identifiers).

All endpoints require `[Authorize(Roles = "staff,admin")]`. No PHI returned in any response (OWASP A01 + HIPAA).

---

## Dependent Tasks

- **task_003_db_metrics_materialized_views.md** (US_033) — `mv_daily_appointment_volumes`, `mv_weekly_noshow_rates`, `mv_daily_kpi`, `mv_document_processing_throughput` must exist.
- **task_001_be_latency_sla_schema_validation.md** (US_032) — `ILatencyRecorder.GetP95Async` used in system health endpoint; Redis latency keys `ai:latency:{featureContext}`.
- **task_002_be_ai_health_check_degradation.md** (US_030) — `IAiAvailabilityState.IsAvailable` used in system health endpoint.

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/PropelIQ.Api/Controllers/AnalyticsController.cs` | 4 endpoints: /kpi, /trends, /system-health, /export; `[Authorize(Roles = "staff,admin")]` |
| CREATE | `server/src/Modules/Admin/Admin.Application/Analytics/IMetricsQueryService.cs` | `GetKpiAsync(range, ct)`, `GetTrendsAsync(range, ct)`, `GetSystemHealthAsync(ct)` |
| CREATE | `server/src/Modules/Admin/Admin.Application/Analytics/PostgresMetricsQueryService.cs` | EF Core raw SQL queries against materialized views; system health via live infrastructure reads |
| CREATE | `server/src/Modules/Admin/Admin.Application/Analytics/MetricsExportService.cs` | `GeneratePdfAsync(data, range, ct)` + `GenerateCsvAsync(data, range, ct)` — streams to `MemoryStream` |
| CREATE | `server/src/Modules/Admin/Admin.Application/Analytics/Dto/` | `KpiMetricsDto`, `TrendDataDto`, `SystemHealthDto`, `DailyVolumeEntry`, `WeeklyTrendEntry`, `DocumentThroughputEntry` |
| MODIFY | `server/src/Modules/Admin/Admin.Presentation/ServiceCollectionExtensions.cs` | Register `IMetricsQueryService`, `MetricsExportService` |
| MODIFY | `server/src/PropelIQ.Api/appsettings.json` | Add `"Analytics": { "ExportTimeoutSeconds": 30 }` |

---

## Implementation Plan

### 1. DTOs

```csharp
// Admin.Application/Analytics/Dto/KpiMetricsDto.cs
public sealed record KpiMetricsDto(
    int AppointmentCount,
    double NoShowRate,           // 0.0–1.0
    double AvgWaitTimeMin,
    double AiAcceptanceRate,     // 0.0–1.0
    int DataFreshnessSec,        // seconds since last materialized view refresh
    DateTimeOffset LastRefreshedAt
);

// DailyVolumeEntry.cs
public sealed record DailyVolumeEntry(string Date, int Count);

// WeeklyTrendEntry.cs
public sealed record WeeklyTrendEntry(string Week, double NoShowRate, double AiLatencyP95Ms);

// DocumentThroughputEntry.cs
public sealed record DocumentThroughputEntry(string Status, int Count);

// TrendDataDto.cs
public sealed record TrendDataDto(
    IReadOnlyList<DailyVolumeEntry> DailyVolumes,
    IReadOnlyList<WeeklyTrendEntry> WeeklyTrends,
    IReadOnlyList<DocumentThroughputEntry> DocumentThroughput
);

// SystemHealthDto.cs
public sealed record SystemHealthDto(
    double ApiLatencyP50Ms,
    double ApiLatencyP95Ms,
    double ApiLatencyP99Ms,
    int DbPoolUsagePct,          // 0–100
    int CacheHitRatioPct,        // 0–100
    string AiGatewayStatus       // "Available" | "Degraded" | "Unavailable"
);
```

### 2. `IMetricsQueryService`

```csharp
public interface IMetricsQueryService
{
    Task<KpiMetricsDto> GetKpiAsync(DateOnly startDate, DateOnly endDate, CancellationToken ct);
    Task<TrendDataDto> GetTrendsAsync(DateOnly startDate, DateOnly endDate, CancellationToken ct);
    Task<SystemHealthDto> GetSystemHealthAsync(CancellationToken ct);
}
```

### 3. `PostgresMetricsQueryService` — key implementation patterns

```csharp
// KPI — reads mv_daily_kpi materialized view
public async Task<KpiMetricsDto> GetKpiAsync(DateOnly start, DateOnly end, CancellationToken ct)
{
    // Raw SQL: SELECT SUM(appointment_count), AVG(noshow_rate), AVG(avg_wait_time_min),
    //          AVG(ai_acceptance_rate) FROM mv_daily_kpi
    //          WHERE metric_date BETWEEN @start AND @end
    var rows = await _db.Database.SqlQueryRaw<KpiRow>(
        """
        SELECT
            SUM(appointment_count)    AS appointment_count,
            AVG(noshow_rate)          AS noshow_rate,
            AVG(avg_wait_time_min)    AS avg_wait_time_min,
            AVG(ai_acceptance_rate)   AS ai_acceptance_rate
        FROM mv_daily_kpi
        WHERE metric_date BETWEEN {0} AND {1}
        """, start, end).FirstOrDefaultAsync(ct);

    var freshness = await _db.Set<MvLastRefresh>()
        .Where(r => r.ViewName == "mv_daily_kpi")
        .Select(r => new { r.LastRefreshedAt })
        .FirstOrDefaultAsync(ct);

    var staleSec = freshness is null ? 999
        : (int)(DateTimeOffset.UtcNow - freshness.LastRefreshedAt).TotalSeconds;

    return new KpiMetricsDto(
        rows?.AppointmentCount ?? 0, rows?.NoshowRate ?? 0,
        rows?.AvgWaitTimeMin ?? 0, rows?.AiAcceptanceRate ?? 0,
        staleSec, freshness?.LastRefreshedAt ?? DateTimeOffset.MinValue);
}

// System Health — LIVE reads (not materialized view)
public async Task<SystemHealthDto> GetSystemHealthAsync(CancellationToken ct)
{
    // API latency from Redis latency lists (ILatencyRecorder — US_032/task_001)
    // Use "ConversationalIntake" as representative featureContext for overall API latency
    var latencies = await GetApiLatencyPercentilesAsync(ct);

    // DB pool — Npgsql exposes pool stats via NpgsqlDataSource but no public API in v8;
    // use a diagnostic counter approximation: total connections / max connections
    var dbPoolPct = await EstimateDbPoolUsageAsync(ct);

    // Cache hit ratio — Redis INFO stats command
    var cacheHitPct = await GetCacheHitRatioAsync(ct);

    // AI gateway status from IAiAvailabilityState
    var aiStatus = _aiAvailabilityState.IsAvailable ? "Available" : "Degraded";

    return new SystemHealthDto(
        latencies.P50, latencies.P95, latencies.P99,
        dbPoolPct, cacheHitPct, aiStatus);
}
```

> **Redis `INFO` for cache hit ratio**: `db.Execute("INFO", "stats")` returns a Redis INFO string. Parse `keyspace_hits` and `keyspace_misses` from the output. `hitRatio = hits / (hits + misses)`. This is a live snapshot — not cached — so each system health request triggers one Redis command.

### 4. `MetricsExportService` — PDF + CSV generation

```csharp
// CSV — no external library; simple StringBuilder
public Task<byte[]> GenerateCsvAsync(
    KpiMetricsDto kpi, TrendDataDto trends, DateOnly start, DateOnly end, CancellationToken ct)
{
    var sb = new StringBuilder();
    sb.AppendLine($"PropelIQ Operational Metrics Report — {start:yyyy-MM-dd} to {end:yyyy-MM-dd}");
    sb.AppendLine();
    sb.AppendLine("KPI SUMMARY");
    sb.AppendLine("Metric,Value");
    sb.AppendLine($"Appointment Count,{kpi.AppointmentCount}");
    sb.AppendLine($"No-show Rate,{kpi.NoShowRate:P1}");
    sb.AppendLine($"Avg Wait Time (min),{kpi.AvgWaitTimeMin:F1}");
    sb.AppendLine($"AI Acceptance Rate,{kpi.AiAcceptanceRate:P1}");
    sb.AppendLine();
    sb.AppendLine("DAILY VOLUMES");
    sb.AppendLine("Date,Count");
    foreach (var row in trends.DailyVolumes)
        sb.AppendLine($"{row.Date},{row.Count}");
    return Task.FromResult(Encoding.UTF8.GetBytes(sb.ToString()));
}

// PDF — PdfSharpCore (OSS, TR-014)
public async Task<byte[]> GeneratePdfAsync(
    KpiMetricsDto kpi, TrendDataDto trends, DateOnly start, DateOnly end, CancellationToken ct)
{
    using var doc = new PdfDocument();
    doc.Info.Title = $"PropelIQ Metrics {start:yyyy-MM-dd} to {end:yyyy-MM-dd}";
    var page = doc.AddPage();
    var gfx = XGraphics.FromPdfPage(page);
    var titleFont = new XFont("Arial", 16, XFontStyleEx.Bold);
    var bodyFont = new XFont("Arial", 10, XFontStyleEx.Regular);
    var y = 40.0;

    gfx.DrawString($"PropelIQ Operational Metrics Report", titleFont, XBrushes.Black,
        new XRect(40, y, page.Width - 80, 30), XStringFormats.TopLeft);
    y += 30;
    gfx.DrawString($"Period: {start:yyyy-MM-dd} to {end:yyyy-MM-dd}", bodyFont, XBrushes.Black,
        new XRect(40, y, page.Width - 80, 20), XStringFormats.TopLeft);
    y += 30;

    // KPI section
    gfx.DrawString("KEY PERFORMANCE INDICATORS", new XFont("Arial", 12, XFontStyleEx.Bold),
        XBrushes.Black, new XRect(40, y, page.Width - 80, 20), XStringFormats.TopLeft);
    y += 20;
    foreach (var (label, val) in new[] {
        ("Appointment Count", kpi.AppointmentCount.ToString()),
        ("No-show Rate", kpi.NoShowRate.ToString("P1")),
        ("Avg Wait Time (min)", kpi.AvgWaitTimeMin.ToString("F1")),
        ("AI Acceptance Rate", kpi.AiAcceptanceRate.ToString("P1"))
    })
    {
        gfx.DrawString($"{label}: {val}", bodyFont, XBrushes.Black,
            new XRect(40, y, page.Width - 80, 16), XStringFormats.TopLeft);
        y += 16;
    }

    using var ms = new MemoryStream();
    doc.Save(ms);
    return ms.ToArray();
}
```

### 5. `AnalyticsController` — endpoints

```csharp
[ApiController]
[Route("api/v1/analytics")]
[Authorize(Roles = "staff,admin")]
public sealed class AnalyticsController(
    IMetricsQueryService metricsService,
    MetricsExportService exportService,
    ILogger<AnalyticsController> logger) : ControllerBase
{
    [HttpGet("kpi")]
    public async Task<IActionResult> GetKpi(
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        CancellationToken ct)
    {
        var start = startDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var end = endDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var result = await metricsService.GetKpiAsync(start, end, ct);
        return Ok(result);
    }

    [HttpGet("trends")]
    public async Task<IActionResult> GetTrends(
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        CancellationToken ct)
    {
        var start = startDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var end = endDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var result = await metricsService.GetTrendsAsync(start, end, ct);
        return Ok(result);
    }

    [HttpGet("system-health")]
    public async Task<IActionResult> GetSystemHealth(CancellationToken ct)
    {
        var result = await metricsService.GetSystemHealthAsync(ct);
        return Ok(result);
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] string format,
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        CancellationToken ct)
    {
        // Validate format param (OWASP A03 — no path traversal via format value)
        if (format is not ("pdf" or "csv"))
            return BadRequest(new { error = "format must be 'pdf' or 'csv'" });

        var start = startDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var end = endDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var kpi = await metricsService.GetKpiAsync(start, end, ct);
        var trends = await metricsService.GetTrendsAsync(start, end, ct);

        if (format == "csv")
        {
            var csv = await exportService.GenerateCsvAsync(kpi, trends, start, end, ct);
            return File(csv, "text/csv",
                $"propeliq-metrics-{start:yyyyMMdd}-{end:yyyyMMdd}.csv");
        }

        var pdf = await exportService.GeneratePdfAsync(kpi, trends, start, end, ct);
        return File(pdf, "application/pdf",
            $"propeliq-metrics-{start:yyyyMMdd}-{end:yyyyMMdd}.pdf");
    }
}
```

> **AC-5 security note**: Export contains NO PHI — only aggregate counts, rates, and latencies. `format` parameter is validated against an explicit allow-list (not used in file path — no directory traversal risk). File name uses ISO dates only.

---

## Current Project State

```
server/src/
├── PropelIQ.Api/
│   ├── Controllers/
│   │   └── AnalyticsController.cs          ← CREATE
│   └── appsettings.json                    ← MODIFY: add "Analytics" section
└── Modules/
    └── Admin/
        ├── Admin.Application/
        │   └── Analytics/
        │       ├── IMetricsQueryService.cs  ← CREATE
        │       ├── PostgresMetricsQueryService.cs ← CREATE
        │       ├── MetricsExportService.cs  ← CREATE
        │       └── Dto/                     ← CREATE (6 DTO files)
        └── Admin.Presentation/
            └── ServiceCollectionExtensions.cs ← MODIFY: register services
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/PropelIQ.Api/Controllers/AnalyticsController.cs` | 4 endpoints: `/kpi`, `/trends`, `/system-health`, `/export`; `[Authorize(Roles = "staff,admin")]`; format validation for export |
| CREATE | `server/src/Modules/Admin/Admin.Application/Analytics/IMetricsQueryService.cs` | `GetKpiAsync`, `GetTrendsAsync`, `GetSystemHealthAsync` |
| CREATE | `server/src/Modules/Admin/Admin.Application/Analytics/PostgresMetricsQueryService.cs` | Raw SQL against materialized views; Redis latency percntile; Redis INFO cache stats; `IAiAvailabilityState` for gateway status |
| CREATE | `server/src/Modules/Admin/Admin.Application/Analytics/MetricsExportService.cs` | CSV via `StringBuilder`; PDF via `PdfSharpCore`; streams to `byte[]` |
| CREATE | `server/src/Modules/Admin/Admin.Application/Analytics/Dto/KpiMetricsDto.cs` | KPI DTO with freshness fields |
| CREATE | `server/src/Modules/Admin/Admin.Application/Analytics/Dto/TrendDataDto.cs` | Trend data with 3 sub-lists |
| CREATE | `server/src/Modules/Admin/Admin.Application/Analytics/Dto/SystemHealthDto.cs` | p50/p95/p99, pool%, cache%, gateway status |
| MODIFY | `server/src/Modules/Admin/Admin.Presentation/ServiceCollectionExtensions.cs` | `AddScoped<IMetricsQueryService, PostgresMetricsQueryService>()` + `AddScoped<MetricsExportService>()` |
| MODIFY | `server/src/PropelIQ.Api/appsettings.json` | Add `"Analytics": { "ExportTimeoutSeconds": 30 }` |

---

## External References

- [PdfSharpCore NuGet — OSS PDF generation for .NET Core](https://www.nuget.org/packages/PdfSharpCore)
- [EF Core Raw SQL queries — SqlQueryRaw](https://learn.microsoft.com/en-us/ef/core/querying/sql-queries)
- [StackExchange.Redis — Execute INFO command](https://stackexchange.github.io/StackExchange.Redis/Basics.html)
- [design.md — TR-014 PDFSharp; TR-017 APM; NFR-018 operational metrics](../.propel/context/docs/design.md#L184-L187)

---

## Build Commands

```powershell
# Add PdfSharpCore package to API project
dotnet add server/src/PropelIQ.Api/PropelIQ.Api.csproj package PdfSharpCore --version 1.3.62

# Build
dotnet build server/PropelIQ.slnx --no-restore

# Test
dotnet test server/PropelIQ.slnx --no-build --filter "Category=US033_Analytics"
```

---

## Implementation Validation Strategy

- [ ] `GET /api/v1/analytics/kpi` without auth → 401; with staff JWT → 200 with correct shape
- [ ] `GET /api/v1/analytics/kpi?startDate=2026-01-01&endDate=2026-04-18` → returns `KpiMetricsDto` with `dataFreshnessSec` + `lastRefreshedAt`
- [ ] `GET /api/v1/analytics/trends` → returns `TrendDataDto` with all 3 collections non-null
- [ ] `GET /api/v1/analytics/system-health` → returns `SystemHealthDto` with `apiLatencyP95Ms > 0`, `aiGatewayStatus ∈ {Available, Degraded, Unavailable}`
- [ ] `GET /api/v1/analytics/export?format=csv&startDate=...` → Content-Type: text/csv; body contains CSV header row + KPI data
- [ ] `GET /api/v1/analytics/export?format=pdf` → Content-Type: application/pdf; body is valid PDF bytes
- [ ] `GET /api/v1/analytics/export?format=json` → 400 with `{"error": "format must be 'pdf' or 'csv'"}`
- [ ] No PHI in any response: no patient names, IDs, DOBs, or medical data in any endpoint output

---

## Implementation Checklist

- [ ] CREATE `KpiMetricsDto`, `TrendDataDto`, `SystemHealthDto`, `DailyVolumeEntry`, `WeeklyTrendEntry`, `DocumentThroughputEntry` records in `Dto/` folder
- [ ] CREATE `IMetricsQueryService` interface with 3 methods (date-range typed as `DateOnly`)
- [ ] CREATE `PostgresMetricsQueryService` — (a) KPI: raw SQL against `mv_daily_kpi`; (b) trends: raw SQL against `mv_daily_appointment_volumes` + `mv_weekly_noshow_rates` + `mv_document_processing_throughput`; (c) system health: Redis latency percntile reads + Redis INFO parse + `IAiAvailabilityState.IsAvailable`
- [ ] CREATE `MetricsExportService` — (a) CSV: `StringBuilder` with header + KPI + daily volumes rows; (b) PDF: `PdfSharpCore` with title, period, KPI table; both return `byte[]`
- [ ] CREATE `AnalyticsController` — 4 endpoints; `[Authorize(Roles = "staff,admin")]`; `format` validation against allow-list; default date range = last 30 days
- [ ] MODIFY `Admin.Presentation/ServiceCollectionExtensions.cs` — register `IMetricsQueryService` (scoped) + `MetricsExportService` (scoped); add PdfSharpCore package reference
- [ ] MODIFY `appsettings.json` — add `"Analytics": { "ExportTimeoutSeconds": 30 }`
- [ ] Ensure NO PHI fields included in any DTO or export output

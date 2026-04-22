using System.Text;
using Admin.Application.Analytics.Dto;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;

namespace Admin.Application.Analytics;

/// <summary>
/// Generates PDF and CSV exports of operational metrics (US_033, AC-5, FR-018, TR-014).
///
/// No PHI included in any export — operational counts, rates, and latencies only
/// (OWASP A01 + HIPAA safeguard).
///
/// PDF: PdfSharpCore (OSS MIT, satisfies TR-014 + NFR-015) — no paid dependency.
/// CSV: Built-in <see cref="StringBuilder"/> — no additional package.
/// </summary>
public sealed class MetricsExportService
{
    private const double PageMargin    = 40.0;
    private const double LineHeight    = 18.0;
    private const double SectionGap   = 10.0;

    // ── CSV ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a UTF-8 CSV byte array containing the KPI summary and daily volumes.
    /// </summary>
    public Task<byte[]> GenerateCsvAsync(
        KpiMetricsDto kpi,
        TrendDataDto  trends,
        DateOnly      startDate,
        DateOnly      endDate,
        CancellationToken ct = default)
    {
        var sb = new StringBuilder();

        // Report header
        sb.AppendLine($"PropelIQ Operational Metrics Report — {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
        sb.AppendLine($"Generated,{DateTimeOffset.UtcNow:yyyy-MM-ddTHH:mm:ssZ}");
        sb.AppendLine();

        // KPI summary
        sb.AppendLine("KPI SUMMARY");
        sb.AppendLine("Metric,Value");
        sb.AppendLine($"Appointment Count,{kpi.AppointmentCount}");
        sb.AppendLine($"No-show Rate,{kpi.NoShowRate:P1}");
        sb.AppendLine($"Avg Wait Time (min),{kpi.AvgWaitTimeMin:F1}");
        sb.AppendLine($"AI Acceptance Rate,{kpi.AiAcceptanceRate:P1}");
        sb.AppendLine($"Data Freshness (sec),{kpi.DataFreshnessSec}");
        sb.AppendLine();

        // Daily volumes
        sb.AppendLine("DAILY APPOINTMENT VOLUMES");
        sb.AppendLine("Date,Count");
        foreach (var row in trends.DailyVolumes)
            sb.AppendLine($"{row.Date},{row.Count}");
        sb.AppendLine();

        // Weekly trends
        sb.AppendLine("WEEKLY TRENDS");
        sb.AppendLine("Week,No-show Rate,AI p95 Latency (ms)");
        foreach (var row in trends.WeeklyTrends)
            sb.AppendLine($"{row.Week},{row.NoShowRate:P1},{row.AiLatencyP95Ms:F0}");
        sb.AppendLine();

        // Document throughput
        sb.AppendLine("DOCUMENT PROCESSING STATUS");
        sb.AppendLine("Status,Count");
        foreach (var row in trends.DocumentThroughput)
            sb.AppendLine($"{row.Status},{row.Count}");

        return Task.FromResult(Encoding.UTF8.GetBytes(sb.ToString()));
    }

    // ── PDF ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a PDF byte array with a title page, KPI table, and daily volumes list.
    /// Uses PdfSharpCore (OSS, TR-014, NFR-015).
    /// </summary>
    public Task<byte[]> GeneratePdfAsync(
        KpiMetricsDto kpi,
        TrendDataDto  trends,
        DateOnly      startDate,
        DateOnly      endDate,
        CancellationToken ct = default)
    {
        using var document   = new PdfDocument();
        document.Info.Title  = $"PropelIQ Metrics {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}";
        document.Info.Author = "PropelIQ Platform";

        var page = document.AddPage();
        var gfx  = XGraphics.FromPdfPage(page);

        var titleFont    = new XFont("Arial", 16, XFontStyle.Bold);
        var headingFont  = new XFont("Arial", 12, XFontStyle.Bold);
        var bodyFont     = new XFont("Arial", 10, XFontStyle.Regular);
        var smallFont    = new XFont("Arial", 9,  XFontStyle.Regular);

        double y = PageMargin;
        double contentWidth = page.Width - PageMargin * 2;

        // Title
        y = DrawLine(gfx, "PropelIQ Operational Metrics Report", titleFont, y, page.Width);
        y = DrawLine(gfx, $"Period: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}", bodyFont, y, page.Width);
        y = DrawLine(gfx, $"Generated: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC", smallFont, y, page.Width);
        y += SectionGap;

        // Horizontal rule
        gfx.DrawLine(XPens.LightGray,
            PageMargin, y, page.Width - PageMargin, y);
        y += SectionGap;

        // KPI section
        y = DrawLine(gfx, "KEY PERFORMANCE INDICATORS", headingFont, y, page.Width);
        var kpiRows = new[]
        {
            ("Appointment Count",   kpi.AppointmentCount.ToString()),
            ("No-show Rate",        kpi.NoShowRate.ToString("P1")),
            ("Avg Wait Time (min)", kpi.AvgWaitTimeMin.ToString("F1")),
            ("AI Acceptance Rate",  kpi.AiAcceptanceRate.ToString("P1")),
            ("Data Freshness",      $"{kpi.DataFreshnessSec}s"),
        };

        foreach (var (label, value) in kpiRows)
        {
            var rowText = $"  {label,-28} {value}";
            y = DrawLine(gfx, rowText, bodyFont, y, page.Width);
        }

        y += SectionGap;

        // Daily volumes section
        y = DrawLine(gfx, "DAILY APPOINTMENT VOLUMES", headingFont, y, page.Width);
        foreach (var row in trends.DailyVolumes)
        {
            if (y > page.Height - PageMargin * 2)
            {
                page = document.AddPage();
                gfx  = XGraphics.FromPdfPage(page);
                y    = PageMargin;
            }

            y = DrawLine(gfx, $"  {row.Date,-15} {row.Count}", bodyFont, y, page.Width);
        }

        y += SectionGap;

        // Document throughput section
        if (trends.DocumentThroughput.Count > 0)
        {
            y = DrawLine(gfx, "DOCUMENT PROCESSING STATUS", headingFont, y, page.Width);
            foreach (var row in trends.DocumentThroughput)
                y = DrawLine(gfx, $"  {row.Status,-20} {row.Count}", bodyFont, y, page.Width);
        }

        using var ms = new MemoryStream();
        document.Save(ms);
        return Task.FromResult(ms.ToArray());
    }

    private static double DrawLine(
        XGraphics gfx, string text, XFont font, double y, double pageWidth)
    {
        gfx.DrawString(
            text,
            font,
            XBrushes.Black,
            new XRect(PageMargin, y, pageWidth - PageMargin * 2, LineHeight),
            XStringFormats.TopLeft);
        return y + LineHeight;
    }
}

using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace PatientAccess.Application.Services;

/// <summary>
/// Generates a plain black-and-white single-page PDF using PDFsharp 6.x (OSS, MIT licence).
/// Contains appointment date/time, patient name, provider, visit reason, and clinic contact.
/// No external font resolver is required — relies on system fonts (Arial) available on Windows.
/// </summary>
public sealed class PdfSharpConfirmationService : IPdfGenerationService
{
    private const double PageMarginX = 50;
    private const double PageMarginY = 60;
    private const double LineHeight  = 22;

    public byte[] Generate(AppointmentConfirmationDetails details)
    {
        using var document = new PdfDocument();
        document.Info.Title   = "Appointment Confirmation";
        document.Info.Subject = "PropelIQ appointment confirmation";

        var page = document.AddPage();
        using var gfx = XGraphics.FromPdfPage(page);

        var fontTitle  = new XFont("Arial", 16, XFontStyleEx.Bold);
        var fontLabel  = new XFont("Arial", 10, XFontStyleEx.Bold);
        var fontNormal = new XFont("Arial", 11, XFontStyleEx.Regular);
        var fontSmall  = new XFont("Arial",  9, XFontStyleEx.Italic);

        double x = PageMarginX;
        double y = PageMarginY;

        // ── Title ────────────────────────────────────────────────────────────
        gfx.DrawString("Appointment Confirmation", fontTitle, XBrushes.Black, x, y);
        y += LineHeight * 1.8;

        // ── Clinic header ────────────────────────────────────────────────────
        gfx.DrawString(details.ClinicName, fontLabel, XBrushes.Black, x, y);
        y += LineHeight;
        gfx.DrawString($"Tel: {details.ClinicPhone}", fontSmall, XBrushes.DarkGray, x, y);
        y += LineHeight * 1.5;

        // ── Patient ───────────────────────────────────────────────────────────
        DrawRow(gfx, fontLabel, fontNormal, x, ref y, "Patient", details.PatientName);
        DrawRow(gfx, fontLabel, fontNormal, x, ref y, "Email",   details.PatientEmail);
        DrawRow(gfx, fontLabel, fontNormal, x, ref y, "Phone",   details.PatientPhone);
        y += LineHeight * 0.5;

        // ── Appointment ───────────────────────────────────────────────────────
        DrawRow(gfx, fontLabel, fontNormal, x, ref y, "Date",     details.SlotDatetime.ToString("dddd, MMMM d, yyyy"));
        DrawRow(gfx, fontLabel, fontNormal, x, ref y, "Time",     details.SlotDatetime.ToString("h:mm tt"));
        DrawRow(gfx, fontLabel, fontNormal, x, ref y, "Provider", details.ProviderName);

        if (!string.IsNullOrWhiteSpace(details.VisitReason))
            DrawRow(gfx, fontLabel, fontNormal, x, ref y, "Visit Reason", details.VisitReason);

        y += LineHeight * 1.5;

        // ── Footer ────────────────────────────────────────────────────────────
        gfx.DrawString(
            "Please arrive 10 minutes before your scheduled time. Reply to your confirmation email to reschedule.",
            fontSmall, XBrushes.DarkGray, x, y);

        using var stream = new MemoryStream();
        document.Save(stream, false);
        return stream.ToArray();
    }

    private static void DrawRow(
        XGraphics gfx,
        XFont labelFont,
        XFont valueFont,
        double x,
        ref double y,
        string label,
        string value)
    {
        const double labelWidth = 110;
        gfx.DrawString($"{label}:", labelFont, XBrushes.Black, x, y);
        gfx.DrawString(value, valueFont, XBrushes.Black, x + labelWidth, y);
        y += LineHeight;
    }
}

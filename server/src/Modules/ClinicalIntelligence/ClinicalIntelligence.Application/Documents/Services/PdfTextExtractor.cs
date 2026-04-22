using System.Text;
using ClinicalIntelligence.Application.Infrastructure;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;

namespace ClinicalIntelligence.Application.Documents.Services;

/// <summary>
/// Extracts ordered text from all pages of a PDF document using PdfPig (MIT, OSS — NFR-015).
///
/// Page text is built by joining <c>IWord.Text</c> values returned from
/// <c>PdfPage.GetWords()</c>, which honours the reading order of text blocks.
/// Pages are separated by a newline to preserve paragraph boundaries.
///
/// Empty / whitespace-only output (scanned PDFs without an OCR layer) is handled
/// by <see cref="DocumentExtractionJob"/> which flags the document for manual review.
/// </summary>
public sealed class PdfTextExtractor
{
    private readonly IFileStorageService         _fileStorage;
    private readonly ILogger<PdfTextExtractor>   _logger;

    public PdfTextExtractor(
        IFileStorageService        fileStorage,
        ILogger<PdfTextExtractor>  logger)
    {
        _fileStorage = fileStorage;
        _logger      = logger;
    }

    /// <summary>
    /// Opens the file at <paramref name="fileUri"/> and returns the full document text.
    /// </summary>
    /// <param name="fileUri">Storage URI as returned by <see cref="IFileStorageService.StoreAsync"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// All page texts concatenated with newline separators, trimmed.
    /// Returns an empty string when no text layer is found (e.g. scanned image PDF).
    /// </returns>
    public async Task<string> ExtractTextAsync(string fileUri, CancellationToken ct = default)
    {
        await using var stream = await _fileStorage.ReadAsync(fileUri, ct);

        // PdfPig requires a seekable stream; copy to MemoryStream when the source is not seekable.
        Stream pdfStream = stream.CanSeek ? stream : await CopyToMemoryStreamAsync(stream, ct);

        await using (pdfStream)
        {
            using var pdf = PdfDocument.Open(pdfStream);

            var sb = new StringBuilder();
            foreach (var page in pdf.GetPages())
            {
                // GetWords() returns words in reading order (left-to-right, top-to-bottom).
                var pageText = string.Join(" ", page.GetWords().Select(w => w.Text));
                if (!string.IsNullOrWhiteSpace(pageText))
                    sb.AppendLine(pageText);
            }

            var result = sb.ToString().Trim();

            _logger.LogDebug(
                "PdfTextExtractor: extracted {CharCount} chars from '{FileUri}'.",
                result.Length, fileUri);

            return result;
        }
    }

    private static async Task<MemoryStream> CopyToMemoryStreamAsync(Stream source, CancellationToken ct)
    {
        var ms = new MemoryStream();
        await source.CopyToAsync(ms, ct);
        ms.Seek(0, SeekOrigin.Begin);
        return ms;
    }
}

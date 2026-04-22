using System.Runtime.CompilerServices;
using ClinicalIntelligence.Application.Documents.Models;
using SharpToken;

namespace ClinicalIntelligence.Application.Documents.Services;

/// <summary>
/// Splits a document text into overlapping token windows per AIR-R01.
///
/// Algorithm:
/// - Tokenise the full text with <c>cl100k_base</c> (GPT-4 tokenizer via SharpToken, MIT — NFR-015).
/// - Slide a 512-token window in steps of 384 tokens (25% overlap = 128 tokens retained between
///   consecutive chunks). This ensures semantic context is not lost at chunk boundaries.
/// - Each chunk is yielded as a <see cref="DocumentChunk"/> via <c>IAsyncEnumerable</c> so the
///   caller never holds all chunks in memory simultaneously (handles 100+ page documents).
///
/// Chunk indices are zero-based and sequential within the document.
/// </summary>
public sealed class DocumentChunker
{
    private const int WindowSize = 512;   // tokens per chunk (AIR-R01)
    private const int StepSize   = 384;   // 25% overlap = WindowSize - StepSize = 128 retained

    private readonly GptEncoding _encoding = GptEncoding.GetEncoding("cl100k_base");

    /// <summary>
    /// Tokenises <paramref name="text"/> and yields sliding-window <see cref="DocumentChunk"/> records.
    /// </summary>
    /// <param name="documentId">FK to the source document.</param>
    /// <param name="text">Full extracted text from <see cref="PdfTextExtractor"/>.</param>
    /// <param name="ct">Cancellation token; checked between chunks.</param>
    /// <returns>Async stream of chunks; never empty when <paramref name="text"/> is non-empty.</returns>
    public async IAsyncEnumerable<DocumentChunk> ChunkAsync(
        Guid   documentId,
        string text,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var tokens     = _encoding.Encode(text);
        int chunkIndex = 0;

        for (int start = 0; start < tokens.Count; start += StepSize)
        {
            ct.ThrowIfCancellationRequested();

            var window = tokens.Skip(start).Take(WindowSize).ToList();
            if (window.Count == 0) break;

            // Decode back to string so each chunk contains the original text (no token IDs).
            var chunkText = _encoding.Decode(window);

            yield return new DocumentChunk(documentId, chunkIndex++, chunkText, window.Count);

            // Yield control to the caller between chunks — avoids thread starvation on huge docs.
            await Task.Yield();
        }
    }
}

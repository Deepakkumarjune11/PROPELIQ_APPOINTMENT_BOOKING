using ClinicalIntelligence.Application.Infrastructure;
using Microsoft.Extensions.Logging;

namespace ClinicalIntelligence.Application.Infrastructure;

/// <summary>
/// Phase-1 local filesystem implementation of <see cref="IFileStorageService"/>.
/// Writes files to <c>./uploads/documents/{patientId}/{guid}_{sanitisedName}</c>.
///
/// Security (OWASP A01 — Path Traversal):
/// - <c>Path.GetFileName()</c> strips any directory components from the caller-supplied name.
/// - Each patient's files are isolated under a per-patient subdirectory.
///
/// Phase-2: replace this with an Azure Blob Storage implementation; no handler changes needed.
/// </summary>
public sealed class LocalFileStorageService : IFileStorageService
{
    private const string BaseDirectory = "uploads/documents";

    private readonly ILogger<LocalFileStorageService> _logger;

    public LocalFileStorageService(ILogger<LocalFileStorageService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> StoreAsync(
        Stream            fileStream,
        string            originalFileName,
        Guid              patientId,
        CancellationToken cancellationToken = default)
    {
        // OWASP A01: strip any directory components to prevent path traversal
        var safeName    = Path.GetFileName(originalFileName);
        var uniquePrefix = Guid.NewGuid().ToString("N");
        var storedName  = $"{uniquePrefix}_{safeName}";

        var patientDir  = Path.Combine(BaseDirectory, patientId.ToString("N"));
        var absoluteDir = Path.GetFullPath(patientDir);

        Directory.CreateDirectory(absoluteDir);

        var absolutePath = Path.Combine(absoluteDir, storedName);
        var relativePath = Path.Combine(patientDir, storedName);

        await using var destination = File.Create(absolutePath);
        fileStream.Seek(0, SeekOrigin.Begin);
        await fileStream.CopyToAsync(destination, cancellationToken);

        _logger.LogInformation(
            "File stored: {RelativePath} ({Bytes} bytes)",
            relativePath, fileStream.Length);

        return relativePath;
    }

    /// <inheritdoc />
    public Task<Stream> ReadAsync(string fileUri, CancellationToken cancellationToken = default)
    {
        var absolutePath = Path.GetFullPath(fileUri);

        if (!File.Exists(absolutePath))
            throw new FileNotFoundException($"Document file not found at '{fileUri}'.");

        Stream stream = File.OpenRead(absolutePath);
        return Task.FromResult(stream);
    }
}

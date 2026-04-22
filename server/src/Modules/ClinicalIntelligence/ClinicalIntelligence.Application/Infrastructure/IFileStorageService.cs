namespace ClinicalIntelligence.Application.Infrastructure;

/// <summary>
/// Abstraction for binary file storage (phase-1: local filesystem; phase-2: Azure Blob).
/// Isolates storage implementation from the upload pipeline so that the backend can
/// switch storage providers without changing application logic (NFR-015, TR-002).
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Persists a file stream and returns a storage URI (relative path or blob URL).
    /// </summary>
    /// <param name="fileStream">The binary content to store.</param>
    /// <param name="originalFileName">
    /// The original client-supplied file name; the implementation MUST sanitise this
    /// using <c>Path.GetFileName()</c> to prevent path traversal (OWASP A01).
    /// </param>
    /// <param name="patientId">Used as a storage namespace to isolate per-patient files.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>A URI string that can be used to retrieve the file via <see cref="ReadAsync"/>.</returns>
    Task<string> StoreAsync(
        Stream            fileStream,
        string            originalFileName,
        Guid              patientId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a read stream for the file at the given <paramref name="fileUri"/>.
    /// </summary>
    Task<Stream> ReadAsync(string fileUri, CancellationToken cancellationToken = default);
}

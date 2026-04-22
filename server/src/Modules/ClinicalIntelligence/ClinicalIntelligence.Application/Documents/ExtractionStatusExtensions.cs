using PatientAccess.Domain.Enums;

namespace ClinicalIntelligence.Application.Documents;

/// <summary>
/// Maps <see cref="ExtractionStatus"/> enum values to the FE-expected lowercase snake_case strings.
/// The FE <c>ExtractionStatus</c> TypeScript union is: 'queued' | 'processing' | 'completed' | 'manual_review' | 'failed'.
/// </summary>
public static class ExtractionStatusExtensions
{
    public static string ToApiString(this ExtractionStatus status) =>
        status switch
        {
            ExtractionStatus.Queued        => "queued",
            ExtractionStatus.Processing    => "processing",
            ExtractionStatus.Completed     => "completed",
            ExtractionStatus.ManualReview  => "manual_review",
            ExtractionStatus.Failed        => "failed",
            ExtractionStatus.Pending       => "queued",   // Treat legacy Pending as queued
            _                              => status.ToString().ToLowerInvariant(),
        };
}

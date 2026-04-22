namespace ClinicalIntelligence.Application.AI.Exceptions;

/// <summary>
/// Thrown by <see cref="IAiGateway"/> when a batch's estimated token count exceeds
/// the AIR-O01 budget of 8,000 tokens per request.
///
/// The caller MUST reduce the batch size or discard oversized inputs.
/// This exception is NOT retried by Hangfire's <c>[AutomaticRetry]</c> — it represents
/// a permanent input constraint violation, not a transient API failure.
/// </summary>
public sealed class TokenBudgetExceededException : InvalidOperationException
{
    public TokenBudgetExceededException(string message) : base(message) { }
}

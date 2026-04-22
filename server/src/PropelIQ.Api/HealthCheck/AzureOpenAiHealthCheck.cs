using System.Diagnostics;
using Azure.AI.OpenAI;
using ClinicalIntelligence.Application.AI;
using ClinicalIntelligence.Application.AI.Availability;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PropelIQ.Api.HealthCheck;

/// <summary>
/// ASP.NET Core health check for the Azure OpenAI service (US_030 / AC-5, TR-019).
///
/// Probe strategy: 1-token embedding generation — the cheapest possible call to
/// verify endpoint reachability, managed-identity auth, and deployment availability
/// without incurring significant token cost or latency.
///
/// Timeout: 2 seconds (TR-019 uptime requirement — health endpoint must never be a
/// single point of failure).
///
/// Result mapping:
/// <list type="table">
///   <item><term>Latency &lt; 1 s, status 2xx</term><description>Healthy</description></item>
///   <item><term>Latency 1–2 s</term><description>Degraded (elevated, but responding)</description></item>
///   <item><term>Timeout (&gt; 2 s)</term><description>Unhealthy + MarkDegraded</description></item>
///   <item><term>Any exception</term><description>Unhealthy + MarkDegraded</description></item>
/// </list>
///
/// Security (OWASP A05 — Security Misconfiguration): this check intentionally does NOT
/// populate <see cref="HealthCheckResult.Description"/> with exception details — the
/// response writer in Program.cs strips internal details from the public HTTP response.
/// </summary>
public sealed class AzureOpenAiHealthCheck : IHealthCheck
{
    private readonly AzureOpenAIClient           _aiClient;
    private readonly AzureOpenAiOptions          _opts;
    private readonly IAiAvailabilityState        _availabilityState;
    private readonly ILogger<AzureOpenAiHealthCheck> _logger;

    public AzureOpenAiHealthCheck(
        AzureOpenAIClient                aiClient,
        IOptions<AzureOpenAiOptions>     options,
        IAiAvailabilityState             availabilityState,
        ILogger<AzureOpenAiHealthCheck>  logger)
    {
        _aiClient          = aiClient;
        _opts              = options.Value;
        _availabilityState = availabilityState;
        _logger            = logger;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken  cancellationToken = default)
    {
        // 2-second hard ceiling — health endpoint MUST NOT block the readiness probe (TR-019).
        using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        probeCts.CancelAfter(TimeSpan.FromSeconds(2));

        var sw = Stopwatch.StartNew();

        try
        {
            // Cheapest possible probe: 1-token embedding to verify auth + connectivity.
            // Uses the same EmbeddingDeploymentName as the gateway to confirm the correct deployment.
            var embeddingClient = _aiClient.GetEmbeddingClient(_opts.EmbeddingDeploymentName);
            await embeddingClient.GenerateEmbeddingAsync("health", options: null, probeCts.Token)
                .ConfigureAwait(false);

            sw.Stop();

            if (sw.ElapsedMilliseconds > 1_000)
            {
                // Responding but slow — do NOT mark availability state as failed here (degraded ≠ down).
                // The gateway remains available; only a hard failure triggers MarkDegraded.
                _logger.LogWarning(
                    "AzureOpenAI health check: elevated latency {LatencyMs}ms (threshold 1000ms).",
                    sw.ElapsedMilliseconds);
                return HealthCheckResult.Degraded($"Elevated latency: {sw.ElapsedMilliseconds}ms");
            }

            _availabilityState.MarkRecovered();

            _logger.LogDebug(
                "AzureOpenAI health check: healthy | latencyMs={LatencyMs}",
                sw.ElapsedMilliseconds);

            return HealthCheckResult.Healthy($"Latency: {sw.ElapsedMilliseconds}ms");
        }
        catch (OperationCanceledException) when (probeCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            const string reason = "Probe timed out after 2000ms";
            _availabilityState.MarkDegraded(reason);

            _logger.LogError(
                "AzureOpenAI health check: probe timeout after {LatencyMs}ms — marking degraded.",
                sw.ElapsedMilliseconds);

            // OWASP A05: return minimal description; exception detail omitted from public response
            return HealthCheckResult.Unhealthy("azure-openai: probe timeout");
        }
        catch (Exception ex)
        {
            sw.Stop();
            var reason = $"Probe failed: {ex.GetType().Name}";
            _availabilityState.MarkDegraded(reason);

            _logger.LogError(ex,
                "AzureOpenAI health check: probe failed | latencyMs={LatencyMs}",
                sw.ElapsedMilliseconds);

            // OWASP A05: exception NOT included in result — stripped at response writer level
            return HealthCheckResult.Unhealthy("azure-openai: probe failed");
        }
    }
}

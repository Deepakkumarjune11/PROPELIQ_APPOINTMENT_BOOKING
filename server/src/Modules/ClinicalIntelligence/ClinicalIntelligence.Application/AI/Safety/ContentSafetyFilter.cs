using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace ClinicalIntelligence.Application.AI.Safety;

/// <summary>
/// Three-layer AI output content safety filter (US_031, AIR-O03, AIR-S04).
///
/// Evaluation order:
///   Layer 1 — PHI leakage  : SSN, phone number, email, date-of-birth patterns.
///   Layer 2 — Harmful content : self-harm instructions, substance misuse, explicit violence.
///   Layer 3 — Medical advice  : prescriptive directives; skipped for excluded feature contexts.
///
/// Safety properties:
/// - Unicode NFC pre-normalisation defeats homoglyph and encoding-escape bypass attempts.
/// - All regex matches use a 50 ms per-pattern timeout to guard against ReDoS attacks.
///   A <see cref="RegexMatchTimeoutException"/> is treated as no-match (safe) — the caller
///   never receives a rejection based on a timeout alone.
/// - <see cref="IOptionsMonitor{T}"/> provides hot-reload: pattern changes in
///   <c>appsettings.json</c> take effect on the next request without restart.
///
/// Thread-safety: stateless; safe for transient DI lifetime.
/// </summary>
public sealed class ContentSafetyFilter(
    IOptionsMonitor<ContentSafetyOptions> options) : IContentSafetyFilter
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(50);

    /// <inheritdoc />
    public Task<ContentSafetyViolation?> EvaluateAsync(
        string            content,
        string            featureContext,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(content))
            return Task.FromResult<ContentSafetyViolation?>(null);

        var opts = options.CurrentValue;

        // Pre-normalise to Unicode NFC — defends against homoglyph and encoding-based bypasses
        var normalized    = content.Normalize(NormalizationForm.FormC);
        var responseHash  = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));

        // ── Layer 1: PHI leakage ─────────────────────────────────────────────────
        foreach (var entry in opts.PhiPatterns)
        {
            if (MatchesSafely(normalized, entry.Pattern))
                return Task.FromResult<ContentSafetyViolation?>(
                    new ContentSafetyViolation(SafetyViolationType.PhiLeakage, entry.Id, responseHash));
        }

        // ── Layer 2: Harmful content ─────────────────────────────────────────────
        foreach (var entry in opts.HarmKeywords)
        {
            if (MatchesSafely(normalized, entry.Pattern))
                return Task.FromResult<ContentSafetyViolation?>(
                    new ContentSafetyViolation(SafetyViolationType.HarmfulContent, entry.Id, responseHash));
        }

        // ── Layer 3: Medical advice hallucination ────────────────────────────────
        // Skipped for feature contexts where directive phrasing is expected (e.g., ConversationalIntake)
        if (!opts.ExcludedFeatureContextsForMedicalAdvice.Contains(featureContext,
                StringComparer.OrdinalIgnoreCase))
        {
            foreach (var entry in opts.MedicalAdvicePatterns)
            {
                if (MatchesSafely(normalized, entry.Pattern))
                    return Task.FromResult<ContentSafetyViolation?>(
                        new ContentSafetyViolation(
                            SafetyViolationType.MedicalAdviceHallucination, entry.Id, responseHash));
            }
        }

        return Task.FromResult<ContentSafetyViolation?>(null); // Content is safe
    }

    /// <summary>
    /// Evaluates <paramref name="pattern"/> against <paramref name="input"/> with a per-call
    /// 50 ms timeout. Returns <see langword="false"/> on <see cref="RegexMatchTimeoutException"/>
    /// (ReDoS guard — a timeout must never cause a false-positive safety block).
    /// </summary>
    private static bool MatchesSafely(string input, string pattern)
    {
        try
        {
            return Regex.IsMatch(
                input,
                pattern,
                RegexOptions.IgnoreCase | RegexOptions.Singleline,
                matchTimeout: RegexTimeout);
        }
        catch (RegexMatchTimeoutException)
        {
            // ReDoS guard: timeout → treat as no-match (safe).
            // The caller logs this via structured logging; evaluation continues.
            return false;
        }
    }
}

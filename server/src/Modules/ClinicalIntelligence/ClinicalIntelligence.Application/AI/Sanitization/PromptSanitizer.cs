using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace ClinicalIntelligence.Application.AI.Sanitization;

/// <summary>
/// Five-layer prompt sanitisation pipeline (AIR-S04, OWASP A03):
///
/// 1. Unicode NFC normalisation — collapses homoglyph sequences.
/// 2. URL decode (single pass) — unescapes percent-encoded injection fragments.
/// 3. HTML decode — unescapes HTML entity injection fragments.
/// 4. Block-pattern regex scan — returns <see cref="SanitizationVerdict.Blocked"/> on first match.
/// 5. Review-pattern regex scan — returns <see cref="SanitizationVerdict.FlaggedForReview"/> on first match.
///
/// A <see cref="RegexMatchTimeoutException"/> (50 ms per pattern) is caught and treated
/// as <see cref="SanitizationVerdict.Safe"/> to prevent ReDoS-based availability attacks.
///
/// Thread-safety: stateless beyond the read-only <see cref="IOptionsMonitor{T}"/> read;
/// registered as singleton.
/// </summary>
public sealed class PromptSanitizer : IPromptSanitizer
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(50);

    private readonly IOptionsMonitor<PromptInjectionOptions> _options;

    public PromptSanitizer(IOptionsMonitor<PromptInjectionOptions> options)
        => _options = options;

    /// <inheritdoc />
    public PromptSanitizationResult Evaluate(string input)
    {
        // Layer 1: Unicode NFC normalisation — collapse homoglyphs
        var normalized = input.Normalize(NormalizationForm.FormC);

        // Layer 2: URL decode (single pass only — avoids double-decode amplification)
        try { normalized = Uri.UnescapeDataString(normalized); }
        catch (UriFormatException) { /* malformed % encoding — use as-is */ }

        // Layer 3: HTML entity decode
        normalized = WebUtility.HtmlDecode(normalized);

        var opts = _options.CurrentValue;

        // Layer 4: Block patterns — first match wins; request is rejected
        foreach (var entry in opts.BlockPatterns)
        {
            try
            {
                if (Regex.IsMatch(normalized, entry.Pattern,
                    RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant,
                    RegexTimeout))
                {
                    return new PromptSanitizationResult(
                        SanitizationVerdict.Blocked, entry.Id, normalized);
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // Timeout → fail-open (avoid ReDoS-based DoS); treat pattern as non-matching
            }
        }

        // Layer 5: Review patterns — first match flags for telemetry but allows the request
        foreach (var entry in opts.ReviewPatterns)
        {
            try
            {
                if (Regex.IsMatch(normalized, entry.Pattern,
                    RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant,
                    RegexTimeout))
                {
                    return new PromptSanitizationResult(
                        SanitizationVerdict.FlaggedForReview, entry.Id, normalized);
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // Timeout → fail-open
            }
        }

        return new PromptSanitizationResult(SanitizationVerdict.Safe, null, normalized);
    }
}

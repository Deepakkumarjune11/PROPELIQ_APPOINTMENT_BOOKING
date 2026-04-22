using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using PatientAccess.Application.Exceptions;
using PatientAccess.Application.Infrastructure;
using PatientAccess.Application.Models;
using PatientAccess.Application.Repositories;

namespace PatientAccess.Application.AI;

/// <summary>
/// Azure OpenAI GPT-4 implementation of <see cref="IAiIntakeService"/>.
/// Enforces token budget (AIR-O01), PII redaction (AIR-S01), 3s timeout (AIR-Q02),
/// content safety check (AIR-S04), and sanitised audit logging (AIR-S03).
/// </summary>
public sealed class AzureOpenAiIntakeService : IAiIntakeService
{
    // ── Constants ──────────────────────────────────────────────────────────
    private const int CharsPerToken = 4;
    private const int MaxTokenBudget = 8_000;
    private const int MaxOutputTokens = 800;
    private const float Temperature = 0.3f;
    private static readonly TimeSpan AiTimeout = TimeSpan.FromSeconds(3);

    // ── PII redaction patterns (AIR-S01) ───────────────────────────────────
    private static readonly Regex EmailRegex = new(
        @"\b[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}\b",
        RegexOptions.Compiled);

    private static readonly Regex PhoneRegex = new(
        @"\b(\+?1[\s\-.]?)?\(?\d{3}\)?[\s\-.]?\d{3}[\s\-.]?\d{4}\b",
        RegexOptions.Compiled);

    private static readonly Regex DateRegex = new(
        @"\b\d{1,2}[/\-\.]\d{1,2}[/\-\.]\d{2,4}\b",
        RegexOptions.Compiled);

    // ── Structured answer extraction ───────────────────────────────────────
    private static readonly Regex StructuredAnswersRegex = new(
        @"__structured_answers__\s*(\{.+?\})\s*$",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.Multiline);

    // ── Fields ─────────────────────────────────────────────────────────────
    private readonly ChatClient? _chatClient;
    private readonly string _deploymentName;
    private readonly IAIPromptLogRepository _promptLogRepository;
    private readonly ILogger<AzureOpenAiIntakeService> _logger;
    private readonly string _systemPrompt;
    private readonly bool _isConfigured;

    public AzureOpenAiIntakeService(
        IOptions<AzureOpenAIOptions> options,
        IAIPromptLogRepository promptLogRepository,
        ILogger<AzureOpenAiIntakeService> logger)
    {
        _promptLogRepository = promptLogRepository;
        _logger = logger;
        _systemPrompt = LoadSystemPrompt();

        var opts = options.Value;
        _deploymentName = opts.DeploymentName;
        _isConfigured = !string.IsNullOrWhiteSpace(opts.Endpoint)
                        && !string.IsNullOrWhiteSpace(opts.ApiKey);

        if (_isConfigured)
        {
            if (!Uri.TryCreate(opts.Endpoint, UriKind.Absolute, out var endpointUri))
                throw new InvalidOperationException(
                    $"AzureOpenAI:Endpoint is not a valid absolute URI.");

            var azureClient = new AzureOpenAIClient(endpointUri, new AzureKeyCredential(opts.ApiKey));
            _chatClient = azureClient.GetChatClient(opts.DeploymentName);
        }
    }

    // ── IAiIntakeService ───────────────────────────────────────────────────

    public async Task<IntakeConversationResult> SendMessageAsync(
        IReadOnlyList<ChatTurn> conversationHistory,
        CancellationToken ct = default)
    {
        if (!_isConfigured || _chatClient is null)
        {
            _logger.LogWarning("Azure OpenAI is not configured; returning service unavailable.");
            throw new AiServiceUnavailableException(
                "Azure OpenAI is not configured in this environment.");
        }

        // a. Token budget enforcement (AIR-O01)
        var trimmedHistory = EnforceTokenBudget(conversationHistory);
        var tokenEstimate = trimmedHistory.Sum(h => h.Content.Length / CharsPerToken);

        // b. PII redaction (AIR-S01) — redact identifiers, preserve clinical content
        var redactedHistory = RedactPii(trimmedHistory);

        // c. Build OpenAI message list
        var messages = BuildMessages(redactedHistory);

        // d. Call Azure OpenAI with 3s timeout (AIR-Q02)
        ChatCompletion completion;
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(AiTimeout);

        try
        {
            var completionOptions = new ChatCompletionOptions
            {
                MaxOutputTokenCount = MaxOutputTokens,
                Temperature = Temperature
            };

            var response = await _chatClient.CompleteChatAsync(messages, completionOptions, timeoutCts.Token);
            completion = response.Value;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Azure OpenAI call timed out after {Timeout}s.", AiTimeout.TotalSeconds);
            throw new AiServiceUnavailableException("Azure OpenAI timed out.");
        }
        catch (RequestFailedException ex) when (ex.Status is 429 or 503)
        {
            _logger.LogWarning("Azure OpenAI returned HTTP {Status}.", ex.Status);
            throw new AiServiceUnavailableException($"Azure OpenAI unavailable (HTTP {ex.Status}).", ex);
        }

        // e. Content safety check (AIR-S04)
        if (completion.FinishReason == ChatFinishReason.ContentFilter)
        {
            _logger.LogWarning("Azure OpenAI response blocked by content safety filter.");
            throw new AiServiceUnavailableException("Response blocked by content safety filter.");
        }

        // f. Parse structured answers from response
        var rawMessage = completion.Content.Count > 0 ? completion.Content[0].Text : string.Empty;
        var (displayMessage, isComplete, structuredAnswers) = ParseStructuredAnswers(rawMessage);

        // g. Sanitised audit log — NO PHI in log fields (AIR-S03, DR-012)
        await _promptLogRepository.LogAsync(new AIPromptLogEntry(
            ModelProvider: "AzureOpenAI",
            DeploymentName: _deploymentName,
            RequestSummary: $"turns:{redactedHistory.Count} est_tokens:{tokenEstimate}",
            ResponseSummary: $"isComplete:{isComplete} words:{displayMessage.Split(' ').Length}",
            IsComplete: isComplete), ct);

        return new IntakeConversationResult(
            displayMessage,
            isComplete,
            isComplete ? structuredAnswers : null);
    }

    // ── Private helpers ────────────────────────────────────────────────────

    private static string LoadSystemPrompt()
    {
        var assembly = typeof(AzureOpenAiIntakeService).Assembly;
        const string resourceName = "PatientAccess.Application.AI.IntakeSystemPrompt.md";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' not found in assembly '{assembly.GetName().Name}'. " +
                "Ensure AI/IntakeSystemPrompt.md is marked as EmbeddedResource in the .csproj.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Trims the oldest turn pairs when estimated token count exceeds <see cref="MaxTokenBudget"/> (AIR-O01).
    /// Logs a warning (no PII) when trimming occurs.
    /// </summary>
    private IReadOnlyList<ChatTurn> EnforceTokenBudget(IReadOnlyList<ChatTurn> history)
    {
        var estimated = history.Sum(h => h.Content.Length / CharsPerToken);
        if (estimated <= MaxTokenBudget)
            return history;

        _logger.LogWarning(
            "Token budget exceeded: estimated {Tokens} tokens for {Turns} turns; trimming oldest pairs.",
            estimated, history.Count);

        var mutable = history.ToList();
        while (mutable.Sum(h => h.Content.Length / CharsPerToken) > MaxTokenBudget && mutable.Count > 2)
        {
            // Remove oldest user turn
            mutable.RemoveAt(0);
            // Remove the following assistant turn if present
            if (mutable.Count > 0 && string.Equals(mutable[0].Role, "assistant", StringComparison.OrdinalIgnoreCase))
                mutable.RemoveAt(0);
        }

        return mutable;
    }

    /// <summary>
    /// Replaces PII identifiers (email, phone, date of birth patterns) with placeholders (AIR-S01).
    /// Clinical content (symptoms, medications, history) is deliberately preserved.
    /// </summary>
    private static IReadOnlyList<ChatTurn> RedactPii(IReadOnlyList<ChatTurn> history)
    {
        return history
            .Select(t => new ChatTurn(t.Role, RedactContent(t.Content)))
            .ToList();
    }

    private static string RedactContent(string content)
    {
        content = EmailRegex.Replace(content, "[REDACTED_EMAIL]");
        content = PhoneRegex.Replace(content, "[REDACTED_PHONE]");
        content = DateRegex.Replace(content, "[REDACTED_DATE]");
        return content;
    }

    private List<ChatMessage> BuildMessages(IReadOnlyList<ChatTurn> history)
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(_systemPrompt)
        };

        foreach (var turn in history)
        {
            if (string.Equals(turn.Role, "user", StringComparison.OrdinalIgnoreCase))
                messages.Add(new UserChatMessage(turn.Content));
            else if (string.Equals(turn.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                messages.Add(new AssistantChatMessage(turn.Content));
        }

        return messages;
    }

    /// <summary>
    /// Extracts the hidden <c>__structured_answers__</c> JSON block from the AI response.
    /// Returns display text (block stripped), completion flag, and parsed answer dictionary.
    /// </summary>
    private static (string displayMessage, bool isComplete, Dictionary<string, string>? answers)
        ParseStructuredAnswers(string rawMessage)
    {
        var match = StructuredAnswersRegex.Match(rawMessage);
        if (!match.Success)
            return (rawMessage.Trim(), false, null);

        var displayMessage = rawMessage[..match.Index].Trim();
        var jsonBlock = match.Groups[1].Value;

        try
        {
            using var doc = JsonDocument.Parse(jsonBlock);
            var root = doc.RootElement;

            var isComplete = root.TryGetProperty("isComplete", out var isCompleteEl)
                             && isCompleteEl.ValueKind == JsonValueKind.True;

            var answers = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var prop in root.EnumerateObject())
            {
                if (prop.NameEquals("isComplete"))
                    continue;

                answers[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                    ? prop.Value.GetString() ?? string.Empty
                    : prop.Value.ToString();
            }

            return (displayMessage, isComplete, isComplete ? answers : null);
        }
        catch (JsonException)
        {
            // Malformed JSON from model — treat as incomplete turn, surface display text
            return (displayMessage, false, null);
        }
    }
}

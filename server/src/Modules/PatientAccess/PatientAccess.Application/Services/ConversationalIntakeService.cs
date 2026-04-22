using System.Text.Json;
using Microsoft.Extensions.Logging;
using PatientAccess.Application.Exceptions;
using PatientAccess.Application.Infrastructure;
using PatientAccess.Application.Models;
using PatientAccess.Application.Repositories;
using PatientAccess.Domain.Enums;

namespace PatientAccess.Application.Services;

/// <summary>
/// Thin orchestration layer for the AI conversational intake flow (AIR-002, FR-012).
/// <list type="number">
///   <item>Validates the patient exists.</item>
///   <item>Loads or restores conversation history from Redis (TTL 10 min — covers 5 min timeout edge case).</item>
///   <item>Appends the user message and delegates to <see cref="IAiIntakeService"/>.</item>
///   <item>Persists updated history back to Redis.</item>
///   <item>On completion: persists <c>IntakeResponse</c> + <c>AuditLog</c> via <see cref="IIntakeSubmissionRepository"/>; removes Redis key.</item>
///   <item>On AI unavailability: returns <c>fallbackToManual: true</c> without propagating the exception (AC-5).</item>
/// </list>
/// PHI guard (DR-015 / AIR-S01): raw conversation content is never written to Serilog.
/// Follows Application-layer architecture: all DB access via <see cref="IIntakeSubmissionRepository"/> interface.
/// </summary>
public sealed class ConversationalIntakeService : IConversationalIntakeService
{
    private const string CacheKeyPrefix = "intake-conv:";
    private static readonly TimeSpan ConversationTtl = TimeSpan.FromMinutes(10);

    private readonly IIntakeSubmissionRepository _intakeRepository;
    private readonly ICacheService _cache;
    private readonly IAiIntakeService _aiIntakeService;
    private readonly ILogger<ConversationalIntakeService> _logger;

    public ConversationalIntakeService(
        IIntakeSubmissionRepository intakeRepository,
        ICacheService cache,
        IAiIntakeService aiIntakeService,
        ILogger<ConversationalIntakeService> logger)
    {
        _intakeRepository = intakeRepository;
        _cache            = cache;
        _aiIntakeService  = aiIntakeService;
        _logger           = logger;
    }

    /// <inheritdoc/>
    public async Task<IntakeChatResponse> SendMessageAsync(
        Guid patientId,
        IntakeChatRequest request,
        CancellationToken ct = default)
    {
        // a. Validate patient exists (soft-delete filter applied by repository)
        var patientExists = await _intakeRepository.PatientExistsAsync(patientId, ct);
        if (!patientExists)
            throw new NotFoundException($"Patient {patientId} was not found.");

        // b. Load conversation history from Redis; fall back to client-provided history on cache miss
        var cacheKey = $"{CacheKeyPrefix}{patientId}";
        var cachedHistory = await _cache.GetAsync<List<ChatTurn>>(cacheKey, ct);
        var history = cachedHistory
            ?? (request.ConversationHistory.Count > 0
                ? new List<ChatTurn>(request.ConversationHistory)
                : new List<ChatTurn>());

        // c. Append user message (empty string on first call — AI produces the opening greeting)
        if (!string.IsNullOrEmpty(request.Message))
            history.Add(new ChatTurn("user", request.Message));

        // d. Delegate to AI service; catch circuit-breaker exception (AC-5 / AIR-O02)
        IntakeConversationResult aiResult;
        try
        {
            aiResult = await _aiIntakeService.SendMessageAsync(history, ct);
        }
        catch (AiServiceUnavailableException ex)
        {
            // PHI guard: no patient identifiers or message content logged
            _logger.LogWarning(ex, "AI intake service unavailable — returning fallbackToManual");
            return new IntakeChatResponse(
                AssistantMessage: string.Empty,
                IsComplete: false,
                FallbackToManual: true,
                StructuredAnswers: null);
        }

        // e. Append AI response to history
        history.Add(new ChatTurn("assistant", aiResult.AssistantMessage));

        // f. Persist updated history to Redis with TTL
        await _cache.SetAsync(cacheKey, history, ConversationTtl, ct);

        // g. On completion: persist IntakeResponse + AuditLog via repository; remove Redis key
        if (aiResult.IsComplete && aiResult.StructuredAnswers is not null)
        {
            var answersJson = JsonSerializer.Serialize(aiResult.StructuredAnswers);
            await _intakeRepository.SubmitIntakeAsync(
                patientId, IntakeMode.Conversational, answersJson, ct);
            await _cache.RemoveAsync(cacheKey, ct);
        }

        return new IntakeChatResponse(
            AssistantMessage: aiResult.AssistantMessage,
            IsComplete: aiResult.IsComplete,
            FallbackToManual: false,
            StructuredAnswers: aiResult.IsComplete ? aiResult.StructuredAnswers : null);
    }
}


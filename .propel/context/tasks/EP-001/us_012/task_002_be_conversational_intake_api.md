# Task - task_002_be_conversational_intake_api

## Requirement Reference

- **User Story**: US_012 — Conversational AI Intake
- **Story Location**: `.propel/context/tasks/EP-001/us_012/us_012.md`
- **Acceptance Criteria**:
  - AC-2: Patient sends a message → system returns AI response within 3 seconds p95 (NFR-011, AIR-Q02).
  - AC-4: When all required information gathered, stores intake with mode="conversational" and structured JSONB answers.
  - AC-5: If AI service is unavailable (circuit breaker open), returns `fallbackToManual: true` — FE shows fallback message.
- **Edge Cases**:
  - Ambiguous/off-topic response → AI layer handles re-prompting; this endpoint proxies the response verbatim.
  - Conversation timeout after 5 min inactivity → Redis conversation history TTL = 10 minutes; client can resume by sending next message and history is replayed from Redis.

---

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | No |
| **Figma URL** | N/A |
| **Wireframe Status** | N/A |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | N/A |
| **Screen Spec** | N/A |
| **UXR Requirements** | N/A |
| **Design Tokens** | N/A |

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Backend | .NET 8 ASP.NET Core Web API | 8.0 LTS |
| Caching | Upstash Redis (ICacheService) | Cloud |
| ORM | Entity Framework Core | 8.0 |
| Database | PostgreSQL | 15.x |
| Serialization | System.Text.Json | .NET 8 built-in |
| Logging | Serilog | 3.x |
| API Docs | Swagger / OpenAPI | 6.x |
| CQRS | MediatR | 12.x |

> All code and libraries MUST be compatible with versions above.

---

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | Yes (orchestrates AI service) |
| **AIR Requirements** | AIR-002 (conversational intake), AIR-Q02 (p95 ≤ 3s), AIR-O02 (circuit breaker degradation to `fallbackToManual`), AIR-S03 (prompts/responses logged to AIPromptLog — handled in AI task), AIR-S01 (PII redaction — handled in AI task) |
| **AI Pattern** | Conversational — this layer orchestrates `IAiIntakeService` (implemented in task_003); does NOT call Azure OpenAI directly |
| **Prompt Template Path** | N/A (prompt owned by task_003) |
| **Guardrails Config** | N/A (guardrails in AI gateway task_003) |
| **Model Provider** | Azure OpenAI GPT-4 (indirect — via IAiIntakeService) |

---

## Mobile References (Mobile Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **Mobile Impact** | No |
| **Platform Target** | N/A |
| **Min OS Version** | N/A |
| **Mobile Framework** | N/A |

---

## Task Overview

Implement the **Conversational Intake API** within the `PatientAccess` bounded context:
- `POST /api/v1/patients/{patientId}/intake/chat` — accepts a single user message + conversation history, delegates to `IAiIntakeService`, returns the AI assistant message, completion status, and (when complete) structured answers.

The endpoint acts as a **thin orchestration layer**:
1. Validates the `patientId` exists.
2. Retrieves or initializes conversation history from Redis (keyed `intake-conv:{patientId}`) with TTL = 10 minutes (handles the 5-minute timeout edge case with margin).
3. Appends the incoming user message to the history.
4. Calls `IAiIntakeService.SendMessageAsync(history, cancellationToken)`.
5. Appends the AI response to history and persists back to Redis.
6. If `IAiIntakeService` returns `IntakeConversationResult.IsComplete = true`, extracts `StructuredAnswers`, persists a new `IntakeResponse` entity with `Mode = IntakeMode.Conversational` (reusing the IntakeResponse persistence pattern from US_011/task_002), and writes an `AuditLog` entry.
7. If `IAiIntakeService` throws `AiServiceUnavailableException` (circuit breaker open), returns `{ fallbackToManual: true }` — does NOT persist intake.

**No MediatR command** is used here because the chat request is a real-time streaming-adjacent operation with ephemeral session state — a direct Service injection into the controller is cleaner and avoids the overhead of a CQRS command for a read-write chat turn.

---

## Dependent Tasks

- **task_003_ai_intake_prompt_setup.md** (US_012) — `IAiIntakeService` must be registered in DI before this controller can be tested.
- **task_002_be_submit_intake_api.md** (US_011) — `IntakeResponse` persistence logic is reused/mirrored for conversational completion.

---

## Impacted Components

| Action | Module | Description |
|--------|--------|-------------|
| MODIFY | `PatientAccess.Presentation` | `Controllers/PatientsController.cs` — add `POST /{patientId:guid}/intake/chat` action |
| CREATE | `PatientAccess.Application` | `Services/IConversationalIntakeService.cs` — thin orchestration interface (patient validation, Redis cache, delegate to AI) |
| CREATE | `PatientAccess.Application` | `Services/ConversationalIntakeService.cs` — implementation; validates patient, manages Redis conversation history, calls `IAiIntakeService`, persists `IntakeResponse` on completion |
| CREATE | `PatientAccess.Application` | `Models/IntakeChatRequest.cs` — request model: `Message (string)`, `ConversationHistory (ChatTurn[])` |
| CREATE | `PatientAccess.Application` | `Models/IntakeChatResponse.cs` — response model: `AssistantMessage (string)`, `IsComplete (bool)`, `FallbackToManual (bool)`, `StructuredAnswers (Dictionary<string,string>?)` |
| CREATE | `PatientAccess.Application` | `Models/ChatTurn.cs` — `Role (string: "user"|"assistant")`, `Content (string)` |
| MODIFY | `PatientAccess.Presentation` | `ServiceCollectionExtensions.cs` — register `IConversationalIntakeService` as scoped |

---

## Implementation Plan

1. **Request / Response Models** (`PatientAccess.Application/Models/`):
   ```csharp
   public sealed record ChatTurn(string Role, string Content);

   public sealed record IntakeChatRequest(
       string Message,
       IReadOnlyList<ChatTurn> ConversationHistory
   );

   public sealed record IntakeChatResponse(
       string AssistantMessage,
       bool IsComplete,
       bool FallbackToManual,
       Dictionary<string, string>? StructuredAnswers
   );
   ```

2. **`IConversationalIntakeService`** interface:
   ```csharp
   public interface IConversationalIntakeService
   {
       Task<IntakeChatResponse> SendMessageAsync(
           Guid patientId,
           IntakeChatRequest request,
           CancellationToken ct = default);
   }
   ```

3. **`ConversationalIntakeService`** implementation — inject `PropelIQDbContext`, `ICacheService`, `IAiIntakeService`, `ILogger<ConversationalIntakeService>`:
   ```
   a. Validate patient exists:
      patientExists = await _db.Patients.AnyAsync(p => p.Id == patientId, ct)
      if (!patientExists) → throw NotFoundException

   b. Load or restore conversation history from Redis:
      cacheKey = $"intake-conv:{patientId}"
      cachedHistory = await _cache.GetAsync<List<ChatTurn>>(cacheKey)
      history = cachedHistory ?? new List<ChatTurn>()
      // If client sent history (first page load after refresh) and cache is empty, trust client history

   c. Append user message:
      history.Add(new ChatTurn("user", request.Message))

   d. Call AI service:
      try {
          aiResult = await _aiIntakeService.SendMessageAsync(history, ct)
      } catch (AiServiceUnavailableException) {
          _logger.LogWarning("AI intake service unavailable for PatientId redacted — fallback to manual")
          return new IntakeChatResponse("", false, FallbackToManual: true, null)
      }

   e. Append AI response to history:
      history.Add(new ChatTurn("assistant", aiResult.AssistantMessage))

   f. Persist updated history to Redis (TTL 10 min):
      await _cache.SetAsync(cacheKey, history, TimeSpan.FromMinutes(10))

   g. If isComplete → persist IntakeResponse + AuditLog:
      answersJson = JsonSerializer.Serialize(aiResult.StructuredAnswers)
      intakeResponse = new IntakeResponse { PatientId=patientId, Mode=IntakeMode.Conversational, Answers=answersJson, CreatedAt=UtcNow }
      _db.IntakeResponses.Add(intakeResponse)
      _db.AuditLogs.Add(new AuditLog { ActorId=patientId, ActionType=AuditActionType.IntakeSubmitted, TargetEntityId=intakeResponse.Id, Payload=Serialize(new { mode="conversational", questionCount=aiResult.StructuredAnswers.Count }) })
      await _db.SaveChangesAsync(ct)
      // Remove Redis conversation key after successful persist
      await _cache.RemoveAsync(cacheKey)

   h. Return IntakeChatResponse
   ```

4. **`PatientsController`** — add action to existing controller (created in US_011/task_002):
   ```csharp
   /// <summary>Send a message to the AI intake assistant and receive the next question.</summary>
   /// <response code="200">AI response returned successfully.</response>
   /// <response code="404">Patient not found.</response>
   [HttpPost("{patientId:guid}/intake/chat")]
   [Authorize]
   [ProducesResponseType(typeof(IntakeChatResponse), StatusCodes.Status200OK)]
   [ProducesResponseType(StatusCodes.Status404NotFound)]
   public async Task<IActionResult> SendIntakeChatMessage(
       Guid patientId,
       [FromBody] IntakeChatRequest request,
       CancellationToken ct)
   {
       var result = await _conversationalIntakeService.SendMessageAsync(patientId, request, ct);
       return Ok(result);
   }
   ```

5. **DI Registration** in `ServiceCollectionExtensions.cs`:
   ```csharp
   services.AddScoped<IConversationalIntakeService, ConversationalIntakeService>();
   ```
   (IAiIntakeService registered in task_003 — must be registered before running this.)

---

## Current Project State

```
server/src/Modules/PatientAccess/
  PatientAccess.Application/
    Infrastructure/
      ICacheService.cs                     ← Stable — Redis cache abstraction
    Commands/
      SubmitIntake/                        ← Created in us_011/task_002
      RegisterForAppointment/              ← Created in us_010/task_002
    Models/                                ← Does NOT exist yet — CREATE
  PatientAccess.Data/
    Entities/
      IntakeResponse.cs                    ← Has Mode, Answers, PatientId, CreatedAt
    PropelIQDbContext.cs                   ← DbSet<IntakeResponse> available
  PatientAccess.Domain/
    Enums/
      IntakeMode.cs                        ← Conversational, Manual (both present)
      AuditActionType.cs                   ← IntakeSubmitted (added in us_011/task_002)
  PatientAccess.Presentation/
    Controllers/
      PatientsController.cs               ← Created in us_011/task_002 — MODIFY to add chat action
    ServiceCollectionExtensions.cs        ← MODIFY — register ConversationalIntakeService
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Models/ChatTurn.cs` | `Role (string)`, `Content (string)` record |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Models/IntakeChatRequest.cs` | `Message (string)`, `ConversationHistory (IReadOnlyList<ChatTurn>)` |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Models/IntakeChatResponse.cs` | `AssistantMessage`, `IsComplete`, `FallbackToManual`, `StructuredAnswers?` |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Services/IConversationalIntakeService.cs` | Interface with `SendMessageAsync` |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Services/ConversationalIntakeService.cs` | Patient validation, Redis conversation history, `IAiIntakeService` delegation, `IntakeResponse` persistence on completion, `AuditLog` entry |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Presentation/Controllers/PatientsController.cs` | Add `POST /{patientId:guid}/intake/chat` action; inject `IConversationalIntakeService` |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Presentation/ServiceCollectionExtensions.cs` | `services.AddScoped<IConversationalIntakeService, ConversationalIntakeService>()` |

---

## External References

- [Upstash Redis / StackExchange.Redis — key TTL](https://redis.io/docs/manual/keyspace-notifications/)
- [EF Core 8 — AnyAsync for existence checks](https://learn.microsoft.com/en-us/ef/core/querying/async)
- [OWASP A01 — Authorize on patient-scoped endpoints](https://owasp.org/Top10/A01_2021-Broken_Access_Control/)
- [ASP.NET Core 8 — CancellationToken in controller actions](https://learn.microsoft.com/en-us/aspnet/core/web-api/?view=aspnetcore-8.0#cancellation-tokens)
- [System.Text.Json — serialize/deserialize Dictionary](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/how-to)

---

## Build Commands

```bash
# From server/
dotnet restore
dotnet build PropelIQ.slnx

# No migration required — IntakeResponse table exists; Redis is runtime-only
```

---

## Implementation Validation Strategy

- [ ] `POST /api/v1/patients/{patientId}/intake/chat` returns 200 with `AssistantMessage` (mock `IAiIntakeService`)
- [ ] `404 Not Found` returned when `patientId` does not exist in DB
- [ ] `401 Unauthorized` returned without `[Authorize]` token
- [ ] Conversation history persisted to Redis with TTL 10 minutes after each turn
- [ ] When `IAiIntakeService` throws `AiServiceUnavailableException`, response is `{ fallbackToManual: true }` — no exception propagated to caller
- [ ] When `isComplete = true`, `IntakeResponse` with `Mode = Conversational` is written to DB; Redis key is removed; `AuditLog` entry created
- [ ] `AuditLog.Payload` does NOT contain raw conversation content — PHI guard (AIR-S01 analogous)
- [ ] Swagger UI shows `POST /{patientId}/intake/chat` with request/response schema

---

## Implementation Checklist

- [ ] Create `ChatTurn.cs`, `IntakeChatRequest.cs`, `IntakeChatResponse.cs` records in `PatientAccess.Application/Models/`
- [ ] Create `IConversationalIntakeService.cs` with `SendMessageAsync(Guid patientId, IntakeChatRequest, CancellationToken)`
- [ ] Create `ConversationalIntakeService.cs` — patient existence check, Redis history load/save (TTL 10min), `IAiIntakeService` call, `AiServiceUnavailableException` catch returning `fallbackToManual: true`, `IntakeResponse` + `AuditLog` persistence on completion, Redis key removal after persist
- [ ] Modify `PatientsController.cs` — inject `IConversationalIntakeService`; add `[HttpPost("{patientId:guid}/intake/chat")]` action with XML doc comments and `[Authorize]`
- [ ] Modify `ServiceCollectionExtensions.cs` — register `IConversationalIntakeService` as scoped
- [ ] Confirm `dotnet build` passes with zero errors

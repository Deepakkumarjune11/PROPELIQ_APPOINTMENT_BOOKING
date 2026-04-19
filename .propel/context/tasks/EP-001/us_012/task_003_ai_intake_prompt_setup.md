# Task - task_003_ai_intake_prompt_setup

## Requirement Reference

- **User Story**: US_012 — Conversational AI Intake
- **Story Location**: `.propel/context/tasks/EP-001/us_012/us_012.md`
- **Acceptance Criteria**:
  - AC-1: AI greets the patient and asks the first intake question with empathetic language. (System prompt governs tone.)
  - AC-2: AI processes patient responses and asks the next relevant question. (Conversation loop, NLU extraction per AIR-002.)
  - AC-5: If the AI model provider is unavailable, throw `AiServiceUnavailableException` so the calling service (task_002) falls back gracefully. (AIR-O02 circuit breaker.)
- **Edge Cases**:
  - Ambiguous/off-topic response → system prompt instructs the model to re-prompt with a specific follow-up question and offer to switch to manual mode.
  - Conversation timeout — handled in Redis layer (task_002); AI layer is stateless per turn.

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
| AI/ML - LLM | Azure OpenAI GPT-4 | 2024-02-01 (API version) |
| AI/ML - SDK | Azure.AI.OpenAI (.NET SDK) | 1.x (stable) |
| Backend | .NET 8 ASP.NET Core | 8.0 LTS |
| Logging | Serilog | 3.x |
| API Docs | Swagger / OpenAPI | 6.x |

> All code and libraries MUST be compatible with versions above.

---

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | Yes — this IS the AI layer implementation |
| **AIR Requirements** | AIR-002 (conversational intake via NLU), AIR-Q02 (p95 ≤ 3s — enforce via `Timeout`), AIR-S01 (redact PII from prompts before invocation), AIR-S03 (log prompts + responses to AIPromptLog table, HIPAA retention), AIR-S04 (Azure OpenAI Content Safety integration), AIR-O01 (token budget 8,000 tokens per turn), AIR-O02 (circuit breaker with automatic fallback), NFR-011 (3s p95 AI response) |
| **AI Pattern** | Conversational (guided, non-RAG) — conversation history passed as message array; NLU extracts structured intake answers |
| **Prompt Template Path** | `server/src/Modules/PatientAccess/PatientAccess.Application/AI/IntakeSystemPrompt.md` |
| **Guardrails Config** | Azure OpenAI Content Safety (AIR-S04); custom JSON schema output enforcer for structured answer extraction (AIR-Q04) |
| **Model Provider** | Azure OpenAI GPT-4 (2024-02-01) with signed HIPAA BAA (NFR-013) |

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

Implement the **AI Intake Service** — the component that wraps Azure OpenAI GPT-4 calls for conversational patient intake. This is the only point in the system where LLM inference is invoked for intake (AIR-002). It implements:

1. **System prompt** — empathetic clinical intake guide persona; defines required question set, re-prompting strategy for ambiguous answers, structured JSON output format when all questions answered.
2. **PII redaction** — strips patient identifiers (name, DOB, email, phone) from message content before sending to Azure OpenAI (AIR-S01). Note: actual clinical information (symptoms, medications, history) IS sent as this is the clinical purpose of the intake.
3. **Token budget enforcement** — rejects requests where `history.Sum(tokens)` exceeds 8,000 tokens; trims oldest turns if over budget (AIR-O01).
4. **Circuit breaker** — if Azure OpenAI returns HTTP 503/429 or times out (>3s), throws `AiServiceUnavailableException` (AIR-O02).
5. **Content safety** — Azure OpenAI Content Safety is configured at the deployment level; if filtered response received, throws `AiContentFilteredException` (subtype of `AiServiceUnavailableException`) so caller falls back gracefully (AIR-S04).
6. **Audit logging** — logs every prompt (sanitized) and response to `AIPromptLog` table (AIR-S03, DR-012 HIPAA retention).
7. **Structured answer extraction** — on each turn, includes an instruction in the prompt to output a `__structured_answers__` JSON block containing all successfully gathered answers so far. When all required intake fields are present, sets `IsComplete = true`.

---

## Dependent Tasks

- **task_002_be_conversational_intake_api.md** (US_012) — registers `IAiIntakeService` via DI; calls `SendMessageAsync`. This task creates `IAiIntakeService` which task_002 depends on.

---

## Impacted Components

| Action | Module | Description |
|--------|--------|-------------|
| CREATE | `PatientAccess.Application` | `AI/IAiIntakeService.cs` — interface with `SendMessageAsync` |
| CREATE | `PatientAccess.Application` | `AI/AzureOpenAiIntakeService.cs` — Azure OpenAI GPT-4 implementation with PII redaction, token budget, circuit breaker, content safety, audit logging |
| CREATE | `PatientAccess.Application` | `AI/IntakeSystemPrompt.md` — system prompt template (versioned) |
| CREATE | `PatientAccess.Application` | `AI/AiServiceUnavailableException.cs` — custom exception for circuit breaker fallback |
| CREATE | `PatientAccess.Application` | `AI/AiConversationResult.cs` — result type: `AssistantMessage (string)`, `IsComplete (bool)`, `StructuredAnswers (Dictionary<string,string>?)` |
| CREATE | `PatientAccess.Data` | `Entities/AIPromptLog.cs` — EF Core entity for audit logging prompts/responses |
| CREATE | `PatientAccess.Data` | `Configurations/AIPromptLogConfiguration.cs` — EF Core fluent config for `ai_prompt_log` table |
| MODIFY | `PatientAccess.Presentation` | `ServiceCollectionExtensions.cs` — register `IAiIntakeService` (singleton for circuit breaker state); configure Azure OpenAI client from `IConfiguration` |
| MODIFY | `server/src/PropelIQ.Api` | `appsettings.json` / `appsettings.Development.json` — add `AzureOpenAI` config section |

---

## Implementation Plan

1. **`IAiIntakeService`**:
   ```csharp
   public interface IAiIntakeService
   {
       Task<AiConversationResult> SendMessageAsync(
           IReadOnlyList<ChatTurn> history,
           CancellationToken ct = default);
   }
   ```

2. **`AiConversationResult`**:
   ```csharp
   public sealed record AiConversationResult(
       string AssistantMessage,
       bool IsComplete,
       Dictionary<string, string>? StructuredAnswers   // non-null when IsComplete = true
   );
   ```

3. **`AiServiceUnavailableException`**:
   ```csharp
   public class AiServiceUnavailableException : Exception
   {
       public AiServiceUnavailableException(string message, Exception? inner = null)
           : base(message, inner) { }
   }
   ```

4. **`IntakeSystemPrompt.md`** — stored as embedded resource; loaded by `AzureOpenAiIntakeService`:
   ```markdown
   You are a compassionate clinical intake assistant helping a patient prepare for their medical appointment.
   Your role is to guide the patient through 5 required intake questions:
   1. Reason for visit
   2. Chief complaint
   3. Current medications (list name and dosage)
   4. Known allergies
   5. Relevant medical history

   Instructions:
   - Ask one question at a time in a warm, empathetic tone.
   - If a response is unclear or off-topic, gently re-ask with additional context and offer: "If you'd prefer to fill out a form instead, you can switch to manual mode."
   - When a patient provides a valid answer, acknowledge it briefly and move to the next unanswered question.
   - At the END of your message, always include a JSON block (hidden from display) in this exact format:
     __structured_answers__ { "reasonForVisit": "...", "chiefComplaint": "...", "currentMeds": "...", "allergies": "...", "medicalHistory": "..." }
     Use empty string "" for questions not yet answered.
   - When ALL 5 fields are non-empty, add "isComplete": true to the JSON block.
   - NEVER ask for or repeat: patient name, date of birth, email, phone number, or insurance details.
   ```

5. **`AzureOpenAiIntakeService`** — inject `OpenAIClient` (Azure), `PropelIQDbContext`, `IConfiguration`, `ILogger<AzureOpenAiIntakeService>`:

   ```
   a. TOKEN BUDGET (AIR-O01):
      Estimate token count: history.Sum(h => h.Content.Length / 4)
      If > 8000: trim oldest user+assistant turn pairs until under budget.
      Log trimming at Warning level (no PII in log message).

   b. PII REDACTION (AIR-S01):
      Before building OpenAI message list, apply regex patterns to each history entry:
      - Email: replace with "[REDACTED_EMAIL]"
      - Phone: replace with "[REDACTED_PHONE]"
      - Date patterns (DOB format): replace with "[REDACTED_DATE]"
      Note: clinical content (symptoms, medications) is NOT redacted — this IS the intake purpose.

   c. BUILD OPENAI MESSAGES:
      messages = [ SystemMessage(IntakeSystemPrompt), ...history.Select(h => h.Role=="user" ? UserMessage(h.Content) : AssistantMessage(h.Content)) ]

   d. CALL AZURE OPENAI (with cancellation and timeout):
      using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct)
      cts.CancelAfter(TimeSpan.FromSeconds(3))    // AIR-Q02 3s p95 budget
      try {
          var options = new ChatCompletionsOptions { MaxTokens = 800, Temperature = 0.3f }
          response = await _openAiClient.GetChatCompletionsAsync(deploymentName, options, cts.Token)
      } catch (OperationCanceledException) → throw new AiServiceUnavailableException("Azure OpenAI timed out")
        catch (RequestFailedException ex) when (ex.Status is 429 or 503) → throw new AiServiceUnavailableException("Azure OpenAI unavailable", ex)

   e. CONTENT SAFETY CHECK (AIR-S04):
      If response.Value.Choices[0].FinishReason == "content_filter":
          throw new AiServiceUnavailableException("Response blocked by content safety filter")

   f. PARSE STRUCTURED ANSWERS:
      rawMessage = response.Value.Choices[0].Message.Content
      // Extract __structured_answers__ JSON block from message (regex)
      displayMessage = rawMessage with __structured_answers__ block stripped
      isComplete = parsedJson["isComplete"] == true
      structuredAnswers = parsedJson (excluding isComplete key) if fields non-empty; null if not complete

   g. AUDIT LOG (AIR-S03):
      _db.AiPromptLogs.Add(new AIPromptLog {
          Id = NewGuid,
          Timestamp = UtcNow,
          ModelProvider = "AzureOpenAI",
          DeploymentName = deploymentName,
          // Sanitized: only log message count and token estimate, NOT content (PHI)
          RequestSummary = $"turns:{history.Count} est_tokens:{tokenEstimate}",
          ResponseSummary = $"isComplete:{isComplete} words:{displayMessage.Split(' ').Length}",
          IsComplete = isComplete
      })
      await _db.SaveChangesAsync(ct)

   h. RETURN AiConversationResult(displayMessage, isComplete, isComplete ? structuredAnswers : null)
   ```

6. **`AIPromptLog` entity** (`PatientAccess.Data/Entities/AIPromptLog.cs`):
   ```csharp
   public class AIPromptLog
   {
       public Guid Id { get; set; }
       public DateTime Timestamp { get; set; }
       public string ModelProvider { get; set; } = default!;
       public string DeploymentName { get; set; } = default!;
       public string RequestSummary { get; set; } = default!;   // sanitized — NO PHI
       public string ResponseSummary { get; set; } = default!;  // sanitized — NO PHI
       public bool IsComplete { get; set; }
   }
   ```
   **NOTE: `RequestSummary` and `ResponseSummary` must NEVER contain patient message content (PHI). Only metadata (turn count, token estimate, word count, completion flag) is stored per AIR-S01 / DR-012.**

7. **`AIPromptLogConfiguration`** — table `ai_prompt_log`; `HasDefaultValueSql("gen_random_uuid()")` on Id; no FK to Patient (intentional — avoids PHI linkage in audit table).

8. **Configuration** (`appsettings.json`):
   ```json
   "AzureOpenAI": {
     "Endpoint": "",
     "ApiKey": "",
     "DeploymentName": "gpt-4-turbo",
     "ApiVersion": "2024-02-01"
   }
   ```
   API key sourced from environment variable / Azure Key Vault in production; `appsettings.Development.json` uses placeholder.

9. **DI Registration** (`ServiceCollectionExtensions.cs`):
   ```csharp
   var azureOpenAiConfig = configuration.GetSection("AzureOpenAI");
   services.AddSingleton(new OpenAIClient(
       new Uri(azureOpenAiConfig["Endpoint"]!),
       new AzureKeyCredential(azureOpenAiConfig["ApiKey"]!)));
   services.AddSingleton<IAiIntakeService, AzureOpenAiIntakeService>();
   // Singleton: circuit breaker state must persist across requests
   ```

10. **EF Core Migration** — Add `AIPromptLog` to `PropelIQDbContext`, generate migration `AddAIPromptLog`:
    ```bash
    dotnet ef migrations add AddAIPromptLog \
      --project PatientAccess.Data \
      --startup-project PropelIQ.Api
    ```

---

## Current Project State

```
server/src/Modules/PatientAccess/
  PatientAccess.Application/
    AI/                                    ← Does NOT exist yet — CREATE
    Infrastructure/
      ICacheService.cs                     ← Stable
  PatientAccess.Data/
    Entities/                              ← AIPromptLog.cs does NOT exist yet
    Configurations/                        ← AIPromptLogConfiguration.cs does NOT exist
    PropelIQDbContext.cs                   ← MODIFY: add DbSet<AIPromptLog>
  PatientAccess.Presentation/
    ServiceCollectionExtensions.cs        ← MODIFY: OpenAIClient + IAiIntakeService registration

server/src/PropelIQ.Api/
  appsettings.json                        ← MODIFY: add AzureOpenAI section
  appsettings.Development.json            ← MODIFY: add AzureOpenAI placeholder section
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/AI/IAiIntakeService.cs` | Interface: `SendMessageAsync(history, ct) → AiConversationResult` |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/AI/AiConversationResult.cs` | Result record: AssistantMessage, IsComplete, StructuredAnswers? |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/AI/AiServiceUnavailableException.cs` | Custom exception for circuit breaker / timeout / content filter |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/AI/IntakeSystemPrompt.md` | Versioned system prompt — embedded resource in `.csproj` |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/AI/AzureOpenAiIntakeService.cs` | Implementation: token budget, PII redaction, Azure OpenAI call (3s timeout), content safety check, structured answer parsing, sanitized audit logging |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Data/Entities/AIPromptLog.cs` | EF Core entity: Id, Timestamp, ModelProvider, DeploymentName, RequestSummary, ResponseSummary, IsComplete — NO PHI columns |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Data/Configurations/AIPromptLogConfiguration.cs` | Fluent config: `ai_prompt_log` table, UUID default |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Data/PropelIQDbContext.cs` | Add `DbSet<AIPromptLog> AiPromptLogs` |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Presentation/ServiceCollectionExtensions.cs` | Register `OpenAIClient` (singleton) + `IAiIntakeService` (singleton) from `AzureOpenAI` config section |
| MODIFY | `server/src/PropelIQ.Api/appsettings.json` | Add `"AzureOpenAI": { "Endpoint": "", "ApiKey": "", "DeploymentName": "gpt-4-turbo", "ApiVersion": "2024-02-01" }` |
| MODIFY | `server/src/PropelIQ.Api/appsettings.Development.json` | Add placeholder `AzureOpenAI` section with dev endpoint / empty key |
| CREATE | EF Migration | `AddAIPromptLog` — creates `ai_prompt_log` table |

---

## External References

- [Azure.AI.OpenAI .NET SDK — ChatCompletionsOptions](https://learn.microsoft.com/en-us/dotnet/api/azure.ai.openai.chatcompletionsoptions)
- [Azure OpenAI Service — Content Safety / Content Filtering](https://learn.microsoft.com/en-us/azure/ai-services/openai/concepts/content-filter)
- [Azure OpenAI Service — HIPAA BAA](https://learn.microsoft.com/en-us/azure/ai-services/openai/concepts/customizing-llms)
- [AIR-O01 — Token budget 8,000 per extraction request](design.md#AIR-O01)
- [AIR-O02 — Circuit breaker pattern for model failures](design.md#AIR-O02)
- [AIR-S01 — PII redaction before model invocation](design.md#AIR-S01)
- [AIR-S03 — Audit log prompts/responses HIPAA retention](design.md#AIR-S03)
- [AIR-S04 — Content filtering gate](design.md#AIR-S04)
- [EF Core 8 — Embedded resource for SQL/text files](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/)
- [OWASP A02 — Cryptographic Failures: API key must not be hardcoded; use env vars or Key Vault](https://owasp.org/Top10/A02_2021-Cryptographic_Failures/)

---

## Build Commands

```bash
# From server/
dotnet restore
dotnet build PropelIQ.slnx

# Add EF Core migration for AIPromptLog table
dotnet ef migrations add AddAIPromptLog \
  --project src/Modules/PatientAccess/PatientAccess.Data \
  --startup-project src/PropelIQ.Api

# Apply migration (development)
dotnet ef database update \
  --project src/Modules/PatientAccess/PatientAccess.Data \
  --startup-project src/PropelIQ.Api
```

---

## Implementation Validation Strategy

- [ ] Unit test — `AzureOpenAiIntakeService` with mocked `OpenAIClient`: returns `AiConversationResult` with `AssistantMessage` on successful call
- [ ] Unit test — `OperationCanceledException` from OpenAI call throws `AiServiceUnavailableException` (AIR-O02 circuit breaker)
- [ ] Unit test — `RequestFailedException` with Status 429/503 throws `AiServiceUnavailableException`
- [ ] Unit test — `FinishReason == "content_filter"` throws `AiServiceUnavailableException` (AIR-S04)
- [ ] Unit test — PII patterns (email, phone, date) are stripped from `history` before `OpenAIClient` call (AIR-S01)
- [ ] Unit test — `__structured_answers__` JSON block is parsed; `isComplete: true` triggers `IsComplete = true` in result
- [ ] Unit test — token count > 8,000 → oldest turns trimmed (AIR-O01)
- [ ] Integration test — `AIPromptLog` row created after each `SendMessageAsync` call; `RequestSummary`/`ResponseSummary` contain NO patient message content
- [ ] `appsettings.json` `AzureOpenAI` section present; API key NOT hardcoded (must be empty string with env-var override)
- [ ] `dotnet build` passes zero errors after migration generated

---

## Implementation Checklist

- [ ] Create `AiServiceUnavailableException.cs`
- [ ] Create `AiConversationResult.cs` record
- [ ] Create `IntakeSystemPrompt.md` as embedded resource (mark as `EmbeddedResource` in `.csproj`); define empathetic persona, 5-question list, re-prompt instruction, `__structured_answers__` JSON block format, `isComplete` flag rule
- [ ] Create `IAiIntakeService.cs` with `SendMessageAsync(IReadOnlyList<ChatTurn>, CancellationToken)`
- [ ] Create `AzureOpenAiIntakeService.cs` — implement in order: token budget trim → PII redaction → OpenAI call (3s timeout CTS) → exception handling (timeout/429/503/content_filter → AiServiceUnavailableException) → parse `__structured_answers__` block → sanitized `AIPromptLog` insert (NO PHI in log fields) → return `AiConversationResult`
- [ ] Create `AIPromptLog.cs` entity (no PHI columns)
- [ ] Create `AIPromptLogConfiguration.cs` — `ai_prompt_log` table, UUID default
- [ ] Modify `PropelIQDbContext.cs` — add `DbSet<AIPromptLog> AiPromptLogs`
- [ ] Modify `ServiceCollectionExtensions.cs` — register `OpenAIClient` (singleton) + `IAiIntakeService` (singleton)
- [ ] Modify `appsettings.json` + `appsettings.Development.json` — add `AzureOpenAI` section with empty/placeholder API key
- [ ] Run `dotnet ef migrations add AddAIPromptLog` and confirm migration file generated
- [ ] Confirm `dotnet build` passes with zero errors

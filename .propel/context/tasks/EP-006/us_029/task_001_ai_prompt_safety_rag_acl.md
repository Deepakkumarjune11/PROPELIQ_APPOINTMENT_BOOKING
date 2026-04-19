# Task - task_001_ai_prompt_safety_rag_acl

## Requirement Reference

- **User Story**: US_029 — AI Prompt Safety & RAG Access Controls
- **Story Location**: `.propel/context/tasks/EP-006/us_029/us_029.md`
- **Acceptance Criteria**:
  - AC-1: Sanitize every user input reaching the AI gateway against known prompt injection patterns (instruction override, context escape, role impersonation) per AIR-S01.
  - AC-2: On injection detection, block the request, return a safe error message, and log the attempt with full context in the audit log per AIR-S01.
  - AC-3: During RAG retrieval, only chunks from documents the requesting user is authorized to access are included in the context window per AIR-S02.
  - AC-4: Staff RAG queries are scoped to patients in the staff member's department or explicitly assigned care team per AIR-S02.
  - AC-5: Injection patterns are loaded via `IOptionsMonitor<PromptInjectionOptions>` — new patterns can be added to `appsettings.json` and take effect without redeployment per AIR-S01.
- **Edge Cases**:
  - Clinical query resembling injection (e.g., "ignore the medication — patient is allergic"): `ReviewPatterns` list flags it as `FlaggedForReview`; request proceeds with `flagged=true` recorded in audit payload; staff can continue with full audit trail. NOT blocked.
  - Encoded/obfuscated injection (Unicode substitution, URL encoding, HTML entities): `PromptSanitizer` normalizes to NFC Unicode, URL-decodes, and HTML-entity-decodes the input before pattern matching, defeating single-encoding bypass attempts.

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
| Backend | .NET 8 ASP.NET Core | 8.0 LTS |
| AI/ML - Gateway | Custom `IAiGateway` middleware | custom |
| AI/ML - LLM | Azure OpenAI GPT-4 Turbo | API version 2024-02-01 |
| AI/ML - SDK | `Azure.AI.OpenAI` NuGet | 1.0.x |
| Vector Store | pgvector (PostgreSQL) | 0.5.x |
| ORM | Entity Framework Core + `Pgvector.EntityFrameworkCore` | 8.0 / 0.2.x |
| Caching | Upstash Redis (`IDistributedCache`) | Cloud |
| Logging | Serilog + `IAuditLogger` (US_026) | 3.x / custom |
| Testing - Unit | xUnit + Moq | 2.x / 4.x |

> `IOptionsMonitor<T>` is a built-in .NET 8 abstraction — no additional NuGet required. Hot-reload of `appsettings.json` on IIS uses the built-in file-watcher (`AddJsonFile(..., reloadOnChange: true)`) already configured by `WebApplication.CreateBuilder`.

---

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | Yes |
| **AIR Requirements** | AIR-S01, AIR-S02 |
| **AI Pattern** | RAG — Access Control + Guardrails layer |
| **Prompt Template Path** | N/A — sanitizer operates on raw user input before prompt assembly |
| **Guardrails Config** | `appsettings.json` → `PromptSanitization:BlockPatterns` + `PromptSanitization:ReviewPatterns` |
| **Model Provider** | Azure OpenAI GPT-4 Turbo (HIPAA BAA — NFR-013) |

### CRITICAL: AI Implementation Requirements

- **MUST** invoke `IPromptSanitizer.Evaluate()` inside `AzureOpenAiGateway.ChatCompletionAsync` on the **raw `userMessage`** before any token counting, context assembly, or GPT call.
- **MUST** audit log every injection event (Blocked AND FlaggedForReview) with `ActorId`, `ActorType`, `IpAddress`, `MatchedPatternId`, `NormalizedInput` (truncated to 500 chars — NO raw patient PHI in audit payload per AIR-S03).
- **MUST** use `IOptionsMonitor<PromptInjectionOptions>` (not `IOptions<>`) so the in-memory pattern list refreshes when `appsettings.json` is updated on disk (AC-5 no-redeploy requirement).
- **MUST** normalize input through Unicode NFC → URL decode → HTML entity decode before any pattern matching to defeat obfuscation bypass attempts (edge case 2).
- **MUST** return `HTTP 400` with the body `{"error":"Request blocked by content policy."}` for Blocked verdict — no pattern details disclosed (A01 information disclosure prevention).
- **MUST** invoke `IRagAccessFilter.GetAuthorizedDocumentIdsAsync(actorId, actorRole)` inside `DocumentSearchService.SearchAsync` overload BEFORE executing the pgvector similarity query. Empty result → return empty list + audit log `"RagQueryUnauthorized"`.

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

Implement the AIR-S01 prompt injection defense pipeline and AIR-S02 RAG row-level access control filter inside the existing `ClinicalIntelligence` AI gateway layer.

**Prompt injection defense (AIR-S01):**
- Multi-layer sanitizer: Unicode NFC normalization → URL decode → HTML entity decode → regex against `BlockPatterns` list (Blocked) → regex against `ReviewPatterns` list (FlaggedForReview) → Safe.
- Configuration-driven pattern lists loaded via `IOptionsMonitor<PromptInjectionOptions>` — patterns can be updated in `appsettings.json` and take effect at runtime without redeployment (AC-5).
- `Blocked` verdict: throw `PromptInjectionBlockedException` after saving audit log. Caller (`AzureOpenAiGateway.ChatCompletionAsync`) catches and translates to `HTTP 400`.
- `FlaggedForReview` verdict: log to audit, continue with GPT call — normalizedInput used as the forwarded message to prevent obfuscated content from reaching the model.

**RAG row-level access control (AIR-S02):**
- `IRagAccessFilter.GetAuthorizedDocumentIdsAsync(actorId, actorRole)`:
  - `patient` role: returns document IDs where `clinical_documents.patient_id = actorId`.
  - `staff` role: UNION of (a) explicit grants from `document_access_grants WHERE grantee_id = actorId AND grantee_type = 'staff'` and (b) department-based join `clinical_documents cd JOIN patients p ON cd.patient_id = p.id JOIN staff s ON s.department = p.department WHERE s.id = actorId`.
  - `system` role (internal jobs): bypasses filter — returns `null` (DocumentSearchService interprets null as "no ownership restriction").
- `DocumentSearchService.SearchAsync` new overload (actor-based) injects the authorized doc ID set into the pgvector `<=>` WHERE clause via `document_id = ANY(:authorizedIds)`.

---

## Dependent Tasks

- **task_002_ai_embedding_pipeline.md** (US_019) — `IAiGateway`, `AzureOpenAiGateway`, `IDocumentSearchService`, `DocumentSearchService` must exist before modifying them.
- **task_001_ai_rag_extraction_job.md** (US_020) — `IChatCompletionGateway.ChatCompletionAsync` established on `AzureOpenAiGateway` — this task modifies that method.
- **task_001_be_audit_compliance_service.md** (US_026) — `IAuditLogger.LogAsync` must exist (stages audit entry on DbContext change tracker).
- **task_002_db_document_acl_schema.md** (US_029, this sprint) — `document_access_grants` table and `Patient.Department` column must exist before `RagAccessFilter` can query them. Implement DB task in parallel; `RagAccessFilter` can compile without DB rows but queries will return empty for staff until grants are seeded.

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Sanitization/IPromptSanitizer.cs` | Interface: `PromptSanitizationResult Evaluate(string input)` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Sanitization/PromptSanitizationResult.cs` | Record: `(SanitizationVerdict Verdict, string? MatchedPatternId, string NormalizedInput)` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Sanitization/PromptInjectionOptions.cs` | POCO bound via `IOptionsMonitor`; `BlockPatterns` + `ReviewPatterns` lists |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Sanitization/PromptSanitizer.cs` | 5-layer pipeline implementation; `IOptionsMonitor<PromptInjectionOptions>` injected |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Sanitization/PromptInjectionBlockedException.cs` | Custom exception: `MatchedPatternId` property; caught at controller/gateway boundary |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Access/IRagAccessFilter.cs` | Interface: `Task<IReadOnlyList<Guid>?> GetAuthorizedDocumentIdsAsync(Guid actorId, string actorRole, CancellationToken ct)` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Access/RagAccessFilter.cs` | Implementation: patient (own docs) / staff (dept join + explicit grants UNION) / system (null = unrestricted) |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/AzureOpenAiGateway.cs` | Inject `IPromptSanitizer`; call `Evaluate(userMessage)` in `ChatCompletionAsync` before GPT; handle Blocked (audit + throw) and FlaggedForReview (audit + proceed with normalizedInput) |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Services/IDocumentSearchService.cs` | Add `Task<List<ChunkSearchResult>> SearchAsync(string queryText, Guid actorId, string actorRole, CancellationToken ct)` overload (existing `patientId` overload retained for internal jobs) |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Services/DocumentSearchService.cs` | Implement new overload: call `IRagAccessFilter.GetAuthorizedDocumentIdsAsync`; if null skip filter; if empty list return `[]` + audit log; else apply `document_id = ANY(:ids)` pre-filter on pgvector query |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Presentation/ServiceCollectionExtensions.cs` | Register `IPromptSanitizer → PromptSanitizer` (transient); `IRagAccessFilter → RagAccessFilter` (scoped); bind `PromptInjectionOptions` via `Configure<>(config.GetSection("PromptSanitization"))` |
| MODIFY | `server/src/PropelIQ.Api/appsettings.json` | Add `PromptSanitization` section with initial `BlockPatterns` (instruction override, context escape, role impersonation IDs) and `ReviewPatterns` (clinical-query lookalikes) |
| MODIFY | `server/src/PropelIQ.Api/appsettings.Development.json` | Mirror `PromptSanitization` section; add review pattern for development-only verbose test phrase |

---

## Implementation Plan

### 1. `PromptInjectionOptions` — configuration POCO

```csharp
// ClinicalIntelligence.Application/AI/Sanitization/PromptInjectionOptions.cs
public sealed class PromptInjectionOptions
{
    public const string SectionName = "PromptSanitization";

    /// <summary>Patterns that BLOCK the request outright (Blocked verdict).</summary>
    public List<InjectionPatternEntry> BlockPatterns { get; set; } = [];

    /// <summary>Patterns that flag for review but allow the request to proceed (FlaggedForReview verdict).</summary>
    public List<InjectionPatternEntry> ReviewPatterns { get; set; } = [];
}

public sealed record InjectionPatternEntry(
    string Id,          // e.g. "INJ-001"
    string Pattern,     // regex string
    string Description  // human-readable label for audit log
);
```

### 2. `PromptSanitizationResult` + `SanitizationVerdict`

```csharp
public enum SanitizationVerdict { Safe, FlaggedForReview, Blocked }

public sealed record PromptSanitizationResult(
    SanitizationVerdict Verdict,
    string? MatchedPatternId,   // null when Safe
    string NormalizedInput      // always populated — use this in downstream GPT calls
);
```

### 3. `PromptSanitizer` — 5-layer pipeline

```csharp
public sealed class PromptSanitizer(IOptionsMonitor<PromptInjectionOptions> options) : IPromptSanitizer
{
    public PromptSanitizationResult Evaluate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new(SanitizationVerdict.Safe, null, input ?? string.Empty);

        // Layer 1: Unicode NFC normalization (defeats lookalike Unicode substitution)
        var normalized = input.Normalize(NormalizationForm.FormC);

        // Layer 2: URL decode + HTML entity decode (defeats %2F-style and &#x…; encoding)
        normalized = Uri.UnescapeDataString(normalized);
        normalized = System.Net.WebUtility.HtmlDecode(normalized);

        var currentOptions = options.CurrentValue; // IOptionsMonitor: always reflects latest appsettings

        // Layer 3: BlockPatterns — reject
        foreach (var entry in currentOptions.BlockPatterns)
        {
            if (Regex.IsMatch(normalized, entry.Pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline))
                return new(SanitizationVerdict.Blocked, entry.Id, normalized);
        }

        // Layer 4: ReviewPatterns — flag but continue
        foreach (var entry in currentOptions.ReviewPatterns)
        {
            if (Regex.IsMatch(normalized, entry.Pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline))
                return new(SanitizationVerdict.FlaggedForReview, entry.Id, normalized);
        }

        // Layer 5: Safe
        return new(SanitizationVerdict.Safe, null, normalized);
    }
}
```

> **Security note (OWASP A03 — Injection):** `Regex.IsMatch` with `RegexOptions.Singleline` prevents `.` from stopping at newlines — a classic bypass using literal `\n` in injections. All regex patterns MUST be tested for catastrophic backtracking (ReDoS). Use `Regex.Match(..., timeout: TimeSpan.FromMilliseconds(50))` in production to bound ReDoS risk. Update `PromptSanitizer` to use `Regex.IsMatch(normalized, entry.Pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline, matchTimeout: TimeSpan.FromMilliseconds(50))` with a try-catch on `RegexMatchTimeoutException` → treat as Safe (log the timeout).

### 4. `AzureOpenAiGateway.ChatCompletionAsync` modification

```csharp
// Inject IPromptSanitizer via constructor (add to existing ctor params)
private readonly IPromptSanitizer _sanitizer;
private readonly IAuditLogger _auditLogger;
private readonly PropelIQDbContext _db;

public async Task<string> ChatCompletionAsync(
    string systemPrompt, string userMessage, Guid documentId, CancellationToken ct = default)
{
    // AIR-S01: sanitize before any token counting or GPT call
    var sanitizationResult = _sanitizer.Evaluate(userMessage);

    if (sanitizationResult.Verdict == SanitizationVerdict.Blocked)
    {
        await _auditLogger.LogAsync(new AuditLogEntry
        {
            ActorId = _currentActorId,   // resolved from IHttpContextAccessor
            ActorType = _currentActorType,
            ActionType = "PromptInjectionBlocked",
            TargetEntityId = documentId.ToString(),
            TargetEntityType = "AiRequest",
            IpAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            Payload = JsonSerializer.Serialize(new
            {
                patternId = sanitizationResult.MatchedPatternId,
                inputPreview = sanitizationResult.NormalizedInput.Length > 500
                    ? sanitizationResult.NormalizedInput[..500]
                    : sanitizationResult.NormalizedInput
                // NEVER log full raw input — may contain PHI (AIR-S03)
            })
        }, ct);
        await _db.SaveChangesAsync(ct); // save audit log before throwing (no caller SaveChanges)
        throw new PromptInjectionBlockedException(sanitizationResult.MatchedPatternId);
    }

    if (sanitizationResult.Verdict == SanitizationVerdict.FlaggedForReview)
    {
        await _auditLogger.LogAsync(new AuditLogEntry
        {
            ActorId = _currentActorId,
            ActorType = _currentActorType,
            ActionType = "PromptInjectionFlagged",
            TargetEntityId = documentId.ToString(),
            TargetEntityType = "AiRequest",
            IpAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            Payload = JsonSerializer.Serialize(new
            {
                patternId = sanitizationResult.MatchedPatternId,
                verdict = "FlaggedForReview"
            })
        }, ct);
        // DO NOT save here — audit log persists atomically with the GPT call result
    }

    // Use normalizedInput downstream — not raw userMessage
    var finalUserMessage = sanitizationResult.NormalizedInput;

    // ... existing circuit-breaker check, token budget, GPT call ...
    if (IsCircuitOpen) { /* existing fallback */ return string.Empty; }

    // existing Azure OpenAI call using finalUserMessage
}
```

### 5. `IRagAccessFilter` + `RagAccessFilter`

```csharp
// IRagAccessFilter.cs
public interface IRagAccessFilter
{
    /// <summary>
    /// Returns authorized document IDs for the given actor.
    /// Returns null for system role (unrestricted — internal background jobs).
    /// Returns empty list if actor has no authorized documents (caller must abort RAG query).
    /// </summary>
    Task<IReadOnlyList<Guid>?> GetAuthorizedDocumentIdsAsync(
        Guid actorId, string actorRole, CancellationToken ct = default);
}

// RagAccessFilter.cs
public sealed class RagAccessFilter(PropelIQDbContext db) : IRagAccessFilter
{
    public async Task<IReadOnlyList<Guid>?> GetAuthorizedDocumentIdsAsync(
        Guid actorId, string actorRole, CancellationToken ct = default)
    {
        return actorRole switch
        {
            "system" => null, // internal jobs bypass ACL — null = unrestricted

            "patient" =>
                await db.Set<ClinicalDocument>()
                    .Where(cd => cd.PatientId == actorId)
                    .Select(cd => cd.Id)
                    .ToListAsync(ct),

            "staff" =>
                await BuildStaffAuthorizedDocumentIdsAsync(actorId, ct),

            _ => [] // unknown roles get no access (fail-closed, OWASP A01)
        };
    }

    private async Task<IReadOnlyList<Guid>> BuildStaffAuthorizedDocumentIdsAsync(
        Guid staffId, CancellationToken ct)
    {
        // Branch A: explicit grants (document_access_grants.grantee_type = 'staff')
        var explicitGrantIds = await db.Set<DocumentAccessGrant>()
            .Where(g => g.GranteeId == staffId && g.GranteeType == "staff")
            .Select(g => g.DocumentId)
            .ToListAsync(ct);

        // Branch B: department-based join — staff.department = patient.department
        var departmentGrantIds = await db.Set<Staff>()
            .Where(s => s.Id == staffId && s.Department != null)
            .Join(db.Set<Patient>(),
                s => s.Department,
                p => p.Department,
                (s, p) => p.Id)
            .Join(db.Set<ClinicalDocument>(),
                patientId => patientId,
                cd => cd.PatientId,
                (patientId, cd) => cd.Id)
            .ToListAsync(ct);

        return explicitGrantIds.Union(departmentGrantIds).Distinct().ToList();
    }
}
```

### 6. `DocumentSearchService` — add actor-based `SearchAsync` overload

```csharp
// Add to DocumentSearchService (existing patientId overload is UNCHANGED)
public async Task<List<ChunkSearchResult>> SearchAsync(
    string queryText, Guid actorId, string actorRole, CancellationToken ct = default)
{
    var authorizedIds = await _ragAccessFilter.GetAuthorizedDocumentIdsAsync(actorId, actorRole, ct);

    if (authorizedIds is not null && authorizedIds.Count == 0)
    {
        // No authorized documents — audit log and return empty (AIR-S02)
        await _auditLogger.LogAsync(new AuditLogEntry
        {
            ActionType = "RagQueryUnauthorized",
            ActorId = actorId,
            ActorType = actorRole == "patient" ? AuditActorType.Patient : AuditActorType.Staff,
            TargetEntityId = actorId.ToString(),
            TargetEntityType = "RagQuery",
            Payload = JsonSerializer.Serialize(new { reason = "no_authorized_documents" })
        }, ct);
        return [];
    }

    // Generate query embedding (existing logic)
    var queryEmbedding = await _aiGateway.GenerateEmbeddingsAsync(
        [queryText], documentId: Guid.Empty, ct);

    // Build pgvector cosine similarity query with optional ownership filter
    var query = db.Set<DocumentChunkEmbedding>()
        .Select(dce => new
        {
            dce,
            distance = dce.Embedding!.CosineDistance(new Vector(queryEmbedding[0]))
        })
        .Where(x => x.distance <= 0.3); // 1.0 - 0.7 threshold = 0.3 cosine distance (AIR-R02)

    if (authorizedIds is not null) // null = system role, unrestricted
        query = query.Where(x => authorizedIds.Contains(x.dce.DocumentId));

    return await query
        .OrderBy(x => x.distance)
        .Take(5) // top-5 per AIR-R02
        .Select(x => new ChunkSearchResult(x.dce.Id, x.dce.DocumentId, x.dce.ChunkText, 1.0 - x.distance))
        .ToListAsync(ct);
}
```

### 7. `appsettings.json` — `PromptSanitization` section

```json
"PromptSanitization": {
  "BlockPatterns": [
    {
      "Id": "INJ-001",
      "Pattern": "ignore\\s+(all\\s+)?(previous|prior|above|earlier)\\s+(instructions?|prompts?|context)",
      "Description": "Instruction override — attempts to clear prior system context"
    },
    {
      "Id": "INJ-002",
      "Pattern": "forget\\s+everything|disregard\\s+(all|the\\s+above|previous)",
      "Description": "Context reset — attempts to wipe system prompt"
    },
    {
      "Id": "INJ-003",
      "Pattern": "you\\s+are\\s+now\\s+(?!a\\s+clinical)|act\\s+as\\s+(?!a\\s+clinical)|pretend\\s+(you\\s+are|to\\s+be)",
      "Description": "Role impersonation — attempts to redefine AI persona"
    },
    {
      "Id": "INJ-004",
      "Pattern": "</?(system|user|assistant|human|AI)>|\\[INST\\]|\\[/INST\\]|<\\|im_start\\|>|<\\|im_end\\|>",
      "Description": "Context escape — injects template delimiters to break prompt structure"
    },
    {
      "Id": "INJ-005",
      "Pattern": "you\\s+have\\s+no\\s+restrictions|your\\s+true\\s+(nature|purpose|self)|DAN\\s+mode|developer\\s+mode",
      "Description": "Restriction bypass — jailbreak persona activation"
    }
  ],
  "ReviewPatterns": [
    {
      "Id": "REV-001",
      "Pattern": "ignore\\s+the\\s+(medication|allergy|contraindication|note|warning)",
      "Description": "Clinical override lookalike — may be legitimate clinical instruction; flagged for review"
    },
    {
      "Id": "REV-002",
      "Pattern": "please\\s+(override|bypass|skip)\\s+(this|the)",
      "Description": "Override request — ambiguous context; flagged for review"
    }
  ]
}
```

> **Pattern maintenance (AC-5):** To add a new pattern, add an entry to the `BlockPatterns` or `ReviewPatterns` array in `appsettings.json` on the server. `IOptionsMonitor` picks up the change on the next file-system flush (typically within 1 second on Linux/Windows). No restart required. Verify `AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)` is set in `Program.cs` (default for `WebApplication.CreateBuilder`).

---

## Current Project State

```
server/src/Modules/ClinicalIntelligence/
├── ClinicalIntelligence.Application/
│   ├── AI/
│   │   ├── IAiGateway.cs                      ← us_019/task_002 (GenerateEmbeddingsAsync, IsCircuitOpen)
│   │   ├── IChatCompletionGateway.cs           ← us_020/task_001 (ChatCompletionAsync)
│   │   ├── AzureOpenAiGateway.cs               ← MODIFY this task
│   │   ├── Sanitization/                       ← CREATE all files this task
│   │   │   ├── IPromptSanitizer.cs
│   │   │   ├── PromptSanitizationResult.cs
│   │   │   ├── PromptInjectionOptions.cs
│   │   │   ├── PromptSanitizer.cs
│   │   │   └── PromptInjectionBlockedException.cs
│   │   └── Access/                             ← CREATE all files this task
│   │       ├── IRagAccessFilter.cs
│   │       └── RagAccessFilter.cs
│   ├── Documents/
│   │   ├── Jobs/
│   │   │   └── FactExtractionJob.cs            ← no change; uses existing patientId overload
│   │   └── Services/
│   │       ├── IDocumentSearchService.cs       ← MODIFY: add actor-based overload
│   │       ├── DocumentSearchService.cs        ← MODIFY: implement new overload
│   │       └── ContextAssembler.cs             ← no change
│   └── Utilities/
│       └── ContextAssembler.cs                 ← no change (BuildCodeContext overload from us_023)
└── ClinicalIntelligence.Presentation/
    └── ServiceCollectionExtensions.cs          ← MODIFY: register new services

server/src/PropelIQ.Api/
├── appsettings.json                            ← MODIFY: add PromptSanitization section
└── appsettings.Development.json               ← MODIFY: mirror + dev-only review pattern

server/src/Modules/PatientAccess/
└── PatientAccess.Data/
    └── Entities/
        └── DocumentAccessGrant.cs              ← created by task_002 (required by RagAccessFilter)
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Sanitization/IPromptSanitizer.cs` | Sanitizer contract: `PromptSanitizationResult Evaluate(string input)` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Sanitization/PromptSanitizationResult.cs` | `record(SanitizationVerdict, string? MatchedPatternId, string NormalizedInput)` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Sanitization/PromptInjectionOptions.cs` | Options POCO: `BlockPatterns` + `ReviewPatterns` lists of `InjectionPatternEntry` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Sanitization/PromptSanitizer.cs` | 5-layer pipeline using `IOptionsMonitor<PromptInjectionOptions>`; ReDoS timeout guard |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Sanitization/PromptInjectionBlockedException.cs` | `sealed class PromptInjectionBlockedException(string? patternId) : Exception` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Access/IRagAccessFilter.cs` | `Task<IReadOnlyList<Guid>?> GetAuthorizedDocumentIdsAsync(Guid, string, CancellationToken)` |
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/Access/RagAccessFilter.cs` | patient / staff (dept JOIN + explicit grants UNION) / system (null) routing |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/AI/AzureOpenAiGateway.cs` | Inject `IPromptSanitizer`; add pre-call `Evaluate()` in `ChatCompletionAsync`; handle Blocked (save audit + throw) and FlaggedForReview (stage audit + proceed with normalizedInput) |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Services/IDocumentSearchService.cs` | Add `Task<List<ChunkSearchResult>> SearchAsync(string, Guid actorId, string actorRole, CancellationToken)` overload |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Application/Documents/Services/DocumentSearchService.cs` | Implement new overload; call `IRagAccessFilter`; apply `document_id = ANY(:ids)` pre-filter or return empty |
| MODIFY | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Presentation/ServiceCollectionExtensions.cs` | `services.AddTransient<IPromptSanitizer, PromptSanitizer>()` + `services.AddScoped<IRagAccessFilter, RagAccessFilter>()` + `services.Configure<PromptInjectionOptions>(config.GetSection("PromptSanitization"))` |
| MODIFY | `server/src/PropelIQ.Api/appsettings.json` | Add `PromptSanitization` section with 5 BlockPatterns (INJ-001..005) + 2 ReviewPatterns (REV-001..002) |
| MODIFY | `server/src/PropelIQ.Api/appsettings.Development.json` | Mirror `PromptSanitization` section; add `REV-DEV-001` review pattern for developer test phrase |

---

## External References

- [OWASP LLM Top 10 — LLM01: Prompt Injection](https://genai.owasp.org/llmrisk/llm01-prompt-injection/)
- [NIST AI 100-1 — Adversarial ML](https://airc.nist.gov/Home)
- [Azure OpenAI Content Safety — Prompt Shield](https://learn.microsoft.com/en-us/azure/ai-services/content-safety/concepts/jailbreak-detection)
- [.NET IOptionsMonitor hot-reload docs](https://learn.microsoft.com/en-us/dotnet/core/extensions/options#ioptionsmonitor)
- [Pgvector.EntityFrameworkCore — cosine distance operator](https://github.com/pgvector/pgvector-dotnet)
- [OWASP A01 — Broken Access Control](https://owasp.org/Top10/A01_2021-Broken_Access_Control/)

---

## Build Commands

```powershell
# Restore + build
dotnet restore server/PropelIQ.slnx
dotnet build server/PropelIQ.slnx --no-restore

# Run unit tests (xUnit)
dotnet test server/PropelIQ.slnx --no-build --filter "Category=US029"
```

---

## Implementation Validation Strategy

- [ ] Unit tests pass (xUnit + Moq)
- [ ] **[AI Tasks]** Prompt templates validated — `PromptSanitizer` tested with 10 injection vectors per pattern (5 direct, 5 encoded variants)
- [ ] **[AI Tasks]** Guardrails tested: BlockPatterns return `Blocked`; ReviewPatterns return `FlaggedForReview`; clean clinical text returns `Safe`
- [ ] **[AI Tasks]** ReDoS guard tested: input with `(a+)+` lookalike throws `RegexMatchTimeoutException` → treated as `Safe` (not thrown to caller)
- [ ] **[AI Tasks]** Fallback tested: empty `BlockPatterns` list → all inputs return `Safe` (safe default)
- [ ] **[AI Tasks]** Token budget enforcement verified — existing `IAiGateway` enforcement unchanged
- [ ] **[AI Tasks]** Audit logging verified: `Blocked` verdict creates saved `AuditLog` row; `FlaggedForReview` stages (not saved) until caller `SaveChanges`
- [ ] **[AI Tasks]** RAG access: patient actor returns only own patient docs; staff actor with department match returns department-scoped docs; unknown role returns empty list

---

## Implementation Checklist

- [ ] CREATE `IPromptSanitizer`, `PromptSanitizationResult`, `SanitizationVerdict` enum, `PromptInjectionOptions`, `InjectionPatternEntry` (AIR-S01 interfaces and config contract)
- [ ] CREATE `PromptSanitizer` with 5-layer pipeline (Unicode NFC → URL decode → HTML decode → BlockPatterns → ReviewPatterns); include `RegexMatchTimeoutException` catch treating timeout as `Safe`; use `IOptionsMonitor.CurrentValue` for hot-reload (AC-5)
- [ ] CREATE `PromptInjectionBlockedException` (sealed, primary constructor with `string? patternId`; message = "Request blocked by content policy." — no pattern detail exposed to caller)
- [ ] CREATE `IRagAccessFilter` + `RagAccessFilter` (patient / staff UNION / system-null routing); fail-closed default (unknown role → empty list per OWASP A01)
- [ ] MODIFY `AzureOpenAiGateway.ChatCompletionAsync` — inject `IPromptSanitizer`, `IAuditLogger`, `IHttpContextAccessor`; call `Evaluate()` on raw `userMessage`; Blocked → save audit + throw; FlaggedForReview → stage audit + use `normalizedInput` downstream
- [ ] MODIFY `IDocumentSearchService` + `DocumentSearchService` — add `SearchAsync(queryText, actorId, actorRole, ct)` overload; existing `(queryText, patientId, ct)` overload MUST remain unchanged (no regression on internal jobs)
- [ ] MODIFY `ServiceCollectionExtensions.cs` — register `IPromptSanitizer` (transient), `IRagAccessFilter` (scoped), bind `PromptInjectionOptions` from `"PromptSanitization"` config section
- [ ] MODIFY `appsettings.json` + `appsettings.Development.json` — add `PromptSanitization` section with 5 BlockPatterns (INJ-001..005) and 2 ReviewPatterns (REV-001..002); test hot-reload by updating a pattern mid-run and confirming `IOptionsMonitor.CurrentValue` reflects the change
- [ ] **[AI Tasks - MANDATORY]** Implement and test guardrails before marking task complete
- [ ] **[AI Tasks - MANDATORY]** Verify AIR-S01 requirements are met: block + audit + safe error; AC-5 hot-reload confirmed
- [ ] **[AI Tasks - MANDATORY]** Verify AIR-S02 requirements are met: patient scoped to own docs; staff scoped to dept + explicit grants; system role unrestricted

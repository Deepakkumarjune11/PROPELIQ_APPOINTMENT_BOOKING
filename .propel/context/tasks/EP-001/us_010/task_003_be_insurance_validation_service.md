# Task - task_003_be_insurance_validation_service

## Requirement Reference

- **User Story**: US_010 — Patient Registration & Insurance Validation
- **Story Location**: `.propel/context/tasks/EP-001/us_010/us_010.md`
- **Acceptance Criteria**:
  - AC-2: The system validates insurance details against the internal dummy reference set and records the result as `pass`, `partial-match`, or `fail` without blocking appointment creation.
  - AC-3: A `partial-match` or `fail` result flags the appointment for staff follow-up; booking still completes.
- **Edge Cases**:
  - Insurance reference data service unavailable (any unhandled exception) → result recorded as `pending`; booking proceeds without disruption. The handler in task_002 wraps `ValidateAsync` in a try/catch for this reason.
  - Insurance provider is null / not provided → result is `pending` (no validation performed; member ID alone is insufficient for matching).

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
| Language | C# | 12 (.NET 8) |
| Configuration | Microsoft.Extensions.Configuration | 8.0 (built-in) |
| Logging | Serilog | 3.x |
| AI/ML | N/A | N/A |
| Mobile | N/A | N/A |

> All code and libraries MUST be compatible with versions above.

---

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |
| **AIR Requirements** | N/A |
| **AI Pattern** | N/A |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | N/A |
| **Model Provider** | N/A |

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

Implement the **Insurance Soft Validation Service** that evaluates a patient's insurance provider name and member ID against an **in-memory dummy reference set** loaded from `appsettings.json` at startup. The service returns one of four outcomes:

| Result | Condition |
|--------|-----------|
| `pass` | Both provider name **and** member ID match a reference entry exactly (case-insensitive). |
| `partial-match` | Provider name matches but member ID does not match any entry for that provider. |
| `fail` | Provider name does not match any known provider in the reference set. |
| `pending` | Provider or member ID is null/empty (validation skipped). Exception path also resolves here (task_002 handler wraps `ValidateAsync`). |

The service is registered as a **scoped** dependency in `PatientAccess.Presentation/ServiceCollectionExtensions.cs` and consumed by `RegisterForAppointmentHandler` (task_002).

**Why in-memory / config-backed (not a DB table)?** FR-009 explicitly calls this a "dummy reference set" — a lightweight fixture for development and testing that mimics the structure of a future real-time payer eligibility API. Loading from `appsettings.json` allows the reference data to be environment-specific and replaceable without schema migrations, satisfying KISS and YAGNI principles.

---

## Dependent Tasks

- None — this is a standalone service task. `task_002_be_patient_registration_api.md` depends on this task (consumes `IInsuranceValidationService`).

---

## Impacted Components

| Action | Module | Description |
|--------|--------|-------------|
| CREATE | `PatientAccess.Domain` | `Enums/InsuranceValidationStatus.cs` — domain enum |
| CREATE | `PatientAccess.Application` | `Services/IInsuranceValidationService.cs` — service interface |
| CREATE | `PatientAccess.Application` | `Services/InsuranceReferenceEntry.cs` — config-bound POCO |
| CREATE | `PatientAccess.Application` | `Services/InsuranceValidationService.cs` — in-memory matching logic |
| MODIFY | `PropelIQ.Api` | `appsettings.Development.json` — add `InsuranceReference` section |
| MODIFY | `PatientAccess.Presentation` | `ServiceCollectionExtensions.cs` — register service + options binding |

---

## Implementation Plan

1. **`InsuranceValidationStatus` enum** (`PatientAccess.Domain/Enums/InsuranceValidationStatus.cs`):
   ```csharp
   public enum InsuranceValidationStatus
   {
       Pass,
       PartialMatch,
       Fail,
       Pending
   }
   ```
   Provides a type-safe way for `RegisterForAppointmentHandler` to map to the string persisted in `Patient.InsuranceStatus`. Serialisation to lowercase hyphenated strings is handled at the handler (`.ToString().ToLower()` with `PartialMatch` → `"partial-match"` mapping).

2. **`InsuranceReferenceEntry` POCO** (`PatientAccess.Application/Services/InsuranceReferenceEntry.cs`):
   ```csharp
   public sealed class InsuranceReferenceEntry
   {
       public string ProviderName { get; set; } = string.Empty;   // e.g., "Blue Cross"
       public List<string> KnownMemberIdPrefixes { get; set; } = []; // e.g., ["BC-", "BCBS"]
   }
   ```
   Bound from `appsettings.json:InsuranceReference:Providers[]` using `IOptions<InsuranceReferenceOptions>`.

3. **`InsuranceReferenceOptions` options class** (`PatientAccess.Application/Services/InsuranceReferenceOptions.cs`):
   ```csharp
   public sealed class InsuranceReferenceOptions
   {
       public const string SectionName = "InsuranceReference";
       public List<InsuranceReferenceEntry> Providers { get; set; } = [];
   }
   ```

4. **`IInsuranceValidationService` interface** (`PatientAccess.Application/Services/IInsuranceValidationService.cs`):
   ```csharp
   public interface IInsuranceValidationService
   {
       Task<InsuranceValidationStatus> ValidateAsync(
           string? providerName,
           string? memberId,
           CancellationToken ct = default);
   }
   ```

5. **`InsuranceValidationService` implementation** — Inject `IOptions<InsuranceReferenceOptions>` and `ILogger<InsuranceValidationService>`. Matching logic:
   ```
   if (providerName is null or whitespace) → return Pending
   
   matched = _options.Providers.FirstOrDefault(
       p => p.ProviderName.Equals(providerName, OrdinalIgnoreCase))
   
   if (matched is null) → return Fail           // provider not in reference set
   
   if (memberId is null or whitespace) → return PartialMatch  // provider known, no ID to check
   
   idMatches = matched.KnownMemberIdPrefixes.Any(
       prefix => memberId.StartsWith(prefix, OrdinalIgnoreCase))
   
   return idMatches ? Pass : PartialMatch
   ```
   Log structured event: `"InsuranceValidation: provider={Provider} memberId=<REDACTED> result={Result}"` — member ID is NEVER logged (PHI per HIPAA, DR-015, AIR-S01 analogous principle).

6. **`appsettings.Development.json` — dummy reference data**:
   ```json
   "InsuranceReference": {
     "Providers": [
       { "ProviderName": "Blue Cross", "KnownMemberIdPrefixes": ["BC-", "BCBS-", "BX"] },
       { "ProviderName": "Aetna",      "KnownMemberIdPrefixes": ["AET-", "ATN"] },
       { "ProviderName": "Cigna",      "KnownMemberIdPrefixes": ["CIG-", "CGN"] },
       { "ProviderName": "UnitedHealth","KnownMemberIdPrefixes": ["UHC-", "UNH"] },
       { "ProviderName": "Other",      "KnownMemberIdPrefixes": [] }
     ]
   }
   ```
   `"Other"` is intentionally present with no prefixes → any member ID input for `"Other"` yields `PartialMatch`.

7. **`ServiceCollectionExtensions` registration**:
   ```csharp
   services.Configure<InsuranceReferenceOptions>(
       configuration.GetSection(InsuranceReferenceOptions.SectionName));
   services.AddScoped<IInsuranceValidationService, InsuranceValidationService>();
   ```
   Update the method signature to accept `IConfiguration configuration` so the options binding can be performed in the module registration (the existing `AddPatientAccessModule()` in `Program.cs` needs the configuration object passed through).

---

## Current Project State

```
server/src/
  PropelIQ.Api/
    appsettings.Development.json        ← MODIFY: add InsuranceReference section
  Modules/PatientAccess/
    PatientAccess.Domain/
      Enums/
        AppointmentStatus.cs
        AuditActionType.cs
        (InsuranceValidationStatus.cs does not exist yet — CREATE)
    PatientAccess.Application/
      Infrastructure/
        ICacheService.cs
      Services/                         ← Does not exist yet — CREATE folder
      Queries/GetAvailability/          ← Created in us_009/task_003
    PatientAccess.Presentation/
      ServiceCollectionExtensions.cs    ← MODIFY: register options + service
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Domain/Enums/InsuranceValidationStatus.cs` | Domain enum: Pass, PartialMatch, Fail, Pending |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Services/IInsuranceValidationService.cs` | Service interface in Application layer |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Services/InsuranceReferenceEntry.cs` | Config-bound POCO for a single provider reference entry |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Services/InsuranceReferenceOptions.cs` | IOptions-bound options class, SectionName = "InsuranceReference" |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Services/InsuranceValidationService.cs` | In-memory matching: pass / partial-match / fail / pending; PHI-safe logging |
| MODIFY | `server/src/PropelIQ.Api/appsettings.Development.json` | Add `InsuranceReference.Providers` array with dummy reference entries |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Presentation/ServiceCollectionExtensions.cs` | Register `InsuranceReferenceOptions` + `IInsuranceValidationService` → `InsuranceValidationService` |

---

## External References

- [Microsoft.Extensions.Options — IOptions binding](https://learn.microsoft.com/en-us/dotnet/core/extensions/options)
- [ASP.NET Core 8 — Configuration sections and binding](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-8.0)
- [Serilog structured logging](https://serilog.net/)
- [HIPAA PHI safe logging — do not log member IDs or patient identifiers](https://www.hhs.gov/hipaa/for-professionals/privacy/index.html)
- [OWASP A02 — Cryptographic Failures: never log PHI in plaintext](https://owasp.org/Top10/A02_2021-Cryptographic_Failures/)

---

## Build Commands

```bash
# From server/ — restore packages
dotnet restore

# Build solution (verify no compile errors after new files)
dotnet build PropelIQ.slnx

# Run API (development) — Insurance reference data loaded from appsettings.Development.json
dotnet run --project src/PropelIQ.Api/PropelIQ.Api.csproj
```

---

## Implementation Validation Strategy

- [ ] Unit tests pass — `"Blue Cross"` + `"BC-123"` → `Pass`
- [ ] Unit tests pass — `"Blue Cross"` + `"XYZ-999"` → `PartialMatch`
- [ ] Unit tests pass — `"UnknownPlan"` + any member ID → `Fail`
- [ ] Unit tests pass — `null` provider → `Pending`
- [ ] Unit tests pass — `"Blue Cross"` + null member ID → `PartialMatch`
- [ ] Unit tests pass — `"Other"` + any member ID → `PartialMatch` (empty prefix list)
- [ ] Matching is case-insensitive: `"blue cross"` matches `"Blue Cross"` reference entry
- [ ] `ILogger` output never contains raw member ID value (PHI logging guard)
- [ ] `InsuranceReferenceOptions` is populated correctly from `appsettings.Development.json` section
- [ ] `IInsuranceValidationService` is resolvable from DI container after `AddPatientAccessModule()` registration

---

## Implementation Checklist

- [X] Create `InsuranceValidationStatus.cs` enum in `PatientAccess.Domain/Enums/` with `Pass, PartialMatch, Fail, Pending`
- [X] Create `InsuranceReferenceEntry.cs` POCO with `ProviderName` + `KnownMemberIdPrefixes` list
- [X] Create `InsuranceReferenceOptions.cs` with `SectionName = "InsuranceReference"` and `Providers` list
- [X] Create `IInsuranceValidationService.cs` interface in `PatientAccess.Application/Services/`
- [X] Create `InsuranceValidationService.cs` — implement `ValidateAsync` with `Pass/PartialMatch/Fail/Pending` logic; log provider + result only (never log memberId)
- [X] Add dummy reference data to `appsettings.Development.json` under `InsuranceReference.Providers`
- [X] Update `ServiceCollectionExtensions.cs` to accept `IConfiguration`, bind `InsuranceReferenceOptions`, and register `IInsuranceValidationService` → `InsuranceValidationService` as scoped
- [X] Update `Program.cs` call to `AddPatientAccessModule()` to pass `builder.Configuration` (if signature changes)
- [X] Confirm `dotnet build` passes with zero errors after all files created

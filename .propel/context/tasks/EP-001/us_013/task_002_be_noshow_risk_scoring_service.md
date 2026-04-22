# Task - task_002_be_noshow_risk_scoring_service

## Requirement Reference

- **User Story**: US_013 — No-Show Risk Scoring & Booking Transaction
- **Story Location**: `.propel/context/tasks/EP-001/us_013/us_013.md`
- **Acceptance Criteria**:
  - AC-1: The system calculates a no-show risk score using configurable scheduling signals (time-to-appointment, day-of-week) and patient-response signals (insurance status, intake completion) per FR-006.
- **Edge Cases**:
  - When risk scoring signals are incomplete (e.g., insurance status not yet available), the service applies available signals only and sets `IsPartialScoring = true` in the result — consumer is responsible for noting this in metadata or logs.

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
| ORM | Entity Framework Core | 8.0 (for GetAvailabilityHandler update only) |
| Language | C# | 12 |
| Logging | Serilog | 3.x |
| Configuration | Microsoft.Extensions.Options | .NET 8 built-in |

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

Implement the **No-Show Risk Scoring Service** — a rule-based, deterministic calculation engine (FR-006) within the `PatientAccess` bounded context. The service has two call modes:

1. **Scheduling-only** (`CalculateSchedulingRisk`) — uses `slotDatetime` only; no patient data required. Called from `GetAvailabilityQueryHandler` to enrich each slot in the availability response with contributing factors and a pre-score. Returns `IsPartialScoring = true` because patient-response signals are absent.

2. **Full-signal** (`CalculateFullRisk`) — uses `slotDatetime` + `InsuranceValidationStatus` + `intakeCompleted` bool. Called from `RegisterForAppointmentHandler` (task_003) at booking commit time. Returns the final score persisted on the `Appointment` record.

**Scoring formula** (weighted sum, configurable via `NoShowRiskOptions`):

```
score = Σ(signal_contribution × weight) / Σ(active_weights)

Signals:
  DaysToAppointment:
    ≤1 day  → contribution = 1.0  (same/next day, high risk)
    ≤3 days → contribution = 0.6
    ≤7 days → contribution = 0.3
    >7 days → contribution = 0.1  (low urgency, lower risk)

  DayOfWeek (slot datetime):
    Monday, Friday → contribution = 0.6   (high-absenteeism days)
    Saturday       → contribution = 0.8
    Other days     → contribution = 0.2

  InsuranceStatus (patient-response signal):
    Fail           → contribution = 0.9
    PartialMatch   → contribution = 0.5
    Pass           → contribution = 0.1
    Pending        → contribution = 0.4   (unknown treated as moderate risk)

  IntakeCompleted (patient-response signal):
    false          → contribution = 0.7   (incomplete intake correlates with disengagement)
    true           → contribution = 0.1
```

Default weights in `NoShowRiskOptions`: DaysToAppointment=0.4, DayOfWeek=0.2, InsuranceStatus=0.25, IntakeCompleted=0.15. All configurable from `appsettings.json`.

**Contributing factors list** is built as a human-readable `List<string>` for tooltip display:
- "Appointment in {days} day(s) — {level} risk"
- "Booked on a {DayName} — {level} risk day"
- "Insurance status: {status} — {level} risk contribution"  _(only in full-signal mode)_
- "Intake not yet completed — elevated risk"  _(only in full-signal mode)_

When `IsPartialScoring = true`, consumers append "Partial scoring — some signals unavailable" to the factors list for display.

This service is **deterministic and pure** — no I/O, no database calls, no async. It is safe to register as `Singleton` and is straightforward to unit test.

---

## Dependent Tasks

- **task_003_be_atomic_booking_transaction.md** (US_013) — consumes `INoShowRiskScoringService.CalculateFullRisk` inside `RegisterForAppointmentHandler`.
- **task_003_be_availability_api.md** (US_009) — `GetAvailabilityQueryHandler` is updated in this task to call `CalculateSchedulingRisk` per slot; the handler must already exist.

---

## Impacted Components

| Action | Module | Description |
|--------|--------|-------------|
| CREATE | `PatientAccess.Application` | `Services/INoShowRiskScoringService.cs` — two-overload interface |
| CREATE | `PatientAccess.Application` | `Services/NoShowRiskScoringService.cs` — deterministic rule implementation |
| CREATE | `PatientAccess.Application` | `Services/NoShowRiskResult.cs` — result record: Score, ContributingFactors, IsPartialScoring |
| CREATE | `PatientAccess.Application` | `Configuration/NoShowRiskOptions.cs` — configurable weights and signal thresholds |
| MODIFY | `PatientAccess.Application` | `Queries/GetAvailability/GetAvailabilityResponse.cs` — add `RiskContributingFactors` and `IsPartialScoring` to `SlotDto` |
| MODIFY | `PatientAccess.Application` | `Queries/GetAvailability/GetAvailabilityHandler.cs` — call `CalculateSchedulingRisk` per slot; populate new DTO fields |
| MODIFY | `PatientAccess.Presentation` | `ServiceCollectionExtensions.cs` — bind `NoShowRiskOptions`; register `INoShowRiskScoringService` as singleton |
| MODIFY | `server/src/PropelIQ.Api` | `appsettings.json` — add `NoShowRisk` section with default weights |

---

## Implementation Plan

1. **`NoShowRiskOptions`** (`PatientAccess.Application/Configuration/NoShowRiskOptions.cs`):
   ```csharp
   public sealed class NoShowRiskOptions
   {
       public const string SectionName = "NoShowRisk";

       // Signal weights (must sum to 1.0; validated on startup)
       public double DaysToAppointmentWeight { get; init; } = 0.40;
       public double DayOfWeekWeight { get; init; } = 0.20;
       public double InsuranceStatusWeight { get; init; } = 0.25;
       public double IntakeCompletedWeight { get; init; } = 0.15;
   }
   ```

2. **`NoShowRiskResult`** record:
   ```csharp
   public sealed record NoShowRiskResult(
       decimal Score,                        // 0.0000–1.0000
       IReadOnlyList<string> ContributingFactors,
       bool IsPartialScoring                 // true when patient-response signals not provided
   );
   ```

3. **`INoShowRiskScoringService`**:
   ```csharp
   public interface INoShowRiskScoringService
   {
       /// <summary>Scheduling signals only — no patient data. IsPartialScoring will be true.</summary>
       NoShowRiskResult CalculateSchedulingRisk(DateTime slotDatetime);

       /// <summary>Full signal calculation — includes patient-response signals.</summary>
       NoShowRiskResult CalculateFullRisk(
           DateTime slotDatetime,
           InsuranceValidationStatus insuranceStatus,
           bool intakeCompleted);
   }
   ```

4. **`NoShowRiskScoringService`** implementation steps:
   ```
   CalculateSchedulingRisk(slotDatetime):
     daysResult   = ScoreDaysToAppointment(slotDatetime, opts.DaysToAppointmentWeight)
     dowResult    = ScoreDayOfWeek(slotDatetime, opts.DayOfWeekWeight)
     activeWeight = opts.DaysToAppointmentWeight + opts.DayOfWeekWeight
     score        = ((daysResult.contribution × daysResult.weight) + (dowResult.contribution × dowResult.weight)) / activeWeight
     factors      = [daysResult.description, dowResult.description]
     return new NoShowRiskResult(Math.Round((decimal)score, 4), factors, IsPartialScoring: true)

   CalculateFullRisk(slotDatetime, insuranceStatus, intakeCompleted):
     daysResult    = ScoreDaysToAppointment(slotDatetime, opts.DaysToAppointmentWeight)
     dowResult     = ScoreDayOfWeek(slotDatetime, opts.DayOfWeekWeight)
     insResult     = ScoreInsuranceStatus(insuranceStatus, opts.InsuranceStatusWeight)
     intakeResult  = ScoreIntakeCompleted(intakeCompleted, opts.IntakeCompletedWeight)
     score         = daysResult.weighted + dowResult.weighted + insResult.weighted + intakeResult.weighted
                     (sum of contribution × weight for all signals)
     factors       = [daysResult.description, dowResult.description, insResult.description, intakeResult.description]
     return new NoShowRiskResult(Math.Round((decimal)score, 4), factors, IsPartialScoring: false)
   ```
   Each `Score*` helper is a private method returning `(double contribution, double weight, string description)`.

5. **Update `GetAvailabilityHandler`** — add `INoShowRiskScoringService` constructor injection; for each slot projected into the response DTO, call `CalculateSchedulingRisk(slot.SlotDatetime)` and map `result.Score → NoShowRiskScore` (only if slot has no pre-stored score, otherwise use stored), `result.ContributingFactors → RiskContributingFactors`, `result.IsPartialScoring → IsPartialScoring`.

6. **Update `SlotDto`** in `GetAvailabilityResponse.cs`:
   ```csharp
   public sealed record SlotDto(
       Guid Id,
       DateTime SlotDatetime,
       decimal? NoShowRiskScore,
       IReadOnlyList<string> RiskContributingFactors,  // NEW
       bool IsPartialScoring                           // NEW
   );
   ```

7. **DI + configuration** in `ServiceCollectionExtensions.cs`:
   ```csharp
   services.Configure<NoShowRiskOptions>(configuration.GetSection(NoShowRiskOptions.SectionName));
   services.AddSingleton<INoShowRiskScoringService, NoShowRiskScoringService>();
   ```

8. **`appsettings.json`** — add `NoShowRisk` section:
   ```json
   "NoShowRisk": {
     "DaysToAppointmentWeight": 0.40,
     "DayOfWeekWeight": 0.20,
     "InsuranceStatusWeight": 0.25,
     "IntakeCompletedWeight": 0.15
   }
   ```

---

## Current Project State

```
server/src/Modules/PatientAccess/
  PatientAccess.Application/
    Configuration/                          ← Does not exist yet — CREATE
    Services/                               ← IConversationalIntakeService exists (us_012)
    Queries/
      GetAvailability/
        GetAvailabilityHandler.cs           ← Created in us_009/task_003 — MODIFY
        GetAvailabilityResponse.cs          ← Created in us_009/task_003 — MODIFY (add SlotDto fields)
  PatientAccess.Domain/
    Enums/
      InsuranceValidationStatus.cs          ← Created in us_010/task_003 (Pass/PartialMatch/Fail/Pending)
  PatientAccess.Presentation/
    ServiceCollectionExtensions.cs          ← MODIFY: bind options + register singleton

server/src/PropelIQ.Api/
  appsettings.json                          ← MODIFY: add NoShowRisk section
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Configuration/NoShowRiskOptions.cs` | Configurable weights for 4 scoring signals; section name constant |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Services/NoShowRiskResult.cs` | Result record: Score, ContributingFactors, IsPartialScoring |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Services/INoShowRiskScoringService.cs` | Interface with `CalculateSchedulingRisk` and `CalculateFullRisk` |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Services/NoShowRiskScoringService.cs` | Deterministic implementation of both overloads with configurable weights |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Application/Queries/GetAvailability/GetAvailabilityResponse.cs` | Add `RiskContributingFactors` and `IsPartialScoring` to `SlotDto` |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Application/Queries/GetAvailability/GetAvailabilityHandler.cs` | Inject `INoShowRiskScoringService`; call `CalculateSchedulingRisk` per slot; populate new DTO fields |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Presentation/ServiceCollectionExtensions.cs` | `Configure<NoShowRiskOptions>` + `AddSingleton<INoShowRiskScoringService>` |
| MODIFY | `server/src/PropelIQ.Api/appsettings.json` | Add `NoShowRisk` config section with default weights |

---

## External References

- [FR-006 — Rule-based no-show risk scoring with configurable signals](`.propel/context/docs/spec.md#FR-006`)
- [Microsoft.Extensions.Options — `IOptions<T>` binding](https://learn.microsoft.com/en-us/dotnet/core/extensions/options)
- [InsuranceValidationStatus — defined in US_010/task_003](`.propel/context/tasks/EP-001/us_010/task_003_be_insurance_validation_service.md`)

---

## Build Commands

```bash
# From server/
dotnet restore
dotnet build PropelIQ.slnx

# No migration required — this is a pure application-layer service with no DB schema changes
```

---

## Implementation Validation Strategy

- [ ] Unit test — `CalculateSchedulingRisk` with slot 1 day away on a Friday: score > 0.7, `IsPartialScoring = true`, contributing factors non-empty
- [ ] Unit test — `CalculateSchedulingRisk` with slot 14 days away on a Wednesday: score < 0.4, `IsPartialScoring = true`
- [ ] Unit test — `CalculateFullRisk` with insurance=Fail, intakeCompleted=false, 2 days away: score > 0.7, `IsPartialScoring = false`
- [ ] Unit test — `CalculateFullRisk` with insurance=Pass, intakeCompleted=true, 14 days away: score < 0.3
- [ ] Unit test — `ContributingFactors` list length = 2 for scheduling-only, 4 for full-signal
- [ ] `GetAvailabilityHandler` integration test — returned `SlotDto` includes `RiskContributingFactors` (non-empty) and `IsPartialScoring = true`
- [ ] `dotnet build` passes with zero errors after all changes
- [ ] Default weights in `NoShowRiskOptions` sum to 1.0 (validated in startup or unit test)

---

## Implementation Checklist

- [x] Create `NoShowRiskOptions.cs` (`Configuration/`) — 4 weight properties with defaults summing to 1.0, `SectionName = "NoShowRisk"`
- [x] Create `NoShowRiskResult.cs` (`Services/`) — immutable record with Score, ContributingFactors, IsPartialScoring
- [x] Create `INoShowRiskScoringService.cs` (`Services/`) — `CalculateSchedulingRisk(DateTime)` + `CalculateFullRisk(DateTime, InsuranceValidationResult, bool)` (uses `InsuranceValidationResult` — the actual enum in the codebase)
- [x] Create `NoShowRiskScoringService.cs` (`Services/`) — implements both overloads; private helpers for each signal; builds human-readable `ContributingFactors` strings; `Math.Round(decimal, 4)` on score
- [x] Modify `AvailabilitySlotDto.cs` — added `RiskContributingFactors: IReadOnlyList<string>` + `IsPartialScoring: bool` positional parameters
- [x] Modify `GetAvailabilityHandler.cs` — injected `INoShowRiskScoringService`; calls `CalculateSchedulingRisk` per slot projection; coalesces stored `NoShowRiskScore ?? risk.Score`; maps ContributingFactors and IsPartialScoring to DTO
- [x] Modify `ServiceCollectionExtensions.cs` — added `Configure<NoShowRiskOptions>` + `AddSingleton<INoShowRiskScoringService, NoShowRiskScoringService>()`
- [x] Modify `appsettings.json` — added `NoShowRisk` section with default weights matching code defaults (sum = 1.0)
- [x] `dotnet build PropelIQ.slnx` → Build succeeded, 0 Error(s)

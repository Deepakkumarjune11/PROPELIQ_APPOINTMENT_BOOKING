# Task - task_003_be_atomic_booking_transaction

## Requirement Reference

- **User Story**: US_013 — No-Show Risk Scoring & Booking Transaction
- **Story Location**: `.propel/context/tasks/EP-001/us_013/us_013.md`
- **Acceptance Criteria**:
  - AC-1: When the appointment transaction is initiated, the system calculates a no-show risk score using all available signals (scheduling + patient-response) and persists it on the `Appointment` record.
  - AC-3: The patient record, appointment, insurance validation, and no-show score are committed atomically in a single database transaction.
  - AC-4: When the slot was claimed by another patient between selection and booking, the system returns `409 Conflict` and the UI reverts to slot selection.
- **Edge Cases**:
  - Concurrent booking attempts for the same slot → PostgreSQL `xmin`-based optimistic concurrency token on `Appointment`; EF Core raises `DbUpdateConcurrencyException`; handler converts to `409 Conflict`.
  - Risk scoring signals incomplete at booking time (intake not yet done) → `INoShowRiskScoringService.CalculateFullRisk` called with `intakeCompleted = false`; `IsPartialScoring = true` logged at Warning; score still persisted (partial scoring is acceptable per US_013 edge case).

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
| ORM | Entity Framework Core | 8.0 |
| Database | PostgreSQL | 15.x |
| Logging | Serilog | 3.x |
| API Docs | Swagger / OpenAPI | 6.x |
| CQRS | MediatR | 12.x |

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

Extend the **`RegisterForAppointmentHandler`** (created in US_010/task_002) to satisfy three US_013 requirements:

1. **No-show risk scoring in the booking transaction (AC-1, AC-3)**: Inject `INoShowRiskScoringService` into the handler. After the patient upsert and insurance validation (already present), call `CalculateFullRisk(slot.SlotDatetime, patient.InsuranceStatus, intakeCompleted: false)`. Assign the returned `score` to `appointment.NoShowRiskScore`. This is done within the same `SaveChangesAsync` call as the patient upsert and slot status transition — satisfying the atomicity requirement of AC-3.

2. **Concurrent booking protection (AC-4)**: Enable PostgreSQL `xmin`-based optimistic concurrency on the `Appointment` entity using EF Core's `UseXminAsConcurrencyToken()` fluent API. When two requests concurrently try to write the same slot, EF Core's change tracker detects the stale `xmin` and throws `DbUpdateConcurrencyException`. The handler catches this and throws `SlotAlreadyBookedException`, which the controller converts to `409 Conflict`.

3. **Enriched response**: `RegisterForAppointmentResponse` is extended to return `NoShowRiskScore`, `IsHighRisk` (`score > 0.70m`), and `ContributingFactors` so the FE can display the badge on the booking confirmation screen.

**Why `xmin` (optimistic) over `SELECT FOR UPDATE` (pessimistic)?**  
`UseXminAsConcurrencyToken()` is idiomatic with EF Core + PostgreSQL (Npgsql provider). It avoids holding a long-lived row lock across the multi-step booking handler (patient lookup, insurance call, scoring). A concurrent second request will fail at `SaveChangesAsync` with no deadlock risk. For the expected booking concurrency (hundreds of users, not thousands), this is the appropriate and simpler approach.

**`intakeCompleted = false` at booking time**  
In the booking flow, `RegisterForAppointmentCommand` is invoked at step 3 (Patient Details / SCR-003). Intake is completed at step 4 (SCR-004/SCR-005). Therefore `intakeCompleted = false` is always passed at booking time. The service sets `IsPartialScoring = true`; a Serilog structured warning is emitted with `[PartialScoring]` tag (no PHI — only appointment ID and signal count). The score is still persisted; clinic staff can interpret the badge knowing intake signals were absent.

---

## Dependent Tasks

- **task_002_be_noshow_risk_scoring_service.md** (US_013) — `INoShowRiskScoringService` and `NoShowRiskResult` must be registered and available before this handler extension is compiled.
- **task_002_be_patient_registration_api.md** (US_010) — `RegisterForAppointmentHandler`, `RegisterForAppointmentCommand`, `RegisterForAppointmentResponse` must exist (DELTA update — not a new file).
- **task_003_be_insurance_validation_service.md** (US_010) — `InsuranceValidationStatus` enum used as input to `CalculateFullRisk`.

---

## Impacted Components

| Action | Module | Description |
|--------|--------|-------------|
| MODIFY | `PatientAccess.Data` | `Configurations/AppointmentConfiguration.cs` — add `UseXminAsConcurrencyToken()` |
| CREATE | EF Core Migration | `AddAppointmentConcurrencyToken` — applies xmin tracking (no schema change; Npgsql handles xmin at driver level) |
| CREATE | `PatientAccess.Application` | `Exceptions/SlotAlreadyBookedException.cs` — custom exception for concurrent booking conflict |
| MODIFY | `PatientAccess.Application` | `Commands/RegisterForAppointment/RegisterForAppointmentHandler.cs` — inject `INoShowRiskScoringService`; call `CalculateFullRisk`; set `NoShowRiskScore`; catch `DbUpdateConcurrencyException` → throw `SlotAlreadyBookedException`; log partial scoring warning |
| MODIFY | `PatientAccess.Application` | `Commands/RegisterForAppointment/RegisterForAppointmentResponse.cs` — add `NoShowRiskScore`, `IsHighRisk`, `ContributingFactors` fields |
| MODIFY | `PatientAccess.Presentation` | `Controllers/AppointmentsController.cs` — handle `SlotAlreadyBookedException` → return `409 Conflict` with problem details |

---

## Implementation Plan

1. **`AppointmentConfiguration.cs`** — add `UseXminAsConcurrencyToken()`:
   ```csharp
   // Optimistic concurrency using PostgreSQL system column xmin.
   // EF Core will include xmin in WHERE clause of UPDATE statements.
   // Concurrent modification throws DbUpdateConcurrencyException → 409 Conflict.
   builder.UseXminAsConcurrencyToken();
   ```
   Note: This requires Npgsql EF Core provider 8.x (already in use). No new DB column is created — `xmin` is a PostgreSQL system column automatically maintained.

2. **EF Core Migration `AddAppointmentConcurrencyToken`**:
   ```bash
   dotnet ef migrations add AddAppointmentConcurrencyToken \
     --project src/Modules/PatientAccess/PatientAccess.Data \
     --startup-project src/PropelIQ.Api
   ```
   The generated migration file will have an empty `Up()` / `Down()` body (xmin requires no DDL change) but registers the concurrency token in the EF model snapshot. Apply with `dotnet ef database update`.

3. **`SlotAlreadyBookedException`**:
   ```csharp
   public sealed class SlotAlreadyBookedException : Exception
   {
       public SlotAlreadyBookedException(Guid slotId)
           : base($"Slot {slotId} is no longer available.") { }
   }
   ```

4. **`RegisterForAppointmentResponse`** — add fields:
   ```csharp
   public sealed record RegisterForAppointmentResponse(
       Guid PatientId,
       InsuranceValidationStatus InsuranceStatus,
       decimal? NoShowRiskScore,          // NEW
       bool IsHighRisk,                   // NEW — true when score > 0.70
       IReadOnlyList<string> ContributingFactors  // NEW — for badge tooltip
   );
   ```

5. **`RegisterForAppointmentHandler`** changes:
   ```
   // Inject INoShowRiskScoringService _riskScoring in constructor (via IOptions already wired)

   // After insurance validation (existing code):

   a. Calculate no-show risk:
      riskResult = _riskScoring.CalculateFullRisk(
          slot.SlotDatetime,
          patient.InsuranceStatus,
          intakeCompleted: false)   // intake not yet completed at booking time

   b. If riskResult.IsPartialScoring:
      _logger.LogWarning(
          "[PartialScoring] Appointment {AppointmentId} scored with partial signals.",
          appointment.Id)
      // NOTE: AppointmentId only — no PHI logged

   c. Set risk score on appointment:
      appointment.NoShowRiskScore = riskResult.Score

   // The existing SaveChangesAsync() call now also persists NoShowRiskScore
   // (same transaction as patient upsert, slot status change, and insurance update)

   d. Wrap SaveChangesAsync in try/catch:
      try {
          await _db.SaveChangesAsync(ct)
      } catch (DbUpdateConcurrencyException) {
          throw new SlotAlreadyBookedException(command.SlotId)
      }

   e. Return updated response:
      return new RegisterForAppointmentResponse(
          patient.Id,
          patient.InsuranceStatus,
          riskResult.Score,
          riskResult.Score > 0.70m,
          riskResult.ContributingFactors)
   ```

6. **`AppointmentsController`** — add exception handler for `SlotAlreadyBookedException`:
   ```csharp
   catch (SlotAlreadyBookedException ex)
   {
       return Conflict(new ProblemDetails
       {
           Title = "Slot no longer available.",
           Detail = ex.Message,
           Status = StatusCodes.Status409Conflict
       });
   }
   ```

---

## Current Project State

```
server/src/Modules/PatientAccess/
  PatientAccess.Application/
    Commands/
      RegisterForAppointment/
        RegisterForAppointmentCommand.cs     ← Created us_010/task_002
        RegisterForAppointmentHandler.cs     ← Created us_010/task_002 — MODIFY (add scoring + concurrency)
        RegisterForAppointmentResponse.cs    ← Created us_010/task_002 — MODIFY (add risk fields)
    Exceptions/                              ← Does not exist yet — CREATE
    Services/
      INoShowRiskScoringService.cs           ← Created us_013/task_002
      NoShowRiskScoringService.cs            ← Created us_013/task_002
  PatientAccess.Data/
    Configurations/
      AppointmentConfiguration.cs           ← MODIFY: add UseXminAsConcurrencyToken()
  PatientAccess.Presentation/
    Controllers/
      AppointmentsController.cs             ← MODIFY: handle SlotAlreadyBookedException → 409
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Data/Configurations/AppointmentConfiguration.cs` | Add `builder.UseXminAsConcurrencyToken()` |
| CREATE | EF Migration | `AddAppointmentConcurrencyToken` — empty DDL, registers xmin token in EF model snapshot |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Exceptions/SlotAlreadyBookedException.cs` | Custom exception with slot ID |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Application/Commands/RegisterForAppointment/RegisterForAppointmentResponse.cs` | Add `NoShowRiskScore`, `IsHighRisk`, `ContributingFactors` to response record |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Application/Commands/RegisterForAppointment/RegisterForAppointmentHandler.cs` | Inject `INoShowRiskScoringService`; call `CalculateFullRisk`; set `NoShowRiskScore`; wrap `SaveChangesAsync` in `DbUpdateConcurrencyException` catch; log partial scoring warning |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Presentation/Controllers/AppointmentsController.cs` | Add `catch (SlotAlreadyBookedException)` → return `409 Conflict` with `ProblemDetails` |

---

## External References

- [Npgsql EF Core — xmin as concurrency token (`UseXminAsConcurrencyToken`)](https://www.npgsql.org/efcore/modeling/concurrency.html)
- [EF Core 8 — `DbUpdateConcurrencyException` handling](https://learn.microsoft.com/en-us/ef/core/saving/concurrency)
- [ASP.NET Core — `ProblemDetails` for RFC 7807 error responses](https://learn.microsoft.com/en-us/aspnet/core/web-api/handle-errors?view=aspnetcore-8.0#problem-details)
- [FR-002 — Create appointment record in one transaction](`.propel/context/docs/spec.md#FR-002`)
- [FR-006 — Rule-based no-show risk scoring during booking](`.propel/context/docs/spec.md#FR-006`)

---

## Build Commands

```bash
# From server/
dotnet restore
dotnet build PropelIQ.slnx

# Generate migration for xmin concurrency token
dotnet ef migrations add AddAppointmentConcurrencyToken \
  --project src/Modules/PatientAccess/PatientAccess.Data \
  --startup-project src/PropelIQ.Api

# Apply migration (development)
dotnet ef database update \
  --project src/Modules/PatientAccess/PatientAccess.Data \
  --startup-project src/PropelIQ.Api
```

---

## Implementation Validation Strategy

- [ ] Unit test — handler returns `NoShowRiskScore`, `IsHighRisk=true` when scoring service returns score > 0.70
- [ ] Unit test — handler returns `IsHighRisk=false` when scoring service returns score <= 0.70
- [ ] Unit test — `DbUpdateConcurrencyException` from `SaveChangesAsync` is caught and re-thrown as `SlotAlreadyBookedException`
- [ ] Unit test — `SlotAlreadyBookedException` contains the `slotId` in its message
- [ ] Integration test — concurrent `POST /api/v1/appointments/{slotId}/register` calls: first returns `200`, second returns `409 Conflict` with `ProblemDetails` body
- [ ] Integration test — `Appointment.NoShowRiskScore` is persisted in DB after successful booking
- [ ] Serilog warning log with `[PartialScoring]` tag emitted when `IsPartialScoring = true`; log message contains only `AppointmentId` (no PHI)
- [ ] `UseXminAsConcurrencyToken()` added to `AppointmentConfiguration`; migration generated successfully
- [ ] `dotnet build` passes with zero errors after all changes

---

## Implementation Checklist

- [x] Modify `AppointmentConfiguration.cs` — added `builder.Property<uint>("xmin").HasColumnType("xid").IsRowVersion()` (non-obsolete Npgsql 8.x approach; `UseXminAsConcurrencyToken()` is obsolete and `TreatWarningsAsErrors=true`)
- [x] Run `dotnet ef migrations add AddAppointmentConcurrencyToken` — migration `20260419151325_AddAppointmentConcurrencyToken` generated; adds xmin shadow property to EF model snapshot
- [x] Create `SlotAlreadyBookedException.cs` in `PatientAccess.Application/Exceptions/` — contains `SlotId` property
- [x] Modify `IAppointmentRegistrationRepository.cs` — added `GetSlotDatetimeAsync(Guid, ct)` + `noShowRiskScore: decimal?` param to `RegisterAsync`; documented `SlotAlreadyBookedException` on interface
- [x] Modify `AppointmentRegistrationRepository.cs` (Data) — implemented `GetSlotDatetimeAsync`; `RegisterAsync` sets `slot.NoShowRiskScore = noShowRiskScore`; wraps `SaveChangesAsync` in `DbUpdateConcurrencyException` → `SlotAlreadyBookedException` catch (consistent with existing pattern)
- [x] Modify `RegisterForAppointmentResponse.cs` — added `NoShowRiskScore (decimal?)`, `IsHighRisk (bool)`, `ContributingFactors (IReadOnlyList<string>)` positional fields
- [x] Modify `RegisterForAppointmentHandler.cs` — injected `INoShowRiskScoringService`; calls `GetSlotDatetimeAsync` → `CalculateFullRisk(slotDatetime, validationResult, intakeCompleted: false)`; logs `[PartialScoring]` warning (slot ID only — no PHI); passes score to `RegisterAsync`; returns enriched response
- [x] Modify `AppointmentsController.cs` — added `using PatientAccess.Application.Exceptions` + `try/catch (SlotAlreadyBookedException)` → `Conflict(new ProblemDetails { Status = 409 })`
- [x] `dotnet build PropelIQ.slnx` → Build succeeded, 0 Error(s)

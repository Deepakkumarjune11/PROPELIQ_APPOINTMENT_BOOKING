# Task - task_002_be_patient_registration_api

## Requirement Reference

- **User Story**: US_010 — Patient Registration & Insurance Validation
- **Story Location**: `.propel/context/tasks/EP-001/us_010/us_010.md`
- **Acceptance Criteria**:
  - AC-1: When patient submits details, the system creates a patient record and associates it with the selected appointment.
  - AC-3: When insurance is partial-match or fail, the appointment is marked for staff follow-up; booking confirmation is still issued.
  - AC-4: When a patient with the same email already exists, the system associates the new appointment with the existing patient (no duplicate creation).
- **Edge Cases**:
  - Insurance reference service unavailable → result recorded as `pending`; appointment proceeds (task_003 handles this; handler treats thrown exception as `pending` status).
  - Concurrent registration with same email → unique constraint on `patient.email` (already in `PatientConfiguration`) surfaces as `409 Conflict` to the caller.

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
| Caching | Upstash Redis (via `ICacheService`) | Cloud |
| Logging | Serilog | 3.x |
| API Docs | Swagger / OpenAPI | 6.x |
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

Implement the **Patient Registration API** within the `PatientAccess` bounded context — endpoint `POST /api/v1/appointments/{slotId}/register`. The handler executes all of the following **in a single database transaction** per FR-002 ("create an appointment record... in one transaction"):

1. **Upsert patient by email** — Look up an existing `Patient` by email. If found, use the existing record (AC-4). If not found, insert a new `Patient` row.
2. **Associate appointment** — Update the `Appointment` row (identified by `slotId`) to set `PatientId` and change `Status` from `Available` to `Booked`.
3. **Run insurance soft validation** — Call `IInsuranceValidationService` (implemented in task_003). Persist the result (`pass`/`partial-match`/`fail`/`pending`) to `Patient.InsuranceStatus`.
4. **Return response** — Return `patientId` and `insuranceStatus` so the frontend can render the non-blocking insurance alert.

The `AuditLog` entry for the booking action is also created within the same transaction (NFR-007 / DR-008 immutable append-only audit).

---

## Dependent Tasks

- **task_003_be_insurance_validation_service.md** (this story) — `IInsuranceValidationService` interface and implementation must be registered before this handler runs.
- **task_004_db_availability_query.md** (US_009) — `AppointmentStatus.Available` enum value must exist; this handler transitions the slot from `Available` → `Booked`.

---

## Impacted Components

| Action | Module | Description |
|--------|--------|-------------|
| CREATE | `PatientAccess.Application` | `Commands/RegisterForAppointment/RegisterForAppointmentCommand.cs` — MediatR command record |
| CREATE | `PatientAccess.Application` | `Commands/RegisterForAppointment/RegisterForAppointmentHandler.cs` — transactional upsert + validation handler |
| CREATE | `PatientAccess.Application` | `Commands/RegisterForAppointment/RegisterForAppointmentResponse.cs` — response DTO |
| MODIFY | `PatientAccess.Presentation` | `Controllers/AppointmentsController.cs` — add `POST /{slotId}/register` action |
| MODIFY | `PatientAccess.Presentation` | `ServiceCollectionExtensions.cs` — ensure MediatR assembly is scanned for this handler |

---

## Implementation Plan

1. **RegisterForAppointmentCommand** (`PatientAccess.Application/Commands/RegisterForAppointment/RegisterForAppointmentCommand.cs`):
   ```csharp
   public sealed record RegisterForAppointmentCommand(
       Guid   SlotId,
       string Email,
       string Name,
       DateOnly Dob,
       string Phone,
       string? InsuranceProvider,
       string? InsuranceMemberId
   ) : IRequest<RegisterForAppointmentResponse>;
   ```

2. **RegisterForAppointmentResponse**:
   ```csharp
   public sealed record RegisterForAppointmentResponse(
       Guid   PatientId,
       string InsuranceStatus   // "pass" | "partial-match" | "fail" | "pending"
   );
   ```

3. **RegisterForAppointmentHandler** — Inject `PropelIQDbContext` and `IInsuranceValidationService`. All DB operations wrapped in `await using var tx = await _db.Database.BeginTransactionAsync(ct)`:

   ```
   a. Resolve patient:
      existing = await _db.Patients.FirstOrDefaultAsync(p => p.Email == cmd.Email, ct)
      if (existing == null) → create new Patient(Id=Guid.NewGuid(), Email, Name, Dob, Phone, ...)
      else → existing.Name = cmd.Name; existing.Phone = cmd.Phone  (update demographics on re-use)

   b. Associate appointment slot:
      slot = await _db.Appointments.FindAsync([cmd.SlotId], ct)
              ?? throw new NotFoundException($"Slot {cmd.SlotId} not found")
      if (slot.Status != AppointmentStatus.Available)
          → throw new ConflictException("Slot is no longer available")  // 409
      slot.PatientId = patient.Id
      slot.Status = AppointmentStatus.Booked

   c. Insurance validation (non-blocking):
      validationResult = await _insuranceService.ValidateAsync(
          cmd.InsuranceProvider, cmd.InsuranceMemberId, ct)
      patient.InsuranceProvider = cmd.InsuranceProvider
      patient.InsuranceMemberId = cmd.InsuranceMemberId
      patient.InsuranceStatus = validationResult.ToString()  // "pass"|"partial-match"|"fail"|"pending"

   d. Audit log entry (DR-008, NFR-007):
      _db.AuditLogs.Add(new AuditLog {
          Id = Guid.NewGuid(),
          ActorId = patient.Id,
          ActorType = AuditActorType.Patient,
          ActionType = AuditActionType.AppointmentBooked,
          TargetEntityId = slot.Id,
          Timestamp = DateTime.UtcNow,
          Payload = JsonSerializer.Serialize(new { slotId = slot.Id, insuranceStatus = patient.InsuranceStatus })
      })

   e. await _db.SaveChangesAsync(ct)
      await tx.CommitAsync(ct)
      return new RegisterForAppointmentResponse(patient.Id, patient.InsuranceStatus!)
   ```

   Exception mapping: `NotFoundException` → `404`, `ConflictException` → `409` (handled by global exception middleware or controller filter).

4. **AppointmentsController** — Add POST action to the existing controller (created in US_009 task_003):
   - Route: `POST api/v1/appointments/{slotId:guid}/register`
   - `[Authorize]` — authenticated patients only (NFR-004).
   - Validates `slotId` path param is non-empty Guid.
   - Binds request body to `RegisterForAppointmentRequest` DTO (same fields as command minus `slotId`).
   - Sends `RegisterForAppointmentCommand` via `IMediator`.
   - Returns `201 Created` with `RegisterForAppointmentResponse` body.
   - XML doc comment for Swagger (TR-011).

5. **ServiceCollectionExtensions** — Confirm MediatR scans `PatientAccess.Application` assembly (added in US_009 task_003); no additional registration needed for this handler.

---

## Current Project State

```
server/src/Modules/PatientAccess/
  PatientAccess.Application/
    Infrastructure/
      ICacheService.cs
    Queries/GetAvailability/              ← Created in us_009/task_003
      GetAvailabilityHandler.cs
      GetAvailabilityQuery.cs
      AvailabilitySlotDto.cs
  PatientAccess.Data/
    PropelIQDbContext.cs                  ← DbSet<Patient>, DbSet<Appointment>, DbSet<AuditLog>
    Entities/
      Patient.cs                          ← InsuranceStatus, InsuranceMemberId, InsuranceProvider (available)
      Appointment.cs                      ← Status (AppointmentStatus enum), PatientId FK
    Configurations/
      PatientConfiguration.cs             ← uix_patient_email unique index (available)
  PatientAccess.Domain/
    Enums/
      AppointmentStatus.cs                ← Available added in us_009/task_004
      AuditActionType.cs
      AuditActorType.cs
  PatientAccess.Presentation/
    Controllers/
      AppointmentsController.cs           ← MODIFY: add POST /{slotId}/register action
    ServiceCollectionExtensions.cs        ← MediatR already registered (us_009/task_003)
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Commands/RegisterForAppointment/RegisterForAppointmentCommand.cs` | MediatR command record |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Commands/RegisterForAppointment/RegisterForAppointmentResponse.cs` | Response DTO |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Commands/RegisterForAppointment/RegisterForAppointmentHandler.cs` | Transactional upsert handler: patient upsert, slot booking, insurance validation, audit log |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Presentation/Controllers/AppointmentsController.cs` | Add `POST /{slotId:guid}/register` action |

---

## External References

- [EF Core 8 — Explicit transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions)
- [EF Core 8 — FindAsync for primary key lookup](https://learn.microsoft.com/en-us/ef/core/change-tracking/entity-entries#finding-entities-by-key)
- [MediatR 12.x — IRequest / IRequestHandler](https://github.com/jbogard/MediatR/wiki)
- [ASP.NET Core 8 — Problem Details (RFC 7807) middleware](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-8.0#problem-details)
- [OWASP A03 — Injection: EF Core parameterizes all queries](https://owasp.org/Top10/A03_2021-Injection/)
- [OWASP A01 — Broken Access Control: [Authorize] on all patient endpoints](https://owasp.org/Top10/A01_2021-Broken_Access_Control/)

---

## Build Commands

```bash
# From server/ — restore packages
dotnet restore

# Build solution
dotnet build PropelIQ.slnx

# Run API (development)
dotnet run --project src/PropelIQ.Api/PropelIQ.Api.csproj
```

---

## Implementation Validation Strategy

- [ ] Unit tests pass — new patient created when email not found in DB
- [ ] Unit tests pass — existing patient reused (no duplicate insert) when email matches (AC-4)
- [ ] Unit tests pass — slot `Status` transitions from `Available` to `Booked` within transaction
- [ ] Unit tests pass — handler throws `ConflictException` when slot is not `Available`
- [ ] Unit tests pass — `AuditLog` entry created with `ActionType = AppointmentBooked`
- [ ] Integration test — `POST /api/v1/appointments/{slotId}/register` returns `201` with `patientId` and `insuranceStatus`
- [ ] `409 Conflict` returned when slot already `Booked`
- [ ] `401 Unauthorized` returned for unauthenticated requests
- [ ] Insurance validation exception does NOT roll back the booking transaction (status = `pending`)
- [ ] Swagger UI shows endpoint with XML doc summary, request body schema, and response codes

---

## Implementation Checklist

- [X] Create `RegisterForAppointmentCommand.cs` — record with `SlotId, Email, Name, Dob, Phone, InsuranceProvider?, InsuranceMemberId?`
- [X] Create `RegisterForAppointmentResponse.cs` — record with `PatientId, InsuranceStatus`
- [X] Create `RegisterForAppointmentHandler.cs` — inject `IAppointmentRegistrationRepository` + `IInsuranceValidationService`; call insurance validation (non-blocking), delegate transactional upsert to repository
- [X] Catch `IInsuranceValidationService` exception → set `InsuranceStatus = "pending"` without re-throwing (non-blocking per edge case)
- [X] Add `POST /{slotId:guid}/register` action to `AppointmentsController` with `[Authorize]`, `[HttpPost]`, XML doc, and `201 Created` return
- [X] Confirm `ConflictException` → `409` and `NotFoundException` → `404` mapping exists in global exception middleware or add problem-details filter
- [X] Confirm MediatR already scans `PatientAccess.Application` assembly (no change needed to `ServiceCollectionExtensions`)

# Task - task_002_be_submit_intake_api

## Requirement Reference

- **User Story**: US_011 — Manual Intake Form
- **Story Location**: `.propel/context/tasks/EP-001/us_011/us_011.md`
- **Acceptance Criteria**:
  - AC-4: When the intake form is submitted, the system stores the intake response with `mode="manual"` and all answers as JSONB payload.
- **Edge Cases**:
  - Patient submits intake for a `patientId` that does not exist → `404 Not Found` returned; no `IntakeResponse` row created.
  - Patient submits the same intake twice (double-submit) → second call inserts a new `IntakeResponse` row (each submission is an independent record; idempotency is not required here — clinical history benefits from retaining all submissions).
  - Empty answers payload → API accepts it (partial completion is valid per AC edge case about navigating away mid-form); no payload validation enforced beyond JSON schema.

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
| Serialization | System.Text.Json | .NET 8 built-in |
| PHI Encryption | .NET Data Protection API | 8.0 |
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

Implement the **Submit Intake API** within the `PatientAccess` bounded context — endpoint `POST /api/v1/patients/{patientId}/intake`. This endpoint:

1. Validates the `patientId` path parameter resolves to an existing `Patient` (soft-delete filter applied by EF Core global query filter).
2. Deserializes the `answers` payload (a JSON object of `questionId → answerText` pairs) and persists a new `IntakeResponse` row with:
   - `PatientId` = path `patientId`
   - `Mode` = `IntakeMode.Manual`
   - `Answers` = serialized JSON (stored as `jsonb` column per `IntakeResponseConfiguration`)
3. Appends an immutable `AuditLog` entry for the intake submission action (NFR-007 / DR-008).
4. Returns `201 Created` with the new `IntakeResponse.Id`.

The `IntakeResponse.Answers` column is typed `jsonb` in PostgreSQL and is marked as a PHI column per DR-015 / `IntakeResponseConfiguration`. The `.NET Data Protection API` encryption `ValueConverter` noted in the configuration comment must be applied during this task so that PHI is encrypted at rest before the first intake record is written.

---

## Dependent Tasks

- **task_002_be_patient_registration_api.md** (US_010) — `PatientId` passed in path must exist; the patient is created during appointment registration. This endpoint is called after registration succeeds.
- No database migration required — `intake_response` table already exists (created in `AddClinicalIntakeSchema` migration, see `IntakeResponseConfiguration`).

---

## Impacted Components

| Action | Module | Description |
|--------|--------|-------------|
| CREATE | `PatientAccess.Application` | `Commands/SubmitIntake/SubmitIntakeCommand.cs` — MediatR command record |
| CREATE | `PatientAccess.Application` | `Commands/SubmitIntake/SubmitIntakeHandler.cs` — handler: validate patient, insert IntakeResponse, audit log |
| CREATE | `PatientAccess.Application` | `Commands/SubmitIntake/SubmitIntakeResponse.cs` — response DTO |
| CREATE | `PatientAccess.Presentation` | `Controllers/PatientsController.cs` — new controller for patient-scoped actions |
| MODIFY | `PatientAccess.Data` | `Configurations/IntakeResponseConfiguration.cs` — add PHI encryption ValueConverter for `Answers` column |
| MODIFY | `PatientAccess.Presentation` | `ServiceCollectionExtensions.cs` — register Data Protection API; confirm MediatR scans new handler |

---

## Implementation Plan

1. **`SubmitIntakeCommand`** (`PatientAccess.Application/Commands/SubmitIntake/SubmitIntakeCommand.cs`):
   ```csharp
   public sealed record SubmitIntakeCommand(
       Guid PatientId,
       string Mode,                          // "manual" | "conversational"
       Dictionary<string, string> Answers    // questionId → answerText
   ) : IRequest<SubmitIntakeResponse>;
   ```

2. **`SubmitIntakeResponse`**:
   ```csharp
   public sealed record SubmitIntakeResponse(Guid IntakeResponseId);
   ```

3. **`SubmitIntakeHandler`** — Inject `PropelIQDbContext` and `ILogger<SubmitIntakeHandler>`:
   ```
   a. Validate patient exists:
      patientExists = await _db.Patients.AnyAsync(p => p.Id == cmd.PatientId, ct)
      if (!patientExists) → throw new NotFoundException($"Patient {cmd.PatientId} not found")

   b. Parse mode:
      mode = Enum.Parse<IntakeMode>(cmd.Mode, ignoreCase: true)

   c. Serialize answers:
      answersJson = JsonSerializer.Serialize(cmd.Answers)

   d. Create IntakeResponse entity:
      intakeResponse = new IntakeResponse {
          Id = Guid.NewGuid(),
          PatientId = cmd.PatientId,
          Mode = mode,
          Answers = answersJson,   // ValueConverter encrypts before write (step 5)
          CreatedAt = DateTime.UtcNow
      }
      _db.IntakeResponses.Add(intakeResponse)

   e. Audit log (DR-008, NFR-007):
      _db.AuditLogs.Add(new AuditLog {
          Id = Guid.NewGuid(),
          ActorId = cmd.PatientId,
          ActorType = AuditActorType.Patient,
          ActionType = AuditActionType.IntakeSubmitted,
          TargetEntityId = intakeResponse.Id,
          Timestamp = DateTime.UtcNow,
          Payload = JsonSerializer.Serialize(new { mode = cmd.Mode, questionCount = cmd.Answers.Count })
          // NOTE: answers content is NOT included in AuditLog.Payload — PHI must not appear in log (DR-015, AIR-S01 analogous)
      })

   f. await _db.SaveChangesAsync(ct)
      return new SubmitIntakeResponse(intakeResponse.Id)
   ```

4. **`PatientsController`** (`PatientAccess.Presentation/Controllers/PatientsController.cs`):
   - Route: `[Route("api/v1/patients")]`
   - `POST /{patientId:guid}/intake`
   - `[Authorize]` — authenticated patients only (NFR-004).
   - Binds `patientId` from route, binds `SubmitIntakeRequest` from body (Mode + Answers dictionary).
   - Returns `201 Created` with `SubmitIntakeResponse` body.
   - Returns `404` on `NotFoundException`.
   - XML doc comments for Swagger (TR-011).

5. **PHI encryption `ValueConverter` for `IntakeResponse.Answers`** (DR-015 / NFR-003) — Update `IntakeResponseConfiguration.Configure()`:
   ```csharp
   // Inject IDataProtectionProvider via constructor parameter on the configuration class
   builder.Property(i => i.Answers)
       .HasColumnType("jsonb")
       .IsRequired()
       .HasConversion(
           v => _protector.Protect(v),       // encrypt before write
           v => _protector.Unprotect(v));     // decrypt after read
   ```
   `IDataProtectionProvider` is resolved from DI in `ServiceCollectionExtensions.cs` and passed to `IntakeResponseConfiguration` via `ModelBuilder.UseEntityTypeConfiguration` override, or alternatively implemented as an EF Core `ValueConverter<string, string>` registered through the configuration class constructor.

   Update `ServiceCollectionExtensions.cs`:
   ```csharp
   services.AddDataProtection()
           .SetApplicationName("PropelIQ");
   ```

6. **`AuditActionType` enum** — Confirm `IntakeSubmitted` value exists in `PatientAccess.Domain/Enums/AuditActionType.cs`. If absent, add it.

---

## Current Project State

```
server/src/Modules/PatientAccess/
  PatientAccess.Application/
    Infrastructure/
      ICacheService.cs
    Commands/
      RegisterForAppointment/              ← Created in us_010/task_002
        RegisterForAppointmentCommand.cs
        RegisterForAppointmentHandler.cs
        RegisterForAppointmentResponse.cs
  PatientAccess.Data/
    PropelIQDbContext.cs                   ← DbSet<IntakeResponse> available
    Entities/
      IntakeResponse.cs                    ← PatientId, Mode, Answers (jsonb), CreatedAt
    Configurations/
      IntakeResponseConfiguration.cs       ← MODIFY: add PHI encryption ValueConverter
  PatientAccess.Domain/
    Enums/
      IntakeMode.cs                        ← Conversational, Manual (available)
      AuditActionType.cs                   ← Confirm IntakeSubmitted value; add if absent
  PatientAccess.Presentation/
    Controllers/
      AppointmentsController.cs            ← Existing; PatientsController is NEW
    ServiceCollectionExtensions.cs         ← MODIFY: AddDataProtection()
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Commands/SubmitIntake/SubmitIntakeCommand.cs` | MediatR command with PatientId, Mode, Answers dictionary |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Commands/SubmitIntake/SubmitIntakeResponse.cs` | Response DTO: IntakeResponseId |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Commands/SubmitIntake/SubmitIntakeHandler.cs` | Patient existence check, IntakeResponse insert, audit log — no transaction needed (single aggregate) |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Presentation/Controllers/PatientsController.cs` | `POST /api/v1/patients/{patientId:guid}/intake` — Authorize, 201/404 responses |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Data/Configurations/IntakeResponseConfiguration.cs` | Add `HasConversion` ValueConverter using `IDataProtector` for PHI encryption on `Answers` column |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Presentation/ServiceCollectionExtensions.cs` | Add `services.AddDataProtection().SetApplicationName("PropelIQ")` |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Domain/Enums/AuditActionType.cs` | Add `IntakeSubmitted` if not present |

---

## External References

- [.NET Data Protection API — IDataProtector](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/using-data-protection?view=aspnetcore-8.0)
- [EF Core 8 — Value Converters](https://learn.microsoft.com/en-us/ef/core/modeling/value-conversions)
- [EF Core 8 — PostgreSQL jsonb column type (Npgsql)](https://www.npgsql.org/efcore/mapping/json.html)
- [MediatR 12.x — IRequest / IRequestHandler](https://github.com/jbogard/MediatR/wiki)
- [OWASP A02 — Cryptographic Failures: PHI must be encrypted at rest (DR-015)](https://owasp.org/Top10/A02_2021-Cryptographic_Failures/)
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

# No migration required — intake_response table already exists
```

---

## Implementation Validation Strategy

- [ ] Unit tests pass — handler inserts `IntakeResponse` with `Mode = IntakeMode.Manual` and serialized `Answers`
- [ ] Unit tests pass — handler throws `NotFoundException` when `patientId` does not exist in DB
- [ ] Unit tests pass — `AuditLog` entry created with `ActionType = IntakeSubmitted`; payload does NOT contain raw answers (PHI guard)
- [ ] Integration test — `POST /api/v1/patients/{patientId}/intake` returns `201 Created` with `IntakeResponseId`
- [ ] `404 Not Found` returned for non-existent `patientId`
- [ ] `401 Unauthorized` returned for unauthenticated requests
- [ ] `IntakeResponse.Answers` value stored in PostgreSQL is encrypted (not plain JSON) — verify via direct DB query
- [ ] `IDataProtector.Unprotect` successfully recovers original JSON on read (round-trip test)
- [ ] Swagger UI shows endpoint with request body schema (Mode + Answers dictionary) and 201/404 response codes

---

## Implementation Checklist

- [ ] Check `AuditActionType.cs` — add `IntakeSubmitted` value if absent
- [ ] Create `SubmitIntakeCommand.cs` — record with `PatientId`, `Mode` (string), `Answers` (`Dictionary<string, string>`)
- [ ] Create `SubmitIntakeResponse.cs` — record with `IntakeResponseId`
- [ ] Create `SubmitIntakeHandler.cs` — patient existence check via `AnyAsync`, `IntakeMode` parse, serialize answers, insert `IntakeResponse`, append `AuditLog` with PHI-safe payload, `SaveChangesAsync`
- [ ] Modify `IntakeResponseConfiguration.cs` — add `HasConversion` `ValueConverter` using `IDataProtector` (Protect on write, Unprotect on read) for `Answers` column (DR-015)
- [ ] Update `ServiceCollectionExtensions.cs` — add `services.AddDataProtection().SetApplicationName("PropelIQ")` and wire `IDataProtector` into `IntakeResponseConfiguration` registration
- [ ] Create `PatientsController.cs` — `[Authorize]`, `[Route("api/v1/patients")]`, `POST /{patientId:guid}/intake` action, 201/404 responses, XML doc for Swagger
- [ ] Confirm `dotnet build` passes with zero errors after all changes

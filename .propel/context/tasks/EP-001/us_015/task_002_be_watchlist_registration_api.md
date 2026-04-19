# Task - task_002_be_watchlist_registration_api

## Requirement Reference

- **User Story**: US_015 — Preferred Slot Swap Watchlist
- **Story Location**: `.propel/context/tasks/EP-001/us_015/us_015.md`
- **Acceptance Criteria**:
  - AC-1: When the patient navigates to preferred slot selection, they see eligible unavailable slots per FR-004 — the API must return slot availability with `available` flag.
  - AC-2: When the patient confirms a preferred unavailable slot, the system registers the watchlist by updating `Appointment.preferred_slot_id` (POST endpoint).
  - AC-4: After a swap executes, `GET /api/v1/appointments` reflects the new slot datetime and `preferred_slot_id = null`.
  - AC-5: If the preferred slot was claimed by another patient, the watchlist entry is preserved (`preferred_slot_id` unchanged).
- **Edge Cases**:
  - POST returns 422 if the selected slot is now available (patient should book directly) or ineligible.
  - Only the appointment owner (authenticated patient) may register a watchlist entry — 403 if another patient attempts.
  - Appointment must be in `booked` status to enroll on watchlist — reject `arrived`, `completed`, `cancelled`, `no-show` with 400.

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
| Auth | ASP.NET Core Identity + JWT Bearer | 8.0 |
| Logging | Serilog | 3.x |
| Testing - Unit | xUnit + Moq | 2.x / 4.x |
| Testing - Integration | Testcontainers | 3.x |
| API Documentation | Swagger / OpenAPI | 6.x |

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

Implement the backend API endpoints consumed by the SCR-008 and SCR-009 frontend screens. Three REST endpoints are required within the `PatientAccess` bounded context:

1. **`GET /api/v1/appointments`** — returns all appointments for the authenticated patient, each including `preferred_slot_datetime` for watchlist display.
2. **`GET /api/v1/slots/availability`** — returns slot availability (available/unavailable) for a given provider and month, enabling SCR-009 to gray out bookable slots.
3. **`POST /api/v1/appointments/{appointmentId}/preferred-slot`** — registers the patient's preferred unavailable slot; updates `Appointment.preferred_slot_id` within a single EF Core `SaveChangesAsync`.

All endpoints are scoped to `[Authorize(Roles = "Patient")]` — unauthorized access returns 401; access to another patient's appointment returns 403. The implementation follows the layered pattern: Controller → Application Service → Domain Entity → EF Core Repository.

---

## Dependent Tasks

- **task_004_db_preferred_slot_schema.md** (US_015) — `Appointment.preferred_slot_id` FK column and watchlist index must exist before this task.
- **task_001_be_patient_registration_api.md** (US_010) — `Appointment` entity and patient-scoped authorization patterns established; follow same conventions.

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Appointments/Queries/GetPatientAppointmentsQuery.cs` | CQRS Query + Handler returning patient's appointment list with preferred slot info |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Appointments/Commands/RegisterPreferredSlotCommand.cs` | CQRS Command + Handler: validate eligibility, update `preferred_slot_id`, save |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Slots/Queries/GetSlotAvailabilityQuery.cs` | CQRS Query + Handler returning availability matrix for a provider/month |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Appointments/Dtos/AppointmentDto.cs` | DTO: `id`, `slotDatetime`, `providerName`, `visitType`, `status`, `preferredSlotDatetime` |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Presentation/Controllers/AppointmentsController.cs` | REST controller: GET appointments, POST preferred-slot |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Presentation/Controllers/SlotsController.cs` | REST controller: GET slot availability |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Presentation/ServiceCollectionExtensions.cs` | Register new Application services (MediatR handlers) |

---

## Implementation Plan

1. **`AppointmentDto`** — maps domain `Appointment` to API response:
   ```csharp
   public record AppointmentDto(
       Guid Id,
       DateTimeOffset SlotDatetime,
       string ProviderName,
       string VisitType,
       AppointmentStatus Status,
       DateTimeOffset? PreferredSlotDatetime   // null when not on watchlist
   );
   ```

2. **`GetPatientAppointmentsQuery` Handler**:
   ```csharp
   // Query: no parameters (patient ID extracted from JWT claims)
   // EF Core query:
   var appointments = await _context.Appointments
       .Where(a => a.PatientId == currentPatientId && !a.IsDeleted)
       .Include(a => a.PreferredSlot)          // self-reference nav property
       .OrderByDescending(a => a.SlotDatetime)
       .Select(a => new AppointmentDto(
           a.Id, a.SlotDatetime, a.ProviderName,
           a.VisitType, a.Status,
           a.PreferredSlot != null ? a.PreferredSlot.SlotDatetime : null))
       .ToListAsync(cancellationToken);
   ```

3. **`RegisterPreferredSlotCommand` Handler** — eligibility checks before persistence:
   ```csharp
   // Step 1: Load appointment; verify ownership (patientId == currentPatientId) → 403
   // Step 2: Verify appointment.Status == AppointmentStatus.Booked → 400 if not
   // Step 3: Verify preferred slot is NOT available (i.e., another booking exists for that datetime/provider)
   //         If available → return 422 "Slot is available; please book directly."
   // Step 4: Verify preferred slot datetime is in the future → 400 if past
   // Step 5: appointment.preferred_slot_id = preferredSlotId (resolve slot record by datetime + providerId)
   // Step 6: await _context.SaveChangesAsync(cancellationToken)
   // Step 7: Write AuditLog entry (actor=patientId, action="WatchlistRegistered", target=appointmentId)
   ```

4. **`GetSlotAvailabilityQuery` Handler**:
   ```csharp
   // Returns all configured slot datetimes for the provider in the requested month
   // For each slot: checks whether an active Appointment exists (status IN booked, arrived)
   // Returns: List<SlotAvailabilityDto> { Datetime, IsAvailable }
   // IsAvailable = false → unavailable (selectable for watchlist)
   // IsAvailable = true  → available (disabled in SCR-009 calendar)
   ```

5. **`AppointmentsController`** — endpoint definitions:
   ```csharp
   [Authorize(Roles = "Patient")]
   [ApiController]
   [Route("api/v1/appointments")]
   public class AppointmentsController : ControllerBase
   {
       [HttpGet]
       public async Task<IActionResult> GetAppointments(...)   // returns 200 List<AppointmentDto>

       [HttpPost("{appointmentId:guid}/preferred-slot")]
       public async Task<IActionResult> RegisterPreferredSlot(
           Guid appointmentId,
           RegisterPreferredSlotRequest request, ...)
       // Returns 204 on success, 400/403/422 on validation failure
   }
   ```

6. **`SlotsController`** — endpoint definition:
   ```csharp
   [HttpGet("availability")]
   // Query: providerId (Guid), year (int), month (int) — validated with FluentValidation
   // Returns 200 List<SlotAvailabilityDto>
   ```

7. **RBAC guard** — patient ownership check extracted to `IPatientOwnershipValidator` service to avoid code duplication (shared with future appointment cancel/reschedule endpoints):
   ```csharp
   var isOwner = await _ownershipValidator.IsOwnerAsync(appointmentId, currentPatientId);
   if (!isOwner) return Forbid();
   ```

---

## Current Project State

```
server/src/
  Modules/
    PatientAccess/
      PatientAccess.Application/
        Class1.cs                       ← placeholder, replace with real handlers
      PatientAccess.Domain/
        (Appointment entity from EP-DATA tasks)
      PatientAccess.Presentation/
        ServiceCollectionExtensions.cs
  PropelIQ.Api/
    Controllers/
      WeatherForecastController.cs      ← example only, unrelated
    Program.cs
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Appointments/Queries/GetPatientAppointmentsQuery.cs` | CQRS Query + Handler |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Appointments/Commands/RegisterPreferredSlotCommand.cs` | CQRS Command + Handler with eligibility checks |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Appointments/Dtos/AppointmentDto.cs` | Response DTO |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Slots/Queries/GetSlotAvailabilityQuery.cs` | Slot availability CQRS Query |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Slots/Dtos/SlotAvailabilityDto.cs` | `{ Datetime, IsAvailable }` response DTO |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Shared/IPatientOwnershipValidator.cs` | Ownership check interface |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Presentation/Controllers/AppointmentsController.cs` | REST controller (GET + POST preferred-slot) |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Presentation/Controllers/SlotsController.cs` | REST controller (GET availability) |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Presentation/ServiceCollectionExtensions.cs` | Register MediatR handlers and `IPatientOwnershipValidator` |

---

## External References

- [MediatR CQRS pattern (.NET) — docs](https://github.com/jbogard/MediatR/wiki)
- [EF Core — self-referencing relationships](https://learn.microsoft.com/en-us/ef/core/modeling/relationships/self-referencing)
- [ASP.NET Core JWT Bearer authentication — claims extraction](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/jwt-authn?view=aspnetcore-8.0)
- [FluentValidation for ASP.NET Core 8](https://docs.fluentvalidation.net/en/latest/aspnet.html)
- [ASP.NET Core — HTTP 422 Unprocessable Entity](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.http.httpresults.unprocessableentity)
- [EF Core — Include / ThenInclude navigation](https://learn.microsoft.com/en-us/ef/core/querying/related-data/eager)

---

## Build Commands

```bash
# Restore and build
cd server && dotnet restore ; dotnet build

# Run API
dotnet run --project src/PropelIQ.Api/PropelIQ.Api.csproj

# Run unit tests
dotnet test --filter "Category=Unit"
```

---

## Implementation Validation Strategy

- [ ] Unit tests pass (`RegisterPreferredSlotCommandHandler` — test eligibility scenarios: wrong owner, wrong status, slot now available, slot in past)
- [ ] Integration tests pass (Testcontainers PostgreSQL — end-to-end GET appointments, POST preferred-slot)
- [ ] `GET /api/v1/appointments` returns 200 with `preferredSlotDatetime` populated after watchlist registration
- [ ] `POST /api/v1/appointments/{id}/preferred-slot` returns 204 on valid watchlist registration
- [ ] `POST` returns 422 when selected slot is currently available
- [ ] `POST` returns 403 when appointment does not belong to the authenticated patient
- [ ] `POST` returns 400 when appointment status is not `booked`
- [ ] AuditLog entry written on successful watchlist registration
- [ ] Swagger/OpenAPI documents all three endpoints with response codes

---

## Implementation Checklist

- [ ] Create `AppointmentDto` and `SlotAvailabilityDto` response types
- [ ] Implement `GetPatientAppointmentsQuery` handler with `PreferredSlot` include and DTO projection
- [ ] Implement `GetSlotAvailabilityQuery` handler returning availability matrix for provider/month
- [ ] Implement `RegisterPreferredSlotCommand` handler with 5-step eligibility validation chain (ownership → status → slot availability → future date → persist)
- [ ] Create `IPatientOwnershipValidator` interface + EF Core implementation for reusable RBAC check
- [ ] Implement `AppointmentsController` (GET + POST preferred-slot) with `[Authorize(Roles = "Patient")]`
- [ ] Implement `SlotsController` (GET availability) with `[Authorize(Roles = "Patient")]`
- [ ] Register all services in `ServiceCollectionExtensions.cs` and write AuditLog entry on successful `RegisterPreferredSlotCommand` execution

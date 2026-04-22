# Task - task_002_be_walkin_booking_api

## Requirement Reference

- **User Story**: US_016 — Staff Walk-In Booking & Patient Creation
- **Story Location**: `.propel/context/tasks/EP-002/us_016/us_016.md`
- **Acceptance Criteria**:
  - AC-1: When staff accesses the walk-in form, the API verifies staff permissions (`[Authorize(Roles = "Staff")]`) and returns 403 for non-staff requests per FR-008.
  - AC-2: When staff searches by email or phone, `GET /api/v1/patients/search?q=` returns matching patient details.
  - AC-3: When no patient exists, `POST /api/v1/patients` (staff-initiated) creates a minimal patient profile and returns the new patient record.
  - AC-4: When a same-day slot is available, `POST /api/v1/appointments/walk-in` creates a `booked` appointment, adds the patient to the same-day queue with a position number, and returns `{ appointmentId, queuePosition, waitQueue: false }`.
  - AC-5: When no same-day slots are available, the endpoint adds the patient to the wait queue and returns `{ queuePosition, waitQueue: true }`.
- **Edge Cases**:
  - Patient role calling any endpoint in this task → `[Authorize(Roles = "Staff")]` returns 403; no data leakage.
  - Duplicate patient creation: `POST /api/v1/patients` checks `email` uniqueness; returns 409 Conflict if duplicate.
  - Walk-in booking for a patient already in today's queue → return 409 with message "Patient already has a walk-in appointment today."
  - Queue position calculation: atomic increment within EF Core transaction to avoid race conditions on concurrent staff bookings.

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
| Caching | Upstash Redis | Cloud |
| Auth | ASP.NET Core Identity + JWT Bearer | 8.0 |
| Background Jobs | Hangfire | 1.8.x |
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

Implement four backend API endpoints within the `PatientAccess` bounded context (staff sub-module). All endpoints require `[Authorize(Roles = "Staff")]`. The implementation follows the CQRS + layered pattern: Controller → Application Service (MediatR Handler) → Domain Entity → EF Core.

**Endpoints:**
1. `GET /api/v1/patients/search?q={term}` — patient search by email or phone (partial match, case-insensitive, top 10 results).
2. `POST /api/v1/patients` (staff-initiated) — creates a minimal patient profile (name, email, phone only); distinct from patient self-registration.
3. `POST /api/v1/appointments/walk-in` — books walk-in appointment or adds to wait queue.
4. `GET /api/v1/staff/dashboard/summary` — aggregated summary counts for staff dashboard (walk-ins today, queue length, verification pending, critical conflicts).

Queue position for walk-in bookings is assigned within a database-level serializable transaction to prevent race conditions when multiple staff members book simultaneously. Redis cache is invalidated on each queue modification (TTL 30s per EP-002 spec).

---

## Dependent Tasks

- **task_003_db_walkin_queue_schema.md** (US_016) — `QueuePosition` column and `SameDayQueue` (or equivalent) table structure must exist.
- **task_001_be_patient_registration_api.md** (US_010) — `Patient` entity and registration patterns already established; staff-initiated creation uses a distinct command to avoid permission confusion.

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Patients/Queries/SearchPatientsQuery.cs` | CQRS Query + Handler: email/phone partial match, top 10 |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Patients/Commands/CreatePatientByStaffCommand.cs` | CQRS Command + Handler: minimal profile creation, email uniqueness check |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Appointments/Commands/BookWalkInCommand.cs` | CQRS Command + Handler: slot check, queue position assignment, Redis invalidation |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Staff/Queries/GetDashboardSummaryQuery.cs` | CQRS Query + Handler: aggregate counts for dashboard |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Presentation/Controllers/PatientsController.cs` | REST controller: GET search, POST (staff-initiated create) |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Presentation/Controllers/StaffController.cs` | REST controller: POST walk-in, GET dashboard summary |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Presentation/ServiceCollectionExtensions.cs` | Register new MediatR handlers |

---

## Implementation Plan

1. **`SearchPatientsQuery` Handler** — partial match search:
   ```csharp
   // EF Core query — case-insensitive partial match on email OR phone
   var results = await _context.Patients
       .Where(p => !p.IsDeleted &&
           (EF.Functions.ILike(p.Email, $"%{query}%") ||
            EF.Functions.ILike(p.Phone, $"%{query}%")))
       .OrderBy(p => p.FullName)
       .Take(10)
       .Select(p => new PatientSearchResultDto(p.Id, p.FullName, p.Email, p.Phone))
       .ToListAsync(cancellationToken);
   ```

2. **`CreatePatientByStaffCommand` Handler** — staff-initiated minimal profile:
   ```csharp
   // Step 1: Check email uniqueness → 409 if already exists
   var exists = await _context.Patients
       .AnyAsync(p => p.Email == command.Email && !p.IsDeleted, cancellationToken);
   if (exists) throw new ConflictException("A patient with this email already exists.");

   // Step 2: Create minimal Patient entity (name + email + phone only)
   // Step 3: Save, write AuditLog (actor = staffId, action = "PatientCreatedByStaff")
   ```

3. **`BookWalkInCommand` Handler** — atomic walk-in booking:
   ```csharp
   using var tx = await _context.Database
       .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
   try
   {
       // Step 1: Check if patient already has a walk-in today → 409 if duplicate
       // Step 2: Query available same-day slots for today (Status = free)
       bool slotAvailable = await _context.Slots
           .AnyAsync(s => s.Date == DateOnly.FromDateTime(DateTime.UtcNow)
                       && s.ProviderId == command.ProviderId
                       && !s.IsBooked, cancellationToken);

       // Step 3: Get next queue position (MAX(QueuePosition) + 1 within today's queue)
       int nextPosition = (await _context.Appointments
           .Where(a => a.SlotDatetime.Date == DateTime.UtcNow.Date && !a.IsDeleted)
           .MaxAsync(a => (int?)a.QueuePosition, cancellationToken) ?? 0) + 1;

       // Step 4: Create Appointment (Status = booked if slot; WaitQueue = true if no slot)
       // Step 5: Write AuditLog (actor = staffId, action = "WalkInBooked")
       // Step 6: Commit transaction
       await tx.CommitAsync(cancellationToken);

       // Step 7: Invalidate Redis queue cache
       await _cache.RemoveAsync("staff:queue:today", cancellationToken);

       return new WalkInBookingResultDto(appointmentId, nextPosition, !slotAvailable);
   }
   catch { await tx.RollbackAsync(cancellationToken); throw; }
   ```

4. **`GetDashboardSummaryQuery` Handler** — aggregate counts:
   ```csharp
   var today = DateOnly.FromDateTime(DateTime.UtcNow);
   return new DashboardSummaryDto(
       WalkInsToday: await _context.Appointments
           .CountAsync(a => DateOnly.FromDateTime(a.SlotDatetime) == today
                         && a.IsWalkIn && !a.IsDeleted),
       QueueLength: await _context.Appointments
           .CountAsync(a => DateOnly.FromDateTime(a.SlotDatetime) == today
                         && a.Status == AppointmentStatus.Booked && !a.IsDeleted),
       VerificationPending: await _context.PatientViews
           .CountAsync(v => v.VerificationStatus == VerificationStatus.Pending),
       CriticalConflicts: await _context.PatientViews
           .CountAsync(v => v.ConflictFlags.Length > 0)
   );
   ```

5. **Controllers** — endpoint definitions:
   ```csharp
   [Authorize(Roles = "Staff")]
   [ApiController]
   public class PatientsController : ControllerBase
   {
       [HttpGet("api/v1/patients/search")]   // query param: q (string, required, minLength 2)
       public async Task<IActionResult> Search([FromQuery] string q, ...)

       [HttpPost("api/v1/patients")]
       public async Task<IActionResult> CreateByStaff(CreatePatientByStaffRequest request, ...)
   }

   [Authorize(Roles = "Staff")]
   [ApiController]
   public class StaffController : ControllerBase
   {
       [HttpPost("api/v1/appointments/walk-in")]
       public async Task<IActionResult> BookWalkIn(BookWalkInRequest request, ...)

       [HttpGet("api/v1/staff/dashboard/summary")]
       public async Task<IActionResult> GetDashboardSummary(...)
   }
   ```

---

## Current Project State

```
server/src/
  Modules/
    PatientAccess/
      PatientAccess.Application/
        Class1.cs                           ← placeholder
      PatientAccess.Domain/
        (Patient, Appointment entities from EP-DATA + US_015 tasks)
      PatientAccess.Presentation/
        ServiceCollectionExtensions.cs
  PropelIQ.Api/
    Program.cs
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Patients/Queries/SearchPatientsQuery.cs` | Case-insensitive email/phone partial match, top 10 |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Patients/Commands/CreatePatientByStaffCommand.cs` | Minimal patient profile creation with email uniqueness guard |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Appointments/Commands/BookWalkInCommand.cs` | Atomic walk-in booking with SERIALIZABLE transaction and Redis invalidation |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Staff/Queries/GetDashboardSummaryQuery.cs` | Aggregate summary counts for staff dashboard |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Patients/Dtos/PatientSearchResultDto.cs` | `{ Id, FullName, Email, Phone }` |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Staff/Dtos/WalkInBookingResultDto.cs` | `{ AppointmentId, QueuePosition, WaitQueue }` |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Presentation/Controllers/PatientsController.cs` | GET search + POST staff-create |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Presentation/Controllers/StaffController.cs` | POST walk-in + GET summary |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Presentation/ServiceCollectionExtensions.cs` | Register all new MediatR handlers |

---

## External References

- [EF Core — `EF.Functions.ILike` (PostgreSQL case-insensitive LIKE)](https://www.npgsql.org/efcore/mapping/full-text-search.html)
- [ASP.NET Core 8 — RBAC with JWT Bearer roles](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/roles?view=aspnetcore-8.0)
- [EF Core — SERIALIZABLE transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions)
- [Upstash Redis .NET client — cache invalidation](https://docs.upstash.com/redis/sdks/dotnet)
- [MediatR — CQRS handlers registration](https://github.com/jbogard/MediatR/wiki)
- [FR-008 — staff-only walk-in restriction](../.propel/context/docs/spec.md#FR-008)

---

## Build Commands

```bash
cd server && dotnet restore ; dotnet build
dotnet run --project src/PropelIQ.Api/PropelIQ.Api.csproj
dotnet test --filter "Category=Unit"
```

---

## Implementation Validation Strategy

- [ ] Unit tests: `BookWalkInCommand` — slot available path, wait-queue path, duplicate booking 409, concurrent booking race condition
- [ ] Unit tests: `CreatePatientByStaffCommand` — email uniqueness 409, successful creation
- [ ] Unit tests: `SearchPatientsQuery` — partial email match, partial phone match, < 2 chars returns empty
- [ ] Integration tests (Testcontainers): end-to-end POST walk-in returns correct queue position
- [ ] `GET /api/v1/patients/search` with Patient JWT returns 403
- [ ] `POST /api/v1/appointments/walk-in` with no same-day slots returns `waitQueue: true`
- [ ] Redis cache invalidated after walk-in booking (verified via cache-miss on subsequent GET queue)
- [ ] AuditLog entries written for `PatientCreatedByStaff` and `WalkInBooked` actions

---

## Implementation Checklist

- [x] Create `PatientSearchResultDto` and `WalkInBookingResultDto` and `DashboardSummaryDto` response types
- [x] Implement `SearchPatientsQuery` handler with `EF.Functions.ILike` for case-insensitive partial match on email and phone
- [x] Implement `CreatePatientByStaffCommand` handler with email uniqueness check (409 on duplicate)
- [x] Implement `BookWalkInCommand` handler with SERIALIZABLE transaction, duplicate same-day 409 guard, queue position increment, and Redis cache invalidation
- [x] Implement `GetDashboardSummaryQuery` handler with 4 aggregate count queries
- [x] Create `PatientsController` (GET search + POST create) with `[Authorize(Roles = "Staff")]`
- [x] Create `StaffController` (POST walk-in + GET summary) with `[Authorize(Roles = "Staff")]`
- [x] Register all handlers in `ServiceCollectionExtensions.cs`

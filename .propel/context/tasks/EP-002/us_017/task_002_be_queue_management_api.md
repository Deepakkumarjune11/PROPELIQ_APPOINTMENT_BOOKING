# Task - task_002_be_queue_management_api

## Requirement Reference

- **User Story**: US_017 — Same-Day Queue & Arrival Management
- **Story Location**: `.propel/context/tasks/EP-002/us_017/us_017.md`
- **Acceptance Criteria**:
  - AC-1: `GET /api/v1/staff/queue` returns today's queue entries ordered by `queue_position` with patient name, appointment time, status, and position number.
  - AC-2: `PATCH /api/v1/staff/queue/reorder` persists new queue positions, invalidates Redis cache, and broadcasts `QueueUpdated` to all connected staff via SignalR.
  - AC-3: `PATCH /api/v1/appointments/{id}/status` transitions appointment to `arrived`, writes an AuditLog entry, invalidates Redis cache, and broadcasts `QueueUpdated` via SignalR.
  - AC-4: `GET /api/v1/staff/queue` returns cached result within Redis 30s TTL per NFR-001; on cache miss, rebuilds cache from DB.
  - AC-5: ASP.NET Core SignalR Hub supports concurrent connections from multiple staff users without degrading to below p95 2s response (NFR-001, NFR-008); Redis-backed cache absorbs DB read pressure.
- **Edge Cases**:
  - "Left" status transition → `PATCH /api/v1/appointments/{id}/status` accepts `left` value; appointment removed from active queue view (filtered by BE query), AuditLog written.
  - Concurrent `PATCH /reorder` from two staff users simultaneously → last write wins (positions are overwritten); both users receive `QueueUpdated` broadcast and re-fetch fresh state.
  - `PATCH /status` for an appointment not in today's queue → 404 Not Found.
  - Non-staff JWT calling any endpoint → `[Authorize(Roles = "Staff")]` returns 403.

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
| Real-Time | ASP.NET Core SignalR | 8.0 (built-in, free/OSS) |
| ORM | Entity Framework Core | 8.0 |
| Database | PostgreSQL | 15.x |
| Caching | Upstash Redis | Cloud |
| Auth | ASP.NET Core Identity + JWT Bearer | 8.0 |
| Logging | Serilog | 3.x |
| Testing - Unit | xUnit + Moq | 2.x / 4.x |
| Testing - Integration | Testcontainers | 3.x |
| API Documentation | Swagger / OpenAPI | 6.x |

> All code and libraries MUST be compatible with versions above. ASP.NET Core SignalR satisfies NFR-015 (free/OSS bundled with .NET 8).

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

Implement three REST endpoints and one SignalR Hub within the `PatientAccess` bounded context (staff sub-module) to support real-time same-day queue management:

1. **`GET /api/v1/staff/queue`** — returns today's active queue (booked/arrived/in-room appointments ordered by `queue_position`) with Redis caching (30s TTL).
2. **`PATCH /api/v1/staff/queue/reorder`** — accepts an ordered array of appointment IDs, bulk-updates `queue_position` values, invalidates Redis cache, and broadcasts `QueueUpdated` event via SignalR.
3. **`PATCH /api/v1/appointments/{id}/status`** — transitions appointment status (valid targets: `arrived`, `in-room`, `left`), writes AuditLog, invalidates Redis cache, and broadcasts `QueueUpdated` via SignalR.
4. **`QueueHub` (SignalR)** — maps to `/hubs/queue`; authenticated staff connections subscribe to queue broadcast group; hub only broadcasts (no client-to-server methods beyond connection).

The `AppointmentStatus` C# enum is extended with `InRoom` and `Left` values (stored as string via EF Core `HasConversion<string>()` — no schema migration required).

---

## Dependent Tasks

- **task_003_db_walkin_queue_schema.md** (US_016) — `queue_position` and `is_walk_in` columns must exist.
- **task_002_be_walkin_booking_api.md** (US_016) — `StaffController` and `IPatientOwnershipValidator` patterns established; reuse controller base class and Redis client injection.

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Staff/Queries/GetSameDayQueueQuery.cs` | CQRS Query + Handler: Redis cache check → DB fallback → cache rebuild |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Staff/Commands/ReorderQueueCommand.cs` | CQRS Command + Handler: bulk position update + Redis invalidate + SignalR broadcast |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Appointments/Commands/UpdateAppointmentStatusCommand.cs` | CQRS Command + Handler: status transition + AuditLog + Redis invalidate + SignalR broadcast |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Presentation/Hubs/QueueHub.cs` | ASP.NET Core SignalR Hub — `/hubs/queue`, `[Authorize(Roles = "Staff")]` |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Staff/Dtos/QueueEntryDto.cs` | `{ AppointmentId, QueuePosition, PatientName, AppointmentTime, Status, VisitType }` |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Domain/Enums/AppointmentStatus.cs` | Add `InRoom` and `Left` enum values |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Presentation/Controllers/StaffController.cs` | Add GET queue + PATCH reorder endpoints |
| MODIFY | `server/src/PropelIQ.Api/Program.cs` | Register SignalR + map `/hubs/queue` + add CORS for SignalR |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Presentation/ServiceCollectionExtensions.cs` | Register new MediatR handlers |

---

## Implementation Plan

1. **Extend `AppointmentStatus` enum** (string-backed — no migration):
   ```csharp
   public enum AppointmentStatus
   {
       Booked, Arrived, Completed, Cancelled, NoShow,
       InRoom,   // NEW — patient called into examination room
       Left      // NEW — patient left before being seen
   }
   // EF Core configuration (existing): builder.Property(a => a.Status).HasConversion<string>();
   ```

2. **`QueueEntryDto`** response shape:
   ```csharp
   public record QueueEntryDto(
       Guid AppointmentId,
       int QueuePosition,
       string PatientName,
       DateTimeOffset AppointmentTime,
       string Status,
       string VisitType
   );
   ```

3. **`GetSameDayQueueQuery` Handler** — Redis-first with DB fallback:
   ```csharp
   const string CacheKey = "staff:queue:today";

   // Try Redis cache first
   var cached = await _cache.GetAsync<List<QueueEntryDto>>(CacheKey);
   if (cached is not null) return cached;

   // Cache miss — query DB
   var today = DateOnly.FromDateTime(DateTime.UtcNow);
   var entries = await _context.Appointments
       .Where(a => DateOnly.FromDateTime(a.SlotDatetime) == today
                && a.QueuePosition != null
                && !a.IsDeleted
                && a.Status != AppointmentStatus.Left
                && a.Status != AppointmentStatus.Completed)
       .OrderBy(a => a.QueuePosition)
       .Include(a => a.Patient)
       .Select(a => new QueueEntryDto(
           a.Id, a.QueuePosition!.Value, a.Patient.FullName,
           a.SlotDatetime, a.Status.ToString(), a.VisitType))
       .ToListAsync(cancellationToken);

   // Rebuild cache (30s TTL per EP-002 spec, NFR-001)
   await _cache.SetAsync(CacheKey, entries, TimeSpan.FromSeconds(30));
   return entries;
   ```

4. **`ReorderQueueCommand` Handler** — bulk position update:
   ```csharp
   // orderedAppointmentIds: ordered list of appointment GUIDs
   using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
   for (int i = 0; i < command.OrderedAppointmentIds.Count; i++)
   {
       await _context.Appointments
           .Where(a => a.Id == command.OrderedAppointmentIds[i])
           .ExecuteUpdateAsync(s => s.SetProperty(a => a.QueuePosition, i + 1), cancellationToken);
   }
   await tx.CommitAsync(cancellationToken);

   // Invalidate cache + broadcast
   await _cache.RemoveAsync("staff:queue:today", cancellationToken);
   await _hubContext.Clients.Group("QueueStaff").SendAsync("QueueUpdated", cancellationToken: cancellationToken);
   ```

5. **`UpdateAppointmentStatusCommand` Handler** — status transition with validation:
   ```csharp
   // Allowed target statuses from this endpoint: Arrived, InRoom, Left
   var allowed = new[] { AppointmentStatus.Arrived, AppointmentStatus.InRoom, AppointmentStatus.Left };
   if (!allowed.Contains(command.NewStatus))
       throw new ValidationException("Invalid status transition.");

   var appointment = await _context.Appointments
       .SingleOrDefaultAsync(a => a.Id == command.AppointmentId
           && DateOnly.FromDateTime(a.SlotDatetime) == DateOnly.FromDateTime(DateTime.UtcNow)
           && !a.IsDeleted, cancellationToken)
       ?? throw new NotFoundException("Appointment not found in today's queue.");

   appointment.UpdateStatus(command.NewStatus);
   _auditLogger.Log(actor: command.StaffId, action: "AppointmentStatusUpdated",
       target: command.AppointmentId,
       payload: new { From = appointment.Status, To = command.NewStatus });
   await _context.SaveChangesAsync(cancellationToken);

   // Invalidate cache + broadcast
   await _cache.RemoveAsync("staff:queue:today", cancellationToken);
   await _hubContext.Clients.Group("QueueStaff").SendAsync("QueueUpdated", cancellationToken: cancellationToken);
   ```

6. **`QueueHub`** — authenticated SignalR Hub:
   ```csharp
   [Authorize(Roles = "Staff")]
   public class QueueHub : Hub
   {
       public override async Task OnConnectedAsync()
       {
           await Groups.AddToGroupAsync(Context.ConnectionId, "QueueStaff");
           await base.OnConnectedAsync();
       }

       public override async Task OnDisconnectedAsync(Exception? exception)
       {
           await Groups.RemoveFromGroupAsync(Context.ConnectionId, "QueueStaff");
           await base.OnDisconnectedAsync(exception);
       }
   }
   ```

7. **`Program.cs` additions** — register SignalR and map hub:
   ```csharp
   builder.Services.AddSignalR();
   // ...
   app.MapHub<QueueHub>("/hubs/queue");
   ```

---

## Current Project State

```
server/src/
  Modules/
    PatientAccess/
      PatientAccess.Application/
        Staff/
          Queries/
            GetDashboardSummaryQuery.cs    ← us_016/task_002
          Commands/
            BookWalkInCommand.cs          ← us_016/task_002
      PatientAccess.Domain/
        Enums/
          AppointmentStatus.cs            ← extend with InRoom, Left
      PatientAccess.Presentation/
        Controllers/
          StaffController.cs              ← extend with GET queue + PATCH reorder
        Hubs/
          QueueHub.cs                     ← THIS TASK (create)
  PropelIQ.Api/
    Program.cs                            ← add SignalR + hub mapping
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Staff/Queries/GetSameDayQueueQuery.cs` | Redis-first cache strategy with 30s TTL + DB fallback |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Staff/Commands/ReorderQueueCommand.cs` | Bulk `ExecuteUpdateAsync` + cache invalidation + SignalR broadcast |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Appointments/Commands/UpdateAppointmentStatusCommand.cs` | Status transition + AuditLog + cache invalidation + SignalR broadcast |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Staff/Dtos/QueueEntryDto.cs` | Response DTO with 6 fields |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Presentation/Hubs/QueueHub.cs` | SignalR Hub — `[Authorize(Roles = "Staff")]`, `QueueStaff` group |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Domain/Enums/AppointmentStatus.cs` | Add `InRoom` and `Left` enum values |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Presentation/Controllers/StaffController.cs` | Add `GET /queue` and `PATCH /queue/reorder` endpoints |
| MODIFY | `server/src/PropelIQ.Api/Program.cs` | `AddSignalR()` + `MapHub<QueueHub>("/hubs/queue")` |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Presentation/ServiceCollectionExtensions.cs` | Register new MediatR handlers |

---

## External References

- [ASP.NET Core 8 SignalR — Hub authentication with JWT Bearer](https://learn.microsoft.com/en-us/aspnet/core/signalr/authn-and-authz?view=aspnetcore-8.0)
- [ASP.NET Core 8 SignalR — Groups](https://learn.microsoft.com/en-us/aspnet/core/signalr/groups?view=aspnetcore-8.0)
- [EF Core 8 — `ExecuteUpdateAsync` bulk update (no entity load)](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-7.0/whatsnew#executeupdate-and-executedelete-bulk-updates)
- [Upstash Redis .NET — `SetAsync` with TTL](https://docs.upstash.com/redis/sdks/dotnet)
- [NFR-001 — 2s p95 response target](../.propel/context/docs/design.md#NFR-001)
- [NFR-008 — horizontal scaling, hundreds of concurrent users](../.propel/context/docs/design.md#NFR-008)
- [TR-009 — Hangfire for background jobs (SignalR complements, not replaces)](../.propel/context/docs/design.md#TR-009)

---

## Build Commands

```bash
cd server && dotnet restore ; dotnet build
dotnet run --project src/PropelIQ.Api/PropelIQ.Api.csproj
dotnet test --filter "Category=Unit"
```

---

## Implementation Validation Strategy

- [ ] Unit tests: `GetSameDayQueueQuery` — cache hit returns cached data without DB call; cache miss queries DB and rebuilds cache
- [ ] Unit tests: `ReorderQueueCommand` — bulk position update persisted correctly; SignalR broadcast called once
- [ ] Unit tests: `UpdateAppointmentStatusCommand` — valid transitions (arrived, in-room, left) succeed; AuditLog written; invalid status returns `ValidationException`
- [ ] Integration test (Testcontainers): `PATCH /status` for appointment not in today's queue returns 404
- [ ] Non-staff JWT returns 403 on all three endpoints and `/hubs/queue` connection
- [ ] SignalR hub maps to `/hubs/queue`; staff JWT connects to `QueueStaff` group successfully
- [ ] `GET /api/v1/staff/queue` Redis TTL validated: second call within 30s returns cache hit (no DB query)
- [ ] `AppointmentStatus.InRoom` and `AppointmentStatus.Left` serialise as `"InRoom"` and `"Left"` strings in DB (no migration needed)

---

## Implementation Checklist

- [ ] Add `InRoom` and `Left` values to `AppointmentStatus` enum; verify EF Core `HasConversion<string>()` covers new values
- [ ] Create `QueueEntryDto` record with 6 fields
- [ ] Implement `GetSameDayQueueQuery` handler with Redis cache check → 30s TTL rebuild; filter out `Left`/`Completed` statuses
- [ ] Implement `ReorderQueueCommand` handler using `ExecuteUpdateAsync` bulk update + Redis invalidation + `IHubContext<QueueHub>` broadcast
- [ ] Implement `UpdateAppointmentStatusCommand` handler with status whitelist validation, AuditLog, Redis invalidation, and SignalR broadcast
- [ ] Create `QueueHub` with `[Authorize(Roles = "Staff")]`, `OnConnectedAsync` adds to `QueueStaff` group
- [ ] Add `GET /api/v1/staff/queue` and `PATCH /api/v1/staff/queue/reorder` to `StaffController`
- [ ] Register `AddSignalR()` and `app.MapHub<QueueHub>("/hubs/queue")` in `Program.cs`

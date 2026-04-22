# Task - task_003_be_availability_api

## Requirement Reference

- **User Story**: US_009 — Appointment Availability Search
- **Story Location**: `.propel/context/tasks/EP-001/us_009/us_009.md`
- **Acceptance Criteria**:
  - AC-1: System displays available slots within 2 seconds at p95 (NFR-001 — async EF Core query + Redis cache).
  - AC-2: Same date range queried within 60 seconds returns results from Redis cache without hitting the database.
  - AC-3: When cache expired (>60s TTL), system queries DB, updates cache, returns fresh results.
- **Edge Cases**:
  - Redis unavailable → `ICacheService.GetAsync` returns `null` (graceful fallback per `RedisCacheService`); system queries DB directly and logs a structured warning. AC-2 cannot be satisfied but AC-1 and AC-3 still hold.
  - Concurrent slot queries → read-only endpoint; no locking required.

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
| Caching | Upstash Redis via StackExchange.Redis | Cloud |
| Cache Abstraction | `ICacheService` (PatientAccess.Application) | custom |
| Serialization | System.Text.Json | .NET 8 built-in |
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

Implement the **Availability Search API** endpoint — `GET /api/v1/appointments/availability` — within the `PatientAccess` bounded context using layered architecture (Presentation → Application → Data). The endpoint accepts `startDate` and `endDate` query parameters and returns a list of open appointment time slots for the requested date range.

The implementation uses a **cache-aside pattern** via the existing `ICacheService` abstraction: check Redis cache first (60-second TTL per AC-2); on cache miss, execute an EF Core query against the `Appointments` table, populate the cache, and return results (AC-3). If Redis is unavailable, the service transparently falls back to the database without throwing (AC-1 edge case, per the existing `RedisCacheService` graceful error handling).

Slot availability is determined by querying the `Appointments` table for slots within the date range that have status `Available` (slots not yet claimed). Since the system represents available time as pre-allocated `Appointment` rows with `Status = Available` (see DR-002 and `AppointmentConfiguration`), the query filters `Status == AppointmentStatus.Available` within the date window. The no-show risk score (`NoShowRiskScore`) is included in the response DTO for the frontend to render the risk badge.

---

## Dependent Tasks

- **task_004_db_availability_query.md** — MUST be complete first; the `IAvailabilityRepository` interface and its EF Core implementation are consumed by `AvailabilityQueryHandler` in this task.

---

## Impacted Components

| Action | Module | Description |
|--------|--------|-------------|
| CREATE | `PatientAccess.Application` | `Queries/GetAvailability/GetAvailabilityQuery.cs` — MediatR query record |
| CREATE | `PatientAccess.Application` | `Queries/GetAvailability/GetAvailabilityHandler.cs` — cache-aside handler |
| CREATE | `PatientAccess.Application` | `Queries/GetAvailability/AvailabilitySlotDto.cs` — response DTO |
| CREATE | `PatientAccess.Presentation` | `Controllers/AppointmentsController.cs` — REST endpoint |
| MODIFY | `PatientAccess.Presentation` | `ServiceCollectionExtensions.cs` — register MediatR and handler |
| MODIFY | `PropelIQ.Api` | `Program.cs` — no change needed (module DI already registered via `AddPatientAccessModule()`) |

---

## Implementation Plan

1. **AvailabilitySlotDto** (`PatientAccess.Application/Queries/GetAvailability/AvailabilitySlotDto.cs`):
   ```csharp
   public sealed record AvailabilitySlotDto(
       Guid   SlotId,
       string SlotDatetime,   // ISO-8601 UTC string: "2026-04-20T09:00:00Z"
       decimal? NoShowRisk    // null if not yet scored; >0.7 triggers FE warning badge
   );
   ```

2. **GetAvailabilityQuery** (`PatientAccess.Application/Queries/GetAvailability/GetAvailabilityQuery.cs`):
   ```csharp
   public sealed record GetAvailabilityQuery(DateOnly StartDate, DateOnly EndDate)
       : IRequest<IReadOnlyList<AvailabilitySlotDto>>;
   ```
   Validation guard in handler: `EndDate >= StartDate`; if not, return empty list (defensive — controller validates first).

3. **GetAvailabilityHandler** — Cache-aside logic:
   ```
   cacheKey = $"availability:{startDate:yyyy-MM-dd}:{endDate:yyyy-MM-dd}"
   
   cached = await _cache.GetAsync<IReadOnlyList<AvailabilitySlotDto>>(cacheKey, ct)
   if (cached != null) → return cached   // AC-2: cache hit within 60s
   
   slots = await _repo.GetAvailableSlotsAsync(startDate, endDate, ct)  // AC-3: DB query
   dtos  = slots.Select(s => new AvailabilitySlotDto(...)).ToList()
   
   await _cache.SetAsync(cacheKey, dtos, TimeSpan.FromSeconds(60), ct)  // Populate cache
   return dtos
   ```
   If `_cache.GetAsync` returns `null` due to Redis unavailability, the handler proceeds to the DB query transparently (edge case per `RedisCacheService` swallowing `RedisConnectionException`).

4. **AppointmentsController** (`PatientAccess.Presentation/Controllers/AppointmentsController.cs`):
   - Route: `[Route("api/v1/appointments")]`
   - `GET /availability` with `[FromQuery] DateOnly startDate, [FromQuery] DateOnly endDate`
   - Validates `startDate <= endDate`; returns `400 Bad Request` with problem details if invalid.
   - Sends `GetAvailabilityQuery` via MediatR `IMediator`.
   - Returns `200 OK` with `IReadOnlyList<AvailabilitySlotDto>`.
   - XML doc comments for Swagger (TR-011).
   - Authorization: `[Authorize]` — authenticated patients only (NFR-004).

5. **MediatR + module registration** — Update `PatientAccess.Presentation/ServiceCollectionExtensions.cs` to call `services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(GetAvailabilityHandler).Assembly))` and wire `IAvailabilityRepository` implementation from task_004.

6. **Security** — `startDate` and `endDate` are `DateOnly` structs deserialized by ASP.NET Core model binding — no raw SQL, no injection surface (OWASP A03). Authorization guard ensures unauthenticated callers receive `401`.

---

## Current Project State

```
server/src/
  PropelIQ.Api/
    Program.cs                              ← Already registers AddPatientAccessModule()
    Infrastructure/Caching/
      RedisCacheService.cs                  ← ICacheService implementation (available)
  Modules/PatientAccess/
    PatientAccess.Application/
      Infrastructure/
        ICacheService.cs                    ← Cache abstraction (available)
      Class1.cs                             ← Placeholder — remove or leave
    PatientAccess.Data/
      PropelIQDbContext.cs                  ← DbSet<Appointment> (available)
      Entities/
        Appointment.cs                      ← SlotDatetime, Status, NoShowRiskScore
    PatientAccess.Presentation/
      ServiceCollectionExtensions.cs        ← MODIFY: add MediatR + handler registration
      Class1.cs                             ← Placeholder — remove or leave
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Queries/GetAvailability/AvailabilitySlotDto.cs` | Response DTO record |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Queries/GetAvailability/GetAvailabilityQuery.cs` | MediatR query record |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Queries/GetAvailability/GetAvailabilityHandler.cs` | Cache-aside query handler |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Presentation/Controllers/AppointmentsController.cs` | REST endpoint GET /api/v1/appointments/availability |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Presentation/ServiceCollectionExtensions.cs` | Register MediatR assembly, IAvailabilityRepository implementation |

---

## External References

- [MediatR 12.x — ASP.NET Core integration](https://github.com/jbogard/MediatR/wiki)
- [ASP.NET Core 8 — Controller-based API routing](https://learn.microsoft.com/en-us/aspnet/core/web-api/?view=aspnetcore-8.0)
- [DateOnly in ASP.NET Core model binding (.NET 8)](https://learn.microsoft.com/en-us/dotnet/api/system.dateonly)
- [StackExchange.Redis — IConnectionMultiplexer](https://stackexchange.github.io/StackExchange.Redis/)
- [EF Core 8 — Async query patterns](https://learn.microsoft.com/en-us/ef/core/querying/async)
- [OWASP A03 Injection — parameterized queries](https://owasp.org/www-project-top-ten/2017/A3_2017-Injection)

---

## Build Commands

```bash
# From server/ — restore packages
dotnet restore

# Build solution
dotnet build PropelIQ.slnx

# Run API (development)
dotnet run --project src/PropelIQ.Api/PropelIQ.Api.csproj

# Apply any pending EF Core migrations
dotnet ef database update --project src/Modules/PatientAccess/PatientAccess.Data --startup-project src/PropelIQ.Api
```

---

## Implementation Validation Strategy

- [ ] Unit tests pass — `GetAvailabilityHandler` returns cached result on cache hit (mock `ICacheService`)
- [ ] Unit tests pass — `GetAvailabilityHandler` queries DB and sets cache on cache miss
- [ ] Unit tests pass — Redis unavailable (`ICacheService` returns `null`) → handler falls back to DB without throwing
- [ ] Integration test — `GET /api/v1/appointments/availability?startDate=2026-04-20&endDate=2026-04-27` returns `200 OK` with slot list
- [ ] `startDate > endDate` returns `400 Bad Request`
- [ ] Unauthenticated request returns `401 Unauthorized`
- [ ] Cache key format verified: `availability:2026-04-20:2026-04-27`
- [ ] Second identical request within 60s does NOT trigger DB query (verified via mock call count)
- [ ] Swagger UI shows endpoint with XML doc summary
- [ ] Response `Content-Type: application/json; charset=utf-8`

---

## Implementation Checklist

- [X] Create `AvailabilitySlotDto.cs` record with `SlotId`, `SlotDatetime` (ISO-8601 string), `NoShowRisk` (nullable decimal)
- [X] Create `GetAvailabilityQuery.cs` — `IRequest<IReadOnlyList<AvailabilitySlotDto>>` with `DateOnly StartDate, EndDate`
- [X] Create `GetAvailabilityHandler.cs` — inject `ICacheService` + `IAvailabilityRepository`; implement cache-aside with 60s TTL
- [X] Ensure handler returns empty list (not throws) when `StartDate > EndDate`
- [X] Create `AppointmentsController.cs` — `[Authorize]`, `[Route("api/v1/appointments")]`, `GET /availability` action returning `IActionResult`
- [X] Add `400 Bad Request` validation when `startDate > endDate` in controller
- [X] Update `ServiceCollectionExtensions.cs` — register MediatR + `IAvailabilityRepository` binding
- [X] Verify `Program.cs` does not require additional changes (module DI already wired)
- [X] Add XML doc comments to controller action for Swagger
- [X] Confirm `ICacheService` cache key is deterministic (`availability:{startDate}:{endDate}` using `yyyy-MM-dd` format)

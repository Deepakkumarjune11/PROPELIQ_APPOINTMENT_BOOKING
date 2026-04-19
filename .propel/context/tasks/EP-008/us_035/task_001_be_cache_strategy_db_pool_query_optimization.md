# Task - task_001_be_cache_strategy_db_pool_query_optimization

## Requirement Reference

- **User Story**: US_035 — Performance Tuning & Resilience Patterns
- **Story Location**: `.propel/context/tasks/EP-008/us_035/us_035.md`
- **Acceptance Criteria**:
  - AC-1: Given 200 concurrent users, API p95 < 500ms for standard CRUD (see NFR discrepancy note)
  - AC-2: All queries complete within 100ms (simple) or 500ms (complex joins/aggregations) (see NFR discrepancy note)
  - AC-3: Provider schedules and slot availability served from Redis cache with TTL-based invalidation; > 90% cache hit ratio
  - AC-5: Linear degradation under ramp from 50 to 200 concurrent users; no dropped requests
- **Edge Cases**:
  - Redis unavailable: `ICacheService` already falls through to `null` (log warning) — callers must fall back to direct DB query. This task codifies the fallback pattern at the slot service level.
  - Memory pressure: `AddDbContextPool` bounds the number of live `DbContext` instances (pool size = 128 by default in EF Core 8); connection pool max=100 in connection string caps DB connections.

> **NFR Tag Discrepancy Notice**:
> - US_035 AC-1 cites `NFR-011` — design.md NFR-011 = "AI-assisted conversational intake prompts within 3 seconds at p95", NOT CRUD 500ms. This task implements the described 500ms CRUD target as an operational performance goal without the NFR-011 label.
> - US_035 AC-2 cites `NFR-016` — design.md NFR-016 = "circuit breaker patterns for external service dependencies". Query timing targets are not covered by NFR-016. This task implements query optimization against the described 100ms/500ms targets.
> - US_035 AC-3 cites `TR-020` — design.md TR-020 = "System MUST use Playwright for end-to-end testing". Cache hit ratio is not TR-020; the cache infrastructure is TR-004 (Redis distributed cache). Implementer should flag these tag mismatches in the next BRD revision.

---

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | No |
| **Figma URL** | N/A |
| **Wireframe Status** | N/A |
| **Screen Spec** | N/A |

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Backend | .NET 8 ASP.NET Core | 8.0 LTS |
| ORM | EF Core 8 + Npgsql | 8.0 |
| Caching | StackExchange.Redis + `ICacheService` | 2.8.x |
| DB | PostgreSQL 15.x | 15.x |
| Config | `IOptions<CacheOptions>` | Built-in |

---

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |

---

## Task Overview

This task addresses three related performance concerns that share the same infrastructure layer (EF Core + Redis):

**1. EF Core Connection Pool Upgrade (AC-1/AC-5)**:
`Program.cs` currently uses `AddDbContext<PropelIQDbContext>` which creates a new `DbContext` instance per request. Upgrading to `AddDbContextPool<PropelIQDbContext>` (pool size = 128) enables EF Core to reuse `DbContext` instances between requests, reducing allocation pressure and improving throughput under 200 concurrent users. `AuditLogImmutabilityInterceptor` is registered as a service — compatible with `AddDbContextPool` via the service provider overload.

**2. Slot Availability + Provider Schedule Cache Strategy (AC-3)**:
The existing `ICacheService` is a generic cache with no slot-specific contract. This task adds `ISlotCacheService` — a thin typed wrapper over `ICacheService` with explicit methods, well-defined Redis key patterns, configurable TTLs, and cache invalidation triggers. Cache hit ratio is tracked via atomic Redis counters.

**3. Missing Performance Indexes + EF Core Read-Side Optimization (AC-2)**:
A new EF Core migration adds CONCURRENTLY-built composite indexes on the `Appointments` table that are used by the most frequent read queries (availability search by date range + status, appointments by patient). EF Core `AsNoTracking()` enforcement guidance for all read-only queries.

---

## Dependent Tasks

- **US_021–US_023 (PatientAccess module)**: `Appointment` entity and `AppointmentConfiguration` exist. No changes to entity model — indexes only.
- **`ICacheService` / `RedisCacheService`**: Already registered as scoped. `ISlotCacheService` wraps it — does NOT replace it.
- **task_002_be_resilience_external_services.md** (this US): Builds on the same infrastructure — no ordering dependency.

---

## Implementation Plan

### 1. `AddDbContextPool` upgrade in `Program.cs`

```csharp
// REPLACE AddDbContext with AddDbContextPool (pool size = 128 — EF Core 8 default)
builder.Services.AddDbContextPool<PropelIQDbContext>((sp, options) =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql =>
        {
            npgsql.SetPostgresVersion(15, 0);
            npgsql.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorCodesToAdd: null);
            npgsql.MigrationsAssembly("PatientAccess.Data");
        })
    .AddInterceptors(sp.GetRequiredService<AuditLogImmutabilityInterceptor>())
    // Slow query logging — development only; suppressed in production to avoid noise
    .LogTo(
        s => System.Diagnostics.Debug.WriteLine(s),
        (_, level) => level == LogLevel.Warning,
        DbContextLoggerOptions.DefaultWithUtcTime),
    poolSize: 128);
```

> **`AddDbContextPool` constraint**: Pooled `DbContext` instances are reset between uses via `DbContext.ResetState()`. Any state stored on the `DbContext` instance (e.g. custom properties) is cleared. `AuditLogImmutabilityInterceptor` is safe — it does not store per-request state on the `DbContext`.

### 2. `CacheOptions` — configurable TTLs

```csharp
// PropelIQ.Api/Infrastructure/Caching/CacheOptions.cs
public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    /// <summary>TTL for slot availability cache per provider per date (seconds). Default 5 minutes.</summary>
    public int SlotAvailabilityTtlSeconds { get; set; } = 300;

    /// <summary>TTL for provider weekly schedule cache (seconds). Default 60 minutes.</summary>
    public int ProviderScheduleTtlSeconds { get; set; } = 3_600;

    /// <summary>Window size for cache hit ratio tracking (seconds). Default 5 minutes.</summary>
    public int HitRatioWindowSeconds { get; set; } = 300;
}
```

### 3. `ISlotCacheService` — typed cache contract for slot/schedule data

```csharp
// PropelIQ.Api/Infrastructure/Caching/ISlotCacheService.cs
public interface ISlotCacheService
{
    // Redis key: slots:availability:{staffId}:{date:yyyy-MM-dd}
    Task<IReadOnlyList<AvailableSlotDto>?> GetAvailableSlotsAsync(
        Guid staffId, DateOnly date, CancellationToken ct = default);

    Task SetAvailableSlotsAsync(
        Guid staffId, DateOnly date, IReadOnlyList<AvailableSlotDto> slots,
        CancellationToken ct = default);

    Task InvalidateAvailableSlotsAsync(
        Guid staffId, DateOnly date, CancellationToken ct = default);

    // Redis key: provider:schedule:{staffId}:week:{iso-week}
    Task<ProviderScheduleDto?> GetProviderScheduleAsync(
        Guid staffId, int isoWeekNumber, int year, CancellationToken ct = default);

    Task SetProviderScheduleAsync(
        Guid staffId, int isoWeekNumber, int year, ProviderScheduleDto schedule,
        CancellationToken ct = default);

    Task InvalidateProviderScheduleAsync(
        Guid staffId, CancellationToken ct = default);

    /// <summary>
    /// Returns the cache hit ratio for slot queries in the configured window.
    /// Returns null if no data has been recorded yet.
    /// </summary>
    Task<double?> GetHitRatioAsync(CancellationToken ct = default);
}
```

### 4. `SlotCacheService` — implementation

```csharp
// PropelIQ.Api/Infrastructure/Caching/SlotCacheService.cs
public sealed class SlotCacheService(
    ICacheService cache,
    IConnectionMultiplexer redis,
    IOptions<CacheOptions> opts,
    ILogger<SlotCacheService> logger) : ISlotCacheService
{
    // Key patterns — stable conventions for all slot-related cache entries
    private static string SlotKey(Guid staffId, DateOnly date)
        => $"slots:availability:{staffId}:{date:yyyy-MM-dd}";

    private static string ScheduleKey(Guid staffId, int week, int year)
        => $"provider:schedule:{staffId}:week:{year}-{week:D2}";

    private const string HitCounterKey   = "cache:hits:slots";
    private const string MissCounterKey  = "cache:misses:slots";

    public async Task<IReadOnlyList<AvailableSlotDto>?> GetAvailableSlotsAsync(
        Guid staffId, DateOnly date, CancellationToken ct)
    {
        var result = await cache.GetAsync<List<AvailableSlotDto>>(
            SlotKey(staffId, date), ct);

        var db = redis.GetDatabase();
        if (result is not null)
        {
            await db.StringIncrementAsync(HitCounterKey);
            await db.KeyExpireAsync(HitCounterKey,
                TimeSpan.FromSeconds(opts.Value.HitRatioWindowSeconds), CommandFlags.FireAndForget);
        }
        else
        {
            await db.StringIncrementAsync(MissCounterKey);
            await db.KeyExpireAsync(MissCounterKey,
                TimeSpan.FromSeconds(opts.Value.HitRatioWindowSeconds), CommandFlags.FireAndForget);
        }

        return result;
    }

    public Task SetAvailableSlotsAsync(
        Guid staffId, DateOnly date, IReadOnlyList<AvailableSlotDto> slots, CancellationToken ct)
        => cache.SetAsync(
            SlotKey(staffId, date), slots,
            TimeSpan.FromSeconds(opts.Value.SlotAvailabilityTtlSeconds), ct);

    public Task InvalidateAvailableSlotsAsync(Guid staffId, DateOnly date, CancellationToken ct)
        => cache.RemoveAsync(SlotKey(staffId, date), ct);

    public async Task<ProviderScheduleDto?> GetProviderScheduleAsync(
        Guid staffId, int isoWeekNumber, int year, CancellationToken ct)
        => await cache.GetAsync<ProviderScheduleDto>(ScheduleKey(staffId, isoWeekNumber, year), ct);

    public Task SetProviderScheduleAsync(
        Guid staffId, int isoWeekNumber, int year, ProviderScheduleDto schedule, CancellationToken ct)
        => cache.SetAsync(
            ScheduleKey(staffId, isoWeekNumber, year), schedule,
            TimeSpan.FromSeconds(opts.Value.ProviderScheduleTtlSeconds), ct);

    public async Task InvalidateProviderScheduleAsync(Guid staffId, CancellationToken ct)
    {
        // Scan for all provider:schedule:{staffId}:* keys and delete
        // Using SCAN pattern — safe for production (non-blocking, cursor-based)
        var db = redis.GetDatabase();
        var server = redis.GetServer(redis.GetEndPoints()[0]);
        var pattern = $"provider:schedule:{staffId}:*";
        await foreach (var key in server.KeysAsync(pattern: pattern))
        {
            await db.KeyDeleteAsync(key);
        }
    }

    public async Task<double?> GetHitRatioAsync(CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var hits   = await db.StringGetAsync(HitCounterKey);
        var misses = await db.StringGetAsync(MissCounterKey);

        if (!hits.HasValue && !misses.HasValue) return null;

        var h = hits.HasValue   && long.TryParse(hits, out var hv)   ? hv : 0L;
        var m = misses.HasValue && long.TryParse(misses, out var mv) ? mv : 0L;
        var total = h + m;

        return total == 0 ? null : Math.Round((double)h / total * 100.0, 2);
    }
}

// DTOs — minimal read-side projections for cache serialization
public sealed record AvailableSlotDto(DateTime SlotDatetime, Guid StaffId, bool IsAvailable);
public sealed record ProviderScheduleDto(Guid StaffId, IReadOnlyList<DayScheduleDto> Days);
public sealed record DayScheduleDto(DateOnly Date, IReadOnlyList<AvailableSlotDto> Slots);
```

> **Cache invalidation trigger points** (must be applied in the appointment booking/cancellation command handlers):
> - On booking: `ISlotCacheService.InvalidateAvailableSlotsAsync(staffId, date)` — remove the affected date's slot cache
> - On cancellation/reschedule: same invalidation call
> - On provider schedule change: `InvalidateProviderScheduleAsync(staffId)` — scans and removes all weekly schedule keys for the provider

### 5. Performance Indexes — EF Core migration

Migration name: `AddPerformanceIndexes`

```csharp
// PatientAccess.Data/Migrations/<timestamp>_AddPerformanceIndexes.cs
public partial class AddPerformanceIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Composite index for slot availability search — most frequent read pattern:
        // "Find available slots for provider {staffId} on date range [start, end]"
        migrationBuilder.Sql(
            "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_appointment_staff_slot_status " +
            "ON \"Appointments\" (\"StaffId\", \"SlotDatetime\", \"Status\") " +
            "WHERE \"IsDeleted\" = false;",
            suppressTransaction: true);

        // Partial index for active (non-cancelled, non-no-show) future appointments
        // Used by patient dashboard and conflict detection queries
        migrationBuilder.Sql(
            "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_appointment_patient_future_active " +
            "ON \"Appointments\" (\"PatientId\", \"SlotDatetime\") " +
            "WHERE \"Status\" IN ('Booked', 'Arrived') AND \"IsDeleted\" = false;",
            suppressTransaction: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "DROP INDEX CONCURRENTLY IF EXISTS ix_appointment_staff_slot_status;",
            suppressTransaction: true);
        migrationBuilder.Sql(
            "DROP INDEX CONCURRENTLY IF EXISTS ix_appointment_patient_future_active;",
            suppressTransaction: true);
    }
}
```

### 6. EF Core read-side conventions (enforcement pattern)

All read-only query methods in application services MUST follow this pattern:

```csharp
// CORRECT — read-only query with no-tracking and explicit projection
var slots = await _db.Appointments
    .AsNoTracking()                                // Never track read-only queries
    .Where(a => a.StaffId == staffId
             && a.SlotDatetime.Date == date.ToDateTime(TimeOnly.MinValue).Date
             && a.Status == AppointmentStatus.Booked
             && !a.IsDeleted)
    .Select(a => new AvailableSlotDto(a.SlotDatetime, a.StaffId, true))
    .ToListAsync(ct);

// WRONG — no AsNoTracking() on a read-only query
var slots = await _db.Appointments.Where(...).ToListAsync(ct);  // NEVER do this for reads
```

> Applying `AsNoTracking()` eliminates EF Core change tracking overhead — typically 20-30% faster for read-only queries, critical at 200 concurrent users.

### 7. DI registrations in `Program.cs`

```csharp
// Add after existing ICacheService registration:
builder.Services.AddScoped<ISlotCacheService, SlotCacheService>();
builder.Services.Configure<CacheOptions>(
    builder.Configuration.GetSection(CacheOptions.SectionName));
```

### 8. `appsettings.json` additions

```json
"Cache": {
  "SlotAvailabilityTtlSeconds": 300,
  "ProviderScheduleTtlSeconds": 3600,
  "HitRatioWindowSeconds": 300
}
```

---

## Current Project State

```
server/src/PropelIQ.Api/
├── Program.cs                                      ← MODIFY: AddDbContextPool + ISlotCacheService DI + CacheOptions
├── appsettings.json                                ← MODIFY: add "Cache" section
└── Infrastructure/
    └── Caching/
        ├── ICacheService.cs                        (EXISTS in PatientAccess.Application — no change)
        ├── RedisCacheService.cs                    (EXISTS — no change)
        ├── CacheOptions.cs                         ← CREATE
        ├── ISlotCacheService.cs                    ← CREATE
        └── SlotCacheService.cs                     ← CREATE (includes AvailableSlotDto, ProviderScheduleDto, DayScheduleDto)

server/src/Modules/PatientAccess/PatientAccess.Data/
└── Migrations/
    └── <timestamp>_AddPerformanceIndexes.cs        ← CREATE (2 CONCURRENTLY indexes)
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | `server/src/PropelIQ.Api/Program.cs` | `AddDbContext` → `AddDbContextPool(poolSize: 128)` + slow query logging (debug only) + `ISlotCacheService` scoped + `Configure<CacheOptions>` |
| CREATE | `server/src/PropelIQ.Api/Infrastructure/Caching/CacheOptions.cs` | `SlotAvailabilityTtlSeconds=300`, `ProviderScheduleTtlSeconds=3600`, `HitRatioWindowSeconds=300` |
| CREATE | `server/src/PropelIQ.Api/Infrastructure/Caching/ISlotCacheService.cs` | 5 methods: get/set/invalidate slots, get/set/invalidate schedule, `GetHitRatioAsync` |
| CREATE | `server/src/PropelIQ.Api/Infrastructure/Caching/SlotCacheService.cs` | Wraps `ICacheService`; Redis INCR for hit/miss counters; SCAN + KeyDelete for schedule invalidation |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Data/Migrations/<ts>_AddPerformanceIndexes.cs` | 2 CONCURRENTLY indexes: `ix_appointment_staff_slot_status` (partial, `IsDeleted=false`) + `ix_appointment_patient_future_active` (partial, active future appointments) |
| MODIFY | `server/src/PropelIQ.Api/appsettings.json` | Add `"Cache"` section with 3 TTL/window settings |

---

## External References

- [EF Core — DbContext Pooling](https://learn.microsoft.com/en-us/ef/core/performance/advanced-performance-topics#dbcontext-pooling)
- [EF Core — No-Tracking Queries](https://learn.microsoft.com/en-us/ef/core/querying/tracking#no-tracking-queries)
- [PostgreSQL — Partial Indexes](https://www.postgresql.org/docs/15/indexes-partial.html)
- [StackExchange.Redis — KeysAsync SCAN cursor](https://stackexchange.github.io/StackExchange.Redis/KeysScan)
- [design.md — TR-004 (Redis cache), TR-021 (connection pool min=10 max=100), NFR-008 (concurrent users)](../.propel/context/docs/design.md)

---

## Build Commands

```powershell
# Generate migration
dotnet ef migrations add AddPerformanceIndexes `
  --project server/src/Modules/PatientAccess/PatientAccess.Data `
  --startup-project server/src/PropelIQ.Api `
  --output-dir Migrations

# Build
dotnet build server/PropelIQ.slnx --no-restore
```

---

## Implementation Validation Strategy

- [ ] After `AddDbContextPool` change: `dotnet build` passes — no compilation errors from `AddInterceptors` usage with pooling
- [ ] `CacheOptions` bound correctly: inject `IOptions<CacheOptions>` in a test and verify `SlotAvailabilityTtlSeconds` = 300
- [ ] `ISlotCacheService.GetAvailableSlotsAsync` on cache miss → `ICacheService.GetAsync` returns null → `MissCounterKey` incremented
- [ ] `ISlotCacheService.GetAvailableSlotsAsync` on cache hit → `HitCounterKey` incremented
- [ ] `GetHitRatioAsync`: 9 hits + 1 miss → returns `90.0`
- [ ] `InvalidateAvailableSlotsAsync`: subsequent `GetAvailableSlotsAsync` returns null (cache miss)
- [ ] `InvalidateProviderScheduleAsync`: removes all `provider:schedule:{staffId}:*` keys (verify SCAN deleted 3+ keys seeded by test setup)
- [ ] Migration `AddPerformanceIndexes`: `dotnet ef database update` runs without error; `\d "Appointments"` in psql shows both new partial indexes
- [ ] Appointment booking handler: calls `InvalidateAvailableSlotsAsync` after saving booking

---

## Implementation Checklist

- [ ] MODIFY `Program.cs` — replace `AddDbContext<PropelIQDbContext>` with `AddDbContextPool<PropelIQDbContext>(poolSize: 128)`; add slow query logging via `LogTo` targeting `LogLevel.Warning` (captures queries slower than configured threshold); add `ISlotCacheService` scoped registration; bind `CacheOptions`
- [ ] CREATE `CacheOptions` — 3 properties (`SlotAvailabilityTtlSeconds=300`, `ProviderScheduleTtlSeconds=3600`, `HitRatioWindowSeconds=300`); `SectionName = "Cache"`
- [ ] CREATE `ISlotCacheService` — typed contract with stable Redis key conventions documented in XML summary for each method; include `GetHitRatioAsync`
- [ ] CREATE `SlotCacheService` — (a) get slot: call `ICacheService.GetAsync`, INCR hit or miss counter with `HitRatioWindowSeconds` TTL; (b) set slot: delegate to `ICacheService.SetAsync` with `SlotAvailabilityTtlSeconds`; (c) invalidate slot: `ICacheService.RemoveAsync`; (d) schedule invalidation uses `server.KeysAsync(pattern)` SCAN — async enumerable, do NOT use `server.Keys()` sync version
- [ ] CREATE `AddPerformanceIndexes` migration — 2 CONCURRENTLY partial indexes; `suppressTransaction: true` on all `migrationBuilder.Sql` calls; `Down()` drops both indexes CONCURRENTLY with `IF EXISTS`
- [ ] MODIFY `appsettings.json` — add `"Cache"` section
- [ ] Apply `AsNoTracking()` to all read-only EF Core queries in PatientAccess application services that were identified as missing it during code review (at minimum: appointment availability search, patient appointment list, staff schedule queries)

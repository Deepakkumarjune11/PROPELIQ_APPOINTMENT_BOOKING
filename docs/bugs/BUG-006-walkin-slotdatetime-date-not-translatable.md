# Bug Fix Task - BUG-006

## Bug Report Reference

- **Bug ID**: BUG-006
- **Source**: Runtime discovery — `POST /api/v1/appointments/walk-in` → HTTP 500

---

## Bug Summary

### Issue Classification

- **Priority**: Critical
- **Severity**: Walk-in booking completely broken — every booking attempt returns 500
- **Affected Version**: HEAD (main)
- **Environment**: All — PostgreSQL 15, EF Core 8, Npgsql 8

### Steps to Reproduce

1. Login as `seed-staff-front-desk`
2. Navigate to Walk-In Booking
3. Search and select a patient (e.g. Alice Dev)
4. Select visit type `General`
5. Click **Book Walk-In**
6. `POST /api/v1/appointments/walk-in` with body `{ "patientId": "...", "visitType": "General" }`
7. **Expected**: 200 OK with `{ appointmentId, queuePosition, waitQueue }`
8. **Actual**: 500 Internal Server Error

**Error Output**:

```text
InvalidOperationException: The LINQ expression 'DbSet<Appointment>()
    .Where(a => a.PatientId == __command_PatientId_0
             && a.IsWalkIn
             && a.SlotDatetime.Date == __todayUtc_1
             && !a.IsDeleted)'
could not be translated. Either rewrite the query in a form that can be translated,
or switch to client evaluation explicitly by inserting a call to 'AsEnumerable'...
```

### Root Cause Analysis

- **File**: `server/src/Modules/PatientAccess/PatientAccess.Data/Repositories/WalkInBookingRepository.cs` (lines 43, 50, 57)
- **Component**: `WalkInBookingRepository.BookWalkInAsync`
- **Cause**: Three LINQ queries used `a.SlotDatetime.Date == todayUtc` to filter today's appointments. EF Core / Npgsql **cannot translate `.Date`** (the `DateTime.Date` property) to a PostgreSQL SQL expression. This causes `InvalidOperationException: could not be translated` at runtime → 500.

  The same defect was present in `QueueRepository` and `StaffDashboardRepository` and was already fixed in those files. `WalkInBookingRepository` was missed.

  Additionally, `!a.IsDeleted` was redundant in all three predicates because `HasQueryFilter(a => !a.IsDeleted)` on `AppointmentConfiguration` already excludes soft-deleted rows from every EF Core query.

### Impact Assessment

- **Affected Features**: Walk-in booking — `POST /api/v1/appointments/walk-in`
- **User Impact**: Staff cannot book any walk-in appointment; the core SCR-011 / US_016 workflow is completely blocked
- **Data Integrity Risk**: No data is written (transaction rolls back on exception)
- **Security Implications**: None

---

## Fix Overview

Replace all three `a.SlotDatetime.Date == todayUtc` predicates with sargable range comparisons `a.SlotDatetime >= todayStart && a.SlotDatetime < todayEnd`. Remove redundant `!a.IsDeleted` predicates (covered by global `HasQueryFilter`).

---

## Fix Dependencies

- None (self-contained to one file)

---

## Impacted Components

### Backend — .NET 8

- `PatientAccess.Data/Repositories/WalkInBookingRepository.cs` — MODIFIED

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Data/Repositories/WalkInBookingRepository.cs` | Replace `.Date ==` with `>= todayStart && < todayEnd` in 3 LINQ predicates; remove redundant `!a.IsDeleted` |

### Before

```csharp
var todayUtc = DateTime.UtcNow.Date;

bool alreadyBooked = await _db.Appointments
    .AnyAsync(a => a.PatientId == command.PatientId
                && a.IsWalkIn
                && a.SlotDatetime.Date == todayUtc   // ← not translatable
                && !a.IsDeleted, ct);                // ← redundant

bool slotAvailable = await _db.Appointments
    .AnyAsync(a => a.SlotDatetime.Date == todayUtc  // ← not translatable
                && a.Status == AppointmentStatus.Available
                && !a.IsDeleted, ct);               // ← redundant

int nextPosition = ((await _db.Appointments
    .Where(a => a.IsWalkIn
             && a.SlotDatetime.Date == todayUtc      // ← not translatable
             && !a.IsDeleted)                        // ← redundant
    .MaxAsync(a => (int?)a.QueuePosition, ct)) ?? 0) + 1;
```

### After

```csharp
var todayStart = DateTime.UtcNow.Date;
var todayEnd   = todayStart.AddDays(1);

bool alreadyBooked = await _db.Appointments
    .AnyAsync(a => a.PatientId == command.PatientId
                && a.IsWalkIn
                && a.SlotDatetime >= todayStart
                && a.SlotDatetime < todayEnd, ct);

bool slotAvailable = await _db.Appointments
    .AnyAsync(a => a.SlotDatetime >= todayStart
                && a.SlotDatetime < todayEnd
                && a.Status == AppointmentStatus.Available, ct);

int nextPosition = ((await _db.Appointments
    .Where(a => a.IsWalkIn
             && a.SlotDatetime >= todayStart
             && a.SlotDatetime < todayEnd)
    .MaxAsync(a => (int?)a.QueuePosition, ct)) ?? 0) + 1;
```

---

## Implementation Plan

1. Replace `var todayUtc = DateTime.UtcNow.Date` with `var todayStart` / `var todayEnd` pair
2. Rewrite all 3 `SlotDatetime.Date ==` predicates as `>= todayStart && < todayEnd`
3. Remove `!a.IsDeleted` from all 3 predicates (covered by `HasQueryFilter`)

---

## Regression Prevention Strategy

- [ ] Integration test: `POST /api/v1/appointments/walk-in` returns 200 with valid `queuePosition`
- [ ] Integration test: second booking for same patient today returns 409
- [ ] Unit test: `LINQ` predicate uses `>=` / `<` range, not `.Date`

---

## Rollback Procedure

1. Revert `WalkInBookingRepository.cs` to `.Date ==` predicates (restores 500)

---

## External References

- [EF Core — Supported LINQ operations for Npgsql](https://www.npgsql.org/efcore/mapping/translations.html)
- Related fixes: `QueueRepository.cs`, `StaffDashboardRepository.cs` (same pattern)

---

## Build Commands

```powershell
cd server/src/Modules/PatientAccess/PatientAccess.Data
dotnet build -v minimal
```

---

## Implementation Validation Strategy

- [ ] `POST /api/v1/appointments/walk-in` returns 200
- [ ] Response contains `appointmentId`, `queuePosition`, `waitQueue`
- [ ] No `InvalidOperationException: could not be translated` in server logs

## Implementation Checklist

- [x] `todayUtc` replaced with `todayStart` / `todayEnd` range variables
- [x] All 3 `SlotDatetime.Date ==` replaced with `>= todayStart && < todayEnd`
- [x] Redundant `!a.IsDeleted` predicates removed
- [ ] Regression test added

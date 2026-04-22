# Bug Fix Task - BUG-007

## Bug Report Reference

- **Bug ID**: BUG-007
- **Source**: Runtime discovery — `POST /api/v1/appointments/walk-in` → HTTP 500 (after BUG-006 fix)

---

## Bug Summary

### Issue Classification

- **Priority**: High
- **Severity**: Walk-in booking returns 500 even though the appointment is successfully committed to the database
- **Affected Version**: HEAD (main)
- **Environment**: Dev — Redis not running locally (localhost:6379 unreachable)

### Steps to Reproduce

1. Ensure Redis is **not** running locally
2. Login as `seed-staff-front-desk`
3. Select a patient and submit a walk-in booking
4. `POST /api/v1/appointments/walk-in`
5. **Expected**: 200 OK — appointment committed, 500ms Redis timeout logged as warning
6. **Actual**: 500 Internal Server Error — `RedisTimeoutException` propagates up through `BookWalkInHandler`

**Error Output**:

```text
StackExchange.Redis.RedisTimeoutException: Timeout awaiting response (outbound=0KiB, inbound=0KiB,
  timeout=500ms, elapsed=500ms), command=DEL, ...
   at PropelIQ.Api.Infrastructure.Caching.RedisCacheService.RemoveAsync(String key, CancellationToken ct)
   at PatientAccess.Application.Appointments.Commands.BookWalkIn.BookWalkInHandler.Handle(...)
```

### Root Cause Analysis

- **File**: `server/src/PropelIQ.Api/Infrastructure/Caching/RedisCacheService.cs`
- **Component**: `RedisCacheService` — `GetAsync`, `SetAsync`, `RemoveAsync`
- **Cause**: All three methods caught only `RedisConnectionException` (thrown when the connection is physically refused at connect time). However, StackExchange.Redis throws `RedisTimeoutException` when a command times out on an already-connected (or lazily-connected) multiplexer — a **different** exception class.

  `RedisTimeoutException` and `RedisConnectionException` both inherit from `RedisException` but are sibling classes. The catch block did not cover `RedisTimeoutException`, so when Redis is offline and the async timeout fires (500ms in dev), the exception propagated uncaught → `BookWalkInHandler.Handle` threw → 500.

  The booking itself **succeeded** (transaction committed before `RemoveAsync` was called) but the uncaught cache exception caused the HTTP response to be 500 instead of 200.

  Additionally `Program.cs` hard-coded `SyncTimeout = 3_000` and `ConnectTimeout = 5_000` which overrode the 500ms values in `appsettings.Development.json`, causing unnecessary 3-second hangs per cache operation.

### Impact Assessment

- **Affected Features**: Walk-in booking (`POST /api/v1/appointments/walk-in`), and any other endpoint that calls `ICacheService` when Redis is offline
- **User Impact**: Staff receives 500 error despite the booking being created — confusing duplicate booking risk on retry
- **Data Integrity Risk**: Low — appointment is committed; retrying causes duplicate booking 409 (which is correct behaviour)
- **Security Implications**: None

---

## Fix Overview

1. Widen the catch clauses in `RedisCacheService` from `RedisConnectionException` to the base `RedisException` class — covers both `RedisConnectionException` and `RedisTimeoutException`
2. Fix Program.cs Redis options to use 500ms fail-fast timeouts in dev

---

## Fix Dependencies

- None

---

## Impacted Components

### Backend — .NET 8

- `PropelIQ.Api/Infrastructure/Caching/RedisCacheService.cs` — MODIFIED
- `PropelIQ.Api/Program.cs` — MODIFIED (Redis timeouts)

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | `server/src/PropelIQ.Api/Infrastructure/Caching/RedisCacheService.cs` | `catch (RedisConnectionException)` → `catch (RedisException ex)` in all 3 methods; add exception to log |
| MODIFY | `server/src/PropelIQ.Api/Program.cs` | `SyncTimeout = 3_000` → `500`; `ConnectTimeout = 5_000` → `500`; add `AsyncTimeout = 500` |

---

## Implementation Plan

1. In `RedisCacheService`, replace all `catch (RedisConnectionException)` with `catch (RedisException ex)` and pass `ex` to `LogWarning`
2. In `Program.cs`, change hard-coded Redis timeout values to 500ms fail-fast

---

## Regression Prevention Strategy

- [ ] Integration test: `POST /api/v1/appointments/walk-in` returns 200 when Redis is offline
- [ ] Unit test: `RedisCacheService.RemoveAsync` with mocked `RedisTimeoutException` — assert no throw, warning logged
- [ ] Unit test: `RedisCacheService.GetAsync` with mocked `RedisTimeoutException` — assert returns `default`

---

## Rollback Procedure

1. Revert `RedisCacheService.cs` catch clauses back to `RedisConnectionException`
2. Revert Program.cs timeout values

---

## External References

- [StackExchange.Redis exception hierarchy](https://stackexchange.github.io/StackExchange.Redis/Exceptions.html)
- `RedisException` → `RedisConnectionException`, `RedisTimeoutException`, `RedisCommandException`

---

## Build Commands

```powershell
cd server/src/PropelIQ.Api
dotnet build -v minimal
```

---

## Implementation Validation Strategy

- [ ] `POST /api/v1/appointments/walk-in` returns 200 regardless of Redis state
- [ ] Server logs show `WARN: Cache remove skipped (Redis unavailable)` instead of throwing
- [ ] No `RedisTimeoutException` in server logs

## Implementation Checklist

- [x] `catch (RedisConnectionException)` → `catch (RedisException ex)` in `GetAsync`
- [x] `catch (RedisConnectionException)` → `catch (RedisException ex)` in `SetAsync`
- [x] `catch (RedisConnectionException)` → `catch (RedisException ex)` in `RemoveAsync`
- [x] Redis `ConnectTimeout`, `SyncTimeout`, `AsyncTimeout` set to 500ms in `Program.cs`
- [ ] Regression tests added

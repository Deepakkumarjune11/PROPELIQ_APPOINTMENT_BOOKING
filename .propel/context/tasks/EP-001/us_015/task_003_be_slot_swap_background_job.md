# Task - task_003_be_slot_swap_background_job

## Requirement Reference

- **User Story**: US_015 — Preferred Slot Swap Watchlist
- **Story Location**: `.propel/context/tasks/EP-001/us_015/us_015.md`
- **Acceptance Criteria**:
  - AC-3: When the preferred slot becomes available, the system atomically moves the appointment to the preferred slot, releases the original slot, and notifies the patient via SMS/email per FR-005.
  - AC-4: After the swap executes, the appointment shows the new slot datetime and the watchlist status is cleared (`preferred_slot_id = null`).
  - AC-5: If another patient claims the preferred slot first (race condition), the original appointment remains unchanged and the watchlist entry is preserved.
- **Edge Cases**:
  - Notification delivery fails after swap → swap is already committed; failure logged; notification retried via Hangfire retry policy (up to 3 attempts with exponential backoff).
  - Double-execution guard → check at start of transaction if appointment is still on watchlist before proceeding; idempotent on re-run.
  - Preferred slot no longer eligible (e.g., slot window passed) → remove watchlist entry, notify patient, keep original appointment.
  - Multiple watchlist entries competing for the same slot → only one succeeds (serializable transaction / row-level lock); others retain their original appointments.

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
| Backend | .NET 8 ASP.NET Core | 8.0 LTS |
| Background Jobs | Hangfire | 1.8.x |
| ORM | Entity Framework Core | 8.0 |
| Database | PostgreSQL | 15.x |
| SMS | Twilio Programmable SMS (free tier) | latest |
| Email | SendGrid Email API (free tier) | latest |
| Logging | Serilog | 3.x |
| Testing - Unit | xUnit + Moq | 2.x / 4.x |

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

Implement the Hangfire recurring background job that monitors the swap watchlist and executes atomic slot swaps when preferred slots become available (UC-002 background monitoring path from models.md).

The job runs every 5 minutes and performs the following:
1. Polls all `Appointment` records where `preferred_slot_id IS NOT NULL` (watchlist entries).
2. For each watchlist entry, checks if the preferred slot's datetime/provider is now free (no active booking).
3. If free: executes an atomic PostgreSQL transaction — row-lock preferred slot, update appointment datetime, release original slot, clear `preferred_slot_id`, write audit log.
4. After commit: dispatches swap confirmation notifications (SMS + email) via the shared notification service from US_014.
5. If slot still taken: skip, preserving the watchlist entry.
6. If preferred slot datetime has passed: remove watchlist entry, send "slot expired" notification, retain original appointment.

The implementation follows the existing Hangfire job pattern established in US_014 (notification/reminder jobs). A `DisableConcurrentExecution` Hangfire attribute prevents overlapping job executions.

---

## Dependent Tasks

- **task_004_db_preferred_slot_schema.md** (US_015) — `preferred_slot_id` index and FK must exist.
- **task_002_be_watchlist_registration_api.md** (US_015) — watchlist entries created by this task's API.
- **task_002_be_notifications_jobs.md** (US_014) — `INotificationService` (Twilio SMS + SendGrid email) interface must be available for reuse.

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Jobs/SlotSwapJob.cs` | Hangfire recurring job — watchlist polling + atomic swap logic |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Jobs/SlotSwapResult.cs` | Value object: `Swapped`, `SlotStillTaken`, `SlotExpired`, `NotOnWatchlist` |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Appointments/Services/SlotSwapService.cs` | Domain service encapsulating the atomic swap transaction |
| MODIFY | `server/src/PropelIQ.Api/Program.cs` | Register `SlotSwapJob` as Hangfire recurring job (`Cron.FromSeconds(300)` = every 5 minutes) |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Presentation/ServiceCollectionExtensions.cs` | Register `SlotSwapService` DI |

---

## Implementation Plan

1. **`SlotSwapResult`** value object — outcome discriminated union:
   ```csharp
   public enum SlotSwapResult { Swapped, SlotStillTaken, SlotExpired, NotOnWatchlist }
   ```

2. **`SlotSwapService`** — core atomic swap logic:
   ```csharp
   public async Task<SlotSwapResult> TrySwapAsync(
       Guid appointmentId,
       CancellationToken cancellationToken)
   {
       // Use IDbContextTransaction (SERIALIZABLE isolation) for row-level locking
       using var tx = await _context.Database
           .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
       try
       {
           // Step 1: Reload appointment with pessimistic lock (FOR UPDATE via raw SQL)
           var appointment = await _context.Appointments
               .FromSqlRaw("SELECT * FROM appointments WHERE id = {0} FOR UPDATE", appointmentId)
               .Include(a => a.PreferredSlot)
               .SingleOrDefaultAsync(cancellationToken);

           if (appointment?.PreferredSlotId is null)
               return SlotSwapResult.NotOnWatchlist;

           // Step 2: Reject if preferred slot datetime has passed
           if (appointment.PreferredSlot!.SlotDatetime <= DateTimeOffset.UtcNow)
           {
               appointment.PreferredSlotId = null;
               _auditLogger.Log(actor: SystemActor, action: "WatchlistExpired", target: appointmentId);
               await _context.SaveChangesAsync(cancellationToken);
               await tx.CommitAsync(cancellationToken);
               return SlotSwapResult.SlotExpired;
           }

           // Step 3: Check preferred slot is free (no active booking at that datetime/provider)
           bool preferredSlotFree = !await _context.Appointments.AnyAsync(
               a => a.SlotDatetime == appointment.PreferredSlot.SlotDatetime
                    && a.ProviderId == appointment.ProviderId
                    && a.Id != appointmentId
                    && a.Status == AppointmentStatus.Booked,
               cancellationToken);

           if (!preferredSlotFree)
               return SlotSwapResult.SlotStillTaken;

           // Step 4: Atomic swap
           var originalDatetime = appointment.SlotDatetime;
           appointment.SlotDatetime = appointment.PreferredSlot.SlotDatetime;
           appointment.PreferredSlotId = null;   // clears watchlist

           _auditLogger.Log(actor: SystemActor, action: "SlotSwapExecuted",
               target: appointmentId,
               payload: new { From = originalDatetime, To = appointment.SlotDatetime });

           await _context.SaveChangesAsync(cancellationToken);
           await tx.CommitAsync(cancellationToken);
           return SlotSwapResult.Swapped;
       }
       catch
       {
           await tx.RollbackAsync(cancellationToken);
           throw;
       }
   }
   ```

3. **`SlotSwapJob`** — Hangfire job entry point:
   ```csharp
   [DisableConcurrentExecution(timeoutInSeconds: 240)]   // prevents overlapping runs
   public class SlotSwapJob
   {
       public async Task ExecuteAsync(CancellationToken cancellationToken)
       {
           var watchlistIds = await _context.Appointments
               .Where(a => a.PreferredSlotId != null && !a.IsDeleted)
               .Select(a => a.Id)
               .ToListAsync(cancellationToken);

           foreach (var appointmentId in watchlistIds)
           {
               var result = await _slotSwapService.TrySwapAsync(appointmentId, cancellationToken);

               if (result == SlotSwapResult.Swapped)
               {
                   // Dispatch notification outside of transaction (swap already committed)
                   BackgroundJob.Enqueue<SwapNotificationJob>(
                       j => j.SendAsync(appointmentId, CancellationToken.None));
               }
               else if (result == SlotSwapResult.SlotExpired)
               {
                   BackgroundJob.Enqueue<WatchlistExpiredNotificationJob>(
                       j => j.SendAsync(appointmentId, CancellationToken.None));
               }
               // SlotStillTaken: no-op, watchlist preserved
           }
       }
   }
   ```

4. **`SwapNotificationJob`** — lightweight Hangfire job enqueued after successful swap:
   - Calls `INotificationService.SendSmsAsync` and `INotificationService.SendEmailAsync` (reuse from US_014).
   - Hangfire retry policy: `[AutomaticRetry(Attempts = 3)]` with exponential backoff.
   - On all retries exhausted: logs `CommunicationLog` with `Status = Failed`; does NOT roll back the already-committed swap.

5. **`WatchlistExpiredNotificationJob`** — notifies patient that their preferred slot is no longer available; sends SMS/email message "Your preferred slot on {date} is no longer available. Your original appointment on {original_date} remains confirmed."

6. **Register recurring job in `Program.cs`**:
   ```csharp
   RecurringJob.AddOrUpdate<SlotSwapJob>(
       "slot-swap-watchlist",
       job => job.ExecuteAsync(CancellationToken.None),
       Cron.FromSeconds(300)   // every 5 minutes
   );
   ```

---

## Current Project State

```
server/src/
  Modules/
    PatientAccess/
      PatientAccess.Application/
        Class1.cs                           ← placeholder, replace
      PatientAccess.Domain/
        (Appointment entity with preferred_slot_id from task_004)
  PropelIQ.Api/
    Program.cs                              ← Hangfire already configured from US_014
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Jobs/SlotSwapJob.cs` | Hangfire recurring job — watchlist poll + per-entry swap call |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Jobs/SwapNotificationJob.cs` | Post-commit notification dispatch (SMS + email) |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Jobs/WatchlistExpiredNotificationJob.cs` | Notification when preferred slot passes its datetime |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Appointments/Services/SlotSwapService.cs` | Atomic swap domain service with SERIALIZABLE transaction |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Application/Jobs/SlotSwapResult.cs` | Swap outcome enum |
| MODIFY | `server/src/PropelIQ.Api/Program.cs` | Register `SlotSwapJob` recurring job (every 5 minutes) |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Presentation/ServiceCollectionExtensions.cs` | Register `SlotSwapService` with DI |

---

## External References

- [Hangfire recurring jobs — official docs](https://docs.hangfire.io/en/latest/background-methods/performing-recurrent-background-jobs.html)
- [Hangfire `DisableConcurrentExecution` filter](https://docs.hangfire.io/en/latest/background-processing/throttling.html)
- [Hangfire `AutomaticRetry` attribute](https://docs.hangfire.io/en/latest/background-methods/dealing-with-exceptions.html)
- [EF Core — raw SQL for pessimistic locking (`FOR UPDATE`)](https://learn.microsoft.com/en-us/ef/core/querying/sql-queries)
- [PostgreSQL transaction isolation levels — SERIALIZABLE](https://www.postgresql.org/docs/15/transaction-iso.html)
- [.NET `IDbContextTransaction` — EF Core 8 transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions)
- [NFR-016 circuit breaker — external service notification retries](../.propel/context/docs/design.md#NFR-016)

---

## Build Commands

```bash
# Restore and build
cd server && dotnet restore ; dotnet build

# Run API (Hangfire dashboard at /hangfire)
dotnet run --project src/PropelIQ.Api/PropelIQ.Api.csproj

# Run unit tests
dotnet test --filter "Category=Unit"
```

---

## Implementation Validation Strategy

- [ ] Unit tests pass for `SlotSwapService.TrySwapAsync`: test all 4 outcomes (`Swapped`, `SlotStillTaken`, `SlotExpired`, `NotOnWatchlist`)
- [ ] Concurrent execution test: two `TrySwapAsync` calls for same preferred slot — only one succeeds, other returns `SlotStillTaken`
- [ ] After `Swapped`: `appointment.SlotDatetime` updated, `appointment.PreferredSlotId = null`, AuditLog entry written
- [ ] After `SlotExpired`: `appointment.PreferredSlotId = null`, AuditLog entry written, expiry notification enqueued
- [ ] Notification failure does NOT roll back committed swap
- [ ] Hangfire `DisableConcurrentExecution` prevents overlapping job runs
- [ ] `SwapNotificationJob` retries 3× on notification failure before logging `CommunicationLog.Status = Failed`
- [ ] Recurring job registered at `/hangfire` dashboard as "slot-swap-watchlist" with 5-minute interval

---

## Implementation Checklist

- [x] Create `SlotSwapResult` outcome enum
- [x] Create `SlotSwapService` with SERIALIZABLE transaction, pessimistic row lock, 4-step swap logic, and AuditLog writes
- [x] Create `SlotSwapJob` with `[DisableConcurrentExecution]`, watchlist poll, and per-entry `TrySwapAsync` dispatch
- [x] Create `SwapNotificationJob` with `[AutomaticRetry(Attempts = 3)]` reusing `INotificationService`
- [x] Create `WatchlistExpiredNotificationJob` for expired slot scenarios
- [x] Register `SlotSwapJob` recurring job in `Program.cs` with 5-minute cron
- [x] Register `SlotSwapService` in `ServiceCollectionExtensions.cs`
- [x] Write unit tests covering all `SlotSwapService` outcome branches including concurrency scenario

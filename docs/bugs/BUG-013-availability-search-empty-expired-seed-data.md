# BUG-013 — Availability Search Always Returns Empty (Expired Seed Data)

| Field        | Value                                                                 |
|--------------|-----------------------------------------------------------------------|
| **ID**       | BUG-013                                                               |
| **Screen**   | SCR-001 — Availability Search / Book Appointment                      |
| **Severity** | Critical — core booking flow completely unusable                      |
| **Status**   | Fixed                                                                 |
| **Reporter** | QA / Manual Testing                                                   |
| **Fixed in** | `server/seed_available_slots.sql`                                     |

---

## Symptom

When a patient opens the availability search page and clicks **Search**, the page always shows:

> **No appointments available**  
> Try selecting different dates or contact the clinic.

This happens for any date range including today through the next 7 days (the default).

---

## Root Cause

### Seed data expiration

`seed_available_slots.sql` inserts appointment slots for `CURRENT_DATE + 1` through `CURRENT_DATE + 14` **at the time the script is first executed**.

The migration was applied on approximately **2026-04-22**, so the seeded slots cover **April 23 – May 6**. Today is **May 7, 2026** — every seeded slot is in the past.

The `AvailabilityRepository.GetAvailableSlotsAsync` correctly filters:

```csharp
.Where(a =>
    a.Status == AppointmentStatus.Available &&
    a.SlotDatetime >= startUtc &&   // today or later
    a.SlotDatetime <= endUtc &&
    !a.IsDeleted)
```

No row passes both `SlotDatetime >= today` and `Status = Available`, so the API returns an empty list and the frontend renders the empty state.

### Idempotency guard blocks re-seeding

The seed script has this guard:

```sql
AND NOT EXISTS (SELECT 1 FROM appointment WHERE "Status" = 'available' LIMIT 1);
```

The **expired** slots from April still have `Status = 'available'` in the database (they were never booked). The guard sees them and skips the INSERT, so re-running the seed has no effect.

---

## Impact

- The primary patient-facing feature (booking an appointment) is completely non-functional.
- The bug is silent: no error is thrown, no log warning is emitted — the API simply returns `[]`.
- The issue will recur every ~14 days whenever all seeded slots age past the current date.

---

## Fix

### `server/seed_available_slots.sql`

Two changes:

1. **Delete expired available slots** — removes stale rows that would otherwise trigger the idempotency guard and clutter the database.
2. **Fix the idempotency guard** — now checks `AND "SlotDatetime" > NOW()` so the seed is skipped only when **future** available slots already exist.

```sql
-- Step 1: Remove expired available slots (past date, never booked) so they don't
-- block the idempotency guard and don't pollute availability queries.
DELETE FROM appointment
WHERE "Status" = 'available'
  AND "SlotDatetime" <= NOW();

-- Step 2: Insert fresh future slots.
-- Guard: skip when future available slots already exist (idempotent).
INSERT INTO appointment (...)
SELECT ...
WHERE ...
  AND NOT EXISTS (
    SELECT 1 FROM appointment
    WHERE  "Status"       = 'available'
      AND  "SlotDatetime" > NOW()       -- BUG-013: guard on FUTURE slots only
    LIMIT 1
  );
```

---

## Verification

1. Run the fixed seed script against the database.
2. Log in as a Patient and open the Availability Search page.
3. Default date range (today → today+7) should show appointment slots.
4. Re-running the seed while future slots exist should be a no-op (idempotent).
5. After all future slots are consumed, re-running the seed inserts a fresh 14-day window.

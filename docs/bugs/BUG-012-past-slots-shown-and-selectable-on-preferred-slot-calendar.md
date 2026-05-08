# BUG-012 — Past Slots Shown and Selectable on Preferred Slot Calendar

| Field        | Value                                                              |
|--------------|--------------------------------------------------------------------|
| **ID**       | BUG-012                                                            |
| **Screen**   | SCR-009 — Preferred Slot Selection                                 |
| **Severity** | High — patients can attempt to watchlist expired appointment slots |
| **Status**   | Fixed                                                              |
| **Reporter** | QA / Manual Testing                                                |
| **Fixed in** | `WatchlistRepository.cs`, `PreferredSlotSelectionPage.tsx`         |

---

## Symptom

When a patient opens the preferred slot calendar on a day that is part of the **current month but already in the past** (e.g., viewing May 2026 on May 7 — days 1–6 have already passed):

1. Past days are **not disabled** in the `DateCalendar` — they are fully clickable.
2. Selecting a past day shows its time-slot chips, which are **selectable**.
3. Clicking **Confirm preferred slot** on a past slot makes an API call that the backend rejects with `400 Conflict: "The preferred slot datetime must be in the future."` — but the UI shows a generic error toast, confusing the patient.
4. Same issue applies to **today's already-elapsed time slots** (e.g., a 9 AM slot shown at 3 PM).

---

## Root Cause

### Backend — `WatchlistRepository.GetSlotsForMonthAsync`

The query filters only by month range, not by future datetime:

```csharp
// Before fix — no > DateTime.UtcNow guard
.Where(a =>
    a.SlotDatetime >= startUtc &&
    a.SlotDatetime < endUtc &&
    !a.IsDeleted)
```

Past slots from the current month (days 1–6 of May when today is May 7, and elapsed hours of today) are returned to the frontend.

### Frontend — `PreferredSlotSelectionPage.tsx`

1. **`daysWithAnySlots`** builds a Set from all returned slots, including past ones. This allows `shouldDisableDate` to mark past days as **enabled**.
2. **`DateCalendar`** has no `disablePast` prop — MUI's built-in past-date guard is never activated.
3. **`slotsForDay`** includes all slots for the selected day, even elapsed time slots on today.

---

## Impact

- Patient UX degraded: past dates are visually indistinguishable from future dates.
- Patients can select past slots, submit the form, receive a cryptic error, and have no idea they selected an expired time.
- Every failed submission is a wasted API round-trip.

---

## Fix

### Backend — `WatchlistRepository.cs`

Added `&& a.SlotDatetime > DateTime.UtcNow` to the EF Core query so only truly future slots are returned:

```csharp
// After fix
.Where(a =>
    a.SlotDatetime >= startUtc &&
    a.SlotDatetime < endUtc &&
    a.SlotDatetime > DateTime.UtcNow &&  // BUG-012: exclude past/elapsed slots
    !a.IsDeleted)
```

### Frontend — `PreferredSlotSelectionPage.tsx`

Three coordinated changes:

1. **`daysWithAnySlots`** — filters source `allSlots` to only future datetimes before building the Set, so past days are no longer considered to "have slots".
2. **`DateCalendar`** — added `disablePast` prop, which activates MUI's built-in grey-out of all past days regardless of slot data.
3. **`slotsForDay`** — filtered to exclude elapsed slots on today (e.g., 9 AM when current time is 3 PM).

---

## Verification

1. Log in as a Patient. Open `/appointments/:appointmentId/preferred-slot`.
2. Current month view: days 1–6 (past) are greyed out and not clickable.
3. Today: elapsed hours are not shown in the slot grid.
4. Tomorrow and beyond: slots render and behave correctly.
5. Navigating to a past month is not possible (calendar prev-arrow disabled when at current month).

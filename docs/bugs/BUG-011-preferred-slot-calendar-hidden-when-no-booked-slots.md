# BUG-011 — Preferred Slot Calendar Hidden When No Booked Slots Exist

| Field        | Value                                                          |
|--------------|----------------------------------------------------------------|
| **ID**       | BUG-011                                                        |
| **Screen**   | SCR-009 — Preferred Slot Selection                             |
| **Severity** | High — core feature unusable in dev/demo environments          |
| **Status**   | Fixed                                                          |
| **Reporter** | QA / Manual Testing                                            |
| **Fixed in** | `PreferredSlotSelectionPage.tsx`                               |

---

## Symptom

When a patient navigates to `/appointments/:appointmentId/preferred-slot`, the page immediately shows:

> **No slots currently available**  
> Select your preferred time slot and we'll notify you when it becomes available.

No `DateCalendar` is rendered. There is no way to browse any month or select any slot.

---

## Root Cause

### Frontend logic (pre-fix)

```tsx
// eligibleDays only accumulates days that have BOOKED/ARRIVED slots
const eligibleDays = useMemo(() => {
  const days = new Set<string>();
  allSlots
    .filter((s) => !s.isAvailable)  // isAvailable=false → Booked/Arrived only
    .forEach((s) => { ... days.add(key); });
  return days;
}, [allSlots]);

const hasEligibleSlots = eligibleDays.size > 0;

// Calendar is completely hidden when no booked slots exist
if (!hasEligibleSlots) {
  return <EmptyStateAlert />;
}
return <DateCalendar ... />;
```

### Backend behaviour

`GetSlotsForMonthAsync` correctly returns **all** appointment rows for the month:
- `Status = Available` → `IsAvailable = true` (open slot, book directly)
- `Status = Booked / Arrived` → `IsAvailable = false` (watchlist eligible)

### Why the empty state always fires in dev/test

The seed script (`seed_available_slots.sql`) inserts appointment rows with `Status = 'available'` only. Until at least one other patient books a slot (setting `Status = 'booked'`), **every slot returned by the API has `isAvailable: true`**. Therefore `eligibleDays.size === 0` on every render and the calendar is never shown.

There is no mechanism to navigate to a different month because the `DateCalendar` itself is never rendered.

---

## Impact

1. The preferred-slot feature is **completely unusable** in any environment where no patient has yet booked an appointment.
2. Even in production, a patient might open this page in a future month where no bookings exist yet — the same blank state appears with no way to look at other months.
3. The feature is gated behind a condition that is impossible to satisfy until real patient data exists, creating a chicken-and-egg problem during development and QA.

---

## Fix

### Changed file

`client/src/pages/preferred-slot/PreferredSlotSelectionPage.tsx`

### Strategy

1. **Always render the `DateCalendar`** (after the API load) so the patient can browse months.
2. **`daysWithAnySlots`** — a new Set that includes every day with at least one slot (available OR booked). Used by `shouldDisableDate` so the calendar only disables days with zero slots.
3. **`eligibleDays`** — unchanged semantics (days with booked/arrived slots). Used to apply a `highlighted` class to selectable days in the calendar.
4. **Slot grid shows all slots for the selected day**:
   - `isAvailable = false` (booked/arrived) → selectable chip labelled "Watchlist"
   - `isAvailable = true` (available) → disabled chip labelled "Available" with tooltip "This slot is open — book it directly"
5. **Empty guidance alert** still shown, but now only when the **selected day** has no watchlist-eligible slots (instead of hiding the whole calendar).
6. When no slots exist at all for the current month the patient can still navigate to a different month via the calendar arrows.

---

## Verification

1. Log in as a Patient.
2. Navigate to `/appointments/:appointmentId/preferred-slot`.
3. **Before fix**: page shows empty-state alert, no calendar.
4. **After fix**: calendar renders; days with slots are clickable; available slots show as disabled chips; booked slots show as selectable chips.
5. Select a booked slot and click **Save Preferred Slot** — 200 OK returned, confirmation toast shown.

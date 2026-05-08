# BUG-009 — Select Preferred Slot: no API call fires, no data shown

| Field       | Value                                                            |
|-------------|------------------------------------------------------------------|
| **ID**      | BUG-009                                                          |
| **Status**  | Fixed                                                            |
| **Severity**| High — watchlist feature (US_015) completely non-functional      |
| **Area**    | Preferred slot selection · SCR-009 · My Appointments            |
| **Reported**| 2026-05-06                                                       |

---

## Symptom

Clicking **"Select preferred slot"** on an appointment card navigates to SCR-009
(`/appointments/:id/preferred-slot`). The page shows a loading skeleton that never
resolves: no API call is made to `GET /api/v1/slots/availability`, the calendar never
renders, and no slots appear.

---

## Root Cause (Three-part)

### Part A — Query permanently disabled (`enabled: false`)

`PreferredSlotSelectionPage` guards the slot-availability query with:

```ts
enabled: Boolean(appointment?.providerId),
```

`appointment?.providerId` is sourced from the `['appointments']` cache, which is populated
by `GET /api/v1/appointments`. The backend `GetPatientAppointmentsHandler` hardcodes
`ProviderId: null` for every row (provider identity is not stored on the `Appointment`
entity in the current schema). Therefore `Boolean(null)` = `false` — the query is
**permanently disabled** and `GET /api/v1/slots/availability` is never called.

### Part B — Response field name mismatch (`available` vs `isAvailable`)

The backend `SlotAvailabilityDto` serialises as:

```json
{ "datetime": "2026-05-10T09:00:00Z", "isAvailable": false }
```

The frontend `SlotAvailabilityEntry` interface declares:

```ts
available: boolean;   // ← wrong casing
```

`available` is `undefined` on every entry. In the slot-grid rendering:

```ts
const isEligible = !slot.available;  // !undefined === true → ALL slots shown as eligible
```

Every slot — including open bookable ones — appears selectable. Eligibility-disabled
rendering (greyed, "Available" label) is never triggered.

### Part C — `AppointmentDto` nullable fields typed as non-nullable

`providerName: string`, `providerId: string`, `visitType: string` are declared non-nullable
in the frontend TS type, while the backend always returns `null` for these fields.
This masks the Part A bug at compile-time (`appointment!.providerId` silences the `null`
warning) and causes type lies that can produce runtime surprises in other consumers.

---

## Fix

### Frontend — `client/src/api/appointments.ts`

- `AppointmentDto.providerName/providerId/visitType` → `string | null`
- `SlotAvailabilityEntry.available` → `isAvailable` (matches backend camelCase)
- `getSlotAvailability` `providerId` parameter → `string | null | undefined` (optional)

### Frontend — `client/src/pages/preferred-slot/PreferredSlotSelectionPage.tsx`

- `queryKey`: replace `appointment?.providerId` with `appointmentId` (stable, from URL)
- `queryFn`: pass `appointment?.providerId ?? undefined` (not required by backend)
- `enabled`: `Boolean(appointment?.providerId)` → `!!appointmentId` (not appointment-dependent)
- All `slot.available` / `!slot.available` → `slot.isAvailable` / `!slot.isAvailable`

---

## Files Changed

| File | Change |
|------|--------|
| `client/src/api/appointments.ts` | Nullable DTO fields; `available` → `isAvailable` on `SlotAvailabilityEntry`; optional `providerId` on `getSlotAvailability` |
| `client/src/pages/preferred-slot/PreferredSlotSelectionPage.tsx` | Fixed `enabled`, `queryKey`, `queryFn`, all `slot.available` references |

---

## Verification

After fix:
1. Navigating to SCR-009 immediately fires `GET /api/v1/slots/availability?year=…&month=…`.
2. Booked slots render as selectable (teal); available slots render greyed with "Available" label.
3. Selecting a booked slot and clicking Confirm fires `POST /api/v1/appointments/:id/preferred-slot`.
4. My Appointments tab refreshes and shows the orange "Watchlist" chip on the appointment.

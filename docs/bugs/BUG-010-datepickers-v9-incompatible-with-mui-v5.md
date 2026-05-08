# BUG-010 — Preferred Slot Page Crashes with "Something went wrong" on Navigation

| Field        | Value                                                                   |
|--------------|-------------------------------------------------------------------------|
| **ID**       | BUG-010                                                                 |
| **Status**   | Fixed                                                                   |
| **Severity** | Critical — SCR-009 (Preferred Slot Selection) completely inaccessible   |
| **Area**     | Preferred slot selection · SCR-009 · `@mui/x-date-pickers` dependency   |
| **Reported** | 2026-05-07                                                              |

---

## Symptom

Clicking **"Select preferred slot"** on any appointment card in My Appointments
(SCR-008) navigates to `/appointments/:appointmentId/preferred-slot` and immediately
renders the `GlobalErrorPage` with title **"Something went wrong"** instead of the
slot calendar. No slot calendar or intake UI is ever visible.

---

## Root Cause

`@mui/x-date-pickers` **v9.0.2** was declared and installed in `client/package.json`,
but the project uses `@mui/material` **v5.18.0**.

The v9 peer dependency contract explicitly requires:

```json
"@mui/material": "^7.3.0 || ^9.0.0"
```

MUI v5 is not in that range. When `<DateCalendar>` mounts, it internally calls MUI
v7/v9 APIs (e.g. `useTheme()` internal slot resolution, `useColorScheme()`) that do
not exist in `@mui/material` v5. This throws a synchronous JavaScript `TypeError`
during the component's render phase.

React Router's `errorElement: <GlobalErrorPage />` (defined on the parent `'/'` route
in `App.tsx`) catches the thrown render error and displays "Something went wrong"
instead of the slot calendar.

`PreferredSlotSelectionPage` is the **only** page in the application that imports from
`@mui/x-date-pickers`, which is why the crash is isolated to this route.

---

## Affected File

| File | Change |
|------|--------|
| `client/package.json` | `@mui/x-date-pickers` `^9.0.2` → `^6.20.2` |

---

## Fix

Downgrade `@mui/x-date-pickers` from `^9.0.2` to `^6.20.2` — the last minor version
series that declared `@mui/material@^5` as a supported peer dependency.

The `DateCalendar`, `LocalizationProvider`, and `AdapterDayjs` APIs used in
`PreferredSlotSelectionPage` are identical between v6 and v9, so **no component-level
code changes** are required.

```diff
- "@mui/x-date-pickers": "^9.0.2",
+ "@mui/x-date-pickers": "^6.20.2",
```

After changing `package.json`, run:

```bash
npm install
```

---

## Verification Steps

1. Navigate to `/appointments` as a patient with at least one booked appointment.
2. Click **"Select preferred slot"** on any appointment card.
3. Confirm the slot calendar renders without error.
4. Confirm the `DateCalendar` displays available months and disables days with no
   booked (watchlist-eligible) slots.
5. Confirm "Something went wrong" page no longer appears.

---

## Related

- BUG-009 — Preferred Slot Query Never Fires (slot availability query permanently disabled)
- SCR-009 — Preferred Slot Selection
- US_015 — Preferred Slot Watchlist

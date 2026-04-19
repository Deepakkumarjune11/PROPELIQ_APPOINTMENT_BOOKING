# Task - task_001_fe_booking_confirmation_ui

## Requirement Reference

- **User Story**: US_014 — Reminders, Calendar Sync & PDF Confirmation
- **Story Location**: `.propel/context/tasks/EP-001/us_014/us_014.md`
- **Acceptance Criteria**:
  - AC-2: When the booking confirmation screen loads, buttons for "Add to Google Calendar" and "Add to Outlook Calendar" are available and functional via OAuth 2.0.
  - AC-3: When the patient clicks a calendar button, a calendar event is created (FE initiates the OAuth flow by redirecting to the auth URL returned by the backend).
  - AC-5: The booking confirmation screen shows a PDF download link, calendar sync options, and success message per SCR-006.
- **Edge Cases**:
  - Calendar sync fails (BE returns error on callback) → error toast "Calendar sync failed. Try again." with a "Try again" button (re-triggers OAuth redirect).
  - SCR-007 (Booking Error) — general booking failure: MUI `Alert` severity="error" with "Retry" button (re-submits booking) and "Select another slot" link navigating to `/appointments/slot-selection`.

---

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | `.propel/context/wireframes/Hi-Fi/wireframe-SCR-006-booking-confirmation.html`, `.propel/context/wireframes/Hi-Fi/wireframe-SCR-007-booking-error.html` |
| **Screen Spec** | `.propel/context/docs/figma_spec.md#SCR-006`, `.propel/context/docs/figma_spec.md#SCR-007` |
| **UXR Requirements** | UXR-403 (booking stepper — step 5 complete, all steps filled), UXR-101 (keyboard accessible — buttons reachable via Tab), UXR-102 (44px min touch targets) |
| **Design Tokens** | `designsystem.md#colors` (`primary.500: #2196F3` primary actions, `success.main: #4CAF50` success alert), `designsystem.md#typography` (Roboto) |

> **Wireframe Status Legend:**
> - **AVAILABLE**: Local file exists at specified path

### CRITICAL: Wireframe Implementation Requirement

- **MUST** open both wireframe files and match layout for SCR-006 (Card, 3 Buttons, 1 Link, Alert) and SCR-007 (Alert, 2 Buttons, 1 Link).
- **MUST** implement SCR-006 states: Default (success with all actions), Loading (calendar sync in progress — button spinner).
- **MUST** implement SCR-007 states: Error (alert + retry + slot link).
- **MUST** validate implementation against wireframes at breakpoints: 375px, 768px, 1440px.
- Run `/analyze-ux` after implementation to verify pixel-perfect alignment.

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | React | 18.x |
| UI Components | Material-UI (MUI) | 5.x |
| State Management | React Query (TanStack Query) | 4.x |
| State Management | Zustand | 4.x |
| Routing | React Router DOM | 6.x |
| Language | TypeScript | 5.x |
| Build | Vite | 5.x |

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

Implement two screens that complete the patient booking flow (FL-001):

**SCR-006 — Booking Confirmation** (success state):
- MUI `Alert` severity="success": "Booking confirmed! A confirmation email and PDF have been sent to {patientEmail}."
- MUI `Card` with booking summary (slot date/time, patient name, visit reason).
- **PDF download**: MUI `Button` variant="outlined" with `PictureAsPdfIcon` — opens `GET /api/v1/appointments/{appointmentId}/pdf` in a new tab. The PDF is generated asynchronously by a Hangfire job (task_002); this button downloads the PDF once ready. The URL returns a pre-generated PDF binary (`application/pdf`).
- **"Add to Google Calendar"** button — calls `GET /api/v1/calendar/google/init?appointmentId={id}` to receive a Google OAuth authorization URL, then performs `window.location.href = authUrl` to redirect the patient through the OAuth consent flow. On return (BE callback redirects back to `/appointments/confirmation?calendarSynced=google`), a success `Snackbar` is shown.
- **"Add to Outlook Calendar"** button — same pattern for Outlook/Microsoft Graph.
- Calendar buttons show MUI `CircularProgress` (size=16) during the `GET /init` API call.
- `useEffect` on mount: reads `?calendarSynced=google|outlook` query param → shows success `Snackbar` "Added to {provider} Calendar!". Reads `?calendarError=true` → shows error `Snackbar` "Calendar sync failed. Try again."
- On mount: calls `intake-store.clearIntake()` and `booking-store.clearIntake()` to reset the booking flow state (patient has completed the full flow).

**SCR-007 — Booking Error** (error state):
- Shown when the booking submission itself fails (non-409 error from `useRegisterPatient`).
- MUI `Alert` severity="error" with message "Booking could not be completed. Please try again."
- "Retry" MUI `Button` → calls `useRegisterPatient` mutation again with stored `booking-store` data.
- "Select another slot" MUI `Link` → `navigate('/appointments/slot-selection')`.
- `booking-store.selectedSlot` must still be populated at this point (not cleared on non-409 errors).

---

## Dependent Tasks

- **task_002_be_notifications_jobs.md** (US_014) — `GET /api/v1/appointments/{appointmentId}/pdf` endpoint that serves the generated PDF must be available.
- **task_003_be_calendar_sync_api.md** (US_014) — `GET /api/v1/calendar/{provider}/init` endpoint must be available for the OAuth redirect.
- **task_002_be_patient_registration_api.md** (US_010) — `useRegisterPatient` hook must exist; `booking-store.appointmentId` set on successful registration to enable the PDF download and calendar init calls.

---

## Impacted Components

| Action | Module | Description |
|--------|--------|-------------|
| CREATE | `client/src/pages/confirmation/BookingConfirmationPage.tsx` | SCR-006 — success card, PDF download, Google/Outlook calendar buttons, success alert |
| CREATE | `client/src/pages/booking-error/BookingErrorPage.tsx` | SCR-007 — error alert, retry button, "Select another slot" link |
| CREATE | `client/src/api/calendar.ts` | Typed API client for `GET /api/v1/calendar/{provider}/init?appointmentId` |
| MODIFY | `client/src/stores/booking-store.ts` | Add `appointmentId: string | null` field + `clearIntake()` action that resets the full booking flow state |
| MODIFY | `client/src/App.tsx` | Add `/appointments/confirmation` and `/appointments/error` routes |

---

## Implementation Plan

1. **`booking-store.ts`** modifications:
   ```typescript
   interface BookingStore {
     // ... existing fields (selectedSlot, patientDetails)
     appointmentId: string | null;   // NEW — set in useRegisterPatient onSuccess
     clearIntake(): void;             // NEW — resets all booking state
   }
   ```
   `clearIntake` sets `selectedSlot = null`, `patientDetails = null`, `appointmentId = null`.
   `intake-store.clearIntake()` is called separately (defined in US_011/task_001).

2. **`calendar.ts` API client**:
   ```typescript
   export async function getCalendarInitUrl(
     provider: 'google' | 'outlook',
     appointmentId: string
   ): Promise<{ authUrl: string }> { /* GET /api/v1/calendar/{provider}/init?appointmentId */ }
   ```

3. **`BookingConfirmationPage`** — key logic:
   ```tsx
   // On mount:
   useEffect(() => {
     const params = new URLSearchParams(location.search);
     if (params.get('calendarSynced')) showSuccessSnackbar(`Added to ${provider} Calendar!`);
     if (params.get('calendarError'))  showErrorSnackbar('Calendar sync failed. Try again.');
     bookingStore.clearIntake();
     intakeStore.clearIntake();
   }, []);

   // Calendar button handler:
   const handleCalendarSync = async (provider: 'google' | 'outlook') => {
     setLoadingProvider(provider);
     try {
       const { authUrl } = await getCalendarInitUrl(provider, appointmentId);
       window.location.href = authUrl;   // OAuth redirect — leaves the page
     } catch {
       showErrorSnackbar('Could not start calendar sync. Try again.');
     } finally {
       setLoadingProvider(null);
     }
   };
   ```

4. **PDF download button**:
   ```tsx
   <Button
     variant="outlined"
     startIcon={<PictureAsPdfIcon />}
     href={`/api/v1/appointments/${appointmentId}/pdf`}
     target="_blank"
     rel="noopener noreferrer"
   >
     Download PDF Confirmation
   </Button>
   ```
   Simple `<a>` via MUI Button — no React Query call needed; browser handles the download.

5. **`BookingErrorPage`** — guard: if `booking-store.selectedSlot` and `booking-store.patientDetails` are null (page loaded directly, not via failed submission), redirect to `/appointments/search`. Otherwise render error state with retry and slot link.

6. **Update `useRegisterPatient`** onSuccess — set `bookingStore.appointmentId = response.appointmentId`; navigate to `/appointments/confirmation`.

7. **Update `useRegisterPatient`** onError — for non-409 errors, navigate to `/appointments/error`.

---

## Current Project State

```
client/src/
  pages/
    LoginPage.tsx
    availability/                          ← us_009/task_001
    slot-selection/                        ← us_009/task_002
    patient-details/                       ← us_010/task_001
    intake/                                ← us_011/task_001, us_012/task_001
    confirmation/                          ← Does NOT exist yet — CREATE
    booking-error/                         ← Does NOT exist yet — CREATE
  stores/
    booking-store.ts                       ← MODIFY: add appointmentId + clearIntake
    intake-store.ts                        ← us_011 (clearIntake already defined)
  hooks/
    useRegisterPatient.ts                  ← MODIFY: onSuccess set appointmentId + navigate; onError navigate to /error
  api/
    calendar.ts                            ← Does NOT exist yet — CREATE
  App.tsx                                  ← MODIFY: add /appointments/confirmation, /appointments/error routes
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `client/src/pages/confirmation/BookingConfirmationPage.tsx` | SCR-006 — success alert, booking summary card, PDF download link, Google/Outlook calendar buttons with OAuth redirect, calendarSynced/calendarError query param handling |
| CREATE | `client/src/pages/booking-error/BookingErrorPage.tsx` | SCR-007 — error alert, retry button, "Select another slot" link |
| CREATE | `client/src/api/calendar.ts` | `getCalendarInitUrl(provider, appointmentId) → { authUrl }` |
| MODIFY | `client/src/stores/booking-store.ts` | Add `appointmentId: string | null`; add `clearIntake()` action |
| MODIFY | `client/src/hooks/useRegisterPatient.ts` | `onSuccess`: set `appointmentId`, navigate to `/appointments/confirmation`. `onError` (non-409): navigate to `/appointments/error` |
| MODIFY | `client/src/App.tsx` | Add `/appointments/confirmation` → `BookingConfirmationPage`; `/appointments/error` → `BookingErrorPage` routes |

---

## External References

- [MUI Alert — success/error severity](https://mui.com/material-ui/react-alert/)
- [MUI Snackbar — transient feedback messages](https://mui.com/material-ui/react-snackbar/)
- [MUI Button as `<a>` link for PDF download](https://mui.com/material-ui/react-button/#file-upload)
- [React Router DOM v6 — `useSearchParams` for query params](https://reactrouter.com/en/main/hooks/use-search-params)
- [TR-012 — Google Calendar API + Microsoft Graph OAuth 2.0](`.propel/context/docs/design.md#TR-012`)

---

## Build Commands

```bash
# From client/
npm install
npx tsc --noEmit
npm run dev
npm run build
```

---

## Implementation Validation Strategy

- [ ] SCR-006 renders with success alert, booking summary, PDF button, Google/Outlook buttons on load
- [ ] PDF download button opens `/api/v1/appointments/{appointmentId}/pdf` in new tab (no JS navigation)
- [ ] "Add to Google Calendar" click → `GET /api/v1/calendar/google/init` called → `window.location.href` set to returned `authUrl`
- [ ] Calendar button shows `CircularProgress` during init call; disabled while loading
- [ ] `?calendarSynced=google` query param on return → success Snackbar shown; `?calendarError=true` → error Snackbar shown
- [ ] On mount: `booking-store.clearIntake()` and `intake-store.clearIntake()` called
- [ ] SCR-007 shows error alert, "Retry" fires `useRegisterPatient` again, "Select another slot" navigates to `/appointments/slot-selection`
- [ ] `useRegisterPatient` onSuccess sets `booking-store.appointmentId`; navigates to `/appointments/confirmation`
- [ ] `useRegisterPatient` onError (non-409) navigates to `/appointments/error`
- [ ] Guard on SCR-007 redirects to `/appointments/search` if no booking data in store
- [ ] **[UI Tasks]** Visual comparison against both wireframes completed at 375px, 768px, 1440px
- [ ] **[UI Tasks]** Run `/analyze-ux` to validate wireframe alignment

---

## Implementation Checklist

- [ ] Modify `booking-store.ts` — add `appointmentId: string | null` field + `clearIntake()` action
- [ ] Create `client/src/api/calendar.ts` — `getCalendarInitUrl(provider, appointmentId)` API client
- [ ] Create `BookingConfirmationPage.tsx` — success alert, booking summary card, PDF download button (`<a>` via MUI Button), Google/Outlook calendar buttons with OAuth redirect + loading state, `useEffect` for query param handling + store clearance
- [ ] Create `BookingErrorPage.tsx` — null-guard redirect, error alert, retry button, "Select another slot" link
- [ ] Modify `useRegisterPatient.ts` — `onSuccess`: set `appointmentId`, navigate `/appointments/confirmation`; `onError` non-409: navigate `/appointments/error`
- [ ] Modify `App.tsx` — add `/appointments/confirmation` + `/appointments/error` routes
- [ ] **[UI Tasks - MANDATORY]** Reference both wireframes during implementation
- [ ] **[UI Tasks - MANDATORY]** Validate UI matches wireframes at 375px, 768px, 1440px

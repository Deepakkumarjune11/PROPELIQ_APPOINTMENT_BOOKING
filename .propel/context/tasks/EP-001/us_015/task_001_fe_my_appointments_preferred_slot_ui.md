# Task - task_001_fe_my_appointments_preferred_slot_ui

## Requirement Reference

- **User Story**: US_015 — Preferred Slot Swap Watchlist
- **Story Location**: `.propel/context/tasks/EP-001/us_015/us_015.md`
- **Acceptance Criteria**:
  - AC-1: When the patient navigates to preferred slot selection, they see a calendar showing unavailable slots that are eligible for watchlist enrollment per FR-004.
  - AC-2: When the patient confirms a preferred unavailable slot, the system registers the appointment on the swap watchlist (preferred_slot_id updated in the Appointment record).
  - AC-4: After a swap is executed, viewing My Appointments shows the new slot datetime and watchlist status is cleared.
  - AC-5: If another patient claimed the preferred slot first, the original appointment remains unchanged and the watchlist entry is preserved.
- **Edge Cases**:
  - Preferred slot no longer eligible → system keeps original appointment and notifies patient (FE must handle 422 response gracefully, show toast explaining situation).
  - Slot calendar shows **only unavailable** (booked) slots as selectable; available slots must be grayed/disabled — patient may not book twice on an already-open slot via this flow.
  - Empty state: no confirmed appointments → encourage booking via CTA.

---

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | `.propel/context/wireframes/Hi-Fi/wireframe-SCR-008-my-appointments.html`, `.propel/context/wireframes/Hi-Fi/wireframe-SCR-009-preferred-slot-selection.html` |
| **Screen Spec** | `.propel/context/docs/figma_spec.md#SCR-008`, `.propel/context/docs/figma_spec.md#SCR-009` |
| **UXR Requirements** | UXR-003 (inline guidance / tooltip explaining automatic swap process), UXR-101 (WCAG 2.2 AA), UXR-102 (ARIA labels), UXR-201 (responsive 375/768/1440px), UXR-401 (loading feedback < 200ms), UXR-402 (success/error toast for watchlist registration), UXR-501 (actionable error messages) |
| **Design Tokens** | `designsystem.md#colors` (`primary.500: #2196F3` CTAs/active states; `success.main: #4CAF50` confirmed badge; `warning.main: #FF9800` watchlist badge), `designsystem.md#typography`, `designsystem.md#spacing` (8px grid) |

> **Wireframe Status Legend:**
> - **AVAILABLE**: Local file exists at specified path

### CRITICAL: Wireframe Implementation Requirement

- **MUST** open both wireframe files and match layout for SCR-008 (appointment Card list, status Badges, "Select Preferred Slot" Button) and SCR-009 (DatePicker with unavailable slots selectable / available slots disabled, Alert, 2 Buttons).
- **MUST** implement all required states:
  - **SCR-008**: Default (appointment list with confirmed/watchlist badges), Loading (Skeleton cards), Empty (no appointments illustration + "Book an appointment" CTA), Error (MUI Alert + Retry).
  - **SCR-009**: Default (slot calendar), Loading (watchlist registration in progress — button spinner), Empty (no eligible unavailable slots), Error (MUI Alert).
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

Implement two patient-facing screens that enable the preferred slot swap watchlist flow (FL-002 from figma_spec.md):

**SCR-008 — My Appointments** (P0):
- Displays all booked appointments for the authenticated patient via `GET /api/v1/appointments`.
- Each appointment is rendered as an MUI `Card` containing: date/time, provider name, visit type, and a status `Badge` (Confirmed = `success.main`; Watchlist = `warning.main` with preferred datetime).
- A "Select preferred slot" `Button` appears on confirmed appointments only (not on appointments already on the watchlist).
- Clicking "Select preferred slot" navigates to `/appointments/{id}/preferred-slot` (SCR-009).
- Watchlist badge includes a `Tooltip` explaining: "We'll notify you automatically if this slot opens."
- States: Default, Loading (Skeleton cards), Empty (no appointments + CTA), Error (MUI `Alert` + Retry).

**SCR-009 — Preferred Slot Selection** (P1):
- Navigated to from SCR-008; displays a DatePicker calendar for the appointment's provider.
- Available slots are **grayed out / disabled** (a patient cannot watchlist for a slot they could book directly).
- Only **unavailable (booked) slots** are selectable — these are highlighted.
- Patient selects one unavailable slot, then clicks "Confirm preferred slot" → calls `POST /api/v1/appointments/{id}/preferred-slot`.
- On success: navigates back to SCR-008 with toast "Watchlist registered! We'll notify you if the slot opens."
- On failure (422 slot ineligible): toast "This slot is no longer available for watchlist. Please select another."
- "Cancel" button navigates back to SCR-008 without changes.
- States: Default (slot calendar), Loading (button spinner during submission), Empty (no unavailable slots for this provider), Error (MUI `Alert`).

---

## Dependent Tasks

- **task_002_be_watchlist_registration_api.md** (US_015) — `GET /api/v1/appointments` and `POST /api/v1/appointments/{id}/preferred-slot` endpoints must be available.
- **task_001_fe_booking_confirmation_ui.md** (US_014) — confirms `booking-store` patterns already established; re-use auth token handling from login flow.
- **task_001_fe_availability_search.md** (US_009) — `DatePicker` and slot rendering patterns established; re-use slot display conventions.

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `client/src/pages/my-appointments/MyAppointmentsPage.tsx` | SCR-008 — appointment list, status badges, "Select preferred slot" button |
| CREATE | `client/src/pages/my-appointments/AppointmentCard.tsx` | Reusable appointment Card sub-component (date/time, provider, status badge, CTA button) |
| CREATE | `client/src/pages/preferred-slot/PreferredSlotSelectionPage.tsx` | SCR-009 — DatePicker with slot availability overlay, confirm/cancel buttons |
| CREATE | `client/src/api/appointments.ts` | Typed API client: `getAppointments()`, `registerPreferredSlot(appointmentId, slotDatetime)`, `getSlotAvailability(providerId, month)` |
| CREATE | `client/src/hooks/useAppointments.ts` | React Query hook — `useQuery` wrapping `getAppointments()` |
| CREATE | `client/src/hooks/useRegisterPreferredSlot.ts` | React Query `useMutation` wrapping `registerPreferredSlot()` with toast callbacks |
| MODIFY | `client/src/App.tsx` | Add routes: `/appointments` (SCR-008) and `/appointments/:appointmentId/preferred-slot` (SCR-009) |

---

## Implementation Plan

1. **`appointments.ts` API client** — define typed interfaces and fetch calls:
   ```typescript
   export interface AppointmentDto {
     id: string;
     slotDatetime: string;       // ISO 8601
     providerName: string;
     visitType: string;
     status: 'booked' | 'arrived' | 'completed' | 'cancelled' | 'no-show';
     preferredSlotDatetime: string | null;   // null = not on watchlist
   }

   export async function getAppointments(): Promise<AppointmentDto[]>
   // GET /api/v1/appointments (authenticated via Authorization header)

   export async function getSlotAvailability(
     providerId: string,
     year: number,
     month: number
   ): Promise<{ datetime: string; available: boolean }[]>
   // GET /api/v1/slots/availability?providerId=&year=&month=

   export async function registerPreferredSlot(
     appointmentId: string,
     preferredSlotDatetime: string
   ): Promise<void>
   // POST /api/v1/appointments/{appointmentId}/preferred-slot
   // Body: { preferredSlotDatetime: string }
   ```

2. **`useAppointments.ts` hook**:
   ```typescript
   export function useAppointments() {
     return useQuery({
       queryKey: ['appointments'],
       queryFn: getAppointments,
       staleTime: 30_000,   // 30s — appointments rarely change mid-session
     });
   }
   ```

3. **`useRegisterPreferredSlot.ts` hook**:
   ```typescript
   export function useRegisterPreferredSlot() {
     const queryClient = useQueryClient();
     return useMutation({
       mutationFn: ({ appointmentId, preferredSlotDatetime }: RegisterPayload) =>
         registerPreferredSlot(appointmentId, preferredSlotDatetime),
       onSuccess: () => {
         queryClient.invalidateQueries({ queryKey: ['appointments'] });
         showToast('success', "Watchlist registered! We'll notify you if the slot opens.");
       },
       onError: (error: ApiError) => {
         if (error.status === 422) {
           showToast('warning', 'This slot is no longer available for watchlist. Please select another.');
         } else {
           showToast('error', 'Could not register watchlist. Please try again.');
         }
       },
     });
   }
   ```

4. **`AppointmentCard.tsx`** sub-component:
   - Renders MUI `Card` with `CardContent`: formatted date/time (locale string), provider name, visit type.
   - Status `Chip`/`Badge`:
     - `status === 'booked'` AND `preferredSlotDatetime === null` → Green chip "Confirmed".
     - `status === 'booked'` AND `preferredSlotDatetime !== null` → Orange chip "Watchlist: {formatted preferred date}" with MUI `Tooltip` "Monitoring for automatic swap."
   - "Select preferred slot" `Button` (variant="outlined", size="small") rendered **only when** `status === 'booked' && preferredSlotDatetime === null`.
   - onClick → `navigate(\`/appointments/${id}/preferred-slot\`)`.

5. **`MyAppointmentsPage.tsx`** — SCR-008 states:
   - Loading: render 3× `Skeleton` cards (height 120px, `animation="wave"`).
   - Error: MUI `Alert` severity="error" + "Try again" `Button` calling `refetch()`.
   - Empty: MUI `Box` with calendar illustration placeholder, Typography "No appointments yet.", `Button` "Book an appointment" → `navigate('/appointments/search')`.
   - Default: map `appointments` → `<AppointmentCard>`.

6. **`PreferredSlotSelectionPage.tsx`** — SCR-009:
   - Read `appointmentId` from route param via `useParams()`.
   - Fetch slot availability for the appointment's provider for the current + next month via `useQuery`.
   - Render MUI `DatePicker` with `shouldDisableDate` callback: disable dates where ALL slots are `available: true` (i.e., permit selection only on dates with unavailable/booked slots).
   - On date select → render time slot grid; highlight unavailable slots as selectable, gray out available slots (disabled).
   - "Confirm preferred slot" `Button`: calls `useRegisterPreferredSlot` mutation; shows `CircularProgress` size=16 during submission.
   - "Cancel" `Button` → `navigate('/appointments')`.

---

## Current Project State

```
client/src/
  pages/
    LoginPage.tsx
    availability/                          ← us_009/task_001
    slot-selection/                        ← us_009/task_002
    patient-details/                       ← us_010/task_001
    intake/                                ← us_011/task_001 + us_012/task_001
    confirmation/                          ← us_014/task_001
    booking-error/                         ← us_014/task_001
    my-appointments/                       ← THIS TASK (SCR-008)
    preferred-slot/                        ← THIS TASK (SCR-009)
  api/
    slots.ts                               ← us_009/task_001
    appointments.ts                        ← THIS TASK (create)
  hooks/
    useSlots.ts                            ← us_009/task_001
    useAppointments.ts                     ← THIS TASK (create)
    useRegisterPreferredSlot.ts            ← THIS TASK (create)
  stores/
    auth-store.ts
    booking-store.ts                       ← us_010/task_001
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `client/src/pages/my-appointments/MyAppointmentsPage.tsx` | SCR-008 — appointment list, all 4 states |
| CREATE | `client/src/pages/my-appointments/AppointmentCard.tsx` | Reusable appointment card with status badge and CTA |
| CREATE | `client/src/pages/preferred-slot/PreferredSlotSelectionPage.tsx` | SCR-009 — slot calendar, confirm/cancel flow |
| CREATE | `client/src/api/appointments.ts` | Typed API client for appointments and slot availability |
| CREATE | `client/src/hooks/useAppointments.ts` | React Query hook for appointment list |
| CREATE | `client/src/hooks/useRegisterPreferredSlot.ts` | React Query mutation for watchlist registration |
| MODIFY | `client/src/App.tsx` | Add `/appointments` and `/appointments/:appointmentId/preferred-slot` routes |

---

## External References

- [MUI DatePicker docs — `shouldDisableDate` prop](https://mui.com/x/api/date-pickers/date-picker/#DatePicker-prop-shouldDisableDate)
- [React Query `useMutation` — TanStack Query v4](https://tanstack.com/query/v4/docs/react/guides/mutations)
- [React Query `useQuery` caching — staleTime](https://tanstack.com/query/v4/docs/react/guides/important-defaults)
- [MUI Chip / Badge — status indicators](https://mui.com/material-ui/react-chip/)
- [MUI Tooltip — accessible tooltip](https://mui.com/material-ui/react-tooltip/)
- [MUI Skeleton — loading states](https://mui.com/material-ui/react-skeleton/)
- [React Router DOM `useParams`](https://reactrouter.com/en/6.x/hooks/use-params)

---

## Build Commands

```bash
# Install dependencies
cd client && npm install

# Development server
npm run dev

# TypeScript type check
npm run type-check

# Production build
npm run build
```

---

## Implementation Validation Strategy

- [ ] Unit tests pass (`useAppointments`, `useRegisterPreferredSlot` hooks mocked)
- [ ] Integration tests pass (React Testing Library — render SCR-008 with mock data, assert cards render correctly)
- [ ] **[UI Tasks]** Visual comparison against wireframes at 375px, 768px, 1440px
- [ ] **[UI Tasks]** Run `/analyze-ux` to validate wireframe alignment
- [ ] SCR-008 Loading state renders 3 Skeleton cards
- [ ] SCR-008 Empty state renders "Book an appointment" CTA
- [ ] SCR-008 Confirmed appointment shows green badge; Watchlist appointment shows orange badge with tooltip
- [ ] SCR-009 Available slots are disabled; unavailable slots are selectable
- [ ] Watchlist registration success triggers toast + navigates back to SCR-008
- [ ] API error 422 triggers warning toast without navigating away

---

## Implementation Checklist

- [X] Create `client/src/api/appointments.ts` with typed `AppointmentDto`, `getAppointments`, `getSlotAvailability`, `registerPreferredSlot`
- [X] Create `client/src/hooks/useAppointments.ts` with 30s staleTime
- [X] Create `client/src/hooks/useRegisterPreferredSlot.ts` with success/error toast callbacks
- [X] Create `client/src/pages/my-appointments/AppointmentCard.tsx` with conditional status badge and "Select preferred slot" button
- [X] Create `client/src/pages/my-appointments/MyAppointmentsPage.tsx` with all 4 states (Default, Loading, Empty, Error)
- [X] Create `client/src/pages/preferred-slot/PreferredSlotSelectionPage.tsx` with `shouldDisableDate` logic and confirm/cancel flow
- [X] Modify `client/src/App.tsx` to add `/appointments` and `/appointments/:appointmentId/preferred-slot` routes (auth-guarded)
- [X] **[UI Tasks - MANDATORY]** Reference wireframes from Design References table during implementation
- [X] **[UI Tasks - MANDATORY]** Validate UI matches wireframe before marking task complete

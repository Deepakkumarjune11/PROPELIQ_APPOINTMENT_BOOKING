# Task - task_001_fe_staff_dashboard_walkin_booking_ui

## Requirement Reference

- **User Story**: US_016 — Staff Walk-In Booking & Patient Creation
- **Story Location**: `.propel/context/tasks/EP-002/us_016/us_016.md`
- **Acceptance Criteria**:
  - AC-1: When authenticated staff accesses walk-in booking, the system verifies staff permissions and displays the walk-in booking interface per FR-008 (staff-role-only route; unauthorized users see 403).
  - AC-2: When staff searches by email or phone, a matching patient's existing details are displayed via autocomplete.
  - AC-3: When no patient account exists, staff can expand an inline form to create a minimal patient profile.
  - AC-4: When a walk-in booking is submitted and a same-day slot is available, the system displays the queue position number.
  - AC-5: When no same-day slots are available, the system adds the patient to the wait queue and displays the wait position.
- **Edge Cases**:
  - Patient user attempts to access walk-in booking → route guarded by `[Authorize(Roles = "Staff")]`; FE must redirect to login with 403 toast, never render the form.
  - Duplicate patient creation prevention → autocomplete must match on email/phone before showing "Create new patient" option; "Create" CTA is hidden while search results are present.

---

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | `.propel/context/wireframes/Hi-Fi/wireframe-SCR-010-staff-dashboard.html`, `.propel/context/wireframes/Hi-Fi/wireframe-SCR-011-walk-in-booking.html` |
| **Screen Spec** | `.propel/context/docs/figma_spec.md#SCR-010`, `.propel/context/docs/figma_spec.md#SCR-011` |
| **UXR Requirements** | UXR-002 (breadcrumbs on staff workflow nested screens), UXR-101 (WCAG 2.2 AA), UXR-401 (loading feedback < 200ms), UXR-402 (success/error toast for booking), UXR-502 (inline validation on walk-in form fields) |
| **Design Tokens** | `designsystem.md#colors` (`primary.500: #2196F3` CTAs; `success.main: #4CAF50` booked badge; `warning.main: #FF9800` wait-queue badge), `designsystem.md#typography`, `designsystem.md#spacing` (8px grid) |

> **Wireframe Status Legend:**
> - **AVAILABLE**: Local file exists at specified path

### CRITICAL: Wireframe Implementation Requirement

- **MUST** open both wireframe files and match layout for:
  - **SCR-010 Staff Dashboard**: 4 summary Cards (walk-ins today, queue length, verification pending, critical conflicts), same-day queue Table with status Badges, "Walk-In Booking" Button navigating to SCR-011.
  - **SCR-011 Walk-In Booking**: Autocomplete patient search (TextField + Autocomplete), collapsible inline "Create new patient" form, slot selection, Book/Cancel Buttons, validation Alert.
- **MUST** implement all required states:
  - **SCR-010**: Default (dashboard with populated cards + queue table), Loading (Skeleton cards + table), Empty (no walk-ins today with CTA), Error (MUI Alert + Retry).
  - **SCR-011**: Default (search field focused), Loading (booking submission spinner), Error (MUI Alert), Validation (inline field errors, submit disabled).
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

Implement two staff-facing screens that open the walk-in booking workflow (FL-003 from figma_spec.md):

**SCR-010 — Staff Dashboard** (P0):
- Entry point for all staff same-day operations.
- Four summary MUI `Card` components in a responsive grid (2-column desktop, 1-column mobile): "Walk-Ins Today" count, "Queue Length" count, "Verification Pending" count, "Critical Conflicts" count (each fetched from `GET /api/v1/staff/dashboard/summary`).
- Same-day queue `Table` below the cards — rows show patient name, visit type, arrival time, status `Badge` (waiting = neutral, arrived = success, in-room = info).
- "Walk-In Booking" primary `Button` in the header area navigates to `/staff/walk-in`.
- Breadcrumb: `Home > Staff Dashboard` (UXR-002).
- States: Default, Loading (Skeleton), Empty (no walk-ins + CTA), Error (Alert + Retry).

**SCR-011 — Walk-In Booking** (P0):
- Staff-only route (guarded by `requireRole('Staff')` higher-order component; redirects to `/` with toast on 403).
- Breadcrumb: `Home > Staff Dashboard > Walk-In Booking` (UXR-002).
- MUI `Autocomplete` (debounced 300ms, min 2 chars) calls `GET /api/v1/patients/search?q=` to search existing patients by email or phone; renders patient name + email in dropdown option.
- When a patient is selected from autocomplete → pre-fill display section (read-only patient card).
- "Create new patient" `Button` visible only when search returns no results → expands inline `Collapse` form with minimal fields: full name (required), email (required, email format), phone (required).
- Visit type `Select` (required): General, Follow-Up, Urgent Care.
- "Book Walk-In" primary `Button` → calls `POST /api/v1/appointments/walk-in`.
- On success → shows toast "Walk-in booked! Queue position: {N}" → navigate to `/staff/queue`.
- On no-slot-available (HTTP 200 with `waitQueue: true`) → toast "No slots available. Patient added to wait queue at position {N}".
- "Cancel" `Button` → navigate back to `/staff/dashboard`.
- Inline validation (UXR-502): required fields show error below on blur; submit disabled while invalid.

---

## Dependent Tasks

- **task_002_be_walkin_booking_api.md** (US_016) — `GET /api/v1/patients/search`, `POST /api/v1/patients`, `POST /api/v1/appointments/walk-in`, `GET /api/v1/staff/dashboard/summary` endpoints must be available.
- **task_001_fe_my_appointments_preferred_slot_ui.md** (US_015) — auth-store token pattern established; reuse `Authorization` header injection pattern.

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `client/src/pages/staff/dashboard/StaffDashboardPage.tsx` | SCR-010 — 4 summary cards, queue table, walk-in CTA |
| CREATE | `client/src/pages/staff/dashboard/DashboardSummaryCard.tsx` | Reusable summary Card sub-component (count + label) |
| CREATE | `client/src/pages/staff/walk-in/WalkInBookingPage.tsx` | SCR-011 — patient search autocomplete, inline create form, booking submit |
| CREATE | `client/src/api/staff.ts` | Typed API client: `searchPatients()`, `createPatient()`, `bookWalkIn()`, `getDashboardSummary()` |
| CREATE | `client/src/hooks/usePatientSearch.ts` | React Query hook — debounced patient search query |
| CREATE | `client/src/hooks/useBookWalkIn.ts` | React Query `useMutation` for walk-in booking with toast callbacks |
| CREATE | `client/src/components/guards/StaffRouteGuard.tsx` | Higher-order component: checks `auth-store.role === 'Staff'`; redirects to `/` on 403 |
| MODIFY | `client/src/App.tsx` | Add staff routes: `/staff/dashboard` and `/staff/walk-in` wrapped in `<StaffRouteGuard>` |

---

## Implementation Plan

1. **`staff.ts` API client** — typed interfaces and fetch calls:
   ```typescript
   export interface PatientSearchResult {
     id: string;
     fullName: string;
     email: string;
     phone: string;
   }

   export interface DashboardSummary {
     walkInsToday: number;
     queueLength: number;
     verificationPending: number;
     criticalConflicts: number;
   }

   export interface WalkInBookingResult {
     appointmentId: string;
     queuePosition: number;
     waitQueue: boolean;   // true when no slots available, patient on wait list
   }

   export async function searchPatients(query: string): Promise<PatientSearchResult[]>
   // GET /api/v1/patients/search?q={query}

   export async function createPatient(data: CreatePatientRequest): Promise<PatientSearchResult>
   // POST /api/v1/patients

   export async function bookWalkIn(data: WalkInBookingRequest): Promise<WalkInBookingResult>
   // POST /api/v1/appointments/walk-in

   export async function getDashboardSummary(): Promise<DashboardSummary>
   // GET /api/v1/staff/dashboard/summary
   ```

2. **`usePatientSearch.ts`** — debounced React Query hook:
   ```typescript
   export function usePatientSearch(query: string) {
     return useQuery({
       queryKey: ['patients', 'search', query],
       queryFn: () => searchPatients(query),
       enabled: query.trim().length >= 2,   // min 2 chars
       staleTime: 10_000,
     });
   }
   ```
   Debounce the `query` input value by 300ms in the component before passing to the hook.

3. **`useBookWalkIn.ts`** — mutation with toast callbacks:
   ```typescript
   export function useBookWalkIn() {
     const navigate = useNavigate();
     return useMutation({
       mutationFn: bookWalkIn,
       onSuccess: (result) => {
         if (result.waitQueue) {
           showToast('info', `No slots available. Patient added to wait queue at position ${result.queuePosition}.`);
         } else {
           showToast('success', `Walk-in booked! Queue position: ${result.queuePosition}.`);
         }
         navigate('/staff/queue');
       },
       onError: () => showToast('error', 'Walk-in booking failed. Please try again.'),
     });
   }
   ```

4. **`StaffRouteGuard.tsx`** — RBAC guard:
   ```tsx
   export function StaffRouteGuard({ children }: { children: ReactNode }) {
     const role = useAuthStore((s) => s.role);
     if (role !== 'Staff' && role !== 'Admin') {
       showToast('error', 'Access denied. Staff role required.');
       return <Navigate to="/" replace />;
     }
     return <>{children}</>;
   }
   ```

5. **`WalkInBookingPage.tsx`** — patient search + inline create form:
   - `useState` for `debouncedQuery` (300ms debounce via `useEffect`).
   - `usePatientSearch(debouncedQuery)` — results populate Autocomplete options.
   - Show "Create new patient" `Button` only when `debouncedQuery.length >= 2 && results.length === 0`.
   - Inline `Collapse` component for minimal patient creation form (fullName, email, phone).
   - On "Create" click → `createPatient(data)` API call → set `selectedPatient` state.
   - Visit type `Select` with options: `General | Follow-Up | Urgent Care`.
   - Form validation state: disable "Book Walk-In" when `selectedPatient === null` or `visitType === ''`.
   - On submit → `useBookWalkIn.mutate({ patientId, visitType })`.

6. **`StaffDashboardPage.tsx`** — SCR-010 states:
   - Loading: 4× `Skeleton` rectangular blocks (height 80px) + table skeleton rows.
   - Error: MUI `Alert` severity="error" + "Try again" `Button` calling `refetch()`.
   - Empty: Typography "No walk-ins today." + "Book a Walk-In" CTA button.
   - Default: `DashboardSummaryCard` grid + queue `Table`.

---

## Current Project State

```
client/src/
  pages/
    LoginPage.tsx
    availability/                          ← us_009
    slot-selection/                        ← us_009
    patient-details/                       ← us_010
    intake/                                ← us_011 + us_012
    confirmation/                          ← us_014
    booking-error/                         ← us_014
    my-appointments/                       ← us_015/task_001
    preferred-slot/                        ← us_015/task_001
    staff/
      dashboard/                           ← THIS TASK (SCR-010)
      walk-in/                             ← THIS TASK (SCR-011)
  api/
    slots.ts                               ← us_009
    appointments.ts                        ← us_015
    staff.ts                               ← THIS TASK (create)
  hooks/
    useSlots.ts                            ← us_009
    useAppointments.ts                     ← us_015
    usePatientSearch.ts                    ← THIS TASK (create)
    useBookWalkIn.ts                       ← THIS TASK (create)
  components/
    guards/
      StaffRouteGuard.tsx                  ← THIS TASK (create)
  stores/
    auth-store.ts
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `client/src/pages/staff/dashboard/StaffDashboardPage.tsx` | SCR-010 — all 4 states, summary cards, queue table |
| CREATE | `client/src/pages/staff/dashboard/DashboardSummaryCard.tsx` | Reusable count/label card sub-component |
| CREATE | `client/src/pages/staff/walk-in/WalkInBookingPage.tsx` | SCR-011 — autocomplete search, inline create, booking submit |
| CREATE | `client/src/api/staff.ts` | Typed API client for all staff endpoints |
| CREATE | `client/src/hooks/usePatientSearch.ts` | Debounced patient search React Query hook |
| CREATE | `client/src/hooks/useBookWalkIn.ts` | Walk-in booking mutation with toast + navigation |
| CREATE | `client/src/components/guards/StaffRouteGuard.tsx` | Role-based route guard for staff-only screens |
| MODIFY | `client/src/App.tsx` | Add `/staff/dashboard` and `/staff/walk-in` routes wrapped in `<StaffRouteGuard>` |

---

## External References

- [MUI Autocomplete — debounced async](https://mui.com/material-ui/react-autocomplete/#load-on-open)
- [MUI Collapse — inline expand/collapse form](https://mui.com/material-ui/transitions/#collapse)
- [React Query — conditional `enabled` flag](https://tanstack.com/query/v4/docs/react/guides/disabling-queries)
- [React Router DOM — `<Navigate>` redirect](https://reactrouter.com/en/6.x/components/navigate)
- [MUI Breadcrumbs — UXR-002 navigation hierarchy](https://mui.com/material-ui/react-breadcrumbs/)
- [MUI Skeleton — loading states](https://mui.com/material-ui/react-skeleton/)

---

## Build Commands

```bash
cd client && npm install
npm run dev
npm run type-check
npm run build
```

---

## Implementation Validation Strategy

- [ ] Unit tests pass (`usePatientSearch` returns results on 2+ char input; disabled on < 2 chars)
- [ ] `StaffRouteGuard` redirects non-staff users to `/` with error toast
- [ ] SCR-010 Loading state renders 4 Skeleton cards
- [ ] SCR-010 Empty state renders "No walk-ins today" + CTA
- [ ] SCR-011 Autocomplete debounces at 300ms and calls search API at ≥ 2 chars
- [ ] "Create new patient" section appears only when search has ≥ 2 chars and returns 0 results
- [ ] "Book Walk-In" is disabled when no patient selected or visit type is empty
- [ ] Success toast shows queue position; wait-queue toast shows wait position
- [ ] **[UI Tasks]** Visual comparison against wireframes at 375px, 768px, 1440px
- [ ] **[UI Tasks]** Run `/analyze-ux` to validate wireframe alignment

---

## Implementation Checklist

- [x] Create `client/src/api/staff.ts` with typed `PatientSearchResult`, `DashboardSummary`, `WalkInBookingResult`, and API functions
- [x] Create `client/src/hooks/usePatientSearch.ts` with `enabled: query.length >= 2` guard and 300ms debounce in consumer
- [x] Create `client/src/hooks/useBookWalkIn.ts` with success/error/wait-queue toast callbacks and `/staff/queue` navigation
- [x] Create `client/src/components/guards/StaffRouteGuard.tsx` checking `auth-store.role`; redirect on non-staff
- [x] Create `client/src/pages/staff/dashboard/DashboardSummaryCard.tsx` reusable count card
- [x] Create `client/src/pages/staff/dashboard/StaffDashboardPage.tsx` with all 4 states + breadcrumb
- [x] Create `client/src/pages/staff/walk-in/WalkInBookingPage.tsx` with autocomplete, inline create form, visit type select, validation
- [x] Modify `client/src/App.tsx` to add `/staff/dashboard` and `/staff/walk-in` routes inside `<StaffRouteGuard>`
- [x] **[UI Tasks - MANDATORY]** Reference wireframes from Design References table during implementation
- [x] **[UI Tasks - MANDATORY]** Validate UI matches wireframes before marking task complete

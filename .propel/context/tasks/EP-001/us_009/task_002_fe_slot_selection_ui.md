# Task - task_002_fe_slot_selection_ui

## Requirement Reference

- **User Story**: US_009 — Appointment Availability Search
- **Story Location**: `.propel/context/tasks/EP-001/us_009/us_009.md`
- **Acceptance Criteria**:
  - AC-1: Within the booking flow, the slot selection step renders within 2 seconds at p95 (slot data already in React Query cache from SCR-001).
- **Edge Cases**:
  - Concurrent slot claims — Optimistic UI shows slot as selected, but on `409 Conflict` API response, revert selection and display toast "Slot no longer available. Please select another." (UXR-404).
  - Redis unavailable — transparent to UI; backend serves direct DB response.

---

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | `.propel/context/wireframes/Hi-Fi/wireframe-SCR-002-slot-selection.html` |
| **Screen Spec** | `.propel/context/docs/figma_spec.md#SCR-002` |
| **UXR Requirements** | UXR-403 (progress stepper, step 2 active), UXR-404 (optimistic UI + rollback on 409), UXR-101 (keyboard accessible), UXR-102 (touch target 44px min) |
| **Design Tokens** | `designsystem.md#colors` (`primary.500: #2196F3` selected border, `warning.main: #FF9800` risk badge), `designsystem.md#typography` |

> **Wireframe Status Legend:**
> - **AVAILABLE**: Local file exists at specified path

### CRITICAL: Wireframe Implementation Requirement

- **MUST** open `.propel/context/wireframes/Hi-Fi/wireframe-SCR-002-slot-selection.html` and match layout, slot highlight, badge placement.
- **MUST** implement all states: Default (slot available), Selected (primary.500 border), Error (409 toast + revert).
- **MUST** validate implementation against wireframe at breakpoints: 375px (mobile), 768px (tablet), 1440px (desktop).
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
| Backend | .NET 8 ASP.NET Core Web API | 8.0 |
| AI/ML | N/A | N/A |
| Mobile | N/A | N/A |

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

Implement the **Slot Selection** screen (SCR-002) — the second step of the patient booking flow (FL-001). The page receives a pre-loaded list of available slots (passed via React Router state from SCR-001 or re-fetched via React Query cache), displays them in a selectable grid, and applies **optimistic UI** on slot click per UXR-404. On slot selection the UI immediately highlights the chosen card (primary.500 border) and reveals the "Continue to booking" CTA. If a `409 Conflict` response is received when proceeding, the selection is reverted and a MUI `Snackbar` toast is shown. A no-show risk badge (orange warning) appears on slots where `noShowRisk > 0.7`.

Navigation: Back → SCR-001 (`/appointments/search`), Continue → SCR-003 (`/appointments/patient-details`), passing selected slot via router state.

---

## Dependent Tasks

- **task_001_fe_availability_search_ui.md** — MUST be complete; `SlotCard` component patterns and `useAvailability` hook are referenced here.
- **task_003_be_availability_api.md** — Backend availability endpoint; slot data originates from its response cached in React Query.

---

## Impacted Components

| Action | Module | Description |
|--------|--------|-------------|
| CREATE | `client/src/pages/slot-selection/` | New page folder |
| CREATE | `client/src/pages/slot-selection/SlotSelectionPage.tsx` | Top-level route component for SCR-002 |
| CREATE | `client/src/pages/slot-selection/components/SelectableSlotCard.tsx` | Slot card with selected-state highlight and no-show risk badge |
| CREATE | `client/src/pages/slot-selection/components/NoShowRiskBadge.tsx` | Orange MUI Badge with tooltip for risk > 70% |
| CREATE | `client/src/pages/slot-selection/components/SlotConflictToast.tsx` | MUI Snackbar toast for 409 conflict error |
| CREATE | `client/src/stores/booking-store.ts` | Zustand store for active booking session (selectedSlot, patientDetails) |
| MODIFY | `client/src/App.tsx` | Add `/appointments/slot-selection` route |

---

## Implementation Plan

1. **Add route** — Add `/appointments/slot-selection` to `App.tsx` `AuthenticatedLayout` children pointing to `SlotSelectionPage`.

2. **Booking store** (`client/src/stores/booking-store.ts`) — Zustand store holding booking session state:
   - `selectedSlot: AvailabilitySlot | null`
   - `setSelectedSlot(slot: AvailabilitySlot): void`
   - `clearBooking(): void`
   This store persists selection across the multi-step booking flow (SCR-002 → SCR-003 → SCR-006).

3. **SlotConflictToast component** — MUI `Snackbar` (position: bottom-center) with `Alert` severity="error" and message "Slot no longer available. Please select another." Auto-hide after 5000ms. Controlled by local `open` state passed via props.

4. **NoShowRiskBadge component** — Renders only when `noShowRisk > 0.7`. MUI `Chip` with icon `WarningAmberOutlined`, label "High no-show risk", `color="warning"` (maps to `warning.main: #FF9800`). Wraps in MUI `Tooltip` with explanatory text: "This slot has a high no-show probability based on scheduling signals. Staff may follow up with additional reminders."

5. **SelectableSlotCard component** — Extends the slot card pattern from SCR-001. Accepts `slot`, `isSelected`, `onSelect` props.
   - **Default state**: Standard MUI `Card` with `variant="outlined"`.
   - **Selected state**: `sx` override applying `border: 2px solid primary.500` (`#2196F3`) and subtle `backgroundColor: primary.50` (`#E3F2FD`).
   - **Optimistic selection**: `onSelect` dispatches to booking store AND triggers visual highlight simultaneously before any API call.
   - Renders `NoShowRiskBadge` if `slot.noShowRisk > 0.7`.
   - ARIA: `role="button"`, `aria-pressed={isSelected}`, keyboard Enter/Space triggers selection.

6. **SlotSelectionPage assembly**:
   - Read slots from React Router location state (`useLocation().state.slots`) if present; otherwise re-fetch via `useAvailability` with stored date range from URL params.
   - Render `SelectableSlotCard` grid (MUI Grid, same responsive breakpoints as SCR-001).
   - Booking progress stepper (step 2 active, UXR-403).
   - "Back" button → `navigate(-1)`.
   - "Continue to booking" button (MUI `Button` variant="contained", `primary`) — enabled only when a slot is selected. On click:
     a. Store selected slot in `booking-store`.
     b. Navigate to `/appointments/patient-details`.
   - **409 Conflict handling**: When backend returns 409 on a subsequent booking attempt (handled in task_003), consume the `booking-store` conflict flag, call `setSelectedSlot(null)`, and open `SlotConflictToast`.

---

## Current Project State

```
client/
  src/
    App.tsx                           ← Add /appointments/slot-selection route
    pages/
      LoginPage.tsx
      availability/                   ← Created in task_001
        AvailabilitySearchPage.tsx
        components/
          SlotCard.tsx
    stores/
      auth-store.ts
    hooks/
      useAvailability.ts              ← Created in task_001
```

> `booking-store.ts` does not yet exist — create it. `SlotSelectionPage` folder does not exist — create it.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `client/src/pages/slot-selection/SlotSelectionPage.tsx` | SCR-002 root page component |
| CREATE | `client/src/pages/slot-selection/components/SelectableSlotCard.tsx` | Slot card with selected-state highlight (primary.500 border) and risk badge |
| CREATE | `client/src/pages/slot-selection/components/NoShowRiskBadge.tsx` | MUI Chip + Tooltip for no-show risk > 70% |
| CREATE | `client/src/pages/slot-selection/components/SlotConflictToast.tsx` | MUI Snackbar for 409 Conflict error per UXR-404 |
| CREATE | `client/src/stores/booking-store.ts` | Zustand store for multi-step booking session state |
| MODIFY | `client/src/App.tsx` | Add `{ path: '/appointments/slot-selection', element: <SlotSelectionPage /> }` |

---

## External References

- [MUI Card — outlined variant with sx border override](https://mui.com/material-ui/react-card/#outlined)
- [MUI Chip — warning color](https://mui.com/material-ui/react-chip/)
- [MUI Tooltip](https://mui.com/material-ui/react-tooltip/)
- [MUI Snackbar + Alert](https://mui.com/material-ui/react-snackbar/)
- [TanStack React Query v4 — useQuery cache read](https://tanstack.com/query/v4/docs/framework/react/guides/initial-query-data)
- [Zustand v4 — create store](https://zustand.pmnd.rs/docs/getting-started/introduction)
- [React Router DOM v6 — useLocation, useNavigate](https://reactrouter.com/en/main/hooks/use-location)
- [ARIA pattern: Toggle Button](https://www.w3.org/WAI/ARIA/apg/patterns/button/)

---

## Build Commands

```bash
# Install dependencies (from client/)
npm install

# Type-check
npx tsc --noEmit

# Development server
npm run dev

# Build
npm run build
```

---

## Implementation Validation Strategy

- [ ] Unit tests pass — `SelectableSlotCard` renders in default and selected states
- [ ] `isSelected` prop applies `primary.500` border and `primary.50` background via `sx`
- [ ] `NoShowRiskBadge` renders only when `noShowRisk > 0.7`; hidden when ≤ 0.7
- [ ] `SlotConflictToast` appears and auto-hides after 5s on 409 response simulation
- [ ] `booking-store` holds `selectedSlot` after selection and clears on `clearBooking()`
- [ ] "Continue to booking" button is disabled before slot selection, enabled after
- [ ] Back navigation returns to `/appointments/search`
- [ ] `aria-pressed` attribute updates on selection (keyboard and mouse)
- [ ] **[UI Tasks]** Visual comparison against wireframe completed at 375px, 768px, 1440px
- [ ] **[UI Tasks]** Run `/analyze-ux` to validate wireframe alignment

---

## Implementation Checklist

- [X] Add `/appointments/slot-selection` route to `App.tsx` authenticated children
- [X] Create `client/src/stores/booking-store.ts` — Zustand store with `selectedSlot`, `setSelectedSlot`, `clearBooking`
- [X] Create `NoShowRiskBadge.tsx` — MUI Chip `color="warning"` + Tooltip, rendered when `noShowRisk > 0.7`
- [X] Create `SelectableSlotCard.tsx` — extends slot card, `isSelected` toggles `primary.500` border via `sx`, ARIA role + aria-pressed
- [X] Create `SlotConflictToast.tsx` — MUI Snackbar bottom-center, 5s auto-hide, severity error message
- [X] Create `SlotSelectionPage.tsx` — grid of `SelectableSlotCard`, stepper step 2, Back + Continue buttons, booking-store integration
- [X] Implement 409 Conflict path: revert selection in booking-store, open `SlotConflictToast`
- [X] **[UI Tasks - MANDATORY]** Reference wireframe from Design References table during implementation
- [X] **[UI Tasks - MANDATORY]** Validate UI matches wireframe at 375px, 768px, 1440px before marking task complete

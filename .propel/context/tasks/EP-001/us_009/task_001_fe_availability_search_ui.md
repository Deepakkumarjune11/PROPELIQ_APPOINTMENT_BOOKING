# Task - task_001_fe_availability_search_ui

## Requirement Reference

- **User Story**: US_009 — Appointment Availability Search
- **Story Location**: `.propel/context/tasks/EP-001/us_009/us_009.md`
- **Acceptance Criteria**:
  - AC-1: Given I am on the availability search page, When I select a date range, Then the system displays all available appointment slots within that range within 2 seconds at p95.
  - AC-4: Given no slots are available for the selected dates, When the search completes, Then the system displays an empty state with a suggestion to try different dates or contact the clinic.
- **Edge Cases**:
  - Redis cache unavailable → UI shows results from API fallback without error banner (transparent to user).
  - No slots available → empty state with CTA renders instead of empty grid.

---

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | `.propel/context/wireframes/Hi-Fi/wireframe-SCR-001-availability-search.html` |
| **Screen Spec** | `.propel/context/docs/figma_spec.md#SCR-001` |
| **UXR Requirements** | UXR-403 (progress stepper, booking steps 1–3), UXR-101 (keyboard accessible), UXR-102 (touch target 44px min) |
| **Design Tokens** | `designsystem.md#colors`, `designsystem.md#typography`, `designsystem.md#spacing` |

> **Wireframe Status Legend:**
> - **AVAILABLE**: Local file exists at specified path

### CRITICAL: Wireframe Implementation Requirement

- **MUST** open `.propel/context/wireframes/Hi-Fi/wireframe-SCR-001-availability-search.html` and match layout, spacing, typography, and colors.
- **MUST** implement all states shown in wireframe: Default, Loading (skeleton shimmer), Empty, Error.
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

Implement the **Availability Search** screen (SCR-001) as a React page component that lets a patient search for open appointment slots by selecting a date range. The screen is the entry point to the booking flow (FL-001 step 2). It must show a date-range picker, fire a React Query-backed API call against `GET /api/v1/appointments/availability`, render slot cards in a responsive grid, and handle all four UI states (Default, Loading/skeleton, Empty, Error) within the 2-second p95 target defined in AC-1 and NFR-001.

The component integrates with the booking flow stepper (UXR-403 — 3 steps: Search → Select → Details) and navigates to SCR-002 (Slot Selection) when a slot card is clicked.

---

## Dependent Tasks

- **task_003_be_availability_api.md** — The `GET /api/v1/appointments/availability` endpoint must be deployed (or mocked) before the React Query hook can resolve real data. Use `msw` request intercepts or a stub response in development until the backend task is complete.
- **task_004_db_availability_query.md** — Feeds data to task_003; no direct FE dependency, but end-to-end testing requires it.

---

## Impacted Components

| Action | Module | Description |
|--------|--------|-------------|
| CREATE | `client/src/pages/availability/` | New page folder |
| CREATE | `client/src/pages/availability/AvailabilitySearchPage.tsx` | Top-level route component for SCR-001 |
| CREATE | `client/src/pages/availability/components/DateRangeFilter.tsx` | Date range picker section (MUI DatePicker pair) |
| CREATE | `client/src/pages/availability/components/SlotCard.tsx` | Individual appointment slot card |
| CREATE | `client/src/pages/availability/components/SlotGrid.tsx` | Responsive grid wrapper for slot cards |
| CREATE | `client/src/pages/availability/components/AvailabilityEmptyState.tsx` | No-slots empty state with CTA |
| CREATE | `client/src/pages/availability/components/SlotGridSkeleton.tsx` | Skeleton shimmer grid (loading state) |
| CREATE | `client/src/hooks/useAvailability.ts` | React Query hook for availability API |
| CREATE | `client/src/api/availability.ts` | Axios/fetch API client function |
| MODIFY | `client/src/App.tsx` | Add `/appointments/search` route |

---

## Implementation Plan

1. **Create route** — Add `/appointments/search` to the React Router config in `App.tsx` within the `AuthenticatedLayout` children array, pointing to `AvailabilitySearchPage`.

2. **API client** (`client/src/api/availability.ts`) — Export `fetchAvailability(startDate: string, endDate: string): Promise<AvailabilitySlot[]>` using the project's existing HTTP client pattern. Accept ISO-8601 date strings (`YYYY-MM-DD`). Map the response to the `AvailabilitySlot` interface.

3. **React Query hook** (`client/src/hooks/useAvailability.ts`) — Wrap `fetchAvailability` with `useQuery`. Key: `['availability', startDate, endDate]`. Enabled only when both dates are set. `staleTime`: 60,000 ms (matches Redis 60s TTL, preventing redundant re-fetches within the cache window per AC-2). Return `{ slots, isLoading, isError, refetch }`.

4. **DateRangeFilter component** — Two MUI `DatePicker` inputs (start date / end date). Default start = today, default end = today + 7 days. Validate that `endDate >= startDate`. Dispatch values up to page via controlled props.

5. **SlotCard component** — MUI `Card` with: time display (`SlotDatetime` formatted `h:mm A`), provider name placeholder (e.g., "Available"), and a "Select" button (MUI `Button` variant `outlined`). `onClick` navigates to SCR-002 passing the slot as React Router state (`useNavigate`).

6. **SlotGrid component** — MUI `Grid` container (responsive: xs=12, sm=6, md=4, lg=3) rendering a `SlotCard` per available slot. Shows header with count: "N slots available".

7. **AvailabilityEmptyState component** — MUI `Box` centred with calendar illustration icon, heading "No appointments available", body copy "Try selecting different dates or contact the clinic.", and a MUI `Button` "Try different dates" that clears the filter back to today → today+7.

8. **SlotGridSkeleton component** — 8x MUI `Skeleton` cards (matching SlotCard dimensions) using `variant="rectangular"` with wave animation. Shown while `isLoading === true`.

9. **AvailabilitySearchPage assembly** — Compose all sub-components. Conditional rendering: `isLoading` → `SlotGridSkeleton`; `isError` → MUI `Alert` (severity="error", message "Unable to load availability. Please try again.") + retry button calling `refetch()`; `slots.length === 0` → `AvailabilityEmptyState`; otherwise → `SlotGrid`. Include BookingProgressStepper (step 1 active) per UXR-403.

---

## Current Project State

```
client/
  src/
    App.tsx                          ← Add /appointments/search route here
    components/
      layout/
        AuthenticatedLayout.tsx
        BottomNav.tsx
        Header.tsx
        Sidebar.tsx
    pages/
      LoginPage.tsx
    stores/
      auth-store.ts
    theme/
      healthcare-theme.ts
```

> Project state at task start. Sub-folders under `pages/availability/` and `hooks/` do not exist yet — create them.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `client/src/pages/availability/AvailabilitySearchPage.tsx` | SCR-001 root page component — orchestrates filter, grid, loading, empty, error states |
| CREATE | `client/src/pages/availability/components/DateRangeFilter.tsx` | Controlled date-range picker using MUI DatePicker (start + end date) |
| CREATE | `client/src/pages/availability/components/SlotCard.tsx` | Single availability slot card with time, label, and select button |
| CREATE | `client/src/pages/availability/components/SlotGrid.tsx` | MUI Grid wrapper rendering SlotCard list with result count header |
| CREATE | `client/src/pages/availability/components/AvailabilityEmptyState.tsx` | Empty state with CTA per AC-4 |
| CREATE | `client/src/pages/availability/components/SlotGridSkeleton.tsx` | 8-card skeleton shimmer loading state |
| CREATE | `client/src/hooks/useAvailability.ts` | React Query hook wrapping availability API call |
| CREATE | `client/src/api/availability.ts` | Typed API client function for availability endpoint |
| MODIFY | `client/src/App.tsx` | Add `{ path: '/appointments/search', element: <AvailabilitySearchPage /> }` to authenticated children |

---

## External References

- [MUI DatePicker v5 — Date and Time Pickers](https://mui.com/x/react-date-pickers/date-picker/)
- [MUI Grid v5 — Layout Grid](https://mui.com/material-ui/react-grid/)
- [MUI Skeleton v5 — Loading Skeletons](https://mui.com/material-ui/react-skeleton/)
- [TanStack React Query v4 — useQuery](https://tanstack.com/query/v4/docs/framework/react/reference/useQuery)
- [React Router DOM v6 — useNavigate](https://reactrouter.com/en/main/hooks/use-navigate)
- [MUI Card v5](https://mui.com/material-ui/react-card/)

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

- [ ] Unit tests pass (component renders without crashing in all 4 states)
- [ ] Integration tests pass — React Query hook resolves mock API response correctly
- [ ] **[UI Tasks]** Visual comparison against wireframe completed at 375px, 768px, 1440px
- [ ] **[UI Tasks]** Run `/analyze-ux` to validate wireframe alignment
- [ ] Date range default values set to today → today+7 on mount
- [ ] `endDate < startDate` triggers inline validation error on the date picker
- [ ] `staleTime` 60,000ms confirmed in `useAvailability` hook options
- [ ] Slot grid renders correct count header ("N slots available")
- [ ] Skeleton grid shows exactly 8 placeholder cards during loading
- [ ] Empty state "Try different dates" button resets filter to default range
- [ ] Error alert includes "Retry" button that calls `refetch()`
- [ ] Navigation to `/appointments/slot-selection` passes slot data via router state

---

## Implementation Checklist

- [X] Add `/appointments/search` route to `App.tsx` `AuthenticatedLayout` children
- [X] Create `client/src/api/availability.ts` with `fetchAvailability(startDate, endDate)` typed function
- [X] Create `client/src/hooks/useAvailability.ts` with React Query `useQuery` (staleTime=60s, enabled guard)
- [X] Create `DateRangeFilter.tsx` — MUI DatePicker pair, controlled, default today → today+7, endDate≥startDate validation
- [X] Create `SlotCard.tsx` — MUI Card with time, label, outlined "Select" button, onClick navigate to slot-selection
- [X] Create `SlotGrid.tsx` — MUI Grid responsive (xs=12, sm=6, md=4) with result count heading
- [X] Create `SlotGridSkeleton.tsx` — 8 MUI Skeleton cards wave animation
- [X] Create `AvailabilityEmptyState.tsx` — calendar icon, heading, body, reset CTA per AC-4
- [X] Create `AvailabilitySearchPage.tsx` — compose all sub-components with conditional state rendering
- [X] **[UI Tasks - MANDATORY]** Reference wireframe from Design References table during implementation
- [X] **[UI Tasks - MANDATORY]** Validate UI matches wireframe at 375px, 768px, 1440px before marking task complete

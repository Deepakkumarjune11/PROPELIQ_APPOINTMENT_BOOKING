# Task - task_001_fe_noshow_risk_badge

## Requirement Reference

- **User Story**: US_013 — No-Show Risk Scoring & Booking Transaction
- **Story Location**: `.propel/context/tasks/EP-001/us_013/us_013.md`
- **Acceptance Criteria**:
  - AC-2: When the no-show risk score exceeds 70%, the slot selection screen displays an orange warning badge "High no-show risk detected" with a tooltip explaining the contributing factors (time-to-appointment, insurance status, day-of-week).
  - AC-4: When the booking transaction returns 409 Conflict ("slot claimed by another patient"), the UI reverts to slot selection with a toast message "Slot no longer available. Please select another."
- **Edge Cases**:
  - N/A for this task — contributing factors display logic is purely presentational; incomplete signal sets are handled by the backend returning `isPartialScoring: true` in the slot data, which the FE renders as "Partial scoring — some signals unavailable" at the bottom of the tooltip.

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
| **UXR Requirements** | UXR-404 (optimistic UI with rollback on failure — on 409, revert selection and show toast), UXR-003 (inline guidance — tooltip explains contributing factors) |
| **Design Tokens** | `designsystem.md#colors` (`warning.main: #FF9800` badge background, `warning.contrastText` badge label), `designsystem.md#typography` (Roboto caption for tooltip content) |

> **Wireframe Status Legend:**
> - **AVAILABLE**: Local file exists at specified path

### CRITICAL: Wireframe Implementation Requirement

- **MUST** open `.propel/context/wireframes/Hi-Fi/wireframe-SCR-002-slot-selection.html` and match badge placement, tooltip trigger, and orange colour usage for the high-risk warning.
- **MUST** implement the tooltip using MUI `Tooltip` component with multi-line content listing each contributing factor.
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

Two focused enhancements to the **Slot Selection** screen (SCR-002):

**1. `NoShowRiskBadge` tooltip (AC-2)**  
The `NoShowRiskBadge` component was created in US_009/task_002 as a simple orange chip rendered when `riskScore > 0.7`. US_013 enhances it to include a MUI `Tooltip` showing the human-readable contributing factors returned by the availability API (implemented in US_013/task_002 backend). The tooltip content is a bulleted list derived from the `riskContributingFactors: string[]` field now included in the slot data — e.g. "Appointment in 2 days (elevated risk)", "Insurance status: Fail (high risk)". If `isPartialScoring: true`, an italic footer line "Partial scoring — some signals unavailable" is appended.

**2. 409 Conflict handling at booking submit (AC-4)**  
The booking API (`POST /api/v1/appointments/{slotId}/register`) can now return `409 Conflict` if the slot was claimed by a concurrent user between selection and submission. This is caught by the `useRegisterPatient` mutation's `onError` handler. On 409: clear `booking-store.selectedSlot`, navigate back to `/appointments/slot-selection`, and show a `SlotConflictToast` "Slot no longer available. Please select another."

The `SlotConflictToast` already exists from US_009/task_002. The 409 error routing is the new addition — previously the toast was only triggered on slot-selection conflicts, not on booking-submit conflicts.

---

## Dependent Tasks

- **task_002_be_noshow_risk_scoring_service.md** (US_013) — Availability API must return `riskContributingFactors: string[]` and `isPartialScoring: boolean` per slot for the tooltip to render.
- **task_002_be_patient_registration_api.md** (US_010) — `useRegisterPatient` mutation hook must exist; this task adds 409 handling to it.
- **task_002_fe_slot_selection_ui.md** (US_009) — `NoShowRiskBadge` component must exist; this task enhances it.

---

## Impacted Components

| Action | Module | Description |
|--------|--------|-------------|
| MODIFY | `client/src/pages/slot-selection/components/NoShowRiskBadge.tsx` | Wrap chip in MUI `Tooltip` with contributing factors list; add `isPartialScoring` footer note |
| MODIFY | `client/src/api/availability.ts` | Update `SlotDto` type to include `riskContributingFactors: string[]` and `isPartialScoring: boolean` |
| MODIFY | `client/src/hooks/useRegisterPatient.ts` | Add `onError` 409 handler: clear `booking-store.selectedSlot`, navigate to `/appointments/slot-selection`, trigger `SlotConflictToast` |
| MODIFY | `client/src/stores/booking-store.ts` | Confirm `clearSelectedSlot()` action (or `setSelectedSlot(null)`) is available; add if missing |

---

## Implementation Plan

1. **Update `SlotDto` type** (`client/src/api/availability.ts`):
   ```typescript
   export interface SlotDto {
     id: string;
     slotDatetime: string;
     noShowRiskScore: number | null;
     riskContributingFactors: string[];   // NEW — list of human-readable factor strings
     isPartialScoring: boolean;           // NEW — true when patient signals unavailable at search time
   }
   ```

2. **Enhance `NoShowRiskBadge`** — wrap the existing MUI `Chip` with a MUI `Tooltip`:
   ```tsx
   const tooltipContent = (
     <Box>
       <Typography variant="caption" fontWeight="bold">Risk factors:</Typography>
       <ul style={{ margin: '4px 0', paddingLeft: 16 }}>
         {riskContributingFactors.map((factor, i) => (
           <li key={i}><Typography variant="caption">{factor}</Typography></li>
         ))}
       </ul>
       {isPartialScoring && (
         <Typography variant="caption" fontStyle="italic" color="text.secondary">
           Partial scoring — some signals unavailable
         </Typography>
       )}
     </Box>
   );

   return (
     <Tooltip title={tooltipContent} arrow placement="top">
       <Chip
         label="High no-show risk detected"
         size="small"
         sx={{ bgcolor: 'warning.main', color: 'warning.contrastText', cursor: 'default' }}
       />
     </Tooltip>
   );
   ```
   The chip only renders when `noShowRiskScore !== null && noShowRiskScore > 0.7`.

3. **Add 409 handling to `useRegisterPatient`** (`client/src/hooks/useRegisterPatient.ts`):
   ```typescript
   onError: (error) => {
     if (axios.isAxiosError(error) && error.response?.status === 409) {
       bookingStore.getState().setSelectedSlot(null);   // clear selection
       navigate('/appointments/slot-selection');
       // SlotConflictToast is shown via a Zustand toast action or by setting a query param
       // Use the existing SlotConflictToast mechanism from US_009/task_002
       showSlotConflictToast('Slot no longer available. Please select another.');
     }
   }
   ```

4. **`booking-store.ts`** — confirm `setSelectedSlot(null)` clears the selected slot. Add `clearSelectedSlot` action alias if not present.

---

## Current Project State

```
client/src/
  pages/
    slot-selection/
      SlotSelectionPage.tsx                   ← US_009/task_002
      components/
        SelectableSlotCard.tsx               ← US_009/task_002
        NoShowRiskBadge.tsx                  ← US_009/task_002 — MODIFY (add tooltip)
        SlotConflictToast.tsx                ← US_009/task_002 — reference existing component
  hooks/
    useRegisterPatient.ts                    ← US_010/task_001 — MODIFY (add 409 handler)
  api/
    availability.ts                          ← US_009/task_003 — MODIFY (extend SlotDto type)
  stores/
    booking-store.ts                         ← US_009/task_002 — MODIFY (confirm clearSelectedSlot)
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | `client/src/api/availability.ts` | Add `riskContributingFactors: string[]` and `isPartialScoring: boolean` to `SlotDto` |
| MODIFY | `client/src/pages/slot-selection/components/NoShowRiskBadge.tsx` | Wrap chip in MUI `Tooltip` with factors list and `isPartialScoring` footer; pass `riskContributingFactors` and `isPartialScoring` props |
| MODIFY | `client/src/hooks/useRegisterPatient.ts` | Add `onError` 409 branch: clear `selectedSlot`, navigate to slot selection, show `SlotConflictToast` |
| MODIFY | `client/src/stores/booking-store.ts` | Add `clearSelectedSlot` action if not present (sets `selectedSlot` to null) |

---

## External References

- [MUI Tooltip — custom rich content](https://mui.com/material-ui/react-tooltip/#variable-width)
- [MUI Chip — `warning.main` colour (design token)](https://mui.com/material-ui/react-chip/)
- [TanStack React Query v4 — `useMutation` onError callback](https://tanstack.com/query/v4/docs/framework/react/reference/useMutation)
- [Axios — `isAxiosError` type guard for HTTP status inspection](https://axios-http.com/docs/handling_errors)
- [UXR-404 — Optimistic UI with rollback on 409 conflict](`.propel/context/docs/figma_spec.md#UXR-404`)

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

- [ ] `NoShowRiskBadge` tooltip opens on hover when `noShowRiskScore > 0.7` and `riskContributingFactors.length > 0`
- [ ] Tooltip lists each factor string on its own line
- [ ] "Partial scoring — some signals unavailable" italic note appears when `isPartialScoring: true`
- [ ] Badge does not render when `noShowRiskScore === null` or `noShowRiskScore <= 0.7`
- [ ] `SlotDto` type includes `riskContributingFactors` and `isPartialScoring` (TypeScript compiles cleanly)
- [ ] On booking submit returning 409: `booking-store.selectedSlot` is cleared, route is `/appointments/slot-selection`, `SlotConflictToast` is visible with correct message
- [ ] On booking submit returning 200: no toast, normal navigation to confirmation page
- [ ] **[UI Tasks]** Visual comparison against wireframe completed at 375px, 768px, 1440px
- [ ] **[UI Tasks]** Run `/analyze-ux` to validate wireframe alignment

---

## Implementation Checklist

- [x] Modify `SlotDto` in `api/availability.ts` — added `riskContributingFactors?: string[]`, `isPartialScoring?: boolean` to `AvailabilitySlot`
- [x] Modify `NoShowRiskBadge.tsx` — added `riskContributingFactors` and `isPartialScoring` props; wrapped chip in MUI `Tooltip` with rich content (factors list + partial scoring italic footer note); label updated to "High no-show risk detected"
- [x] Modify `useRegisterPatient.ts` — added 409 slot-conflict branch: `clearSelectedSlot()` + `setConflictError(true)` + navigate to `/appointments/slot-selection`; email-conflict 409 now distinguished by presence of `emailConflictMessage`
- [x] Modify `booking-store.ts` — added `clearSelectedSlot` action (sets `selectedSlot: null`, preserves rest of booking state)
- [x] Confirmed `SlotConflictToast` in `SlotSelectionPage` consumes `hasConflictError` from store — no duplication; call site unchanged
- [x] `SelectableSlotCard.tsx` updated to pass `riskContributingFactors` and `isPartialScoring` to `NoShowRiskBadge`
- [x] `npx tsc --noEmit` passes with zero errors

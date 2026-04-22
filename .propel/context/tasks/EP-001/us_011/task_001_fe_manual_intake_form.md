# Task - task_001_fe_manual_intake_form

## Requirement Reference

- **User Story**: US_011 — Manual Intake Form
- **Story Location**: `.propel/context/tasks/EP-001/us_011/us_011.md`
- **Acceptance Criteria**:
  - AC-1: When the form loads, all required intake questions are displayed as form fields with progress indication.
  - AC-2: When switching to conversational AI mode, previously entered manual answers are preserved and available in the conversational context.
  - AC-3: When returning to manual form from conversational mode, all previously entered answers (from both modes) are pre-populated.
  - AC-4: On submit, the system stores the intake response with mode="manual" (handled in task_002; this task fires the mutation).
- **Edge Cases**:
  - Patient navigates away mid-form → partial answers persist in Zustand `intake-store`; returning to `/appointments/intake/manual` restores progress.
  - Very long free-text answers → MUI TextField `inputProps={{ maxLength: 500 }}` with visible character counter; overflow truncated with helper-text warning "Max 500 characters".

---

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | `.propel/context/wireframes/Hi-Fi/wireframe-SCR-004-manual-intake-form.html` |
| **Screen Spec** | `.propel/context/docs/figma_spec.md#SCR-004` |
| **UXR Requirements** | UXR-403 (progress stepper, step 4 active — Search→Select→Details→Intake), UXR-003 (inline guidance/tooltips for complex intake fields), UXR-502 (inline validation on blur, error text below field, submit disabled until required fields valid), UXR-101 (keyboard accessible), UXR-102 (touch target 44px min) |
| **Design Tokens** | `designsystem.md#colors` (`primary.500: #2196F3` progress bar fill, `error.main: #F44336` validation errors), `designsystem.md#typography`, `designsystem.md#spacing` |

> **Wireframe Status Legend:**
> - **AVAILABLE**: Local file exists at specified path

### CRITICAL: Wireframe Implementation Requirement

- **MUST** open `.propel/context/wireframes/Hi-Fi/wireframe-SCR-004-manual-intake-form.html` and match layout, field grouping, progress bar, and mode-switch button placement.
- **MUST** implement all states: Default, Loading (submit spinner), Error (API error alert), Validation (inline field errors).
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

Implement the **Manual Intake Form** screen (SCR-004) — step 4 of the patient booking flow (FL-001). This screen renders a structured set of clinical intake questions as typed form fields, tracks completion progress, and allows the patient to switch to conversational AI mode (SCR-005) at any point while preserving their entered answers.

The core architectural challenge is **cross-mode answer preservation** (AC-2, AC-3): answers entered in manual mode must be accessible to the conversational intake flow and vice versa. This is solved by a **shared `intake-store`** (Zustand) that holds the canonical `Record<questionId, string>` answers object, decoupled from either mode's UI state. Both SCR-004 and SCR-005 read from and write to this store.

On submit, the page fires a React Query mutation against `POST /api/v1/patients/{patientId}/intake` (implemented in task_002). The `patientId` is read from `booking-store`.

Intake questions are defined in a static configuration file (`intakeQuestions.ts`) rather than fetched from the API — they represent fixed clinical intake fields for this phase of the product (chief complaint, medications, allergies, medical history, reason for visit). This satisfies YAGNI for phase-1 while leaving a clear upgrade path to a configurable question set.

---

## Dependent Tasks

- **task_002_be_submit_intake_api.md** (US_011) — `POST /api/v1/patients/{patientId}/intake` endpoint must be available (or mocked) for the submit mutation to resolve.
- **task_002_be_patient_registration_api.md** (US_010) — `booking-store.patientDetails.patientId` must be populated (set after successful registration).

---

## Impacted Components

| Action | Module | Description |
|--------|--------|-------------|
| CREATE | `client/src/pages/intake/` | New page folder shared by manual and conversational intake routes |
| CREATE | `client/src/pages/intake/ManualIntakeFormPage.tsx` | SCR-004 root page component |
| CREATE | `client/src/pages/intake/components/IntakeQuestionField.tsx` | Single intake question renderer (text, select, or checkbox) |
| CREATE | `client/src/pages/intake/components/IntakeProgressBar.tsx` | MUI LinearProgress showing answered/total ratio |
| CREATE | `client/src/pages/intake/components/ModeSwitchButton.tsx` | "Switch to conversational" icon button + label |
| CREATE | `client/src/config/intakeQuestions.ts` | Static intake question definitions (id, label, type, required, maxLength) |
| CREATE | `client/src/stores/intake-store.ts` | Zustand store: shared answers map + mode flag |
| CREATE | `client/src/api/intake.ts` | API client for `POST /api/v1/patients/{patientId}/intake` |
| CREATE | `client/src/hooks/useSubmitIntake.ts` | React Query `useMutation` hook for intake submission |
| MODIFY | `client/src/App.tsx` | Add `/appointments/intake/manual` and `/appointments/intake/conversational` routes |

---

## Implementation Plan

1. **Static intake question definitions** (`client/src/config/intakeQuestions.ts`):
   ```typescript
   export interface IntakeQuestion {
     id: string;
     label: string;
     type: 'text' | 'multiline' | 'select' | 'checkbox';
     required: boolean;
     maxLength?: number;
     options?: string[];       // for 'select' type
     tooltip?: string;         // UXR-003 inline guidance
   }

   export const INTAKE_QUESTIONS: IntakeQuestion[] = [
     { id: 'reasonForVisit',  label: 'Reason for visit',         type: 'multiline', required: true,  maxLength: 500, tooltip: 'Describe your main concern or reason for this appointment.' },
     { id: 'chiefComplaint',  label: 'Chief complaint',          type: 'multiline', required: true,  maxLength: 500 },
     { id: 'currentMeds',     label: 'Current medications',      type: 'multiline', required: false, maxLength: 500, tooltip: 'List name and dosage of all medications you currently take.' },
     { id: 'allergies',       label: 'Known allergies',          type: 'multiline', required: false, maxLength: 500 },
     { id: 'medicalHistory',  label: 'Relevant medical history', type: 'multiline', required: false, maxLength: 500 },
   ];
   ```

2. **`intake-store`** (`client/src/stores/intake-store.ts`) — Zustand store:
   ```typescript
   interface IntakeStore {
     answers: Record<string, string>;       // questionId → answer text
     mode: 'manual' | 'conversational' | null;
     setAnswer(questionId: string, value: string): void;
     setMode(mode: 'manual' | 'conversational'): void;
     clearIntake(): void;
   }
   ```
   Persisted in `sessionStorage` via Zustand `persist` middleware with storage key `"propeliq-intake"` — survives page refresh/back-navigation within the session but is cleared on tab close (edge case: navigating away and returning restores progress).

3. **`IntakeProgressBar` component** — MUI `LinearProgress` (variant="determinate") with value = `(answeredRequiredCount / totalRequired) * 100`. Shows label "3 of 5 required questions answered" using MUI `Typography` variant="caption".

4. **`ModeSwitchButton` component** — MUI `Button` variant="outlined" with `SwapHorizIcon` start icon and label "Switch to conversational". On click: calls `intake-store.setMode('conversational')` then `navigate('/appointments/intake/conversational')`. Does NOT clear existing answers (AC-2 — answers preserved).

5. **`IntakeQuestionField` component** — Renders one question based on `question.type`:
   - `multiline` → MUI `TextField` multiline rows=3, `inputProps={{ maxLength }}`, `helperText` shows `{value.length}/{maxLength}` character counter when `value.length > maxLength * 0.8`. Error helperText on blur when required and empty.
   - `select` → MUI `Select` with provided `options`.
   - `checkbox` → MUI `FormControlLabel` + `Checkbox`.
   Reads initial value from `intake-store.answers[question.id]` (pre-populates from store — AC-3). Updates store via `setAnswer` on `onChange`.

6. **`ManualIntakeFormPage` assembly**:
   - Guard: if `booking-store.patientDetails` is null, redirect to `/appointments/search`.
   - Render booking stepper (step 4 active, UXR-403).
   - Render `IntakeProgressBar` above question list.
   - Render `ModeSwitchButton` in the top-right corner of the form card.
   - Render `INTAKE_QUESTIONS.map(q => <IntakeQuestionField key={q.id} question={q} />)`.
   - "Submit intake" MUI `Button` (variant="contained") — disabled until all required questions answered and `!isSubmitting`.
   - On submit: call `submitIntake` mutation from `useSubmitIntake`. On success: navigate to `/appointments/confirmation`. On error: show MUI `Alert` severity="error".

7. **Routes** — Add to `App.tsx`:
   - `/appointments/intake/manual` → `ManualIntakeFormPage`
   - `/appointments/intake/conversational` → placeholder `ConversationalIntakePage` (implemented in a future US_012 story; route stub prevents 404 when mode-switch button is used during development).

---

## Current Project State

```
client/
  src/
    App.tsx                                ← Add /appointments/intake/* routes
    pages/
      LoginPage.tsx
      availability/                        ← us_009/task_001
      slot-selection/                      ← us_009/task_002
      patient-details/                     ← us_010/task_001
    stores/
      auth-store.ts
      booking-store.ts                     ← has selectedSlot + patientDetails
    hooks/
      useAvailability.ts
      useRegisterPatient.ts                ← us_010/task_001
    api/
      availability.ts
      registration.ts
    config/                                ← Does not exist yet — CREATE
```

> `pages/intake/`, `stores/intake-store.ts`, `config/intakeQuestions.ts`, `api/intake.ts`, `hooks/useSubmitIntake.ts` do not exist yet — create them.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `client/src/config/intakeQuestions.ts` | Static intake question definitions (id, label, type, required, maxLength, tooltip) |
| CREATE | `client/src/stores/intake-store.ts` | Zustand store with sessionStorage persistence: answers map, mode, setAnswer, setMode, clearIntake |
| CREATE | `client/src/api/intake.ts` | Typed API client for `POST /api/v1/patients/{patientId}/intake` |
| CREATE | `client/src/hooks/useSubmitIntake.ts` | React Query `useMutation` wrapping intake API call |
| CREATE | `client/src/pages/intake/ManualIntakeFormPage.tsx` | SCR-004 root page: stepper, progress bar, mode-switch, question fields, submit |
| CREATE | `client/src/pages/intake/components/IntakeQuestionField.tsx` | Single question renderer with answer pre-population from intake-store |
| CREATE | `client/src/pages/intake/components/IntakeProgressBar.tsx` | MUI LinearProgress with answered/total label |
| CREATE | `client/src/pages/intake/components/ModeSwitchButton.tsx` | "Switch to conversational" button — updates mode in store, navigates to SCR-005 |
| MODIFY | `client/src/App.tsx` | Add `/appointments/intake/manual` + `/appointments/intake/conversational` routes |

---

## External References

- [MUI TextField — multiline, maxLength, character counter](https://mui.com/material-ui/react-text-field/#multiline)
- [MUI LinearProgress — determinate value](https://mui.com/material-ui/react-progress/#linear-determinate)
- [MUI Tooltip — inline guidance (UXR-003)](https://mui.com/material-ui/react-tooltip/)
- [Zustand v4 — persist middleware (sessionStorage)](https://zustand.pmnd.rs/docs/integrations/persisting-store-data)
- [TanStack React Query v4 — useMutation](https://tanstack.com/query/v4/docs/framework/react/reference/useMutation)
- [React Router DOM v6 — useNavigate](https://reactrouter.com/en/main/hooks/use-navigate)

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

- [ ] Unit tests pass — `IntakeQuestionField` pre-populates value from `intake-store.answers` on mount
- [ ] Unit tests pass — `IntakeProgressBar` shows correct percentage when 2 of 2 required questions answered
- [ ] `ModeSwitchButton` navigates to `/appointments/intake/conversational` without clearing `intake-store.answers`
- [ ] Navigating back from `/appointments/intake/conversational` to `/appointments/intake/manual` restores all previously entered answers (AC-3)
- [ ] Character counter appears when field value exceeds 80% of `maxLength`; hard limit enforced via `inputProps.maxLength`
- [ ] Submit button disabled until all required questions have non-empty answers
- [ ] `intake-store` answers survive page refresh (sessionStorage persistence)
- [ ] `booking-store.patientDetails` null guard redirects to `/appointments/search`
- [ ] **[UI Tasks]** Visual comparison against wireframe completed at 375px, 768px, 1440px
- [ ] **[UI Tasks]** Run `/analyze-ux` to validate wireframe alignment

---

## Implementation Checklist

- [x] Create `client/src/config/intakeQuestions.ts` with 5 question definitions (reasonForVisit, chiefComplaint, currentMeds, allergies, medicalHistory) including tooltips and maxLength
- [x] Create `client/src/stores/intake-store.ts` — Zustand store with sessionStorage `persist` middleware; expose `answers`, `mode`, `setAnswer`, `setMode`, `clearIntake`
- [x] Create `client/src/api/intake.ts` — typed `submitIntake(patientId, payload)` function targeting `POST /api/v1/patients/{patientId}/intake`
- [x] Create `client/src/hooks/useSubmitIntake.ts` — `useMutation` with success navigation to `/appointments/confirmation` and error surfacing
- [x] Create `IntakeProgressBar.tsx` — MUI `LinearProgress` determinate, label "X of Y required questions answered"
- [x] Create `ModeSwitchButton.tsx` — `setMode('conversational')` + navigate, answers NOT cleared
- [x] Create `IntakeQuestionField.tsx` — reads initial value from `intake-store`, writes back via `setAnswer`, enforces maxLength + character counter, blur validation for required fields
- [x] Create `ManualIntakeFormPage.tsx` — guard, stepper (step 4), progress bar, mode-switch button, question fields, submit button + mutation
- [x] Add `/appointments/intake/manual` + `/appointments/intake/conversational` (stub) routes to `App.tsx`
- [x] TypeScript type-check: `npx tsc --noEmit` → 0 errors
- [ ] **[UI Tasks - MANDATORY]** Reference wireframe from Design References table during implementation
- [ ] **[UI Tasks - MANDATORY]** Validate UI matches wireframe at 375px, 768px, 1440px before marking task complete

# Task - task_001_fe_patient_details_form

## Requirement Reference

- **User Story**: US_010 — Patient Registration & Insurance Validation
- **Story Location**: `.propel/context/tasks/EP-001/us_010/us_010.md`
- **Acceptance Criteria**:
  - AC-1: When I submit email, name, DOB, phone, insurance provider, and member ID, the system creates the patient record and associates it with the selected appointment.
  - AC-2: Insurance validation result (pass/partial-match/fail) is displayed as informational feedback — does not block booking confirmation.
  - AC-3: When insurance is partial-match or fail, patient still receives confirmation (no blocking UI gate).
- **Edge Cases**:
  - Invalid phone number format → inline validation error on blur with guidance message ("Use format: 555-0123 or +1-555-0123").
  - Duplicate email (AC-4) is detected server-side → API returns `409`; UI shows inline error on the email field ("An account with this email already exists. Your appointment will be linked to your existing account.").
  - Insurance validation service unavailable → booking proceeds; insurance alert not shown (server returns `pending` status silently).

---

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | `.propel/context/wireframes/Hi-Fi/wireframe-SCR-003-patient-details-form.html` |
| **Screen Spec** | `.propel/context/docs/figma_spec.md#SCR-003` |
| **UXR Requirements** | UXR-403 (progress stepper, step 3 active), UXR-502 (inline validation on blur, error text below field, submit disabled until valid), UXR-101 (keyboard accessible), UXR-102 (touch target 44px min) |
| **Design Tokens** | `designsystem.md#colors` (`error.main: #F44336` field borders, `info.main: #2196F3` insurance alert), `designsystem.md#typography`, `designsystem.md#spacing` |

> **Wireframe Status Legend:**
> - **AVAILABLE**: Local file exists at specified path

### CRITICAL: Wireframe Implementation Requirement

- **MUST** open `.propel/context/wireframes/Hi-Fi/wireframe-SCR-003-patient-details-form.html` and match layout, field order, spacing, and validation states.
- **MUST** implement all states: Default, Loading (submit button spinner), Error (API error), Validation (inline field errors).
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

Implement the **Patient Details Form** screen (SCR-003) — step 3 of the patient booking flow (FL-001). This page collects the contact and insurance information required to create or identify a patient record and associate it with the selected appointment slot.

The form contains 5 text fields (email, full name, date of birth, phone, insurance member ID) and 2 selects (insurance provider from a fixed list). All fields validate inline on blur per UXR-502. The submit action calls `POST /api/v1/appointments/{slotId}/register` via a React Query mutation. The response includes an `insuranceStatus` field that drives a non-blocking MUI `Alert` informing the patient of their validation result. The booking progress stepper shows step 3 (Details) as active per UXR-403.

---

## Dependent Tasks

- **task_001_fe_availability_search_ui.md** (US_009) — `booking-store` Zustand store (`selectedSlot`) must exist; the `slotId` is read from it.
- **task_002_fe_slot_selection_ui.md** (US_009) — `booking-store.ts` created there; this task reads `selectedSlot.SlotId`.
- **task_002_be_patient_registration_api.md** (US_010, this story) — `POST /api/v1/appointments/{slotId}/register` endpoint must be deployed (or mocked) for integration.

---

## Impacted Components

| Action | Module | Description |
|--------|--------|-------------|
| CREATE | `client/src/pages/patient-details/` | New page folder |
| CREATE | `client/src/pages/patient-details/PatientDetailsFormPage.tsx` | SCR-003 root page component |
| CREATE | `client/src/pages/patient-details/components/PatientDetailsForm.tsx` | Controlled form with all fields and validation |
| CREATE | `client/src/pages/patient-details/components/InsuranceStatusAlert.tsx` | Non-blocking MUI Alert showing validation result |
| CREATE | `client/src/api/registration.ts` | API client for `POST /api/v1/appointments/{slotId}/register` |
| CREATE | `client/src/hooks/useRegisterPatient.ts` | React Query `useMutation` hook wrapping registration API |
| MODIFY | `client/src/stores/booking-store.ts` | Add `patientDetails` field and `setPatientDetails` action |
| MODIFY | `client/src/App.tsx` | Add `/appointments/patient-details` route |

---

## Implementation Plan

1. **Add route** — Add `/appointments/patient-details` to `App.tsx` `AuthenticatedLayout` children pointing to `PatientDetailsFormPage`. Redirect to `/appointments/search` if `booking-store.selectedSlot` is `null` (guard against direct URL access without prior slot selection).

2. **Extend booking-store** — Add `patientDetails: PatientDetailsFields | null` and `setPatientDetails(details: PatientDetailsFields): void` to the existing Zustand store in `booking-store.ts`. `PatientDetailsFields` interface: `{ email, name, dob, phone, insuranceProvider, insuranceMemberId }`.

3. **API client** (`client/src/api/registration.ts`) — Export `registerPatient(slotId: string, payload: PatientRegistrationRequest): Promise<PatientRegistrationResponse>`. Response type: `{ patientId: string; insuranceStatus: 'pass' | 'partial-match' | 'fail' | 'pending' }`.

4. **React Query mutation hook** (`client/src/hooks/useRegisterPatient.ts`) — `useMutation` wrapping `registerPatient`. On success: store `patientDetails` in `booking-store`, navigate to `/appointments/intake`. On `409`: surface `emailConflictMessage` from API error body. On other API errors: surface generic error message.

5. **PatientDetailsForm component** — Controlled form state (local `useState` per field). Fields:
   - `email` — MUI `TextField`, `type="email"`, validate on blur: RFC-compliant email format.
   - `name` — MUI `TextField`, validate on blur: non-empty, max 200 chars.
   - `dob` — MUI `DatePicker`, validate on blur: must be in the past, must make patient ≥ 0 years old.
   - `phone` — MUI `TextField`, validate on blur: regex `/^[+]?[(]?[0-9]{3}[)]?[-\s.]?[0-9]{3}[-\s.]?[0-9]{4,6}$/`. Error guidance: "Use format: 555-0123 or +1-555-0123".
   - `insuranceProvider` — MUI `Select` with fixed option list (Blue Cross, Aetna, Cigna, UnitedHealth, Other). Defaults to empty / placeholder.
   - `insuranceMemberId` — MUI `TextField`, optional, max 100 chars.
   - Submit `Button` (variant="contained") disabled while any required validation error is present or `isLoading` is true per UXR-502.

6. **InsuranceStatusAlert component** — Renders only after a successful registration response. Maps `insuranceStatus` to:
   - `pass` → no alert rendered (confirmed silently).
   - `partial-match` → MUI `Alert` severity="warning": "Insurance details partially matched. Staff may follow up to confirm your coverage."
   - `fail` → MUI `Alert` severity="info": "We could not verify your insurance on file. Your appointment is confirmed and staff will follow up."
   - `pending` → no alert rendered (validation skipped).

7. **PatientDetailsFormPage assembly** — Compose `PatientDetailsForm` + `InsuranceStatusAlert`. Show MUI `Alert` severity="error" for generic API errors with retry option. Include BookingProgressStepper step 3 active (UXR-403). On successful mutation, navigate to next step.

---

## Current Project State

```
client/
  src/
    App.tsx                              ← Add /appointments/patient-details route
    pages/
      LoginPage.tsx
      availability/                      ← Created in us_009/task_001
        AvailabilitySearchPage.tsx
      slot-selection/                    ← Created in us_009/task_002
        SlotSelectionPage.tsx
    stores/
      auth-store.ts
      booking-store.ts                   ← MODIFY: add patientDetails field
    hooks/
      useAvailability.ts                 ← Created in us_009/task_001
    api/
      availability.ts                    ← Created in us_009/task_001
```

> `pages/patient-details/`, `api/registration.ts`, `hooks/useRegisterPatient.ts` do not yet exist — create them.

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `client/src/pages/patient-details/PatientDetailsFormPage.tsx` | SCR-003 root page — stepper, form, insurance alert, error handling |
| CREATE | `client/src/pages/patient-details/components/PatientDetailsForm.tsx` | Controlled form: email, name, DOB, phone, insuranceProvider (Select), memberId |
| CREATE | `client/src/pages/patient-details/components/InsuranceStatusAlert.tsx` | Non-blocking MUI Alert driven by `insuranceStatus` response field |
| CREATE | `client/src/api/registration.ts` | Typed API client for `POST /api/v1/appointments/{slotId}/register` |
| CREATE | `client/src/hooks/useRegisterPatient.ts` | React Query `useMutation` hook with success navigation and 409 handling |
| MODIFY | `client/src/stores/booking-store.ts` | Add `patientDetails` field + `setPatientDetails` action |
| MODIFY | `client/src/App.tsx` | Add `/appointments/patient-details` route with slot-guard redirect |

---

## External References

- [MUI TextField — error state, helperText](https://mui.com/material-ui/react-text-field/#validation)
- [MUI Select v5](https://mui.com/material-ui/react-select/)
- [MUI DatePicker v5 — Controlled](https://mui.com/x/react-date-pickers/date-picker/#controlled-vs-uncontrolled)
- [MUI Alert — severity variants](https://mui.com/material-ui/react-alert/)
- [TanStack React Query v4 — useMutation](https://tanstack.com/query/v4/docs/framework/react/reference/useMutation)
- [Zustand v4 — updating existing store](https://zustand.pmnd.rs/docs/getting-started/introduction)
- [OWASP A01 — Client-side validation should not be sole defence](https://owasp.org/Top10/A01_2021-Broken_Access_Control/)

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

- [ ] Unit tests pass — form renders all 6 fields with correct MUI component types
- [ ] Unit tests pass — Submit button is disabled when any required validation error is present
- [ ] Phone number regex rejects "abcdef" and accepts "555-0123" and "+1-555-0123"
- [ ] Email field shows inline error on blur for malformed address
- [ ] `dob` DatePicker rejects future dates
- [ ] `InsuranceStatusAlert` renders warning for `partial-match`, info for `fail`, nothing for `pass` and `pending`
- [ ] `booking-store.selectedSlot` null guard redirects to `/appointments/search`
- [ ] 409 response surfaces email-conflict inline message below email field (not page-level error)
- [ ] Submit triggers `isLoading` spinner on button; form fields remain editable for correction
- [ ] **[UI Tasks]** Visual comparison against wireframe completed at 375px, 768px, 1440px
- [ ] **[UI Tasks]** Run `/analyze-ux` to validate wireframe alignment

---

## Implementation Checklist

- [X] Add `/appointments/patient-details` route to `App.tsx` with `selectedSlot` null-guard redirect
- [X] Extend `booking-store.ts` with `patientDetails: PatientDetailsFields | null` and `setPatientDetails` action
- [X] Create `client/src/api/registration.ts` — typed `registerPatient(slotId, payload)` function
- [X] Create `client/src/hooks/useRegisterPatient.ts` — `useMutation` with 409 error mapping and success navigation
- [X] Create `PatientDetailsForm.tsx` — 5 TextFields + 1 DatePicker + 1 Select, all with blur-triggered inline validation, submit disabled until valid (UXR-502)
- [X] Implement phone regex inline validation with guidance message per edge case
- [X] Create `InsuranceStatusAlert.tsx` — maps `insuranceStatus` → MUI Alert severity (warning/info) or null
- [X] Create `PatientDetailsFormPage.tsx` — compose form, stepper (step 3, UXR-403), insurance alert, error alert
- [ ] **[UI Tasks - MANDATORY]** Reference wireframe from Design References table during implementation
- [ ] **[UI Tasks - MANDATORY]** Validate UI matches wireframe at 375px, 768px, 1440px before marking task complete

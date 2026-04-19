# Task - TASK_001: FE Inline Validation & Error Summary

## Requirement Reference

- **User Story:** us_039 — Error Handling, Validation & Session Timeout UX
- **Story Location:** `.propel/context/tasks/EP-009-II/us_039/us_039.md`
- **Acceptance Criteria:**
  - AC-1: Given filling out a form, When leaving a required/invalid field, Then inline validation error
    displays below the field within 300ms with red border and descriptive text per UXR-501.
  - AC-2: Given multiple validation errors, When submitting, Then an error summary appears at the top
    listing all errors as clickable links that scroll to and focus the corresponding field per UXR-501.
  - AC-3: Given a server-side error during submission, When the error response is received, Then the
    system preserves all form data, displays the error message, and allows retry without re-entering
    data per UXR-502.
- **Edge Cases:**
  - Client vs server validation conflict → server validation is authoritative; server override error
    messages are displayed inline in the same field error slot.

> ⚠️ **UXR Definition Discrepancy (flag for BRD revision):**
> US_039 AC-1/AC-2 map both inline field validation and error summary to `UXR-501`, but
> `figma_spec.md` defines `UXR-501` as "System MUST display actionable error messages with recovery
> paths" (cross-cutting, non-form-specific) and `UXR-502` as "System MUST provide inline validation
> feedback on form fields — Field validation triggers on blur, error text displays below field."
> The inline validation behaviour described in AC-1 matches `figma_spec.md` `UXR-502`.
> US_039 AC-3 references `UXR-502`, which in figma_spec.md is the inline-validation UXR (matches).
> Tasks implement AC intent. Recommend realigning US_039 AC-1 traceability to `UXR-502` in a future
> BRD revision.

---

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | `.propel/context/wireframes/Hi-Fi/wireframe-SCR-024-login.html` (Validation state — email/password field errors); all wireframes in `.propel/context/wireframes/Hi-Fi/` for form screens (SCR-003, SCR-004, SCR-011, SCR-022) |
| **Screen Spec** | `figma_spec.md#SCR-024` (Login — validation state), `figma_spec.md#SCR-003` (Patient Details Form), `figma_spec.md#SCR-011` (Walk-In Booking), `figma_spec.md#SCR-022` (Create/Edit User) |
| **UXR Requirements** | UXR-501, UXR-502 (see discrepancy note above) |
| **Design Tokens** | `designsystem.md#colors` (`--color-error-500` #F44336 field border + error text), `designsystem.md#typography` (`--typography-caption` 12px for error helper text), `designsystem.md#spacing` (8px grid) |

> **Wireframe Implementation Requirement:**
> MUST open `wireframe-SCR-024-login.html` (Validation state) to match error message colour
> (`--color-error-500`), position (below input field), and typography (`caption` 12px).
> Validate field error display and `FormErrorSummary` at 375px, 768px, 1440px.

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | React | 18.x |
| Frontend Framework | TypeScript | 5.x |
| UI Library | Material-UI (MUI) | 5.x |
| Data Fetching | React Query (@tanstack/react-query) | 4.x |
| State Management | Zustand | 4.x |
| Build Tool | Vite | 5.x |
| Backend | .NET / ASP.NET Core | 8.0 |
| Database | PostgreSQL | 15.x |
| AI/ML | N/A | N/A |
| Vector Store | N/A | N/A |
| Mobile | N/A | N/A |

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

Build a reusable form validation system that all feature forms (EP-001 through EP-005) can adopt
without duplicating validation logic. The system has two parts:

1. **`useFormValidation` hook** — generic, field-agnostic validation hook. Takes a schema of
   validator functions per field; returns `errors`, `touched`, `handleBlur`, `handleServerErrors`,
   and `isValid`. Triggers validation within 300ms of `blur` (AC-1). Merges server-returned field
   errors directly into the same error state (edge case: server overrides client).

2. **`FormErrorSummary` component** — renders an `Alert severity="error"` at the top of a form
   listing all active errors as anchor links (`<a href="#field-id">`). On click, scrolls to the
   field AND calls `element.focus()` (AC-2). Integrates with `LiveRegion` (US_036) for assertive
   screen-reader announcement of error count on submit.

These two pieces replace the existing ad-hoc per-page validation state in `LoginPage.tsx` and
establish the canonical pattern for all subsequent feature forms.

---

## Dependent Tasks

- `EP-009-I/us_036/task_002_fe_semantic_html_form_a11y.md` — `useAccessibleForm` hook and
  `LiveRegion` component defined there; `useFormValidation` extends the pattern established by
  `useAccessibleForm` (the two hooks are complementary — `useAccessibleForm` handles ARIA/focus
  wiring, `useFormValidation` handles validation rules and error state).
- `EP-009-II/us_038/task_002_fe_toast_notification_system.md` — `useToast` should exist before
  AC-3 server error handling, so `handleServerErrors` can trigger `showError(serverMsg)` toast
  in addition to the inline field display.

---

## Impacted Components

| Component | Module | Action |
|-----------|--------|--------|
| `useFormValidation.ts` | `client/src/hooks/` | CREATE — generic validation hook |
| `FormErrorSummary.tsx` | `client/src/components/forms/` | CREATE — error summary Alert with clickable field links |
| `LoginPage.tsx` | `client/src/pages/` | MODIFY — replace manual `emailError`/`passwordError` state with `useFormValidation` |

---

## Implementation Plan

1. **Define `FieldValidator` and `ValidationSchema` types** in `useFormValidation.ts`:
   ```ts
   type FieldValidator<V> = (value: V) => string; // returns '' for valid, message for invalid
   type ValidationSchema<T extends Record<string, unknown>> = {
     [K in keyof T]?: FieldValidator<T[K]>;
   };
   ```
   These are generic over form shape `T`, enabling full TypeScript inference at call sites — no
   `any` casts required.

2. **`useFormValidation<T>` hook implementation**:
   - State: `errors: Partial<Record<keyof T, string>>` (only fields with messages), `touched:
     Partial<Record<keyof T, boolean>>`.
   - `handleBlur(field: keyof T, value: T[keyof T])`: sets `touched[field] = true`; runs
     `schema[field]?.(value)` (synchronous); updates `errors[field]`. No `setTimeout` needed —
     MUI `onBlur` fires immediately after focus loss, so the 300ms AC-1 threshold is met by
     direct synchronous execution.
   - `handleServerErrors(serverErrors: Partial<Record<keyof T, string>>)`: merges into `errors`
     (server overrides client — edge case from AC). Also sets `touched[field] = true` for all
     server-errored fields so error messages render.
   - `validate(values: T) → boolean`: validates all fields regardless of touched state; sets all
     errors; returns `isValid`. Called on form submit (AC-2 trigger).
   - `clearError(field: keyof T)`: called on field change to clear stale error immediately.
   - `isValid: boolean`: `Object.values(errors).every(e => !e)`.

3. **`FormErrorSummary` component**:
   - Props: `errors: Partial<Record<string, string>>`, `fieldIds: Record<string, string>` (maps
     field key → DOM `id` attribute on the input element).
   - Renders nothing when `Object.values(errors).filter(Boolean).length === 0`.
   - Otherwise renders: `<Alert severity="error" role="alert" aria-live="assertive">`:
     - Heading: `<Typography variant="subtitle2">Please fix the following errors:</Typography>`
     - `<List dense>` of `<ListItem>` per error: each item contains `<Link component="button"
       onClick={scrollAndFocus(fieldId)}>` with the error message text.
   - `scrollAndFocus(id: string)`: calls `document.getElementById(id)?.scrollIntoView({ behavior:
     'smooth', block: 'center' })` then `.focus()` with a 100ms `setTimeout` to allow scroll
     to settle (AC-2 requirement).
   - `aria-atomic="true"` on the `Alert` so screen readers announce the full list on update.

4. **Validate on submit — 300ms constraint for AC-1**: MUI `TextField` `onBlur` fires synchronously
   within the browser's event loop — no artificial delay is introduced. The "within 300ms" AC-1
   threshold is satisfied because blur validation is synchronous. Add a JSDoc comment explaining
   this for future developers.

5. **Server error merge (AC-3)**: Feature form `onError` React Query callback calls
   `handleServerErrors(parseApiErrors(err.response?.data))`. Define `parseApiErrors` utility
   function in `client/src/lib/apiErrors.ts` (CREATE):
   ```ts
   // Returns field-keyed error map from a 422 Unprocessable Entity response body
   export function parseApiErrors(data: unknown): Record<string, string> {
     if (!data || typeof data !== 'object') return {};
     const body = data as Record<string, unknown>;
     // ASP.NET Core ValidationProblemDetails: { errors: { fieldName: ["message"] } }
     if (body.errors && typeof body.errors === 'object') {
       return Object.fromEntries(
         Object.entries(body.errors as Record<string, string[]>).map(
           ([k, v]) => [k.toLowerCase(), v[0] ?? 'Invalid value']
         )
       );
     }
     return {};
   }
   ```
   This handles ASP.NET Core `ValidationProblemDetails` format (400/422 responses). OWASP A03:
   only `v[0]` (first message) is used — prevents unbounded error string injection from server.

6. **Migrate `LoginPage.tsx`** — replace `const [emailError, setEmailError] = useState('')` and
   `const [passwordError, setPasswordError] = useState('')` with a single `useFormValidation` call.
   Add `<FormErrorSummary>` above the form's first `TextField`. Pass `inputRef` / `id` props to
   each `TextField` so `scrollAndFocus` can locate them. This is a direct refactor — no behaviour
   change visible to users.

7. **`fieldIds` naming convention** — establish `{formName}-{fieldName}` as the ID pattern
   (e.g., `login-email`, `login-password`, `walkin-patientName`). Document in `FormErrorSummary`
   JSDoc so all feature teams follow the same pattern.

8. **WCAG 3.3.1 compliance**: All error messages must be programmatically associated with their
   field via MUI `TextField`'s `helperText` prop + `error` prop (which MUI renders as
   `aria-describedby` pointing to the helper text element). Verify in Chrome DevTools
   Accessibility panel that each errored field has `aria-describedby` linking to the error message.

---

## Current Project State

```
client/src/
├── hooks/
│   └── useFormValidation.ts         ← CREATE
├── lib/
│   └── apiErrors.ts                 ← CREATE (new folder)
├── components/
│   └── forms/                       ← CREATE (new folder)
│       └── FormErrorSummary.tsx
└── pages/
    └── LoginPage.tsx                ← MODIFY (adopt useFormValidation)
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `client/src/hooks/useFormValidation.ts` | Generic validation hook: `FieldValidator`, `ValidationSchema` types; `handleBlur`, `handleServerErrors`, `validate`, `clearError`, `isValid`; synchronous blur execution (≤ 300ms AC-1) |
| CREATE | `client/src/lib/apiErrors.ts` | `parseApiErrors` utility: parses ASP.NET Core ValidationProblemDetails 422 response into field-keyed error map; OWASP A03 guard (first message only) |
| CREATE | `client/src/components/forms/FormErrorSummary.tsx` | Error summary `Alert` with `List` of clickable `Link` buttons; `scrollAndFocus` with 100ms setTimeout; `aria-live="assertive"` + `aria-atomic="true"` |
| MODIFY | `client/src/pages/LoginPage.tsx` | Replace ad-hoc `emailError`/`passwordError` state with `useFormValidation`; add `<FormErrorSummary>`; add `id` props to `TextField` elements matching `{formName}-{fieldName}` convention |

---

## External References

- [MUI TextField — error, helperText, inputRef, id props (MUI v5)](https://mui.com/material-ui/react-text-field/)
- [MUI Alert — severity, role, aria-live (MUI v5)](https://mui.com/material-ui/react-alert/)
- [MUI List, ListItem, Link component="button" (MUI v5)](https://mui.com/material-ui/react-list/)
- [React — useRef, focus management, getElementById](https://react.dev/reference/react-dom/components/input#providing-a-label-for-an-input)
- [ASP.NET Core — ValidationProblemDetails response format](https://learn.microsoft.com/en-us/aspnet/core/web-api/handle-errors#validation-failure-error-response)
- [WCAG 3.3.1 Error Identification — programmatic field-error association](https://www.w3.org/WAI/WCAG22/Understanding/error-identification.html)
- [WCAG 3.3.3 Error Suggestion — descriptive error messages](https://www.w3.org/WAI/WCAG22/Understanding/error-suggestion.html)
- [OWASP A03 Injection — guard against server-injected error string payloads](https://owasp.org/Top10/A03_2021-Injection/)

---

## Build Commands

```bash
cd client
npm run dev     # Verify inline error appears on blur, FormErrorSummary appears on submit
npm run build   # Confirm TypeScript generics compile cleanly (ValidationSchema<T> inference)
npm run lint    # Confirm no a11y warnings (aria-describedby, role="alert")
```

---

## Implementation Validation Strategy

- [ ] Unit tests pass
- [ ] Integration tests pass (if applicable)
- [ ] **[UI Tasks]** Visual comparison against wireframe completed at 375px, 768px, 1440px
- [ ] **[UI Tasks]** Run `/analyze-ux` to validate wireframe alignment
- [ ] `handleBlur` fires synchronously (no setTimeout) — error appears in same render cycle as blur
- [ ] Error text appears in `--color-error-500` (#F44336) below the field (MUI `error` + `helperText` props)
- [ ] `FormErrorSummary` renders nothing when `errors` object has no non-empty string values
- [ ] Clicking a link in `FormErrorSummary` scrolls to the corresponding field AND focuses it
- [ ] `aria-describedby` on errored `TextField` points to the helper text element (Chrome DevTools Accessibility panel)
- [ ] `parseApiErrors` returns `{}` for null/undefined/non-object input (no crash)
- [ ] `parseApiErrors` returns `{ email: 'Email already exists' }` for `{ errors: { Email: ['Email already exists'] } }`
- [ ] Server error from `handleServerErrors` overrides client error for same field
- [ ] `LoginPage.tsx` has no remaining `useState` for `emailError`/`passwordError` (replaced by `useFormValidation`)

---

## Implementation Checklist

- [ ] **1.** Create `client/src/hooks/useFormValidation.ts`: define `FieldValidator<V>` and `ValidationSchema<T>` types; implement `useFormValidation<T>(schema)` returning `{ errors, touched, handleBlur, handleServerErrors, validate, clearError, isValid }`; synchronous execution on `handleBlur`; server override merge in `handleServerErrors`
- [ ] **2.** Create `client/src/lib/apiErrors.ts`: implement `parseApiErrors(data: unknown): Record<string, string>`; handle `ValidationProblemDetails` `{ errors: { Field: [messages] } }` format; only take `v[0]` per field (OWASP A03 guard); return `{}` for all unexpected shapes
- [ ] **3.** Create `client/src/components/forms/FormErrorSummary.tsx`: props `{ errors, fieldIds }`; render nothing when all errors empty; render `Alert severity="error" role="alert" aria-live="assertive" aria-atomic="true"`; list each error as `<Link component="button">` calling `scrollAndFocus`; implement `scrollAndFocus(id)` with `scrollIntoView({ behavior: 'smooth', block: 'center' })` + 100ms `setTimeout(() => el.focus())`
- [ ] **4.** Establish `{formName}-{fieldName}` ID naming convention: add JSDoc comment in `FormErrorSummary.tsx` header documenting the convention; e.g., `login-email`, `login-password`, `walkin-patientName`
- [ ] **5.** Modify `client/src/pages/LoginPage.tsx`: remove `emailError`/`passwordError` `useState` pairs; add `useFormValidation<LoginForm>({ email: validateEmail, password: validatePassword })`; add `id="login-email"` and `id="login-password"` on TextFields; add `<FormErrorSummary errors={errors} fieldIds={{ email: 'login-email', password: 'login-password' }} />`
- [ ] **6.** Verify WCAG 3.3.1: in Chrome DevTools → Accessibility panel, confirm each errored `TextField` has `aria-describedby` linking to its helper text element (MUI sets this automatically when `error` + `helperText` props are both provided)
- [ ] **7.** Test server-override edge case: call `handleServerErrors({ email: 'Email already registered' })` after `handleBlur` set `email` error to 'Please enter valid email' — confirm server message replaces client message in the same field slot
- [ ] **8.** Confirm `FormErrorSummary` focus management: Tab to a form field; submit with errors; verify focus moves to `FormErrorSummary` alert; click a link in the summary; verify field receives focus (not just scroll)
- [ ] **[UI Tasks - MANDATORY]** Reference `wireframe-SCR-024-login.html` Validation state for error colour and field-below positioning during implementation
- [ ] **[UI Tasks - MANDATORY]** Validate error display matches wireframe before marking task complete

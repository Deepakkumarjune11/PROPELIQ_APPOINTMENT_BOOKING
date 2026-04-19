# Task - task_002_fe_semantic_html_form_accessibility

## Requirement Reference

- **User Story**: US_036 — WCAG 2.2 AA Accessibility Implementation
- **Story Location**: `.propel/context/tasks/EP-009-I/us_036/us_036.md`
- **Acceptance Criteria**:
  - AC-2: Screen readers (NVDA, JAWS, VoiceOver) parse semantic HTML, heading hierarchy (h1→h6), ARIA landmarks (main, nav, complementary), and descriptive labels per UXR-102
  - AC-4: Form submission with errors → error messages announced via `aria-live="assertive"`, associated via `aria-describedby`, focus moves to first error per UXR-104
- **Edge Cases**:
  - Custom components (date pickers, comboboxes) without native accessibility: Implement WAI-ARIA Authoring Practices `combobox` + `listbox` roles with full keyboard interaction model (arrow keys, Enter, Escape) per the edge case specification
  - Dynamic content updates: `aria-live="polite"` for non-critical updates (slot availability refresh), `aria-live="assertive"` for errors and critical alerts

> **UXR Mapping Discrepancy Notice**:
> User story AC-2 cites UXR-102; AC-4 cites UXR-104. `figma_spec.md` maps:
> - UXR-102 = screen reader support
> - UXR-104 = color contrast (not form errors)
> Form error behavior (aria-live, focus-to-first-error) is more precisely UXR-502 (inline validation) and UXR-401 (immediate feedback) in figma_spec.md. This task implements per the described AC-4 behavior regardless of UXR tag. Flagged for BRD revision.

---

## Design References

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes — affects LoginPage and all future form-bearing pages (SCR-003, SCR-004, SCR-011, SCR-022, SCR-023, SCR-024, SCR-026, SCR-027) |
| **Figma URL** | `figma_spec.md` (UXR-102, UXR-104 from UXR table) |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | `.propel/context/wireframes/Hi-Fi/` |
| **Screen Spec** | All screens (SCR-001 through SCR-028) — semantic HTML and ARIA apply globally |
| **UXR Requirements** | UXR-102, UXR-104, UXR-502 |
| **Design Tokens** | Inherited from task_001 theme changes |

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | React 18.x + TypeScript 5.x | 18.x / 5.x |
| UI Library | MUI 5.x | 5.16.x |
| State | React hooks (`useRef`, `useCallback`, `useState`) | Built-in |
| Routing | React Router v6 | 6.x |

> **No new dependencies required.** All patterns use React built-ins and MUI's existing accessibility support.

---

## AI References

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |

---

## Task Overview

This task delivers component-level semantic HTML, ARIA landmark structure, and form accessibility behavior. It depends on task_001 for the theme-level focus ring and contrast fixes.

**Three problem areas addressed:**

**1. ARIA Landmarks & Heading Hierarchy (AC-2)**:
The existing layout structure (`Header`, `Sidebar`, `AuthenticatedLayout`) uses MUI components that render as `<div>` unless overridden. This task ensures:
- `Header`'s `<AppBar>` renders with `role="banner"` (already implicit for `<header>`) — add `component="header"` to `AppBar`
- `AuthenticatedLayout`'s `<Box component="main">` gets `aria-label` — done in task_001
- `Sidebar`'s `<Drawer>` gets `component="nav"` — done in task_001
- Page-level `<h1>` heading present on every screen (currently `LoginPage` uses `variant="h4" component="h1"` — correct; future pages must follow this pattern)
- `BottomNav`'s `<BottomNavigation>` gets `aria-label="Mobile navigation"` and each `BottomNavigationAction` gets proper label (MUI renders `aria-label` from the `label` prop — verify this is correct, add explicit `aria-label` if needed)

**2. `useAccessibleForm` Hook — form error management (AC-4)**:
A reusable React hook that encapsulates three WCAG form accessibility behaviors:
1. `aria-live="assertive"` announcement of error count when form submits with errors
2. Focus management — move focus to the first field with an error (WCAG 3.3.1)
3. Individual field `aria-describedby` wiring to error messages

**3. `LoginPage.tsx` AC-4 upgrades**:
- Add `aria-live="assertive"` to the auth error `<Alert>` container element (not just `role="alert"` — `role="alert"` already implies `aria-live="assertive"` but MUI's `Alert` needs explicit `aria-live` for cross-browser screen reader compatibility)
- Add `useAccessibleForm` hook for field-level error focus management on submit
- Add a visually hidden `aria-live="assertive"` announcement region that announces "Form has N errors" on submit failure

**4. `LiveRegion.tsx` — reusable ARIA live region component**:
A reusable component for dynamically announcing status updates to screen readers without visual display. Used by forms for error announcements and by slot availability updates (non-critical → `polite`).

**5. `Header.tsx` — landmark and skip link target completion**:
- Add `component="header"` to `AppBar` (renders semantic `<header>` element — implicit `role="banner"`)
- Ensure the Typography "PropelIQ Healthcare" logo/link is properly accessible (`<a>` vs `<span onClick>` — currently `<Typography component="span" onClick>` is not keyboard-accessible; must use a proper `<Link>` or `<Button>` component for navigation)

---

## Dependent Tasks

- **task_001_fe_theme_focus_motion_contrast.md** (this US): Theme focus ring and ARIA on `Sidebar` + `AuthenticatedLayout.main` must be applied first.
- **All future EP-001 through EP-005 feature pages**: `useAccessibleForm` hook is available for reuse by all form-bearing pages (booking, intake, patient registration, etc.).

---

## Implementation Plan

### 1. `LiveRegion.tsx` — reusable ARIA live region

```tsx
// client/src/components/accessibility/LiveRegion.tsx
// Renders a visually hidden element that announces updates to screen readers
// without showing content on screen.

interface LiveRegionProps {
  message: string;
  politeness?: 'polite' | 'assertive';
  atomic?: boolean;
}

// Visually hidden style — content is read by screen readers but not visible
const visuallyHidden: React.CSSProperties = {
  position: 'absolute',
  width: '1px',
  height: '1px',
  padding: 0,
  margin: '-1px',
  overflow: 'hidden',
  clip: 'rect(0,0,0,0)',
  whiteSpace: 'nowrap',
  borderWidth: 0,
};

export default function LiveRegion({
  message,
  politeness = 'polite',
  atomic = true,
}: LiveRegionProps) {
  return (
    <div
      aria-live={politeness}
      aria-atomic={atomic}
      style={visuallyHidden}
      // Empty string clears the region; screen readers only announce changes
    >
      {message}
    </div>
  );
}
```

### 2. `useAccessibleForm` hook — centralized form a11y behavior

```typescript
// client/src/hooks/useAccessibleForm.ts
// Centralizes WCAG 3.3.1 (Error Identification) and 3.3.3 (Error Suggestion)
// behaviors for all form pages.

import { useCallback, useRef, useState } from 'react';

interface FieldRef {
  fieldId: string;
  ref: React.RefObject<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>;
}

interface UseAccessibleFormReturn {
  // Announce message to aria-live="assertive" region
  announcement: string;
  // Call this with field refs in tab order; focuses first error field
  focusFirstError: (errorFieldIds: string[]) => void;
  // Register a field ref (call once per field at mount)
  registerField: (fieldId: string, ref: React.RefObject<HTMLInputElement>) => void;
}

export function useAccessibleForm(): UseAccessibleFormReturn {
  const [announcement, setAnnouncement] = useState('');
  const fieldRefs = useRef<FieldRef[]>([]);

  const registerField = useCallback(
    (fieldId: string, ref: React.RefObject<HTMLInputElement>) => {
      // Avoid duplicates on re-render
      if (!fieldRefs.current.find((f) => f.fieldId === fieldId)) {
        fieldRefs.current.push({ fieldId, ref });
      }
    },
    []
  );

  const focusFirstError = useCallback((errorFieldIds: string[]) => {
    if (errorFieldIds.length === 0) {
      setAnnouncement('');
      return;
    }

    // Announce error count to screen reader immediately (assertive = interrupts current reading)
    setAnnouncement(
      `Form submission failed. ${errorFieldIds.length} error${
        errorFieldIds.length === 1 ? '' : 's'
      } found. Please review and correct the highlighted fields.`
    );

    // Focus the first field with an error (in DOM order)
    const firstErrorField = fieldRefs.current.find((f) =>
      errorFieldIds.includes(f.fieldId)
    );
    if (firstErrorField?.ref.current) {
      // Defer focus by one tick to ensure announcement fires before focus shift
      setTimeout(() => {
        firstErrorField.ref.current?.focus();
      }, 100);
    }
  }, []);

  return { announcement, focusFirstError, registerField };
}
```

### 3. `LoginPage.tsx` — AC-4 accessibility upgrades

Key changes:
1. Use `useAccessibleForm` hook
2. Add `<LiveRegion>` with `assertive` for error announcement
3. Register input `ref`s for focus management
4. Replace `<Typography component="span" onClick>` logo with an accessible `<Link>`

```tsx
// LoginPage.tsx additions (not full rewrite — surgical changes only):

import { useRef } from 'react';
import { Link as MuiLink } from '@mui/material';
import LiveRegion from '@/components/accessibility/LiveRegion';
import { useAccessibleForm } from '@/hooks/useAccessibleForm';

// Inside LoginPage():
const emailRef = useRef<HTMLInputElement>(null);
const passwordRef = useRef<HTMLInputElement>(null);
const { announcement, focusFirstError, registerField } = useAccessibleForm();

// Register field refs on mount:
// useEffect(() => {
//   registerField('email', emailRef);
//   registerField('password', passwordRef);
// }, [registerField]);

// On submit: collect error field IDs and call focusFirstError:
const handleSubmit = (e: React.FormEvent) => {
  e.preventDefault();
  const emailErr = validateEmail(email);
  const passErr = validatePassword(password);
  setEmailError(emailErr);
  setPasswordError(passErr);

  const errorFields: string[] = [];
  if (emailErr)    errorFields.push('email');
  if (passErr)     errorFields.push('password');

  if (errorFields.length > 0) {
    focusFirstError(errorFields);  // announces + moves focus
    return;
  }
  // ... rest of submit logic
};

// In JSX, add LiveRegion BEFORE the form:
// <LiveRegion message={announcement} politeness="assertive" />

// Existing Alert — add aria-live="assertive" explicitly (belt-and-suspenders over role="alert"):
// <Alert severity="error" role="alert" aria-live="assertive" sx={{ mb: 3 }}>

// TextField additions — add inputRef for focus management:
// <TextField
//   id="email"
//   inputRef={emailRef}
//   ...existing props...
// />
```

### 4. `Header.tsx` — semantic `<header>` landmark + accessible logo link

```tsx
// Header.tsx modifications:

// 1. AppBar: add component="header" (renders as <header> element — implicit role="banner")
<AppBar
  component="header"           // renders as <header>, implicit role="banner"
  position="fixed"
  color="default"
  elevation={1}
  sx={{ zIndex: (theme) => theme.zIndex.drawer + 1 }}
>

// 2. Typography logo: replace <Typography component="span" onClick> with
//    <Typography component={RouterLink} to="/" ...> for keyboard accessibility
//    (span with onClick is not keyboard accessible — no Enter/Space trigger)
import { Link as RouterLink } from 'react-router-dom';

<Typography
  variant="h6"
  component={RouterLink}      // renders as <a> — keyboard + screen reader accessible
  to="/"
  aria-label="PropelIQ Healthcare - go to dashboard"
  sx={{
    color: 'primary.main',
    fontWeight: 500,
    fontSize: '1.25rem',
    flexGrow: 0,
    textDecoration: 'none',   // visual: no underline on logo
    '&:focus-visible': {
      outline: '2px solid #2196F3',
      outlineOffset: '2px',
      borderRadius: '2px',
    },
  }}
>
  PropelIQ Healthcare
</Typography>
```

### 5. `BottomNav.tsx` — accessible navigation labels

```tsx
// BottomNav.tsx modifications:

// 1. Add aria-label to BottomNavigation container
<BottomNavigation
  value={selectedIndex === -1 ? false : selectedIndex}
  onChange={(_, newValue: number) => { navigate(items[newValue].path); }}
  showLabels
  aria-label="Mobile navigation"    // identifies the nav region to screen readers
  component="nav"                   // renders as <nav> — semantic landmark
>

// 2. BottomNavigationAction: MUI already passes label as aria-label; verify
//    by inspecting DOM — MUI 5.x BottomNavigationAction renders:
//    <button aria-label="{label}"> when showLabels is false
//    When showLabels=true, the label is visible text — no aria-label needed (redundant)
//    No additional change needed here; confirm in browser DevTools
```

### 6. WAI-ARIA guidance for custom components (edge case documentation)

For any future custom component that lacks native accessibility (date pickers, comboboxes, multi-select):

**Combobox pattern** (WAI-ARIA 1.2):
```tsx
// Minimum required ARIA for a custom combobox dropdown:
<div role="combobox" aria-expanded={isOpen} aria-haspopup="listbox"
     aria-controls="listbox-id" aria-labelledby="label-id">
  <input
    aria-autocomplete="list"
    aria-activedescendant={activeOptionId}  // points to focused option
    onKeyDown={handleKeyDown}               // Arrow keys, Enter, Escape, Home, End
  />
</div>
<ul role="listbox" id="listbox-id" aria-labelledby="label-id">
  {options.map((opt, i) => (
    <li role="option" id={`option-${i}`} aria-selected={opt.value === selected}>
      {opt.label}
    </li>
  ))}
</ul>
```

**Date picker pattern** (WAI-ARIA date picker dialog):
- When MUI X `DatePicker` is added, it already implements ARIA grid pattern (v6+)
- If using `<TextField type="date">` (zero-dependency fallback per US_033 task_001), it has native browser accessibility — no additional ARIA needed

---

## Current Project State

```
client/src/
├── components/
│   ├── accessibility/
│   │   ├── SkipToMainContent.tsx              (EXISTS from task_001)
│   │   └── LiveRegion.tsx                     ← CREATE
│   └── layout/
│       ├── Header.tsx                         ← MODIFY: component="header" on AppBar; RouterLink logo
│       └── BottomNav.tsx                      ← MODIFY: aria-label + component="nav" on BottomNavigation
├── hooks/
│   └── useAccessibleForm.ts                   ← CREATE
└── pages/
    └── LoginPage.tsx                          ← MODIFY: useAccessibleForm + LiveRegion + inputRef + aria-live
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `client/src/components/accessibility/LiveRegion.tsx` | Visually hidden `aria-live` region; `politeness` prop (`polite`\|`assertive`); `atomic` prop; used for form error announcement + slot availability updates |
| CREATE | `client/src/hooks/useAccessibleForm.ts` | `announcement` state + `focusFirstError(errorIds[])` + `registerField(id, ref)` — centralizes WCAG 3.3.1/3.3.3 form a11y for reuse across all form pages |
| MODIFY | `client/src/pages/LoginPage.tsx` | (a) import `useAccessibleForm` + `LiveRegion`; (b) add `emailRef`/`passwordRef` via `useRef`; (c) register refs in `useEffect`; (d) call `focusFirstError` on submit validation failure; (e) render `<LiveRegion message={announcement} politeness="assertive" />`; (f) add `aria-live="assertive"` to `<Alert>` auth error; (g) add `inputRef` to both TextFields |
| MODIFY | `client/src/components/layout/Header.tsx` | (a) `AppBar` gains `component="header"`; (b) Typography logo becomes `component={RouterLink} to="/"` with `aria-label`; remove `onClick={() => navigate('/')}` (replaced by RouterLink) |
| MODIFY | `client/src/components/layout/BottomNav.tsx` | `BottomNavigation` gains `component="nav"` + `aria-label="Mobile navigation"` |

---

## Security Notes (OWASP)

- No OWASP implications for this task — purely client-side accessibility attributes. No user-supplied input used in ARIA attribute values (all hardcoded strings or stable IDs like `"email"`, `"password"`).

---

## External References

- [WCAG 2.2 — 1.3.1 Info and Relationships (semantic HTML)](https://www.w3.org/WAI/WCAG22/Understanding/info-and-relationships.html)
- [WCAG 2.2 — 2.4.3 Focus Order](https://www.w3.org/WAI/WCAG22/Understanding/focus-order.html)
- [WCAG 2.2 — 3.3.1 Error Identification](https://www.w3.org/WAI/WCAG22/Understanding/error-identification.html)
- [WCAG 2.2 — 3.3.2 Labels or Instructions](https://www.w3.org/WAI/WCAG22/Understanding/labels-or-instructions.html)
- [WAI-ARIA Authoring Practices — Combobox Pattern](https://www.w3.org/WAI/ARIA/apg/patterns/combobox/)
- [WAI-ARIA Authoring Practices — Dialog Modal Pattern](https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/)
- [MUI Accessibility — AppBar / BottomNavigation](https://mui.com/material-ui/guides/accessibility/)
- [ARIA live regions — MDN](https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/ARIA_Live_Regions)
- [figma_spec.md — UXR-102 through UXR-105 requirements](../.propel/context/docs/figma_spec.md)

---

## Build Commands

```bash
cd client
npm run build    # Verify no TypeScript errors from new hook + components
npm run lint     # eslint-plugin-jsx-a11y should pass — no new violations
```

---

## Implementation Validation Strategy

- [ ] `LiveRegion`: render with `message="Test"` and `politeness="assertive"` → inspect DOM: `aria-live="assertive"` present; element visually invisible (confirm with browser DevTools — `width: 1px; height: 1px; overflow: hidden`)
- [ ] `useAccessibleForm.focusFirstError(['email'])`: `emailRef.current.focus()` called after 100ms; `announcement` state = "Form submission failed. 1 error found..."
- [ ] `useAccessibleForm.focusFirstError([])`: `announcement` = `''`; no focus change
- [ ] `LoginPage` submit with empty fields → screen reader test: NVDA/VoiceOver announces "Form submission failed. 2 errors found..." → then reads the email field label (focus moved to email)
- [ ] `LoginPage` submit with empty fields → visible focus ring appears on email `<input>` (inheriting task_001 focus ring)
- [ ] `Header` logo: Tab to logo `<a>` → Enter → navigates to `/`; logo is in Tab order (it's an `<a>` now)
- [ ] `Header` AppBar renders as `<header>` element: inspect DOM → `<header class="MuiAppBar-root ...">` (not `<div>`)
- [ ] `BottomNav` renders as `<nav>` element with `aria-label="Mobile navigation"`: inspect DOM on mobile viewport
- [ ] Screen reader test: VoiceOver on Safari lists landmarks → should announce: "Banner", "Navigation (Main navigation)", "Main (Main content)" — three distinct regions
- [ ] `aria-current="page"` on active Sidebar item (from task_001): JAWS announces "Dashboard, link, current page" when on `/`

---

## Implementation Checklist

- [ ] CREATE `LiveRegion.tsx` — `aria-live` + `aria-atomic` props; visually hidden CSS using `position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0,0,0,0)` (the "clip" technique is widely supported); empty string resets the region between announcements
- [ ] CREATE `useAccessibleForm.ts` — (a) `announcement` state string; (b) `fieldRefs` ref array; (c) `registerField` callback adds to array without duplicates; (d) `focusFirstError` sets `announcement` with count message, then calls `setTimeout(() => ref.current?.focus(), 100)` on first matching field; (e) export interface `UseAccessibleFormReturn` for TypeScript consumers
- [ ] MODIFY `LoginPage.tsx` — (a) `useRef<HTMLInputElement>(null)` for email and password; (b) `useEffect` registers both refs with `registerField`; (c) `handleSubmit` collects `errorFields` array and calls `focusFirstError`; (d) add `<LiveRegion message={announcement} politeness="assertive" />` immediately before `<Box component="form">`; (e) existing `<Alert>` gains `aria-live="assertive"`; (f) both `<TextField>` elements gain `inputRef` prop pointing to their respective refs
- [ ] MODIFY `Header.tsx` — (a) `AppBar` gets `component="header"` prop; (b) replace `<Typography component="span" onClick={() => navigate('/')}>` with `<Typography component={RouterLink} to="/" aria-label="PropelIQ Healthcare - go to dashboard">` with `textDecoration: 'none'`; remove the `cursor: pointer` (it's an `<a>` now so cursor is auto); (c) do NOT change any existing ARIA on `IconButton` user-menu — it is already correct
- [ ] MODIFY `BottomNav.tsx` — `BottomNavigation` gets `component="nav"` + `aria-label="Mobile navigation"`; verify in DOM that each `<BottomNavigationAction>` already exposes the `label` prop as visible text (no aria-label override needed when `showLabels={true}`)
- [ ] After task_001 + task_002 both applied: run axe-core in browser dev console → zero critical or serious violations on `LoginPage` and `AuthenticatedLayout`

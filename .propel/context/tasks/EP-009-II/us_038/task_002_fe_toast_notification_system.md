# Task - TASK_002: FE Toast Notification System

## Requirement Reference

- **User Story:** us_038 — Loading Feedback, Toasts & Progress Indicators
- **Story Location:** `.propel/context/tasks/EP-009-II/us_038/us_038.md`
- **Acceptance Criteria:**
  - AC-2: Given a successful action, When it completes, Then a success toast appears top-right,
    auto-dismisses after 5 seconds, and is dismissible via close button per UXR-402.
  - AC-3: Given a failed action, When error occurs, Then an error toast with meaningful message
    persists until dismissed and includes a "Retry" action when applicable per UXR-402.
  - AC-5: Given multiple toasts in quick succession, When they queue, Then maximum 3 toasts visible
    simultaneously, new toasts stack below existing ones, oldest auto-dismiss first per UXR-404.
- **Edge Cases:**
  - Page not visible (tab inactive) → queue notifications via Page Visibility API; display all queued
    toasts when user returns to the tab

> ⚠️ **UXR-402 Scope Discrepancy (flag for BRD revision):**
> US_038 AC-2/AC-3 reference `UXR-402`, aligned with `figma_spec.md` UXR-402:
> "System MUST provide success/error feedback for all state-changing actions — Toast notification
> appears for all mutations." This mapping is consistent. However, US_038 adds more specific
> behaviour (5s auto-dismiss, top-right positioning, Retry action, persistent error toasts) that
> is not captured in the `figma_spec.md` UXR-402 definition. Recommend enriching `figma_spec.md`
> UXR-402 with these specifics in a future BRD revision.

> ⚠️ **UXR-404 Definition Discrepancy (flag for BRD revision):**
> US_038 AC-5 maps toast queue management (max 3 simultaneous, FIFO auto-dismiss) to `UXR-404`.
> However, `figma_spec.md` defines `UXR-404` as: "System MUST use optimistic UI updates with
> rollback on failure — Slot selection immediately updates UI, reverts on conflict error."
> These are entirely different concerns. This task implements AC-5 toast queue intent. Recommend
> creating a dedicated UXR (e.g., UXR-405) for toast queue management in a future BRD revision.

---

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | `.propel/context/wireframes/Hi-Fi/wireframe-SCR-024-login.html` (error alert/toast pattern shown); all wireframes in `.propel/context/wireframes/Hi-Fi/` apply for cross-cutting toast usage |
| **Screen Spec** | `figma_spec.md#SCR-025` (cross-cutting; all screens); `figma_spec.md#SCR-002`, `SCR-013`, `SCR-014` (UXR-402 toast trigger screens) |
| **UXR Requirements** | UXR-402, UXR-404 (see discrepancy notes above) |
| **Design Tokens** | `designsystem.md#colors` (success `--color-success-500`, error `--color-error-500`, info `--color-info-500`, warning `--color-warning-500`), `designsystem.md#elevation` (`--elevation-8` for toast shadow), `designsystem.md#transitions` (`--transition-standard` 300ms for fade-in/out) |

> **Wireframe Implementation Requirement:**
> MUST reference toast/alert visual patterns shown in `wireframe-SCR-024-login.html` (Error state
> Alert component) for colour, typography, and layout guidance. Position: fixed top-right
> (right: 24px, top: 80px — below AppBar). Validate toast layout at 375px and 1440px.

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | React | 18.x |
| Frontend Framework | TypeScript | 5.x |
| UI Library | Material-UI (MUI) | 5.x |
| State Management | Zustand | 4.x |
| Data Fetching | React Query (@tanstack/react-query) | 4.x |
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

Build a complete toast notification system using MUI `Snackbar` + `Alert` (native to
`@mui/material 5.x` — no third-party toast library required) backed by a **Zustand store**
(`toast-store.ts`). This aligns with the existing Zustand pattern (`auth-store.ts`).

The system consists of:
- **`toast-store.ts`** (Zustand): queue state, `addToast`, `dismissToast`, `dismissAll` actions,
  page-visibility queue flush logic.
- **`ToastProvider.tsx`**: renders at most 3 simultaneous `Snackbar` + `Alert` instances stacked
  vertically at fixed top-right position. Reads from `toast-store`.
- **`useToast.ts`** hook: thin wrapper around `toast-store` actions for convenient imperative use
  by feature components. Exports `showSuccess(message)`, `showError(message, retry?)`,
  `showInfo(message)`, `showWarning(message)`.

All components use MUI primitives exclusively. No additional npm dependencies.

---

## Dependent Tasks

- `EP-009-I/us_036/task_002_fe_semantic_html_form_a11y.md` — `LiveRegion.tsx` and `aria-live`
  pattern established in US_036 should be the reference for accessible toast announcements.
  Error toasts must use `aria-live="assertive"`, success/info toasts must use `aria-live="polite"`.

---

## Impacted Components

| Component | Module | Action |
|-----------|--------|--------|
| `toast-store.ts` | `client/src/stores/` | CREATE — Zustand toast queue store |
| `ToastProvider.tsx` | `client/src/components/feedback/` | CREATE — renders stacked toasts |
| `useToast.ts` | `client/src/hooks/` | CREATE — imperative toast API hook |
| `App.tsx` | `client/src/` | MODIFY — add `<ToastProvider />` inside `ThemeProvider` |

---

## Implementation Plan

1. **Create `toast-store.ts`** (Zustand) — define `Toast` type:
   ```ts
   type ToastSeverity = 'success' | 'error' | 'info' | 'warning';
   interface Toast {
     id: string;            // nanoid(6) — avoids crypto import overhead
     message: string;
     severity: ToastSeverity;
     autoDismissMs: number | null;  // null = persist (used for error toasts)
     retryFn?: () => void;          // optional retry callback for error toasts
   }
   ```
   Store state: `queue: Toast[]` (all pending + visible toasts).
   Actions:
   - `addToast(toast: Omit<Toast, 'id'>)`: push to queue; if queue length > 10, drop oldest overflow
     (prevent unbounded growth — OWASP memory safety).
   - `dismissToast(id: string)`: remove by id.
   - `dismissAll()`: clear queue.

2. **Page Visibility API integration in `toast-store.ts`** — edge case from AC:
   - Add `paused: boolean` state (default false).
   - On store creation, bind `document.addEventListener('visibilitychange', ...)`.
   - When `document.hidden === true`: set `paused = true`.
   - When `document.hidden === false`: set `paused = false`; all queued toasts become visible
     (no additional flushing needed — `ToastProvider` reads `queue` directly).
   - Guard: only bind when `typeof document !== 'undefined'` (SSR-safe).

3. **Create `ToastProvider.tsx`** — renders stacked toast list:
   - Read `queue` from `toast-store`. Slice to first 3: `const visible = queue.slice(0, 3)`.
   - Render `visible.map((toast, index) =>` a positioned `<Snackbar>`:
     - `open={true}` (controlled by queue membership — present = open).
     - `anchorOrigin={{ vertical: 'top', horizontal: 'right' }}`.
     - `sx={{ top: `${80 + index * 72}px !important` }}` — stack below AppBar (80px) with 72px
       per-toast increment (64px toast height + 8px gap, matching `--spacing-1` gap).
     - Auto-dismiss: if `toast.autoDismissMs !== null`, pass `autoHideDuration={toast.autoDismissMs}`.
       On `onClose`, call `dismissToast(toast.id)`. Error toasts (`autoDismissMs = null`) omit
       `autoHideDuration` entirely.
   - Each `Snackbar` children: `<Alert severity={toast.severity} onClose={() => dismissToast(id)}`
     `role={toast.severity === 'error' ? 'alert' : 'status'}`:
     - Close `IconButton` always rendered (AC-2 "dismissible via close button").
     - If `toast.retryFn`: render `<Button size="small" onClick={toast.retryFn}>Retry</Button>` as
       `action` prop on `Alert` (AC-3).
   - `<ToastProvider />` is a React `Fragment` — no DOM wrapper element.

4. **Create `useToast.ts`** — exports convenience functions from the store:
   ```ts
   export function useToast() {
     const addToast = useToastStore((s) => s.addToast);
     return {
       showSuccess: (message: string) =>
         addToast({ message, severity: 'success', autoDismissMs: 5000 }),
       showError: (message: string, retryFn?: () => void) =>
         addToast({ message, severity: 'error', autoDismissMs: null, retryFn }),
       showInfo: (message: string) =>
         addToast({ message, severity: 'info', autoDismissMs: 5000 }),
       showWarning: (message: string) =>
         addToast({ message, severity: 'warning', autoDismissMs: 8000 }),
     };
   }
   ```

5. **Add `nanoid` as dependency** — `toast-store.ts` needs a UUID generator for toast IDs.
   Use `nanoid` (tiny, tree-shakeable, zero-dependency) with `nanoid(6)` for 6-char IDs:
   - `npm install nanoid` — add to `client/package.json` dependencies.
   - Import: `import { nanoid } from 'nanoid'`.
   - **Security note:** `nanoid` uses `crypto.getRandomValues` under the hood — cryptographically
     random, suitable for non-security-critical UI IDs. No UUID collision risk at queue depth ≤ 10.

6. **Register `ToastProvider` in `App.tsx`** — add `<ToastProvider />` as a sibling of
   `<RouterProvider>` inside `<QueryClientProvider>`. It must be inside `ThemeProvider` (needs
   theme for MUI `Alert` colours) but outside `RouterProvider` (toasts are app-global).

7. **Accessibility hardening in `ToastProvider.tsx`**:
   - Each `Alert` rendered with `role="alert"` for error severity (screen reader announces
     immediately — matches `aria-live="assertive"` behaviour).
   - Non-error `Alert` rendered with `role="status"` (`aria-live="polite"` behaviour).
   - Add `aria-atomic="true"` on the `Alert` to announce complete message on update.
   - Ensure close button has `aria-label="Dismiss notification"` on `IconButton`.

8. **OWASP A03 input guard in `addToast`** — sanitise `message` length:
   - Truncate messages > 200 characters to `message.slice(0, 197) + '...'` before storing.
   - Prevents unbounded string storage and oversized toast render. This is important because
     error messages can come from API responses and may contain unexpected content.

---

## Current Project State

```
client/src/
├── App.tsx                              ← MODIFY (add <ToastProvider />)
├── stores/
│   ├── auth-store.ts                    (EXISTS)
│   └── toast-store.ts                   ← CREATE
├── components/
│   └── feedback/                        ← CREATE (new folder)
│       └── ToastProvider.tsx
└── hooks/
    └── useToast.ts                      ← CREATE
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `client/src/stores/toast-store.ts` | Zustand queue store with `Toast` type, `addToast`/`dismissToast`/`dismissAll` actions, page visibility pause/resume, 200-char message guard, max 10 queue depth |
| CREATE | `client/src/components/feedback/ToastProvider.tsx` | Renders `queue.slice(0,3)` as stacked `Snackbar`+`Alert` at fixed top-right; auto-dismiss + manual close + Retry action; ARIA roles per severity |
| CREATE | `client/src/hooks/useToast.ts` | Imperative toast API: `showSuccess`, `showError`, `showInfo`, `showWarning` |
| MODIFY | `client/src/App.tsx` | Add `<ToastProvider />` as sibling of `<RouterProvider>` inside `<QueryClientProvider>` |
| MODIFY | `client/package.json` | Add `"nanoid": "^5.0.0"` to `dependencies` |

---

## External References

- [MUI Snackbar API — anchorOrigin, autoHideDuration, onClose (MUI v5)](https://mui.com/material-ui/react-snackbar/)
- [MUI Alert API — severity, action, onClose, role (MUI v5)](https://mui.com/material-ui/react-alert/)
- [MUI Snackbar — stacking multiple Snackbars (MUI v5 customization)](https://mui.com/material-ui/react-snackbar/#consecutive-snackbars)
- [Zustand — store creation with actions, selective subscriptions (v4)](https://docs.pmnd.rs/zustand/getting-started/introduction)
- [nanoid — tiny URL-safe ID generator (v5, ESM-compatible with Vite)](https://github.com/ai/nanoid)
- [Page Visibility API — visibilitychange event, document.hidden (MDN)](https://developer.mozilla.org/en-US/docs/Web/API/Page_Visibility_API)
- [WCAG 4.1.3 Status Messages — role="status" and role="alert" for live notifications](https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html)
- [OWASP A03 Injection — sanitise all externally-sourced strings before display](https://owasp.org/Top10/A03_2021-Injection/)

---

## Build Commands

```bash
cd client
npm install          # Install nanoid after adding to package.json
npm run dev          # Verify toast stacking at top-right, auto-dismiss timer, Retry button
npm run build        # Confirm no TypeScript errors (Zustand store types, Toast interface)
npm run lint         # Confirm no a11y warnings (role="alert"/role="status" on Alert)
```

---

## Implementation Validation Strategy

- [ ] Unit tests pass
- [ ] Integration tests pass (if applicable)
- [ ] **[UI Tasks]** Visual comparison against wireframe completed at 375px and 1440px
- [ ] **[UI Tasks]** Run `/analyze-ux` to validate wireframe alignment
- [ ] Success toast appears at top-right, auto-dismisses after exactly 5000ms
- [ ] Error toast persists until manually dismissed (no `autoHideDuration` set)
- [ ] Error toast with `retryFn` renders a "Retry" `Button` inside `Alert` action prop
- [ ] Maximum 3 toasts visible simultaneously when 4+ toasts are queued (4th remains in `queue` but not rendered)
- [ ] New toasts stack below existing ones (top: 80 + index × 72 px)
- [ ] Oldest toast auto-dismisses first (queue is FIFO — `slice(0, 3)` shows first 3, dismiss removes from front)
- [ ] Tab inactive → toast queue pauses; returning to tab → all queued toasts appear
- [ ] Error toast `Alert` has `role="alert"` (announced immediately by screen reader)
- [ ] Success/info toast `Alert` has `role="status"` (announced politely)
- [ ] `message` strings > 200 chars are truncated to 197 chars + `'...'` in `addToast`
- [ ] `nanoid` import resolves correctly in Vite build (no CommonJS/ESM conflict)

---

## Implementation Checklist

- [ ] **1.** Create `client/src/stores/toast-store.ts`: define `Toast` and `ToastSeverity` types; Zustand store with `queue: Toast[]`, `paused: boolean`; implement `addToast` (nanoid id, 200-char truncation, max 10 queue guard), `dismissToast`, `dismissAll`; bind `visibilitychange` listener in store initializer with SSR guard
- [ ] **2.** Add `"nanoid": "^5.0.0"` to `dependencies` in `client/package.json`; run `npm install` to update lockfile
- [ ] **3.** Create `client/src/components/feedback/ToastProvider.tsx`: read `queue` from store; render `queue.slice(0, 3)`; each `Snackbar` positioned `top: 80 + index * 72`px; `autoHideDuration` only for non-null `autoDismissMs`; `<Alert>` with `severity`, `role` (alert/status), `aria-atomic="true"`, close `IconButton` `aria-label="Dismiss notification"`, optional Retry `Button` in `action` prop
- [ ] **4.** Create `client/src/hooks/useToast.ts`: export `useToast()` returning `{ showSuccess, showError, showInfo, showWarning }` with correct `severity` and `autoDismissMs` per type (success=5000, error=null, info=5000, warning=8000)
- [ ] **5.** Modify `client/src/App.tsx`: import `ToastProvider`; add `<ToastProvider />` after `<RouterProvider router={router} />` and before closing `</QueryClientProvider>`
- [ ] **6.** Verify stacking offset: open 3 toasts in rapid succession in dev server; measure computed `top` values (80px, 152px, 224px) in Chrome DevTools → Elements → Computed
- [ ] **7.** Test page visibility queue: trigger 2 toasts, switch to another tab (toasts should pause/not auto-dismiss), return to tab — confirm toasts resume visibility
- [ ] **8.** Test OWASP guard: call `addToast({ message: 'a'.repeat(250), ... })` in browser console; confirm rendered toast message is truncated to 200 chars
- [ ] **[UI Tasks - MANDATORY]** Reference toast/alert colour and layout patterns from `wireframe-SCR-024-login.html` (Error state) during implementation
- [ ] **[UI Tasks - MANDATORY]** Validate toast UI matches wireframe Alert styles before marking task complete

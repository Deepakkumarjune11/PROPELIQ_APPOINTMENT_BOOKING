# Task - TASK_002: FE Error Boundary & Global Error Page

## Requirement Reference

- **User Story:** us_039 — Error Handling, Validation & Session Timeout UX
- **Story Location:** `.propel/context/tasks/EP-009-II/us_039/us_039.md`
- **Acceptance Criteria:**
  - AC-4: Given an unexpected error (500, network failure), When the error is caught, Then the system
    displays a user-friendly error page with "Try Again" and "Go to Dashboard" options and logs the
    technical details per UXR-503.
- **Edge Cases:**
  - File upload partial failure → error boundary catches render errors only; upload API errors are
    handled by the toast system (task_002 from US_038). This task covers component-tree crashes and
    unrecoverable route-level failures.

> ⚠️ **UXR-503 Definition Discrepancy (flag for BRD revision):**
> US_039 AC-4 references `UXR-503` for "unexpected error page with Try Again + Go to Dashboard".
> `figma_spec.md` defines `UXR-503` as: "System MUST handle network errors gracefully with retry
> options — Network error toast with 'Retry' button, offline state indicator." The figma_spec.md
> definition is narrower (network errors → toast) while US_039 AC-4 describes a full error page
> (any 500/crash). This task implements the US_039 AC-4 intent (error page + boundary). The
> figma_spec.md UXR-503 toast-on-network-error behaviour is handled by the toast system
> (`useToast().showError()` with retry). Recommend splitting UXR-503 into two UXRs in a future
> BRD revision: one for network-error toasts, one for unrecoverable error pages.

---

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | All wireframes in `.propel/context/wireframes/Hi-Fi/` (Error states defined per `figma_spec.md` — Default, Loading, Empty, **Error**, Validation); `wireframe-SCR-007-booking-error.html` provides the booking error page pattern for visual reference |
| **Screen Spec** | `figma_spec.md#SCR-007` (Booking Error — Error state with retry + alternative path); cross-cutting for all authenticated SCR-XXX |
| **UXR Requirements** | UXR-503 (see discrepancy note above), UXR-501 |
| **Design Tokens** | `designsystem.md#colors` (`--color-error-500` icon tint, `--color-neutral-700` body text), `designsystem.md#elevation` (`--elevation-1` for error card), `designsystem.md#spacing` (8px grid) |

> **Wireframe Implementation Requirement:**
> MUST reference the Error state pattern in `figma_spec.md` section 8 (Edge Cases — Error state
> handling) and `figma_spec.md#SCR-007` (Booking Error) for layout guidance: centered card,
> error icon, friendly heading, body text, two action buttons. Validate error page at 375px,
> 768px, 1440px.

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | React | 18.x |
| Frontend Framework | TypeScript | 5.x |
| UI Library | Material-UI (MUI) | 5.x |
| Routing | React Router | 6.x |
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

Implement two complementary error-catching layers:

1. **`AppErrorBoundary` (React class component)** — wraps the entire app component tree. Catches
   unhandled JavaScript errors in the render tree (`componentDidCatch`). Renders a friendly
   `GlobalErrorPage` fallback instead of a blank white screen. Logs technical details to
   `console.error` (Phase 1 — a real error monitoring service integration is deferred to
   infrastructure scope).

2. **`GlobalErrorPage`** — a pure UI component displaying: error icon, friendly heading
   ("Something went wrong"), body copy, and two CTA buttons: "Try Again" (`window.location.reload()`)
   and "Go to Dashboard" (`navigate('/')`). Used as both the `AppErrorBoundary` fallback and the
   React Router `errorElement` for route-level errors (404 + loader failures).

3. **React Router `errorElement` wiring in `App.tsx`** — assign `<GlobalErrorPage />` as the
   `errorElement` on the root route, ensuring loader failures and 404s render the same friendly
   page rather than React Router's default error boundary.

---

## Dependent Tasks

- `EP-009-II/us_039/task_001_fe_inline_validation_error_summary.md` — no strict code dependency,
  but should be implemented first so the error handling layer (this task) is layered above the
  inline validation layer (task_001) in the component hierarchy.

---

## Impacted Components

| Component | Module | Action |
|-----------|--------|--------|
| `AppErrorBoundary.tsx` | `client/src/components/errors/` | CREATE — React class component error boundary |
| `GlobalErrorPage.tsx` | `client/src/components/errors/` | CREATE — friendly error page UI |
| `App.tsx` | `client/src/` | MODIFY — wrap root with `AppErrorBoundary`; add `errorElement` to router config |

---

## Implementation Plan

1. **Create `GlobalErrorPage.tsx`** — functional component with props
   `{ title?: string; message?: string; onRetry?: () => void }`:
   - Centred `<Box>` (full-viewport, `minHeight: '100vh'`, `display: flex`, `flexDirection:
     'column'`, `alignItems: 'center'`, `justifyContent: 'center'`, `gap: 3`, `px: 3`).
   - MUI `ErrorOutline` icon from `@mui/icons-material/ErrorOutline` at 64px in `error.main` colour.
   - `<Typography variant="h5" gutterBottom>` — `title` prop or default `"Something went wrong"`.
   - `<Typography variant="body1" color="text.secondary" align="center" maxWidth={400}>` — `message`
     prop or default `"An unexpected error occurred. Please try again or return to the dashboard."`.
   - Two buttons in a `<Box sx={{ display: 'flex', gap: 2 }}>`:
     - `"Try Again"` — `variant="contained"` — calls `onRetry ?? (() => window.location.reload())`.
     - `"Go to Dashboard"` — `variant="outlined"` — uses `useNavigate()` to navigate to `'/'`.
   - **React Router compatibility**: when used as `errorElement`, wrap `useNavigate()` in a
     `try/catch` — if Router context is unavailable (boundary above router), fall back to
     `window.location.href = '/'`.

2. **Create `AppErrorBoundary.tsx`** — React class component (required for `componentDidCatch`):
   ```ts
   interface State { hasError: boolean; error?: Error }
   class AppErrorBoundary extends Component<{ children: ReactNode }, State> {
     state: State = { hasError: false };
     static getDerivedStateFromError(error: Error): State { return { hasError: true, error }; }
     componentDidCatch(error: Error, info: ErrorInfo) {
       // Phase 1: console logging only. Replace with monitoring service (Sentry, etc.) in future.
       console.error('[AppErrorBoundary] Unhandled render error:', error, info.componentStack);
     }
     render() {
       if (this.state.hasError) {
         return <GlobalErrorPage onRetry={() => this.setState({ hasError: false })} />;
       }
       return this.props.children;
     }
   }
   ```
   - `onRetry={() => this.setState({ hasError: false })}` resets boundary state, allowing
     React to attempt re-rendering the child tree (AC-4 "Try Again").
   - **Security note (OWASP A09 Logging)**: `error.message` and `info.componentStack` are logged
     to `console.error` only — never rendered in the UI. This prevents stack trace disclosure
     to end users (information exposure vulnerability).

3. **Wrap `App.tsx` with `AppErrorBoundary`** — in `App()` return value, wrap the
   `<ThemeProvider>` subtree:
   ```tsx
   return (
     <AppErrorBoundary>
       <ThemeProvider theme={healthcareTheme}>
         ...
       </ThemeProvider>
     </AppErrorBoundary>
   );
   ```
   `AppErrorBoundary` must be outermost so it catches errors in `ThemeProvider`, `QueryClientProvider`,
   and `RouterProvider` render trees.

4. **Add `errorElement` in `createBrowserRouter`** — assign `<GlobalErrorPage />` as the `errorElement`
   on the root route `'/'` and as a catch-all for the `'*'` path:
   ```ts
   { path: '/', element: <AuthenticatedLayout />, errorElement: <GlobalErrorPage />, children: [...] }
   ```
   React Router v6 `errorElement` catches loader/action errors and passes them via `useRouteError()`.
   `GlobalErrorPage` does not need to read `useRouteError()` for Phase 1 — the friendly generic
   message is sufficient.

5. **Differentiate 404 vs 500 in `GlobalErrorPage`** — add `useRouteError()` call (optional,
   only when inside React Router context):
   ```ts
   const routeError = (() => { try { return useRouteError(); } catch { return undefined; } })();
   const is404 = (routeError as { status?: number })?.status === 404;
   ```
   When `is404`: use title `"Page not found"` and message `"The page you're looking for doesn't
   exist."` with "Go to Dashboard" only (no "Try Again" for 404). This provides minimal
   differentiation without creating a separate 404 page component.

6. **Preserve `console.error` override guard** — React's test environment overrides `console.error`
   for error boundary tests. Do not suppress or replace `console.error` — use it as-is so test
   frameworks can capture error boundary activations.

7. **Accessibility on `GlobalErrorPage`** — add `role="main"` + `aria-labelledby="error-heading"`
   on the root `<Box>`; add `id="error-heading"` on the `<Typography variant="h5">`. This
   satisfies WCAG 1.3.1 Info and Relationships for the error page landmark structure.

---

## Current Project State

```
client/src/
├── App.tsx                         ← MODIFY (wrap with AppErrorBoundary, add errorElement)
└── components/
    └── errors/                     ← CREATE (new folder)
        ├── AppErrorBoundary.tsx
        └── GlobalErrorPage.tsx
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `client/src/components/errors/AppErrorBoundary.tsx` | React class error boundary with `getDerivedStateFromError` + `componentDidCatch` (console-only logging, no UI stack trace); onRetry resets `hasError` state |
| CREATE | `client/src/components/errors/GlobalErrorPage.tsx` | Friendly error page: MUI ErrorOutline icon (64px, error.main), heading, body, "Try Again" + "Go to Dashboard" buttons; 404 vs 500 differentiation via `useRouteError()` |
| MODIFY | `client/src/App.tsx` | Wrap root with `<AppErrorBoundary>`; add `errorElement: <GlobalErrorPage />` to root route and `*` catch-all in `createBrowserRouter` |

---

## External References

- [React — Error Boundaries (class component, getDerivedStateFromError, componentDidCatch)](https://react.dev/reference/react/Component#catching-rendering-errors-with-an-error-boundary)
- [React Router v6 — errorElement and useRouteError](https://reactrouter.com/en/6.28.0/route/error-element)
- [MUI Icons — ErrorOutline from @mui/icons-material](https://mui.com/material-ui/material-icons/?query=error)
- [MUI Typography, Button, Box — layout composition (MUI v5)](https://mui.com/material-ui/react-typography/)
- [WCAG 1.3.1 Info and Relationships — landmark roles for error pages](https://www.w3.org/WAI/WCAG22/Understanding/info-and-relationships.html)
- [OWASP A09 Security Logging — stack traces must not be rendered in UI](https://owasp.org/Top10/A09_2021-Security_Logging_and_Monitoring_Failures/)

---

## Build Commands

```bash
cd client
npm run dev     # Trigger error boundary by throwing in a child component temporarily
npm run build   # Confirm no TypeScript errors (class component lifecycle types)
npm run lint    # Confirm no unused imports or a11y warnings
```

---

## Implementation Validation Strategy

- [ ] Unit tests pass
- [ ] Integration tests pass (if applicable)
- [ ] **[UI Tasks]** Visual comparison against wireframe completed at 375px, 768px, 1440px
- [ ] **[UI Tasks]** Run `/analyze-ux` to validate wireframe alignment
- [ ] `AppErrorBoundary` catches a thrown error and renders `GlobalErrorPage` (test: temporarily throw in a child)
- [ ] `componentDidCatch` logs to `console.error` with error + componentStack — no error text rendered in UI (stack trace not visible)
- [ ] "Try Again" button resets `AppErrorBoundary` state (`hasError = false`) and re-renders child tree
- [ ] "Go to Dashboard" navigates to `'/'` (or falls back to `window.location.href = '/'` when outside Router context)
- [ ] React Router `errorElement` catches a 404 route and renders `GlobalErrorPage` with "Page not found" title
- [ ] `GlobalErrorPage` renders correct `role="main"` + `aria-labelledby` on root Box and `id` on heading
- [ ] Error icon colour is `error.main` (#F44336) from MUI theme palette
- [ ] `GlobalErrorPage` renders correctly without a React Router context (used as `AppErrorBoundary` fallback — Router may not be available)

---

## Implementation Checklist

- [ ] **1.** Create `client/src/components/errors/GlobalErrorPage.tsx`: centred full-viewport layout; `ErrorOutline` icon 64px `color="error"`; heading `variant="h5"` with `id="error-heading"`; body `variant="body1"`; two buttons ("Try Again" + "Go to Dashboard"); 404 detection via `useRouteError()` guarded in try/catch; `role="main"` + `aria-labelledby="error-heading"` on root `<Box>`
- [ ] **2.** Add 404 branch in `GlobalErrorPage`: when `useRouteError()?.status === 404`, render title `"Page not found"` + message `"The page you're looking for doesn't exist."` + only "Go to Dashboard" button (no Try Again)
- [ ] **3.** Create `client/src/components/errors/AppErrorBoundary.tsx`: class component with `State = { hasError: boolean; error?: Error }`; `getDerivedStateFromError` returns `{ hasError: true, error }`; `componentDidCatch` calls `console.error('[AppErrorBoundary]', error, info.componentStack)` only; render `GlobalErrorPage` when `hasError`; `onRetry` resets state
- [ ] **4.** Modify `client/src/App.tsx`: import `AppErrorBoundary`; wrap entire return with `<AppErrorBoundary>`; import `GlobalErrorPage`; add `errorElement: <GlobalErrorPage />` to root route object `{ path: '/', element: <AuthenticatedLayout />, errorElement: <GlobalErrorPage />, children: [...] }`; replace `{ path: '*', element: <Navigate to="/login" replace /> }` with `{ path: '*', element: <GlobalErrorPage /> }` (React Router will handle 404 via errorElement)
- [ ] **5.** Test error boundary activation: temporarily add `throw new Error('test')` inside `AuthenticatedLayout`, visit any route, confirm `GlobalErrorPage` renders with "Something went wrong" and two buttons; click "Try Again", confirm boundary resets; remove test throw
- [ ] **6.** Test 404 route: navigate to `/nonexistent`, confirm `GlobalErrorPage` renders with "Page not found" heading and only "Go to Dashboard" button
- [ ] **7.** Confirm OWASP A09: open browser DevTools Console after triggering error boundary; verify `console.error` logs appear; open DevTools Elements panel; verify no stack trace text in rendered DOM
- [ ] **[UI Tasks - MANDATORY]** Reference SCR-007 error state pattern (centred card, error icon, retry CTA) from `figma_spec.md#SCR-007` during implementation
- [ ] **[UI Tasks - MANDATORY]** Validate error page layout matches wireframe Error state before marking task complete

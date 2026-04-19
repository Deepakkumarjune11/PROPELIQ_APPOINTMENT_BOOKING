# Task - TASK_003: FE Session Timeout Modal

## Requirement Reference

- **User Story:** us_039 — Error Handling, Validation & Session Timeout UX
- **Story Location:** `.propel/context/tasks/EP-009-II/us_039/us_039.md`
- **Acceptance Criteria:**
  - AC-5: Given my session is about to expire, When 1 minute remains before timeout, Then a modal
    dialog appears with countdown timer, "Stay Logged In" button (extends session), and "Logout"
    button per UXR-504.
- **Edge Cases:** N/A specific to this task (general edge cases — in-flight mutation on timeout,
  simultaneous tabs — are handled in `EP-005/us_024/task_001_fe_login_session_guards.md`).

> ⚠️ **Pre-existing Design Overlap (coordination note):**
> `EP-005/us_024/task_001_fe_login_session_guards.md` already contains a `SessionTimeoutModal`
> design blueprint (Part D of that task, covering `useSessionTimeout` hook and
> `SessionTimeoutModal` component). This task (US_039) is the **canonical implementation task**
> for that design — it makes the component real as a cross-cutting UX pattern. The EP-005/us_024
> task provided the design intent; this task provides the implementation artefacts. Both tasks
> reference the same NFR-005 (15-minute session timeout) and UXR-504.

> ⚠️ **UXR-504 Consistency:** `figma_spec.md` UXR-504 definition aligns closely with US_039 AC-5:
> "System MUST warn users 1 minute before session timeout (15-minute inactivity) — Modal warning
> at 14-minute mark with 'Stay Logged In' button." This UXR is consistent — no discrepancy.

---

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | All wireframes in `.propel/context/wireframes/Hi-Fi/` (modal/overlay inventory: `figma_spec.md` lists "Session Timeout Warning Dialog" as P0 cross-cutting); no dedicated wireframe file — visual pattern derived from `wireframe-SCR-024-login.html` (MUI Dialog + Alert styling) |
| **Screen Spec** | `figma_spec.md#SCR-025` (cross-cutting modal on all authenticated screens); modal inventory entry: Session Timeout Warning Dialog, P0 |
| **UXR Requirements** | UXR-504 |
| **Design Tokens** | `designsystem.md#colors` (`--color-warning-500` countdown chip background), `designsystem.md#elevation` (`--elevation-16` for Dialog), `designsystem.md#transitions` (`--transition-standard` 300ms for modal open/close) |

> **Wireframe Implementation Requirement:**
> MUST reference MUI Dialog component styling from `wireframe-SCR-024-login.html` (modal pattern)
> and `design-tokens-applied.md` `--elevation-16` for Dialog shadow. Modal title "Session Expiring
> Soon", countdown chip, two action buttons. Validate modal display at 375px and 1440px.

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | React | 18.x |
| Frontend Framework | TypeScript | 5.x |
| UI Library | Material-UI (MUI) | 5.x |
| State Management | Zustand | 4.x |
| Data Fetching | React Query (@tanstack/react-query) | 4.x |
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

## Task Overview

Implement the session timeout warning system as a cross-cutting authenticated-layout concern:

1. **`useSessionTimeout` hook** — tracks inactivity since the last user activity event.
   Fires a callback at 14 minutes of inactivity (`WARNING_AT_MS = 14 * 60 * 1000`) and a
   second callback at 15 minutes (`TIMEOUT_AT_MS = 15 * 60 * 1000`) per NFR-005.
   Activity events reset the inactivity clock: `mousemove`, `keydown`, `pointerdown`, `scroll`.

2. **`SessionTimeoutModal` component** — MUI `Dialog` that renders when the 14-minute warning
   fires. Shows countdown (60 → 0 seconds), "Stay Logged In" and "Logout" buttons. On "Stay
   Logged In": calls `POST /api/v1/auth/refresh` via React Query mutation; on success resets
   the inactivity clock. On "Logout": calls `logout()` from `auth-store` and navigates to
   `/login`. On 15-minute expiry (modal ignored): same forced logout flow.

3. **Wire in `AuthenticatedLayout.tsx`** — `useSessionTimeout` is called once in the authenticated
   layout shell; `SessionTimeoutModal` is rendered there as a global sibling to `<Outlet />`.

---

## Dependent Tasks

- `EP-005/us_024/task_001_fe_login_session_guards.md` — `auth-store.ts` must have `logout()`
  action and `resetActivity()` action defined (per the design in that task). `useSessionTimeout`
  calls `resetActivity()` on user activity events and `logout()` on forced expiry.
- `EP-009-II/us_038/task_002_fe_toast_notification_system.md` — `useToast().showInfo()` is called
  after forced logout to display "Session expired. Please login again." toast (per `figma_spec.md`
  edge case description).

---

## Impacted Components

| Component | Module | Action |
|-----------|--------|--------|
| `useSessionTimeout.ts` | `client/src/hooks/` | CREATE — inactivity timer with warning/expiry callbacks |
| `SessionTimeoutModal.tsx` | `client/src/components/feedback/` | CREATE — MUI Dialog with countdown + two action buttons |
| `AuthenticatedLayout.tsx` | `client/src/components/layout/` | MODIFY — wire `useSessionTimeout`; render `<SessionTimeoutModal>` |

---

## Implementation Plan

1. **Define named constants** in `useSessionTimeout.ts`:
   ```ts
   const WARNING_AT_MS  = 14 * 60 * 1000;  // 14 minutes — show modal
   const TIMEOUT_AT_MS  = 15 * 60 * 1000;  // 15 minutes — force logout (NFR-005)
   const ACTIVITY_EVENTS = ['mousemove', 'keydown', 'pointerdown', 'scroll'] as const;
   ```
   No magic numbers in code body — all time constants named at module scope.

2. **`useSessionTimeout(onWarning, onExpired)` hook**:
   - Uses a single `setInterval` every 10 seconds (polling interval is coarse enough — no UX
     impact for 10s granularity against 15-minute threshold).
   - On mount: record `lastActivityRef = useRef(Date.now())`.
   - Bind all `ACTIVITY_EVENTS` to a single `resetActivity` handler:
     `lastActivityRef.current = Date.now()`. No state update — avoids re-render on every mouse move.
   - On each interval tick: compute `elapsed = Date.now() - lastActivityRef.current`.
     - `elapsed >= TIMEOUT_AT_MS && !expiredRef.current` → set `expiredRef.current = true`,
       call `onExpired()`.
     - `elapsed >= WARNING_AT_MS && !warningFiredRef.current` → set `warningFiredRef.current = true`,
       call `onWarning()`.
   - Refs `warningFiredRef` and `expiredRef` prevent duplicate callback fires per inactivity cycle.
   - `resetTimer()`: exposed function that resets `lastActivityRef` + clears both refs (called by
     "Stay Logged In" success path).
   - On unmount: clear interval + remove event listeners.

3. **`SessionTimeoutModal` component**:
   - Props: `open: boolean`, `onStayLoggedIn: () => void`, `onLogout: () => void`.
   - Internal countdown: `useEffect` with `setInterval(1000)` counting from 60 to 0 when `open`.
     Clears on `!open` or unmount.
   - MUI `Dialog` with `open` prop, `disableEscapeKeyDown` (force explicit action — no accidental
     dismiss), `maxWidth="sm"`, `fullWidth`.
   - `DialogTitle`: `"Session Expiring Soon"`.
   - `DialogContent`:
     - `<Typography variant="body1">` — "Your session will expire in"
     - `<Chip label={`${countdown}s`} color="warning" size="small" sx={{ mx: 1 }} />` — countdown chip.
     - `<Typography variant="body1" component="span">` — ". Stay logged in?"
   - `DialogActions`:
     - `<Button variant="outlined" onClick={onLogout}>Logout</Button>` (secondary action — left)
     - `<Button variant="contained" onClick={onStayLoggedIn} autoFocus>Stay Logged In</Button>`
       (`autoFocus` — WCAG 2.4.3: modal focus management, focus lands on primary action)
   - `aria-labelledby="session-timeout-title"` on `Dialog`; `id="session-timeout-title"` on
     `DialogTitle` content. `aria-describedby="session-timeout-desc"` on `Dialog`;
     `id="session-timeout-desc"` on `DialogContent` `Typography`.

4. **Wire in `AuthenticatedLayout.tsx`**:
   - Add state `const [showTimeoutModal, setShowTimeoutModal] = useState(false)`.
   - Call `useSessionTimeout(onWarning, onExpired)`:
     - `onWarning`: `setShowTimeoutModal(true)`.
     - `onExpired`: `logout(); navigate('/login'); showInfo('Session expired. Please login again.')`.
   - Render `<SessionTimeoutModal open={showTimeoutModal} onStayLoggedIn={handleStayLoggedIn}
     onLogout={handleLogout} />` after `<Outlet />`.
   - `handleStayLoggedIn`: call `POST /api/v1/auth/refresh` mutation (inline `useMutation` in
     layout or extract to `useRefreshToken` hook). On success: `resetTimer();
     setShowTimeoutModal(false)`. On error: forced logout.
   - `handleLogout`: `setShowTimeoutModal(false); logout(); navigate('/login')`.

5. **`POST /api/v1/auth/refresh` mutation** — create `useRefreshToken.ts` hook in `client/src/hooks/`:
   ```ts
   export function useRefreshToken() {
     const { setAuth } = useAuthStore();
     return useMutation({
       mutationFn: () => api.post<RefreshResponse>('/api/v1/auth/refresh').then(r => r.data),
       onSuccess: (data) => setAuth(data.user, data.token, data.expiresAt),
     });
   }
   ```
   **Security (OWASP A07 — Authentication Failures)**: the refresh endpoint must be called with
   the existing Bearer token in the `Authorization` header (handled by Axios interceptor in
   `client/src/lib/api.ts`). On 401 from refresh, the Axios interceptor triggers `logout()` —
   preventing an infinite refresh loop.

6. **Event listener performance guard** — `ACTIVITY_EVENTS` are bound on `document` with
   `{ passive: true }` option for scroll and pointerdown. Passive listeners do not block the main
   thread and ensure no scroll jank from the inactivity tracker.

7. **`disableEscapeKeyDown` rationale comment** — add an inline JSDoc comment explaining why ESC
   is disabled: "Modal requires explicit user choice (Stay Logged In or Logout) to prevent
   accidental session continuation after inactivity." This is both a UX decision and a
   security decision (ensures user acknowledges session state).

---

## Current Project State

```
client/src/
├── hooks/
│   ├── useSessionTimeout.ts       ← CREATE
│   └── useRefreshToken.ts         ← CREATE
├── components/
│   ├── layout/
│   │   └── AuthenticatedLayout.tsx  ← MODIFY
│   └── feedback/
│       ├── ToastProvider.tsx        (EXISTS from US_038 task_002)
│       ├── OperationProgressBar.tsx (EXISTS from US_038 task_003)
│       └── SessionTimeoutModal.tsx  ← CREATE
└── stores/
    └── auth-store.ts               (EXISTS — logout(), resetActivity() per EP-005/us_024)
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `client/src/hooks/useSessionTimeout.ts` | Inactivity timer: `WARNING_AT_MS`/`TIMEOUT_AT_MS` constants; interval-based elapsed check; activity event bindings (passive); `resetTimer()` exposed; `warningFiredRef`/`expiredRef` prevent duplicate fires |
| CREATE | `client/src/hooks/useRefreshToken.ts` | React Query `useMutation` for `POST /api/v1/auth/refresh`; calls `setAuth` on success; Axios interceptor handles 401 fallback |
| CREATE | `client/src/components/feedback/SessionTimeoutModal.tsx` | MUI `Dialog` with countdown chip; `disableEscapeKeyDown`; `autoFocus` on "Stay Logged In"; ARIA `aria-labelledby`/`aria-describedby` |
| MODIFY | `client/src/components/layout/AuthenticatedLayout.tsx` | Add `useSessionTimeout` call; add `showTimeoutModal` state; wire `handleStayLoggedIn`/`handleLogout`; render `<SessionTimeoutModal>` |

---

## External References

- [MUI Dialog API — open, disableEscapeKeyDown, aria-labelledby, DialogTitle, DialogActions (MUI v5)](https://mui.com/material-ui/react-dialog/)
- [MUI Chip API — label, color="warning", size (MUI v5)](https://mui.com/material-ui/react-chip/)
- [React Query — useMutation for token refresh (@tanstack/react-query v4)](https://tanstack.com/query/v4/docs/react/guides/mutations)
- [MDN — addEventListener passive option (performance for scroll/pointer)](https://developer.mozilla.org/en-US/docs/Web/API/EventTarget/addEventListener#passive)
- [NFR-005 — 15-minute session timeout requirement](d:\Propal IQ\Appontment Booking and Clinical Intell Platform\PROPELIQ_APPOINTMENT_BOOKING\.propel\context\docs\design.md)
- [WCAG 2.2.1 Timing Adjustable — session timeout warning requirement](https://www.w3.org/WAI/WCAG22/Understanding/timing-adjustable.html)
- [WCAG 2.4.3 Focus Order — modal focus management, autoFocus on primary action](https://www.w3.org/WAI/WCAG22/Understanding/focus-order.html)
- [OWASP A07 Authentication Failures — refresh token handling, 401 loop prevention](https://owasp.org/Top10/A07_2021-Identification_and_Authentication_Failures/)

---

## Build Commands

```bash
cd client
npm run dev     # Temporarily set WARNING_AT_MS = 5000 to test modal at 5s; verify countdown + buttons
npm run build   # Confirm no TypeScript errors on class/hook interaction
npm run lint    # Confirm no a11y warnings on Dialog ARIA attributes
```

---

## Implementation Validation Strategy

- [ ] Unit tests pass
- [ ] Integration tests pass (if applicable)
- [ ] **[UI Tasks]** Visual comparison against wireframe completed at 375px and 1440px
- [ ] **[UI Tasks]** Run `/analyze-ux` to validate wireframe alignment
- [ ] `useSessionTimeout` calls `onWarning` after `WARNING_AT_MS` of no activity (test with reduced constant)
- [ ] `useSessionTimeout` calls `onExpired` after `TIMEOUT_AT_MS` (not called a second time if already fired)
- [ ] `onWarning` and `onExpired` are not fired twice in a single inactivity cycle (refs prevent duplicates)
- [ ] `resetTimer()` resets the inactivity clock AND clears warning/expiry refs — modal does not re-appear until next inactivity period
- [ ] `SessionTimeoutModal` countdown starts at 60 and decrements each second when `open=true`
- [ ] Countdown stops and resets when `open=false`
- [ ] "Stay Logged In" calls refresh mutation; on success modal closes and inactivity clock resets
- [ ] "Logout" closes modal, clears `auth-store`, navigates to `/login`
- [ ] `Dialog` has `aria-labelledby` pointing to `DialogTitle` id; `aria-describedby` pointing to `DialogContent` id
- [ ] `autoFocus` is on "Stay Logged In" button (primary action receives focus on modal open)
- [ ] Activity events are bound with `{ passive: true }` (verify in Chrome DevTools → Performance → Event Listeners)

---

## Implementation Checklist

- [ ] **1.** Create `client/src/hooks/useSessionTimeout.ts`: define `WARNING_AT_MS = 14 * 60 * 1000`, `TIMEOUT_AT_MS = 15 * 60 * 1000`, `ACTIVITY_EVENTS` constant; use `useRef` for `lastActivityRef`, `warningFiredRef`, `expiredRef`; bind events with `{ passive: true }`; `setInterval` every 10s elapsed check; expose `resetTimer()`; clean up on unmount
- [ ] **2.** Create `client/src/hooks/useRefreshToken.ts`: `useMutation` for `POST /api/v1/auth/refresh`; `onSuccess` calls `setAuth(data.user, data.token, data.expiresAt)` from `auth-store`; no `onError` handler needed — Axios 401 interceptor handles forced logout
- [ ] **3.** Create `client/src/components/feedback/SessionTimeoutModal.tsx`: MUI `Dialog` with `open`, `disableEscapeKeyDown`, `maxWidth="sm"`, `aria-labelledby`/`aria-describedby`; `DialogTitle` with `id`; `DialogContent` with countdown `Chip color="warning"`; `DialogActions` with Logout (outlined) and Stay Logged In (contained, `autoFocus`)
- [ ] **4.** Add countdown `useEffect` in `SessionTimeoutModal`: `setInterval(1000)` counting `countdown` from 60 to 0; clear on `!open` or unmount; reset `countdown` to 60 on `open` becoming true
- [ ] **5.** Add `disableEscapeKeyDown` JSDoc comment in `SessionTimeoutModal.tsx` explaining the UX + security rationale
- [ ] **6.** Modify `AuthenticatedLayout.tsx`: add `showTimeoutModal` state; call `useSessionTimeout(onWarning, onExpired)` in component body; implement `handleStayLoggedIn` (calls `refreshToken.mutate()`, on success `resetTimer() + setShowTimeoutModal(false)`, on error `handleLogout()`); implement `handleLogout`; render `<SessionTimeoutModal>` after `<Outlet />`
- [ ] **7.** Test forced expiry path: set `TIMEOUT_AT_MS = 5000` temporarily; wait 5s without activity; confirm: modal appears at `WARNING_AT_MS`, forced logout fires at `TIMEOUT_AT_MS`, toast "Session expired. Please login again." shown, navigated to `/login`
- [ ] **[UI Tasks - MANDATORY]** Reference MUI Dialog modal pattern from `figma_spec.md` modal inventory (Session Timeout Warning Dialog, P0) during implementation
- [ ] **[UI Tasks - MANDATORY]** Validate modal layout matches MUI Dialog design token specifications before marking task complete

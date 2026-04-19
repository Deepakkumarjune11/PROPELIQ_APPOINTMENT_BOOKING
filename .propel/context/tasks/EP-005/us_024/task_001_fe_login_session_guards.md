# Task - task_001_fe_login_session_guards

## Requirement Reference

- **User Story**: US_024 — Role-Based Authentication & Session Management
- **Story Location**: `.propel/context/tasks/EP-005/us_024/us_024.md`
- **Acceptance Criteria**:
  - AC-1: Unauthenticated user accessing a protected route is redirected to `/login` (SCR-024) per FR-001.
  - AC-2: After successful login, user is redirected to the role-appropriate dashboard: patient → `/`, staff → `/staff/dashboard`, admin → `/admin/dashboard` per TR-010.
  - AC-3: Patient users attempting to access `/staff/*` or `/admin/*` routes see 403 UI state; role-based navigation items are hidden per NFR-004.
  - AC-4: At 14 minutes of inactivity, `SessionTimeoutModal` appears: "Your session will expire in 1 minute. Stay logged in?" with "Yes" and "Logout" buttons per AC-5 and UXR-504.
  - AC-5: At 15 minutes of inactivity (modal ignored), session terminates: token cleared, user navigated to `/login`, toast displayed: "Session expired. Please login again." per FR-017 and NFR-005.
- **Edge Cases**:
  - Form submitted (in-flight mutation) when session timeout fires → clear token, navigate to `/login`, toast displayed; in-flight request returns 401 which Axios interceptor catches and also triggers the same logout flow.
  - Staff clicks "Logout" in the modal → same logout flow as idle timeout (token cleared, navigate, no toast).
  - "Stay Logged In" clicked → `POST /api/v1/auth/refresh` called; on success `expiresAt` updated; on failure (401 from refresh) → force logout per NFR-005.
  - Simultaneous tabs: activity in one tab does NOT reset timer in other tabs (per-session independence per US_024 edge case).

---

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | `.propel/context/wireframes/Hi-Fi/wireframe-SCR-024-login.html` |
| **Screen Spec** | `.propel/context/docs/figma_spec.md#SCR-024` |
| **UXR Requirements** | UXR-502, UXR-504 |
| **Design Tokens** | `designsystem.md#colors` (primary: `#2196F3`, error: `#F44336`), `designsystem.md#typography` (Roboto, 8px grid) |

### CRITICAL: Wireframe Implementation Requirement

**Wireframe Status = AVAILABLE:**
- **MUST** open and reference wireframe file during implementation
- **SCR-024 key details** (`wireframe-SCR-024-login.html`):
  - Centred card layout: `max-width: 400px`, `border-radius: 8px`, `box-shadow: elevation-1`
  - `padding: 48px` desktop / `24px` mobile (≤ 600px)
  - Logo: `PropelIQ Healthcare` in `h4` (1.5rem, 500 weight) centered, `color: #2196F3`
  - Error Alert: `background #FFEBEE; border: 1px solid #EF5350; color: #C62828` (shown on auth failure)
  - Email + Password `TextField` with blur-trigger inline validation (UXR-502)
  - "Remember me" `Checkbox` + `FormControlLabel`
  - Login `Button`: full-width, uppercase, `#2196F3`; Loading state: spinner overlay, text transparent
  - `Link` "Forgot password?" centered below button (not in Phase 1 scope — render as disabled/no-op)
  - Navigation: login success → role-dependent redirect (not SCR-025 directly)
  - States: Default, Loading (button spinner), Error (Alert banner), Validation (field errors on blur)
- **`SessionTimeoutModal`** (global overlay — no dedicated wireframe):
  - MUI `Dialog` centered; title "Session Expiring Soon"; body text: "Your session will expire in 1 minute. Stay logged in?"
  - Countdown chip showing remaining seconds (e.g., "45s")
  - Two actions: "Logout" (outlined) + "Stay Logged In" (contained primary)
  - On mount: call `POST /api/v1/auth/refresh`; on success close modal and reset timer
- **MUST** validate at 375px, 768px, 1440px
- Run `/analyze-ux` after implementation to verify wireframe alignment

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | React | 18.x |
| UI Components | Material-UI (MUI) | 5.x |
| State Management | Zustand | 4.x |
| HTTP Client | Axios | 1.x |
| Data Fetching | React Query | 4.x |
| Routing | React Router | 6.x |
| Language | TypeScript | 5.x |
| Build Tool | Vite | 5.x |

> Axios instance (`src/lib/api.ts`) should include a 401 response interceptor that triggers the same `logout()` flow as session timeout — any expired token response is caught globally.

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

Wire the existing `LoginPage.tsx` shell to the real auth API, implement the session timeout modal and inactivity timer, and add role-based route guards to `App.tsx`.

### Part A — `auth-store.ts` (MODIFY)

Extend the existing Zustand store with token persistence and activity tracking:

```typescript
interface AuthState {
  user: UserProfile | null;
  token: string | null;
  expiresAt: number | null;   // Unix ms
  isAuthenticated: boolean;
  setAuth: (user: UserProfile, token: string, expiresAt: number) => void;
  logout: () => void;
  resetActivity: () => void;
  lastActivity: number;       // Unix ms — updated by useSessionTimeout
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      user: null, token: null, expiresAt: null, isAuthenticated: false, lastActivity: Date.now(),
      setAuth: (user, token, expiresAt) =>
        set({ user, token, expiresAt, isAuthenticated: true, lastActivity: Date.now() }),
      logout: () => set({ user: null, token: null, expiresAt: null, isAuthenticated: false }),
      resetActivity: () => set({ lastActivity: Date.now() }),
    }),
    { name: 'propeliq-auth', partialState: s => ({ token: s.token, user: s.user, expiresAt: s.expiresAt }) }
  )
);
```

> **Security note (OWASP A02)**: `token` is stored in `localStorage` via `persist`. This is acceptable for this architecture (no XSS risk beyond `httpOnly` cookie alternative) but the token must be short-lived (15 min per NFR-005) and Axios interceptor must clear it on 401.

### Part B — `useLogin.ts` (CREATE)

```typescript
export function useLogin() {
  const setAuth = useAuthStore(s => s.setAuth);
  const navigate = useNavigate();

  return useMutation({
    mutationFn: (credentials: LoginRequest) =>
      api.post<LoginResponse>('/api/v1/auth/login', credentials).then(r => r.data),
    onSuccess: (data) => {
      setAuth(data.user, data.token, data.expiresAt);
      const role = data.user.role;
      if (role === 'staff') navigate('/staff/dashboard', { replace: true });
      else if (role === 'admin') navigate('/admin/dashboard', { replace: true });
      else navigate('/', { replace: true });
    },
  });
}
```

### Part C — `LoginPage.tsx` (MODIFY — wire mutation + blur validation)

Replace the `setTimeout` placeholder with `useLogin()`. Trigger field validation `onBlur` (UXR-502 — not on every keystroke). Display `mutation.error` response as the alert banner.

```typescript
const { mutate: login, isPending, isError, error } = useLogin();

const handleSubmit = (e: React.FormEvent) => {
  e.preventDefault();
  const emailErr = validateEmail(email);
  const passErr = validatePassword(password);
  setEmailError(emailErr); setPasswordError(passErr);
  if (emailErr || passErr) return;
  login({ email, password, rememberMe });
};
// isPending drives the CircularProgress overlay on the Login button
// isError drives the Alert banner (error.response?.data?.message or fallback text)
```

### Part D — `useSessionTimeout.ts` (CREATE)

```typescript
const TIMEOUT_MS = 15 * 60 * 1000;   // 15 minutes (NFR-005)
const WARNING_MS = 1 * 60 * 1000;    // warn at 1 min remaining

export function useSessionTimeout() {
  const { isAuthenticated, lastActivity, logout, resetActivity } = useAuthStore();
  const navigate = useNavigate();
  const [showModal, setShowModal] = useState(false);
  const [countdown, setCountdown] = useState(60);

  useEffect(() => {
    if (!isAuthenticated) return;

    const events = ['mousemove', 'keydown', 'click', 'scroll', 'touchstart'];
    const handleActivity = () => { resetActivity(); setShowModal(false); };
    events.forEach(e => window.addEventListener(e, handleActivity, { passive: true }));

    const interval = setInterval(() => {
      const idle = Date.now() - lastActivity;
      const remaining = TIMEOUT_MS - idle;

      if (remaining <= 0) {
        logout();
        navigate('/login', { replace: true });
        // toast is shown in the navigate destination via location.state or global snackbar
        return;
      }
      if (remaining <= WARNING_MS && !showModal) {
        setShowModal(true);
        setCountdown(Math.ceil(remaining / 1000));
      }
      if (showModal) {
        setCountdown(Math.max(0, Math.ceil(remaining / 1000)));
      }
    }, 1_000);

    return () => {
      events.forEach(e => window.removeEventListener(e, handleActivity));
      clearInterval(interval);
    };
  }, [isAuthenticated, lastActivity, showModal]);

  return { showModal, setShowModal, countdown };
}
```

### Part E — `SessionTimeoutModal.tsx` (CREATE)

```typescript
interface Props { open: boolean; countdown: number; onClose: () => void; }

export const SessionTimeoutModal: React.FC<Props> = ({ open, countdown, onClose }) => {
  const { logout, token } = useAuthStore();
  const navigate = useNavigate();
  const { mutate: refresh, isPending } = useMutation({
    mutationFn: () => api.post<{ token: string; expiresAt: number }>('/api/v1/auth/refresh').then(r => r.data),
    onSuccess: (data) => {
      useAuthStore.getState().setAuth(useAuthStore.getState().user!, data.token, data.expiresAt);
      onClose();
    },
    onError: () => { logout(); navigate('/login', { replace: true }); },
  });

  return (
    <Dialog open={open} onClose={() => {}} disableEscapeKeyDown maxWidth="xs" fullWidth>
      <DialogTitle>Session Expiring Soon</DialogTitle>
      <DialogContent>
        <Typography>Your session will expire in 1 minute. Stay logged in?</Typography>
        <Chip label={`${countdown}s`} color="warning" sx={{ mt: 2 }} />
      </DialogContent>
      <DialogActions>
        <Button variant="outlined" onClick={() => { logout(); navigate('/login', { replace: true }); }}>
          Logout
        </Button>
        <Button variant="contained" onClick={() => refresh()} disabled={isPending}>
          Stay Logged In
        </Button>
      </DialogActions>
    </Dialog>
  );
};
```

### Part F — `RoleGuard.tsx` (CREATE)

```typescript
interface Props { roles: Array<'patient' | 'staff' | 'admin'>; }

export const RoleGuard: React.FC<Props> = ({ roles }) => {
  const { user, isAuthenticated } = useAuthStore();
  if (!isAuthenticated) return <Navigate to="/login" replace />;
  if (!roles.includes(user!.role)) {
    return (
      <Box display="flex" flexDirection="column" alignItems="center" justifyContent="center" minHeight="60vh">
        <Typography variant="h5" color="error" gutterBottom>403 — Access Denied</Typography>
        <Typography color="text.secondary">You do not have permission to view this page.</Typography>
        <Button sx={{ mt: 2 }} variant="contained" onClick={() => window.history.back()}>Go Back</Button>
      </Box>
    );
  }
  return <Outlet />;
};
```

### Part G — `AuthenticatedLayout.tsx` (MODIFY — add SessionTimeoutModal)

```typescript
// Add to AuthenticatedLayout after the existing layout Box:
const { showModal, setShowModal, countdown } = useSessionTimeout();
// ... return (
//   <Box ...>
//     ...existing content...
//     <SessionTimeoutModal open={showModal} countdown={countdown} onClose={() => setShowModal(false)} />
//   </Box>
// )
```

### Part H — `App.tsx` (MODIFY — add role-specific route groups)

```typescript
// Add route groups inside the authenticated layout children:
{
  path: '/',
  element: <AuthenticatedLayout />,
  children: [
    // Patient routes
    { element: <RoleGuard roles={['patient']} />, children: [
      { index: true, element: <PatientDashboard /> }, // placeholder
    ]},
    // Staff routes
    { path: 'staff', element: <RoleGuard roles={['staff', 'admin']} />, children: [
      { path: 'dashboard', element: <StaffDashboard /> }, // placeholder
    ]},
    // Admin routes
    { path: 'admin', element: <RoleGuard roles={['admin']} />, children: [
      { path: 'dashboard', element: <AdminDashboard /> }, // placeholder
    ]},
  ],
}
// Add Axios 401 interceptor registration in App.tsx or api.ts:
// api.interceptors.response.use(undefined, (error) => {
//   if (error.response?.status === 401) {
//     useAuthStore.getState().logout();
//   }
//   return Promise.reject(error);
// });
```

---

## Dependent Tasks

- **task_002_be_auth_api.md** (US_024) — `POST /api/v1/auth/login` and `POST /api/v1/auth/refresh` endpoints must exist.
- No other US dependencies — this is foundational; route guards added here are used by all subsequent staff/admin tasks.

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | `client/src/stores/auth-store.ts` | Add `token`, `expiresAt`, `lastActivity` fields; `setAuth()`, `resetActivity()` actions; `persist` middleware |
| MODIFY | `client/src/pages/LoginPage.tsx` | Replace setTimeout placeholder with `useLogin()` mutation; blur validation; role-based redirect on success |
| CREATE | `client/src/hooks/useLogin.ts` | `useMutation → POST /api/v1/auth/login`; stores token via `setAuth`; role-based `navigate` |
| CREATE | `client/src/hooks/useSessionTimeout.ts` | Activity listeners; 15-min inactivity timer; opens modal at t-60s; auto-logout + navigate at t=0 |
| CREATE | `client/src/components/auth/SessionTimeoutModal.tsx` | MUI Dialog: countdown chip, "Stay Logged In" (calls refresh), "Logout" |
| CREATE | `client/src/components/auth/RoleGuard.tsx` | `<RoleGuard roles={[...]}>` wrapping `<Outlet>`; 403 state for unauthorized roles |
| MODIFY | `client/src/components/layout/AuthenticatedLayout.tsx` | Integrate `useSessionTimeout` + `<SessionTimeoutModal>` |
| MODIFY | `client/src/App.tsx` | Role-grouped routes with `<RoleGuard>`; Axios 401 interceptor registration |

---

## Current Project State

```
client/src/
  App.tsx                                               ← MODIFY — role route groups + 401 interceptor
  stores/
    auth-store.ts                                       ← MODIFY — token, expiresAt, lastActivity, setAuth, persist
  pages/
    LoginPage.tsx                                       ← MODIFY — wire useLogin mutation (replace setTimeout)
  hooks/
    useLogin.ts                                         ← THIS TASK (create)
    useSessionTimeout.ts                                ← THIS TASK (create)
  components/
    auth/
      SessionTimeoutModal.tsx                           ← THIS TASK (create)
      RoleGuard.tsx                                     ← THIS TASK (create)
    layout/
      AuthenticatedLayout.tsx                           ← MODIFY — add SessionTimeoutModal
      Header.tsx                                        ← existing (no change)
      Sidebar.tsx                                       ← existing (no change)
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | `client/src/stores/auth-store.ts` | Token + activity tracking; `setAuth`; `persist` with `localStorage` |
| MODIFY | `client/src/pages/LoginPage.tsx` | Replace placeholder with `useLogin` mutation; blur-triggered validation |
| CREATE | `client/src/hooks/useLogin.ts` | Mutation wrapper for `/api/v1/auth/login` |
| CREATE | `client/src/hooks/useSessionTimeout.ts` | Inactivity timer: 14-min warning, 15-min auto-logout |
| CREATE | `client/src/components/auth/SessionTimeoutModal.tsx` | Countdown dialog with "Stay Logged In" + refresh call |
| CREATE | `client/src/components/auth/RoleGuard.tsx` | Role-based route guard wrapping `<Outlet>` |
| MODIFY | `client/src/components/layout/AuthenticatedLayout.tsx` | Add `<SessionTimeoutModal>` integrated via `useSessionTimeout` |
| MODIFY | `client/src/App.tsx` | Patient / staff / admin route groups + Axios 401 interceptor |

---

## External References

- [MUI 5 — `Dialog` for modal patterns](https://mui.com/material-ui/react-dialog/)
- [React Router 6 — nested routes with `<Outlet>` for role guards](https://reactrouter.com/en/main/components/outlet)
- [Zustand 4 — `persist` middleware with `localStorage`](https://docs.pmnd.rs/zustand/integrations/persisting-store-data)
- [React Query 4 — `useMutation` with `onSuccess` / `onError` callbacks](https://tanstack.com/query/v4/docs/react/reference/useMutation)
- [Axios 1.x — response interceptors for 401 handling](https://axios-http.com/docs/interceptors)
- [NFR-004 — RBAC strict separation patient/staff/admin](../.propel/context/docs/design.md)
- [NFR-005 — 15-minute session timeout](../.propel/context/docs/design.md)
- [FR-017 — secure session handling and inactivity enforcement](../.propel/context/docs/spec.md)
- [UXR-504 — warn 1 minute before session timeout](../.propel/context/docs/figma_spec.md)
- [figma_spec.md#SCR-024 — Login screen spec: 5 states, component inventory](../.propel/context/docs/figma_spec.md)

---

## Build Commands

```bash
cd client
npm install
npm run build
npm run type-check
```

---

## Implementation Validation Strategy

- [ ] Unit test: `useSessionTimeout` — after 14 min of simulated idle, `showModal = true`; after 15 min, `logout()` called and navigate to `/login`
- [ ] Unit test: `RoleGuard` — renders `<Outlet>` when role matches; renders 403 state when role does not match; redirects to `/login` when unauthenticated
- [ ] Unit test: `useLogin` `onSuccess` — navigates to `/staff/dashboard` for `role = 'staff'`, `/admin/dashboard` for `role = 'admin'`, `/` for `role = 'patient'`
- [ ] **[UI Tasks - MANDATORY]** Visual comparison against `wireframe-SCR-024-login.html` at 375px, 768px, 1440px; Loading state spinner; Error Alert banner; blur validation messages
- [ ] **[UI Tasks - MANDATORY]** Run `/analyze-ux` to verify wireframe alignment for SCR-024
- [ ] Axios 401 interceptor fires `logout()` when any API call returns 401 (simulated with MSW)
- [ ] "Stay Logged In" in modal calls `POST /api/v1/auth/refresh`; on failure, auto-logout fires

---

## Implementation Checklist

- [ ] Modify `auth-store.ts`: add `token`, `expiresAt`, `lastActivity` to state; replace `login()` with `setAuth(user, token, expiresAt)`; add `resetActivity()`; wrap store in `persist({ name: 'propeliq-auth' })` storing only `token`, `user`, `expiresAt`
- [ ] Modify `LoginPage.tsx`: import `useLogin`; replace `setTimeout` block with `const { mutate: login, isPending, isError, error } = useLogin()`; trigger `login({ email, password, rememberMe })` on submit; drive loading spinner with `isPending`; drive error `Alert` with `isError`; trigger field validation `onBlur`
- [ ] Create `useLogin.ts`: `useMutation` → `POST /api/v1/auth/login`; on success call `setAuth` and `navigate` by role; on error propagate to component
- [ ] Create `useSessionTimeout.ts`: activity event listeners (mousemove, keydown, click, scroll, touchstart) reset `lastActivity` via `resetActivity()`; 1-second `setInterval` checks idle duration; opens modal at `remaining ≤ 60s`; calls `logout()` + `navigate('/login')` at `remaining ≤ 0`
- [ ] Create `SessionTimeoutModal.tsx`: MUI `Dialog` with countdown `Chip`; "Stay Logged In" calls `POST /api/v1/auth/refresh` via `useMutation`; success → `setAuth` with new token + close modal; failure → `logout()` + navigate; "Logout" button → same logout flow
- [ ] Create `RoleGuard.tsx`: reads `isAuthenticated` + `user.role` from `useAuthStore`; redirects to `/login` if not authenticated; renders 403 state if role not in `roles` prop; renders `<Outlet>` if authorized
- [ ] Modify `AuthenticatedLayout.tsx`: call `useSessionTimeout()`; render `<SessionTimeoutModal>` at root of layout JSX
- [ ] **[UI Tasks - MANDATORY]** Reference `wireframe-SCR-024-login.html` from Design References table during implementation
- [ ] **[UI Tasks - MANDATORY]** Validate UI matches wireframe before marking task complete

# Task - TASK_002: FE Guided Onboarding Tour

## Requirement Reference

- **User Story:** us_040 — Navigation Optimization, Guidance & Semantic Colors
- **Story Location:** `.propel/context/tasks/EP-009-II/us_040/us_040.md`
- **Acceptance Criteria:**
  - AC-2: Given I am a first-time user, When I log in for the first time, Then a guided onboarding
    tour highlights key features (appointment booking, document upload, patient profile) with
    dismissible tooltip overlays per UXR-003.
- **Edge Cases:**
  - Returning users who missed steps → Onboarding can be re-triggered from a "Help" menu;
    individual tooltips can be replayed (per AC edge case definition).

> ⚠️ **UXR-003 Scope Discrepancy (flag for BRD revision):**
> `figma_spec.md` defines `UXR-003` as: "System MUST provide inline guidance for complex workflows
> (conversational intake, conflict resolution, code verification) — Help text/tooltips present,
> user task completion rate > 90% — SCR-004, SCR-005, SCR-009, SCR-017, SCR-018, SCR-019."
> This definition is scoped to specific complex workflow screens, not a general first-time
> onboarding tour. US_040 AC-2 repurposes UXR-003 for a global platform onboarding tour
> (post-login welcome tour). The `figma_spec.md` "Data & Edge Cases" section (section 8)
> describes: "First Use — New patient onboarding after inline account creation — Welcome tooltip
> on dashboard, optional onboarding tour." This pattern has no dedicated UXR ID in the current
> spec. This task implements the AC-2 intent (first-time guided tour). Recommend adding a
> dedicated UXR (e.g., UXR-004) for the onboarding tour and limiting UXR-003 to complex
> workflow inline guidance in a future BRD revision.

---

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | All wireframes in `.propel/context/wireframes/Hi-Fi/`; onboarding pattern described in `figma_spec.md` section 8 (Data Scenarios: "First Use — Welcome tooltip on dashboard, optional onboarding tour"); component inventory includes C/Feedback/Tooltip (Dark background) from `designsystem.md` |
| **Screen Spec** | Post-login dashboard (SCR-010 for Staff, SCR-001 for Patient, SCR-021 for Admin) — tour highlights differ by user role |
| **UXR Requirements** | UXR-003 (inline guidance — see discrepancy note above) |
| **Design Tokens** | `designsystem.md#colors` (`info.main` `#2196F3` tooltip background per `affected_components: ["Tooltip"]`), `designsystem.md#elevation` (`--elevation-16` / z-index 1500 for tooltip overlay per `designsystem.md` z-index section), `designsystem.md#transitions` (Tooltip fade 150ms easeInOut) |

> **Wireframe Implementation Requirement:**
> MUST reference `designsystem.md` Tooltip component spec (Dark background, default variant)
> and `figma_spec.md` section 8 "First Use" edge case for tour content. Tour tooltip should
> align with MUI `Tooltip` component positioned with `placement="bottom"` or `"right"` depending
> on target element. Validate tour display at 375px, 768px, 1440px — tour must be functional on
> all breakpoints (mobile uses bottom nav targets, desktop uses sidebar targets).

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | React | 18.x |
| Frontend Framework | TypeScript | 5.x |
| UI Library | Material-UI (MUI) | 5.x |
| State Management | Zustand | 4.x |
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

Implement a lightweight, custom onboarding tour using MUI `Tooltip` + `Popover` overlays.
No third-party tour library is introduced (YAGNI — avoids adding an unversioned dependency
for a one-time UX pattern). The tour consists of ordered steps, each highlighting a specific
DOM element by `id`. A Zustand `onboarding-store` persists tour state to `localStorage` via
Zustand persist middleware, enabling:
- First-time detection (`hasCompletedOnboarding: false` on initial load)
- Per-step dismissal (stores `completedSteps: string[]`)
- Re-trigger from Help menu (exposed `resetOnboarding()` action)

---

## Dependent Tasks

- `EP-009-I/us_037/task_001_fe_responsive_navigation_shell.md` — `AuthenticatedLayout` with
  Sidebar and BottomNav must be in place as tour targets (`#nav-book`, `#nav-documents`,
  `#nav-profile` element IDs).

---

## Impacted Components

| Component | Module | Action |
|-----------|--------|--------|
| `onboarding-store.ts` | `client/src/stores/` | CREATE — Zustand store with persist; `hasCompletedOnboarding`, `currentStep`, `startTour()`, `nextStep()`, `skipTour()`, `resetOnboarding()` |
| `OnboardingTour.tsx` | `client/src/components/onboarding/` | CREATE — tour orchestrator; renders active step tooltip overlay |
| `TourStepTooltip.tsx` | `client/src/components/onboarding/` | CREATE — single step Tooltip/Popover with title, body, step counter, "Next", "Skip" buttons |
| `AuthenticatedLayout.tsx` | `client/src/components/layout/` | MODIFY — render `<OnboardingTour />`; call `startTour()` on first login |

---

## Implementation Plan

1. **Create `onboarding-store.ts`** using Zustand with persist middleware:
   ```ts
   interface OnboardingState {
     hasCompletedOnboarding: boolean;
     currentStep: number;           // -1 = tour not active
     startTour: () => void;
     nextStep: () => void;
     prevStep: () => void;
     skipTour: () => void;
     resetOnboarding: () => void;   // re-trigger from Help menu (edge case)
   }
   ```
   - `persist` key: `'propeliq-onboarding'` in localStorage.
   - `startTour()`: sets `currentStep = 0` only if `!hasCompletedOnboarding`.
   - `skipTour()`: sets `hasCompletedOnboarding = true`, `currentStep = -1`.
   - `nextStep()`: increments `currentStep`; when step exceeds last step index, calls `skipTour()`.
   - `resetOnboarding()`: sets `hasCompletedOnboarding = false`, `currentStep = -1` — allows
     re-trigger from Help menu.
   - **Security note**: localStorage persist stores only a boolean flag and step number — no PII,
     no auth tokens. Safe for localStorage use (OWASP A02 does not apply here).

2. **Define tour steps** as a static constant in `OnboardingTour.tsx`:
   ```ts
   const TOUR_STEPS: TourStep[] = [
     { id: 'book-appointment', targetId: 'nav-book',
       title: 'Book an Appointment',
       body: 'Search available slots and book your appointment in just a few clicks.',
       placement: 'right' },
     { id: 'upload-document', targetId: 'nav-documents',
       title: 'Upload Documents',
       body: 'Upload insurance cards, referrals, and medical records securely.',
       placement: 'right' },
     { id: 'patient-profile', targetId: 'nav-profile',
       title: 'Your Profile',
       body: 'View and update your personal details and appointment history.',
       placement: 'right' },
   ];
   ```
   The `targetId` maps to the `id` attribute on the sidebar nav item or bottom nav action
   (e.g., `id="nav-book"` on the booking navigation link). These IDs must be added to
   `Sidebar.tsx` and `BottomNav.tsx` as part of this task.

3. **Create `TourStepTooltip.tsx`** — renders as a MUI `Popover` (not `Tooltip`) anchored to
   the target element:
   - `anchorEl`: `document.getElementById(step.targetId)` — computed via `useEffect` on step change.
   - `open={currentStep === stepIndex}`.
   - Content: step counter chip (`{currentStep + 1}/{TOUR_STEPS.length}`), bold title `Typography
     variant="subtitle2"`, body `Typography variant="body2"`, two action buttons: "Skip Tour"
     (text secondary) and "Next →" (contained primary; last step shows "Done ✓").
   - Applies a semi-transparent `backdrop` (MUI `Backdrop` with `sx={{ zIndex: 1400 }}`) to focus
     attention on the highlighted element.
   - **Why `Popover` not `Tooltip`**: `Tooltip` is hover-activated only; `Popover` supports
     programmatic `open` control with proper focus management and `aria-describedby` support.
   - Accessibility: `aria-live="polite"` on Popover content so screen readers announce step
     content on change; "Skip Tour" button has `aria-label="Skip onboarding tour"`.
   - Reduced motion: when `prefers-reduced-motion: reduce`, set `transitionDuration={0}` on
     Popover (consistent with `healthcare-theme.ts` motion guard from US_036).

4. **Create `OnboardingTour.tsx`** — orchestrator component:
   - Reads `currentStep` from `useOnboardingStore()`.
   - Returns `null` when `currentStep === -1` (tour inactive).
   - Renders `<TourStepTooltip>` for the active step + `<Backdrop>`.
   - Effect: `useEffect(() => { if (!hasCompletedOnboarding && isAuthenticated) startTour(); }, [isAuthenticated])`.
   - `isAuthenticated` comes from `useAuthStore()` — tour only fires when user is logged in.

5. **Wire into `AuthenticatedLayout.tsx`**:
   - Import `OnboardingTour` and render it after `<SessionTimeoutModal />` (bottom of layout return).
   - The `OnboardingTour` self-activates via its internal `useEffect` — no prop drilling needed.

6. **Add `id` attributes to tour target elements**:
   - In `Sidebar.tsx`: add `id="nav-book"` to the booking `ListItemButton`, `id="nav-documents"`
     to documents link, `id="nav-profile"` to profile link.
   - In `BottomNav.tsx`: add `id="nav-book-mobile"` etc. with `targetId` fallback logic in
     `OnboardingTour.tsx` — check mobile IDs when desktop IDs are not found in DOM:
     ```ts
     const el = document.getElementById(step.targetId) ??
                document.getElementById(step.mobileTargetId ?? '');
     ```

7. **Help menu re-trigger** (edge case) — add a "Restart Tour" `MenuItem` to the Header's
   avatar dropdown (or `BottomNav` overflow menu) that calls `resetOnboarding()` followed by
   `startTour()`. This satisfies the "re-triggered from Help menu" edge case without requiring
   a dedicated Help page.

---

## Current Project State

```
client/src/
├── stores/
│   ├── auth-store.ts               (EXISTS)
│   ├── toast-store.ts              (EXISTS from US_038)
│   └── onboarding-store.ts         ← CREATE
└── components/
    ├── layout/
    │   ├── AuthenticatedLayout.tsx ← MODIFY
    │   ├── Sidebar.tsx             ← MODIFY (add id attrs to nav items)
    │   ├── Header.tsx              ← MODIFY (add "Restart Tour" to avatar dropdown)
    │   └── BottomNav.tsx           ← MODIFY (add id attrs to bottom nav actions)
    └── onboarding/                 ← CREATE (new folder)
        ├── OnboardingTour.tsx
        └── TourStepTooltip.tsx
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `client/src/stores/onboarding-store.ts` | Zustand persist store; `hasCompletedOnboarding`, `currentStep`, `startTour/nextStep/skipTour/resetOnboarding` actions; localStorage key `propeliq-onboarding` |
| CREATE | `client/src/components/onboarding/TourStepTooltip.tsx` | MUI `Popover` anchored to target element; step counter chip; Next/Skip buttons; `aria-live="polite"`; reduced-motion guard |
| CREATE | `client/src/components/onboarding/OnboardingTour.tsx` | Tour orchestrator; `TOUR_STEPS` constant; renders active `TourStepTooltip` + `Backdrop`; auto-starts on first auth |
| MODIFY | `client/src/components/layout/AuthenticatedLayout.tsx` | Render `<OnboardingTour />` at bottom of layout |
| MODIFY | `client/src/components/layout/Sidebar.tsx` | Add `id` props to booking, documents, profile `ListItemButton` elements |
| MODIFY | `client/src/components/layout/BottomNav.tsx` | Add `id` props to booking, documents, profile `BottomNavigationAction` elements |
| MODIFY | `client/src/components/layout/Header.tsx` | Add "Restart Tour" `MenuItem` in avatar dropdown calling `resetOnboarding() + startTour()` |

---

## External References

- [MUI Popover API — open, anchorEl, placement, transitionDuration (MUI v5)](https://mui.com/material-ui/react-popover/)
- [MUI Backdrop API — sx zIndex for overlay effect (MUI v5)](https://mui.com/material-ui/react-backdrop/)
- [Zustand Persist Middleware — localStorage state persistence (Zustand 4.x)](https://docs.pmnd.rs/zustand/integrations/persisting-store-data)
- [MUI Chip API — step counter styling (MUI v5)](https://mui.com/material-ui/react-chip/)
- [WCAG 3.2.4 Consistent Identification — onboarding elements identified consistently](https://www.w3.org/WAI/WCAG22/Understanding/consistent-identification.html)
- [WAI-ARIA — aria-live polite for tour step announcements](https://www.w3.org/TR/wai-aria/#aria-live)
- [figma_spec.md section 8 — "First Use" edge case, onboarding tour description](d:\Propal IQ\Appontment Booking and Clinical Intell Platform\PROPELIQ_APPOINTMENT_BOOKING\.propel\context\docs\figma_spec.md)

---

## Build Commands

```bash
cd client
npm run dev     # Clear localStorage propeliq-onboarding; login; verify tour auto-starts
npm run build   # Confirm no TypeScript errors on Zustand persist generic typing
npm run lint    # Confirm no a11y warnings on Popover content structure
```

---

## Implementation Validation Strategy

- [ ] Unit tests pass
- [ ] Integration tests pass (if applicable)
- [ ] **[UI Tasks]** Visual comparison against wireframe completed at 375px, 768px, 1440px
- [ ] **[UI Tasks]** Run `/analyze-ux` to validate wireframe alignment
- [ ] First login (clear localStorage): tour auto-starts at step 1 after authenticated layout renders
- [ ] "Next →" advances tour to next step; Popover repositions to new target element
- [ ] "Skip Tour" sets `hasCompletedOnboarding = true`; tour does not appear on next login
- [ ] "Done ✓" on last step sets `hasCompletedOnboarding = true`; tour ends
- [ ] Refreshing page mid-tour: Zustand persist restores `currentStep`, tour resumes at correct step
- [ ] "Restart Tour" in Header avatar dropdown resets state and restarts tour from step 1
- [ ] `aria-live="polite"` on Popover content — screen reader announces new step on advance
- [ ] Tour Popover renders correctly on mobile (targets `#nav-book-mobile` in BottomNav)
- [ ] Tour does NOT start if `hasCompletedOnboarding === true` in localStorage

---

## Implementation Checklist

- [ ] **1.** Create `client/src/stores/onboarding-store.ts`: Zustand persist with key `'propeliq-onboarding'`; state: `hasCompletedOnboarding: boolean`, `currentStep: number`; actions: `startTour`, `nextStep`, `skipTour`, `resetOnboarding`; `startTour` guard: only activates when `!hasCompletedOnboarding`
- [ ] **2.** Define `TOUR_STEPS` constant in `OnboardingTour.tsx`: 3 steps (book-appointment, upload-document, patient-profile) with `targetId`, `mobileTargetId`, `title`, `body`, `placement` fields
- [ ] **3.** Create `TourStepTooltip.tsx`: MUI `Popover` anchored via `document.getElementById(step.targetId)`; step counter `Chip`; title `Typography variant="subtitle2"`; body `Typography variant="body2"`; "Skip Tour" + "Next →"/"Done ✓" buttons; `aria-live="polite"` on content; `transitionDuration={prefersReducedMotion ? 0 : undefined}`
- [ ] **4.** Create `OnboardingTour.tsx`: reads `currentStep` + `hasCompletedOnboarding` from store; `useEffect` to `startTour()` when `isAuthenticated && !hasCompletedOnboarding`; renders `<Backdrop>` + `<TourStepTooltip>` when `currentStep >= 0`
- [ ] **5.** Add `id="nav-book"`, `id="nav-documents"`, `id="nav-profile"` to `Sidebar.tsx` `ListItemButton` elements; add corresponding mobile IDs to `BottomNav.tsx` `BottomNavigationAction` elements
- [ ] **6.** Modify `Header.tsx`: add "Restart Tour" `MenuItem` in avatar `Menu` calling `useOnboardingStore().resetOnboarding()` then `startTour()`; `MenuItem` should only render when `isAuthenticated`
- [ ] **7.** Modify `AuthenticatedLayout.tsx`: import `OnboardingTour`; render `<OnboardingTour />` as last child in layout return (after `<SessionTimeoutModal />`)
- [ ] **[UI Tasks - MANDATORY]** Reference `figma_spec.md` section 8 "First Use" scenario and `designsystem.md` Tooltip component spec (dark background, 150ms fade) during TourStepTooltip implementation
- [ ] **[UI Tasks - MANDATORY]** Validate tour Popover renders correctly at 375px (mobile — BottomNav targets) and 1440px (desktop — Sidebar targets) before marking task complete

# Task - TASK_001: FE Responsive Navigation Shell

## Requirement Reference

- **User Story:** us_037 — Responsive Breakpoints & Adaptive Navigation
- **Story Location:** `.propel/context/tasks/EP-009-I/us_037/us_037.md`
- **Acceptance Criteria:**
  - AC-1: Desktop (≥ 1200px) → full sidebar navigation, multi-column layout, expanded tables (UXR-201)
  - AC-2: Tablet (768–1199px) → sidebar collapses to icon rail, condensed view (UXR-202)
  - AC-3: Mobile (< 768px) → bottom tab bar or hamburger menu, vertical stacking (UXR-203)
  - AC-4: All breakpoints → touch targets minimum 44×44px, 8px spacing, swipe gestures on mobile (UXR-301)
- **Edge Cases:**
  - Split-screen / picture-in-picture modes → CSS container queries ensure components adapt to container width, not just viewport

> ⚠️ **UXR ID Discrepancy (flag for BRD revision):**
> US_037 AC-4 maps to `UXR-301`, but `figma_spec.md` defines `UXR-301` as "Material-UI design system /
> healthcare-appropriate color palette". The 44×44px touch target requirement matches `figma_spec.md` `UXR-202`
> ("System MUST provide touch targets minimum 44×44px on mobile and tablet"). Tasks implement AC intent;
> recommend aligning US_037 AC-4 to reference `UXR-202` in a future BRD revision.

> ⚠️ **Breakpoint Discrepancy (flag for BRD revision):**
> US_037 AC-2 declares "tablet = 768–1199px", AC-3 declares "mobile < 768px". However,
> `design-tokens-applied.md` defines `--breakpoint-md = 900px` as the point where sidebar navigation
> appears (not 768px). This task follows `design-tokens-applied.md` and the MUI theme values in
> `healthcare-theme.ts` (md=900px) as the architectural source of truth. Recommend updating US_037
> AC-2/AC-3 thresholds to 900px and 1200px to match design tokens.

---

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | `.propel/context/wireframes/Hi-Fi/wireframe-SCR-025-header-navigation.html` |
| **Screen Spec** | `figma_spec.md#SCR-025` (Header/Navigation — all personas, all screens) |
| **UXR Requirements** | UXR-201, UXR-202, UXR-203, UXR-301 (see discrepancy note above) |
| **Design Tokens** | `designsystem.md#breakpoints`, `designsystem.md#spacing`, `designsystem.md#component-specifications` |

> **Wireframe Implementation Requirement:**
> MUST open `.propel/context/wireframes/Hi-Fi/wireframe-SCR-025-header-navigation.html` during
> implementation. MUST match sidebar (240px full, 64px icon rail), bottom nav (56px height), and
> header AppBar layout. Validate at 375px, 768px, 900px, 1200px, and 1440px breakpoints.

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

Implement a three-tier adaptive navigation shell in `AuthenticatedLayout.tsx`, `Sidebar.tsx`, and
`BottomNav.tsx` that responds to MUI breakpoints as defined in `design-tokens-applied.md`:

- **lg+ (≥ 1200px) — Desktop:** Persistent full sidebar (240px width, `SIDEBAR_WIDTH` constant).
- **md–lg (900–1199px) — Tablet:** Icon rail sidebar (64px width, icons only, labels in `Tooltip`).
- **xs–sm (0–899px) — Mobile:** Fixed bottom navigation bar (56px height, role-aware tab sets).

Additionally enforces WCAG 2.5.5 Level AA 44×44px minimum touch targets on all interactive navigation
items, and adds supplementary swipe-right-to-open / swipe-left-to-close gesture support for a
temporary overlay drawer on mobile (bottom nav remains the primary mobile navigation pattern).

---

## Dependent Tasks

- `EP-009-I/us_036/task_001_fe_theme_focus_motion_contrast.md` — `healthcare-theme.ts` accessibility
  overrides (focus rings, reduced motion) must be applied before further MUI theme component extensions
  in this task
- `EP-009-I/us_036/task_002_fe_semantic_html_form_a11y.md` — `AuthenticatedLayout.tsx` semantic
  landmark changes (`<SkipToMainContent />`, `id="main-content"`, `aria-label`) must be merged before
  this task modifies the same file

---

## Impacted Components

| Component | Module | Action |
|-----------|--------|--------|
| `AuthenticatedLayout.tsx` | `client/src/components/layout/` | MODIFY — three-tier breakpoint logic, swipe wiring |
| `Sidebar.tsx` | `client/src/components/layout/` | MODIFY — add `iconRail` prop and icon-only render mode |
| `BottomNav.tsx` | `client/src/components/layout/` | MODIFY — 44×44px touch target enforcement via `sx` |
| `healthcare-theme.ts` | `client/src/theme/` | MODIFY — `MuiListItemButton` and `MuiBottomNavigationAction` global min-height overrides |
| `useSwipeGesture.ts` | `client/src/hooks/` | CREATE — touchstart/touchend delta hook |

---

## Implementation Plan

1. **Extend breakpoint detection in `AuthenticatedLayout.tsx`** — replace the current single
   `isDesktop` (`theme.breakpoints.up('md')`) with two separate queries:
   - `isDesktop = useMediaQuery(theme.breakpoints.up('lg'))` — full sidebar (≥ 1200px)
   - `isTablet = useMediaQuery(theme.breakpoints.between('md', 'lg'))` — icon rail (900–1199px)
   - Derive `showFullSidebar`, `showIconRail`, `showBottomNav` booleans from those two queries.
   - Adjust main content `marginLeft`: `0` (mobile), `64px` (icon rail), `SIDEBAR_WIDTH` (full sidebar).

2. **Add `iconRail` prop to `Sidebar.tsx`** — controlled by `showIconRail` from parent:
   - When `iconRail=true`: render permanent `Drawer` with width 64px; suppress `<ListItemText>` via
     `sx={{ display: 'none' }}`.
   - Wrap each `ListItemButton` in MUI `<Tooltip placement="right" title={label}>` so keyboard and
     pointer users can discover the nav label when only an icon is visible.
   - Maintain `aria-current={isActive ? 'page' : undefined}` on each `ListItemButton` (preserves
     US_036 a11y).

3. **Enforce 44×44px touch targets on `Sidebar.tsx` nav items** — add `sx={{ minHeight: 44 }}` on
   every `ListItemButton` in both full-sidebar and icon-rail modes (WCAG 2.5.5 AA).

4. **Add global `MuiListItemButton` min-height override in `healthcare-theme.ts`** — add to
   `components` section: `MuiListItemButton: { styleOverrides: { root: { minHeight: 44 } } }`.
   This applies to all `ListItemButton` instances across the app, preventing regression.

5. **Add global `MuiBottomNavigationAction` min-touch-target override in `healthcare-theme.ts`** —
   MUI `BottomNavigation` renders at 56px height by default; ensure each action has
   `minWidth: 44, touchAction: 'manipulation'` to satisfy WCAG 2.5.5 on all mobile devices.
   Add: `MuiBottomNavigationAction: { styleOverrides: { root: { minWidth: 44 } } }`.

6. **Create `useSwipeGesture.ts`** — lightweight hook:
   - Tracks `touchstart` X position on the target element ref.
   - On `touchend`, computes delta; if `|delta| >= threshold` (default 50px) fires `onSwipeLeft` or
     `onSwipeRight` callbacks.
   - Guards: only binds events when `'ontouchstart' in window` (prevents desktop SSR crash).
   - Returns `{ onTouchStart, onTouchEnd }` props to spread onto the gesture-receiving element.

7. **Wire swipe gesture in `AuthenticatedLayout.tsx` on mobile** — add `mobileDrawerOpen: boolean`
   state; when `showBottomNav` is true, spread `useSwipeGesture` props onto main content `<Box>`:
   - `onSwipeRight` → `setMobileDrawerOpen(true)` (opens temporary overlay `Drawer` with full nav list)
   - `onSwipeLeft` → `setMobileDrawerOpen(false)` (closes overlay drawer)
   - Bottom nav remains the primary mobile navigation; swipe-open drawer is a supplementary shortcut.
   - The temporary `Drawer` uses `variant="temporary"` with `onClose={() => setMobileDrawerOpen(false)}`
     and renders the same `Sidebar` component (without icon-rail prop).

8. **Update `SIDEBAR_WIDTH` import and margin calculation** — introduce `ICON_RAIL_WIDTH = 64` constant
   exported from `Sidebar.tsx`; use in `AuthenticatedLayout.tsx` content `marginLeft` calc to ensure
   content always fills exactly the remaining viewport width without overlap or gap.

---

## Current Project State

```
client/src/
├── components/
│   └── layout/
│       ├── AuthenticatedLayout.tsx  ← MODIFY (single isDesktop breakpoint → three-tier)
│       ├── Sidebar.tsx              ← MODIFY (add iconRail prop)
│       ├── BottomNav.tsx            ← MODIFY (touch target sx)
│       └── Header.tsx               ← no change in this task
├── hooks/                           ← CREATE useSwipeGesture.ts
└── theme/
    └── healthcare-theme.ts          ← MODIFY (MuiListItemButton + MuiBottomNavigationAction)
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | `client/src/components/layout/AuthenticatedLayout.tsx` | Replace single `isDesktop` breakpoint with `isDesktop` (lg+) + `isTablet` (md–lg); derive `showFullSidebar`, `showIconRail`, `showBottomNav`; add `mobileDrawerOpen` state; wire `useSwipeGesture`; adjust `marginLeft` and content width calc |
| MODIFY | `client/src/components/layout/Sidebar.tsx` | Add `iconRail?: boolean` prop; 64px drawer width when true; hide `ListItemText`; wrap icons in `Tooltip`; export `ICON_RAIL_WIDTH = 64` constant; add `sx={{ minHeight: 44 }}` on `ListItemButton` |
| MODIFY | `client/src/components/layout/BottomNav.tsx` | Add `sx={{ minWidth: 44 }}` on `BottomNavigationAction` items; `touchAction: 'manipulation'` on `BottomNavigation` |
| MODIFY | `client/src/theme/healthcare-theme.ts` | Add `MuiListItemButton.styleOverrides.root.minHeight: 44` and `MuiBottomNavigationAction.styleOverrides.root.minWidth: 44` to `components` block |
| CREATE | `client/src/hooks/useSwipeGesture.ts` | Touchstart/touchend X-delta hook; `onSwipeLeft`/`onSwipeRight` callbacks; `threshold` param (default 50px); `ontouchstart` guard |

---

## External References

- [MUI Drawer API — variant, width, breakpoints (MUI v5)](https://mui.com/material-ui/react-drawer/)
- [MUI useMediaQuery — theme.breakpoints.up/between (MUI v5)](https://mui.com/material-ui/react-use-media-query/)
- [MUI BottomNavigation API (MUI v5)](https://mui.com/material-ui/react-bottom-navigation/)
- [MUI Tooltip API — placement, title (MUI v5)](https://mui.com/material-ui/react-tooltip/)
- [WCAG 2.5.5 Target Size (Minimum)](https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html)
- [WCAG 1.4.4 Reflow — no horizontal scroll at 320px viewport width](https://www.w3.org/WAI/WCAG22/Understanding/reflow.html)
- [Touch event API — touchstart / touchend (MDN)](https://developer.mozilla.org/en-US/docs/Web/API/Touch_events)
- [design-tokens-applied.md#8-responsive-breakpoint-token-application — breakpoint definitions and nav pattern changes](d:\Propal IQ\Appontment Booking and Clinical Intell Platform\PROPELIQ_APPOINTMENT_BOOKING\.propel\context\wireframes\design-tokens-applied.md)

---

## Build Commands

```bash
# From workspace root
cd client
npm run dev           # Vite dev server — verify responsive behaviour in browser devtools
npm run build         # Production build — confirm no TypeScript errors
npm run lint          # ESLint — confirm no new warnings
```

---

## Implementation Validation Strategy

- [ ] Unit tests pass
- [ ] Integration tests pass (if applicable)
- [ ] **[UI Tasks]** Visual comparison against wireframe completed at 375px, 768px, 900px, 1200px, 1440px
- [ ] **[UI Tasks]** Run `/analyze-ux` to validate wireframe alignment
- [ ] Sidebar renders at 240px on lg+ (≥ 1200px viewport)
- [ ] Sidebar renders as 64px icon rail on md–lg (900–1199px viewport)
- [ ] Bottom nav renders and sidebar is hidden on xs–sm (< 900px viewport)
- [ ] Each `ListItemButton` in icon rail mode shows `Tooltip` with nav label on hover/focus
- [ ] `aria-current="page"` present on active nav item in all three modes (browser DevTools → Elements)
- [ ] All nav touch targets measure ≥ 44×44px (browser DevTools → Computed → box model)
- [ ] Swipe-right on mobile viewport (375px) opens overlay drawer; swipe-left closes it
- [ ] Content does not overflow horizontally at any breakpoint (no horizontal scrollbar)
- [ ] `ICON_RAIL_WIDTH` (64px) and `SIDEBAR_WIDTH` (240px) exported correctly and consumed by `AuthenticatedLayout.tsx` for margin calculation

---

## Implementation Checklist

- [ ] **1.** Update `AuthenticatedLayout.tsx`: replace `isDesktop = useMediaQuery(theme.breakpoints.up('md'))` with `isDesktop = useMediaQuery(theme.breakpoints.up('lg'))` and `isTablet = useMediaQuery(theme.breakpoints.between('md', 'lg'))`; derive `showFullSidebar`, `showIconRail`, `showBottomNav` flags; adjust `marginLeft` in main `<Box>` to `showFullSidebar ? SIDEBAR_WIDTH : showIconRail ? ICON_RAIL_WIDTH : 0`
- [ ] **2.** Update `Sidebar.tsx`: add `iconRail?: boolean` prop; when true render `Drawer` at 64px width, suppress `ListItemText` via `sx={{ display: 'none' }}`, wrap each icon `ListItemButton` in MUI `Tooltip` with `placement="right"` and `title={label}`; export `ICON_RAIL_WIDTH = 64`
- [ ] **3.** Add `sx={{ minHeight: 44 }}` to all `ListItemButton` elements in `Sidebar.tsx` (both full and icon-rail modes) to enforce WCAG 2.5.5 44×44px touch target
- [ ] **4.** Add `MuiListItemButton: { styleOverrides: { root: { minHeight: 44 } } }` to `components` block in `healthcare-theme.ts` as global enforcement
- [ ] **5.** Add `MuiBottomNavigationAction: { styleOverrides: { root: { minWidth: 44 } } }` to `components` block in `healthcare-theme.ts`; add `touchAction: 'manipulation'` on `BottomNavigation` `sx` in `BottomNav.tsx`
- [ ] **6.** Create `client/src/hooks/useSwipeGesture.ts`: exports `useSwipeGesture({ threshold?: number, onSwipeLeft?: () => void, onSwipeRight?: () => void })` returning `{ onTouchStart, onTouchEnd }` spread props; uses `useRef` for `touchStartX`; only binds when `typeof window !== 'undefined' && 'ontouchstart' in window`
- [ ] **7.** Add `mobileDrawerOpen` state to `AuthenticatedLayout.tsx`; spread `useSwipeGesture` onto main `<Box>` when `showBottomNav=true`; render temporary `<Drawer variant="temporary" open={mobileDrawerOpen} onClose>` containing `<Sidebar />` (non icon-rail) for swipe-open overlay
- [ ] **8.** Verify no horizontal scrollbar at 375px viewport width after changes (Vite dev server → Chrome DevTools responsive mode); confirm `Toolbar` spacer height equals AppBar height in all three nav modes
- [ ] **[UI Tasks - MANDATORY]** Reference wireframe `.propel/context/wireframes/Hi-Fi/wireframe-SCR-025-header-navigation.html` during implementation
- [ ] **[UI Tasks - MANDATORY]** Validate UI matches wireframe before marking task complete

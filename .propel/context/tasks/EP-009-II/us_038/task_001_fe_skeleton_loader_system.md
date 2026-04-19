# Task - TASK_001: FE Skeleton Loader System

## Requirement Reference

- **User Story:** us_038 — Loading Feedback, Toasts & Progress Indicators
- **Story Location:** `.propel/context/tasks/EP-009-II/us_038/us_038.md`
- **Acceptance Criteria:**
  - AC-1: Given a page or component is loading data, When content is not yet available, Then skeleton
    loaders matching the content layout are displayed (not spinners) within 100ms of the request
    per UXR-401.
- **Edge Cases:** N/A for skeleton loaders specifically (no content truncation or stall edge cases apply here)

> ⚠️ **UXR-401 Definition Discrepancy (flag for BRD revision):**
> US_038 AC-1 references `UXR-401` and specifies **skeleton loaders within 100ms**.
> However, `figma_spec.md` defines `UXR-401` as: "System MUST provide loading feedback within
> **200ms** of user action — User perceives immediate response, <200ms **spinner** display."
> Two conflicts: (1) threshold 100ms vs 200ms, (2) skeleton vs spinner. This task implements
> the US_038 AC intent (skeleton, 100ms) as it is the more specific and WCAG-aligned specification.
> Recommend updating `figma_spec.md` UXR-401 to reference skeleton loaders and align on a single
> threshold (100ms is stricter and preferred).

---

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | `.propel/context/wireframes/Hi-Fi/wireframe-SCR-001-availability-search.html` (slot cards loading skeleton) · `.propel/context/wireframes/Hi-Fi/wireframe-SCR-010-staff-dashboard.html` (summary card + table row skeleton) |
| **Screen Spec** | `figma_spec.md#SCR-001`, `figma_spec.md#SCR-010` (Loading states defined); cross-cutting all SCR-001 through SCR-028 |
| **UXR Requirements** | UXR-401 (see discrepancy note above) |
| **Design Tokens** | `designsystem.md#colors` (`--color-neutral-200` shimmer base, `--color-neutral-100` shimmer highlight), `designsystem.md#spacing` (8px grid for skeleton dimensions), `designsystem.md#component-specifications` |

> **Wireframe Implementation Requirement:**
> MUST open `wireframe-SCR-001-availability-search.html` (Loading state) and
> `wireframe-SCR-010-staff-dashboard.html` (Loading state) to match skeleton shapes
> (card dimensions, table row height, text line heights). Validate skeleton layout matches
> actual content layout at 375px, 768px, and 1440px breakpoints.

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

Create a set of reusable, layout-matched skeleton loader components using MUI `Skeleton` (built into
`@mui/material`). Skeletons must match the exact shape and dimensions of the content they represent
so users experience a smooth, cumulative layout shift-free (CLS = 0) transition from loading to
loaded state.

Components to create:
- **`CardSkeleton`** — mimics a summary card (title line, subtitle line, metric value block): used on
  SCR-010 (Staff Dashboard summary cards), SCR-008 (appointment cards), SCR-028 (metric tiles).
- **`TableRowSkeleton`** — mimics a table row (N column cells with appropriate widths): used on
  SCR-010 (queue table), SCR-012 (same-day queue), SCR-016 (patient chart review), SCR-021
  (user management table).
- **`FormSkeleton`** — mimics a form section (label line + input block pairs): used on SCR-003
  (patient details), SCR-011 (walk-in booking form), SCR-022 (create/edit user).
- **`DetailViewSkeleton`** — mimics a 360-view detail panel (section heading + fact rows): used on
  SCR-017 (360-degree patient view), SCR-019 (code verification).

All skeletons use MUI `Skeleton` `variant="rectangular"` / `variant="text"` / `variant="circular"`
with `animation="wave"` (shimmer). The 100ms display guarantee is achieved by rendering skeletons
synchronously on component mount — no `useEffect` delay or artificial timeout.

---

## Dependent Tasks

- `EP-009-I/us_036/task_001_fe_theme_focus_motion_contrast.md` — `healthcare-theme.ts` reduced motion
  override must be in place; skeleton `animation="wave"` must be disabled when
  `prefers-reduced-motion: reduce` is active (replace wave with `animation="pulse"` or `false`).
- `EP-009-I/us_037/task_002_fe_responsive_layout_content.md` — `ResponsiveTable` component and
  responsive padding patterns should be established first, as `TableRowSkeleton` must match the same
  responsive layout at each breakpoint.

---

## Impacted Components

| Component | Module | Action |
|-----------|--------|--------|
| `CardSkeleton.tsx` | `client/src/components/skeletons/` | CREATE |
| `TableRowSkeleton.tsx` | `client/src/components/skeletons/` | CREATE |
| `FormSkeleton.tsx` | `client/src/components/skeletons/` | CREATE |
| `DetailViewSkeleton.tsx` | `client/src/components/skeletons/` | CREATE |
| `index.ts` | `client/src/components/skeletons/` | CREATE — barrel export |
| `healthcare-theme.ts` | `client/src/theme/` | MODIFY — `MuiSkeleton` component override: disable wave animation when reduced motion |

---

## Implementation Plan

1. **Create `client/src/components/skeletons/` folder** with a barrel `index.ts` that re-exports all
   skeleton components. This enables consumers to `import { CardSkeleton } from '@/components/skeletons'`.

2. **`CardSkeleton.tsx`** — props: `count?: number` (default 1, renders N skeleton cards in a grid).
   Each card uses:
   - `<Skeleton variant="rectangular" height={48} width="40%" sx={{ mb: 1 }} />` — metric value
   - `<Skeleton variant="text" width="60%" />` — card title line
   - `<Skeleton variant="text" width="40%" />` — subtitle/secondary line
   - Wrap in `<Card sx={{ p: 3, borderRadius: 2 }}>` matching `--border-radius-medium` (8px) and
     `--spacing-3` (24px) padding from design tokens.
   - When `count > 1`, render inside a responsive MUI `Grid` matching the content grid layout
     (xs=12, sm=6, md=3 — same as summary cards in SCR-010).

3. **`TableRowSkeleton.tsx`** — props: `columns: number` (default 5), `rows?: number` (default 5).
   Renders a `<TableBody>` containing `rows` × `<TableRow>` each with `columns` × `<TableCell>`:
   - Each cell: `<Skeleton variant="text" width={`${70 + Math.random() * 30}%`} />` — randomised widths
     prevent the "barcode" effect of all-same-width skeletons.
   - **Important:** Use a seeded width array (pre-computed static array, not `Math.random()` in render)
     to avoid hydration mismatches and CLS.

4. **`FormSkeleton.tsx`** — props: `fields?: number` (default 4).
   Renders `fields` label+input pairs stacked vertically with `gap: 3` (24px):
   - Label: `<Skeleton variant="text" width="35%" sx={{ mb: 0.5 }} />`
   - Input: `<Skeleton variant="rectangular" height={56} width="100%" sx={{ borderRadius: 1 }} />`
   - Matches MUI `OutlinedInput` height (56px) and `--border-radius-small` (4px).

5. **`DetailViewSkeleton.tsx`** — props: `sections?: number` (default 3), `factsPerSection?: number`
   (default 4).
   Renders `sections` × section heading + `factsPerSection` × fact rows:
   - Section heading: `<Skeleton variant="text" width="25%" sx={{ mb: 1 }} />`
   - Fact row: two-column flex — `<Skeleton variant="text" width="30%" />` (label) +
     `<Skeleton variant="text" width="55%" />` (value).

6. **Reduced motion guard in `healthcare-theme.ts`** — add to `components` block:
   ```ts
   MuiSkeleton: {
     defaultProps: {
       animation: prefersReducedMotion ? false : 'wave',
     },
   },
   ```
   This globally overrides skeleton animation when the user has reduced motion preferences enabled,
   consistent with the `prefersReducedMotion` constant already defined in `healthcare-theme.ts`
   (US_036).

7. **Usage pattern documentation in `index.ts`** — add JSDoc comments to each export explaining
   which screen(s) and React Query loading states the skeleton is designed for. This guides future
   feature implementation teams (EP-001 through EP-005) to use the correct skeleton variant
   without creating new ad-hoc loading states.

8. **Integration proof-of-concept in `AuthenticatedLayout.tsx`** — wrap the `<Outlet />` in a
   React Suspense-compatible pattern: the layout shell is always rendered (Header, Sidebar/BottomNav),
   only the page-level content region can show a skeleton. Add a `PageLoadingFallback` component
   that renders `<CardSkeleton count={4} />` as the default Suspense fallback — used in
   `createBrowserRouter` `errorElement`/`lazy` pages in future.

---

## Current Project State

```
client/src/
├── components/
│   ├── skeletons/          ← CREATE (new folder)
│   │   ├── index.ts
│   │   ├── CardSkeleton.tsx
│   │   ├── TableRowSkeleton.tsx
│   │   ├── FormSkeleton.tsx
│   │   └── DetailViewSkeleton.tsx
│   └── layout/
│       └── AuthenticatedLayout.tsx  ← MODIFY (PageLoadingFallback proof-of-concept)
└── theme/
    └── healthcare-theme.ts          ← MODIFY (MuiSkeleton reduced motion override)
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `client/src/components/skeletons/CardSkeleton.tsx` | Summary card skeleton with `count` prop and responsive Grid layout matching SCR-010 summary cards (xs=12, sm=6, md=3) |
| CREATE | `client/src/components/skeletons/TableRowSkeleton.tsx` | Table body skeleton with configurable `columns` and `rows`; static width array to prevent CLS |
| CREATE | `client/src/components/skeletons/FormSkeleton.tsx` | Label + OutlinedInput-height (56px) skeleton pairs with configurable `fields` count |
| CREATE | `client/src/components/skeletons/DetailViewSkeleton.tsx` | Section heading + two-column fact row skeleton with configurable `sections` and `factsPerSection` |
| CREATE | `client/src/components/skeletons/index.ts` | Barrel export for all four skeleton components with JSDoc screen references |
| MODIFY | `client/src/theme/healthcare-theme.ts` | Add `MuiSkeleton.defaultProps.animation` reduced-motion guard using existing `prefersReducedMotion` constant |
| MODIFY | `client/src/components/layout/AuthenticatedLayout.tsx` | Add `PageLoadingFallback` component and `<Suspense fallback={<PageLoadingFallback />}>` wrapper around `<Outlet />` |

---

## External References

- [MUI Skeleton API — variants, animation, wave/pulse/false (MUI v5)](https://mui.com/material-ui/react-skeleton/)
- [MUI Skeleton — wave animation override with prefers-reduced-motion (MUI v5 customization)](https://mui.com/material-ui/react-skeleton/#accessibility)
- [React Suspense — Suspense boundary with loading fallback](https://react.dev/reference/react/Suspense)
- [React Query — `isLoading` vs `isFetching` distinction for skeleton display (@tanstack/react-query v4)](https://tanstack.com/query/v4/docs/react/guides/queries#query-basics)
- [Web.dev — Cumulative Layout Shift (CLS) — zero-CLS skeleton best practices](https://web.dev/articles/cls)
- [WCAG 2.3.3 Animation from Interactions — prefers-reduced-motion for skeleton animation](https://www.w3.org/WAI/WCAG22/Understanding/animation-from-interactions.html)
- [MDN — prefers-reduced-motion media query](https://developer.mozilla.org/en-US/docs/Web/CSS/@media/prefers-reduced-motion)

---

## Build Commands

```bash
cd client
npm run dev     # Verify skeleton renders at correct dimensions in browser devtools
npm run build   # Confirm no TypeScript errors (generic props on skeleton components)
npm run lint    # Confirm no a11y warnings on Skeleton elements
```

---

## Implementation Validation Strategy

- [ ] Unit tests pass
- [ ] Integration tests pass (if applicable)
- [ ] **[UI Tasks]** Visual comparison against wireframe completed at 375px, 768px, and 1440px
- [ ] **[UI Tasks]** Run `/analyze-ux` to validate wireframe alignment
- [ ] `CardSkeleton` dimensions match actual `Card` dimensions in SCR-010 wireframe (no layout shift on data load)
- [ ] `TableRowSkeleton` cell widths are static (no `Math.random()` in render — prevent hydration mismatch)
- [ ] `FormSkeleton` input height is exactly 56px (matches `OutlinedInput` rendered height)
- [ ] Skeleton `animation="wave"` is overridden to `false` when `prefers-reduced-motion: reduce` is active (verify in Chrome DevTools → Rendering → Emulate CSS media: prefers-reduced-motion)
- [ ] Skeleton renders synchronously on first render with no `useEffect` or `setTimeout` delay (confirms < 100ms display)
- [ ] No cumulative layout shift (CLS = 0) when transitioning from skeleton to actual content — verify with Chrome Lighthouse CLS audit
- [ ] `PageLoadingFallback` renders `<CardSkeleton count={4} />` inside the main content area (not full-page overlay)

---

## Implementation Checklist

- [ ] **1.** Create `client/src/components/skeletons/CardSkeleton.tsx` with `count?: number` prop; render in responsive `Grid` (xs=12, sm=6, md=3); card padding 24px (`spacing: 3`); metric value block 48px tall, title text, subtitle text; no `Math.random()` in JSX
- [ ] **2.** Create `client/src/components/skeletons/TableRowSkeleton.tsx` with `columns: number` and `rows?: number` props; define static `CELL_WIDTHS` array (e.g., `[70, 85, 60, 90, 75]`) for deterministic widths; render inside `<TableBody>` so component drops directly into `<Table>` usage
- [ ] **3.** Create `client/src/components/skeletons/FormSkeleton.tsx` with `fields?: number` prop; each field = label text skeleton (35% width) + input rectangular skeleton (100% width, 56px height, borderRadius 4px); vertical gap 24px (`spacing: 3`)
- [ ] **4.** Create `client/src/components/skeletons/DetailViewSkeleton.tsx` with `sections?: number` and `factsPerSection?: number` props; section heading + two-column flex rows (30% label, 55% value); section spacing 32px (`spacing: 4`)
- [ ] **5.** Create `client/src/components/skeletons/index.ts` barrel: `export { CardSkeleton } from './CardSkeleton'` etc.; add JSDoc screen references per component (e.g., `/** Used in SCR-010 summary cards, SCR-008 appointment cards, SCR-028 metric tiles */`)
- [ ] **6.** Add `MuiSkeleton: { defaultProps: { animation: prefersReducedMotion ? false : 'wave' } }` to `components` block in `healthcare-theme.ts` — use existing `prefersReducedMotion` constant (established in US_036)
- [ ] **7.** Add `PageLoadingFallback` component inside `AuthenticatedLayout.tsx` (no separate file — small enough to inline): renders `<Box sx={{ pt: 3 }}><CardSkeleton count={4} /></Box>`; wrap `<Outlet />` in `<Suspense fallback={<PageLoadingFallback />}>`
- [ ] **8.** Verify zero CLS: use Chrome DevTools Performance tab to record a page load with skeleton visible, confirm no layout shift when skeleton unmounts and actual content mounts
- [ ] **[UI Tasks - MANDATORY]** Reference wireframe `wireframe-SCR-001-availability-search.html` and `wireframe-SCR-010-staff-dashboard.html` Loading states during implementation
- [ ] **[UI Tasks - MANDATORY]** Validate skeleton shapes match wireframe content areas before marking task complete

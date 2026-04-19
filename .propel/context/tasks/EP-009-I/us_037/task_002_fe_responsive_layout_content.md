# Task - TASK_002: FE Responsive Layout & Content Adaptation

## Requirement Reference

- **User Story:** us_037 — Responsive Breakpoints & Adaptive Navigation
- **Story Location:** `.propel/context/tasks/EP-009-I/us_037/us_037.md`
- **Acceptance Criteria:**
  - AC-1: Desktop (≥ 1200px) → multi-column content areas, expanded data tables (UXR-201)
  - AC-2: Tablet (768–1199px) → content adapts to fewer columns, tables switch to condensed view (UXR-202)
  - AC-3: Mobile (< 768px) → content stacks vertically, tables transform to card layouts (UXR-203)
  - AC-5: Any breakpoint resize/rotate → smooth layout transitions, scroll position preserved, no horizontal scrollbar (UXR-302)
- **Edge Cases:**
  - Content doesn't fit in mobile card layout → truncate with "Show more" expansion; critical data always visible
  - Split-screen / picture-in-picture modes → CSS container queries ensure components adapt to container width

> ⚠️ **UXR ID Discrepancy (flag for BRD revision):**
> US_037 AC-5 maps to `UXR-302`, but `figma_spec.md` defines `UXR-302` as "consistent spacing based on
> 8px grid system". The smooth-transitions and scroll-preservation intent from AC-5 is not explicitly
> captured in any named UXR in `figma_spec.md`. The 8px grid enforcement (actual UXR-302) is also
> addressed in this task. Recommend adding a dedicated UXR for transition behaviour in a future BRD
> revision.

> ⚠️ **Breakpoint Discrepancy (flag for BRD revision):**
> US_037 AC-2 declares "tablet = 768–1199px" and AC-3 "mobile < 768px". `design-tokens-applied.md`
> defines `--breakpoint-md = 900px` as the table/content threshold. This task follows design tokens
> (md=900px) as single source of truth. See task_001 for full discrepancy note.

---

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | `.propel/context/wireframes/Hi-Fi/wireframe-SCR-025-header-navigation.html` (cross-cutting nav shell); all screen wireframes in `.propel/context/wireframes/Hi-Fi/` apply |
| **Screen Spec** | `figma_spec.md#SCR-001` through `figma_spec.md#SCR-028` (cross-cutting responsive behaviour) |
| **UXR Requirements** | UXR-201, UXR-202, UXR-203, UXR-302 (see discrepancy note above) |
| **Design Tokens** | `designsystem.md#breakpoints`, `designsystem.md#spacing`, `designsystem.md#component-specifications` |

> **Wireframe Implementation Requirement:**
> MUST reference `design-tokens-applied.md` Section 8 (Responsive Breakpoint Token Application) and
> Section 4 (Spacing Token Application, 8px grid) during implementation. Validate grid layouts and
> table-to-card transformations at 375px, 768px, 900px, 1200px, and 1440px.

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

Implement responsive content layout adaptation across all authenticated screens. The focus is on:

1. **Grid column collapsing** — main content area padding/margin uses responsive `sx` breakpoint objects
   (MUI system) that apply 8px-grid multiples at each tier.

2. **Table → card transformation** — a reusable `ResponsiveTable` component renders MUI `<Table>` on
   `md+` (≥ 900px) and a `<Stack>` of MUI `<Card>` items on `xs/sm` (< 900px). Priority columns always
   appear in card headers; optional columns collapse into the card body. Truncation with `Tooltip` handles
   overflow edge cases.

3. **Horizontal overflow prevention** — global `overflow-x: hidden` on main content box + `max-width: 100%`
   on images and table containers via `GlobalStyles` in `App.tsx`.

4. **Scroll position restoration on resize** — `useScrollRestoration` hook saves and restores
   `window.scrollY` across debounced resize / orientation-change events.

5. **8px grid spacing audit** — all `gap`, `spacing`, `padding`, and `margin` in layout components
   verified to use `theme.spacing()` multiples (single source of truth: UXR-302).

---

## Dependent Tasks

- `EP-009-I/us_037/task_001_fe_responsive_navigation_shell.md` — sidebar width constants
  (`SIDEBAR_WIDTH`, `ICON_RAIL_WIDTH`) and the three-tier `showFullSidebar`/`showIconRail`/`showBottomNav`
  flags must exist before this task can adjust content-area margins
- `EP-009-I/us_036/task_002_fe_semantic_html_form_a11y.md` — `AuthenticatedLayout.tsx` semantic
  landmark changes and `App.tsx` `GlobalStyles` must be merged before this task extends them

---

## Impacted Components

| Component | Module | Action |
|-----------|--------|--------|
| `AuthenticatedLayout.tsx` | `client/src/components/layout/` | MODIFY — responsive `px`/`pb` padding, `overflowX: 'hidden'` on main Box |
| `App.tsx` | `client/src/` | MODIFY — extend `GlobalStyles` with `img` max-width and `table` overflow rules |
| `ResponsiveTable.tsx` | `client/src/components/common/` | CREATE — MUI table on md+, card stack on xs/sm |
| `useScrollRestoration.ts` | `client/src/hooks/` | CREATE — scroll position save/restore on resize |

---

## Implementation Plan

1. **Responsive padding on main content area in `AuthenticatedLayout.tsx`** — update `<Box component="main">`
   `sx` to use responsive breakpoint objects for padding: `px: { xs: 2, sm: 3, md: 3 }` (maps to 16px,
   24px, 24px using 8px base), `pt: { xs: 2, md: 3 }`. Add `overflowX: 'hidden'` and `maxWidth: '100%'`
   to prevent horizontal scroll at all breakpoints (AC-5). Existing `pb` logic for bottom nav clearance
   is preserved.

2. **Global overflow guard in `App.tsx`** — extend the existing `<GlobalStyles>` component (added in
   US_036 for `prefers-reduced-motion`) with additional rules:
   - `'img, video': { maxWidth: '100%', height: 'auto' }` — prevents media overflow on mobile.
   - `'table': { maxWidth: '100%' }` — prevents wide tables from creating horizontal scrollbars inside
     non-scrollable containers.

3. **Create `client/src/components/common/ResponsiveTable.tsx`** — generic table-to-card transformer:
   - Props: `columns: Column[]` where `Column = { key: string; label: string; priority: 'always' | 'optional' }`;
     `rows: Record<string, ReactNode>[]`; `keyField: string` (row identity for React `key`).
   - On `md+` (`useMediaQuery(theme.breakpoints.up('md'))`): render standard MUI `<Table>` with
     `<TableHead>` and `<TableBody>`. All columns rendered.
   - On `xs/sm`: render `<Stack spacing={2}>` of `<Card variant="outlined">`. Each card:
     - Header row: `priority: 'always'` columns only (patient name, status, time — critical data).
     - Body row: `priority: 'optional'` columns in key-value `<Typography variant="body2">` pairs.
     - Truncate body values > 60 chars with `sx={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}`;
       wrap in `<Tooltip title={fullValue}>` for disclosure.
   - Include "Show more / Show less" `<Collapse>` toggle on optional columns when count > 3 (edge case).

4. **Scroll position restoration hook `useScrollRestoration.ts`** — addresses AC-5 scroll preservation:
   - On mount, bind `window.addEventListener('resize', handleResizeStart)`.
   - `handleResizeStart`: save `scrollY = window.scrollY` to a `ref` (no re-render).
   - Use `setTimeout(() => window.scrollTo({ top: scrollYRef.current, behavior: 'instant' }), 200)`
     as a debounce — restores position after the layout has re-painted at the new breakpoint.
   - On unmount, remove listener.
   - Exported as `useScrollRestoration()` with no arguments; intended to be called once in
     `AuthenticatedLayout.tsx`.

5. **Wire `useScrollRestoration` in `AuthenticatedLayout.tsx`** — add a single `useScrollRestoration()`
   call at the top of the component. No additional props or configuration required.

6. **8px grid spacing audit and correction** — scan `AuthenticatedLayout.tsx`, `Sidebar.tsx`, and
   `BottomNav.tsx` for any hardcoded pixel values in `sx` props that are not multiples of 8
   (e.g., `pt: 1.5` = 12px is acceptable as 8×1.5; `pt: 7px` is not). Replace non-conforming values
   with `theme.spacing()` equivalents. Document confirmed conformance in the checklist.

7. **Apply `ResponsiveTable` to reference screens** — update `client/src/pages/` placeholder pages
   (where table-structured data will eventually be rendered) to import and use `ResponsiveTable`.
   For any page that currently hard-codes an MUI `<Table>`, replace with `<ResponsiveTable>`.
   This establishes the pattern for all future feature implementations (EP-001 through EP-005 teams).

---

## Current Project State

```
client/src/
├── App.tsx                           ← MODIFY (extend GlobalStyles)
├── components/
│   ├── common/                       ← CREATE ResponsiveTable.tsx
│   └── layout/
│       └── AuthenticatedLayout.tsx   ← MODIFY (padding, overflow, useScrollRestoration)
└── hooks/                            ← CREATE useScrollRestoration.ts
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | `client/src/components/layout/AuthenticatedLayout.tsx` | Add responsive `px`/`pt` breakpoint objects on main `<Box>`; add `overflowX: 'hidden'`, `maxWidth: '100%'`; call `useScrollRestoration()` |
| MODIFY | `client/src/App.tsx` | Extend `GlobalStyles` with `img/video` max-width and `table` overflow rules |
| CREATE | `client/src/components/common/ResponsiveTable.tsx` | Column-priority table-to-card component with Tooltip truncation and Show more/less Collapse |
| CREATE | `client/src/hooks/useScrollRestoration.ts` | Resize-event scroll-position save/restore hook with 200ms debounce |

---

## External References

- [MUI System — responsive values with breakpoint objects (`sx={{ px: { xs: 2, md: 3 } }}`)](https://mui.com/system/getting-started/the-sx-prop/#responsive-values)
- [MUI Grid v2 — column collapsing across breakpoints (MUI v5)](https://mui.com/material-ui/react-grid/)
- [MUI Table API (MUI v5)](https://mui.com/material-ui/react-table/)
- [MUI Card API (MUI v5)](https://mui.com/material-ui/react-card/)
- [MUI Collapse API — animated show more/less](https://mui.com/material-ui/transitions/#collapse)
- [MUI GlobalStyles API (MUI v5)](https://mui.com/material-ui/customization/how-to-customize/#global-css-override)
- [MUI useMediaQuery (MUI v5)](https://mui.com/material-ui/react-use-media-query/)
- [MUI Tooltip API (MUI v5)](https://mui.com/material-ui/react-tooltip/)
- [CSS Container Queries — MDN](https://developer.mozilla.org/en-US/docs/Web/CSS/CSS_containment/Container_queries)
- [WCAG 1.4.10 Reflow — no two-dimensional scrolling at 320px viewport width](https://www.w3.org/WAI/WCAG22/Understanding/reflow.html)
- [Window.scrollY + resize event (MDN)](https://developer.mozilla.org/en-US/docs/Web/API/Window/scrollY)
- [design-tokens-applied.md Section 4 — 8px spacing grid token application](.propel/context/wireframes/design-tokens-applied.md)
- [design-tokens-applied.md Section 8 — responsive breakpoint adaptations (grid layout + table)](.propel/context/wireframes/design-tokens-applied.md)

---

## Build Commands

```bash
# From workspace root
cd client
npm run dev           # Vite dev server — test table→card transformation at mobile viewport
npm run build         # Production build — confirm no TypeScript type errors in ResponsiveTable generics
npm run lint          # ESLint — confirm no new accessibility warnings
```

---

## Implementation Validation Strategy

- [ ] Unit tests pass
- [ ] Integration tests pass (if applicable)
- [ ] **[UI Tasks]** Visual comparison against wireframe completed at 375px, 768px, 900px, 1200px, 1440px
- [ ] **[UI Tasks]** Run `/analyze-ux` to validate wireframe alignment
- [ ] Main content area padding is 16px (xs), 24px (sm+) — matches 8px grid token `--spacing-2` / `--spacing-3`
- [ ] No horizontal scrollbar appears at 375px viewport width (Chrome DevTools responsive mode)
- [ ] Wide content (table cells, long names) does not overflow container on mobile
- [ ] `ResponsiveTable` renders MUI `<Table>` at 900px+ and `<Card>` stack at < 900px
- [ ] `priority: 'always'` columns always visible in mobile card header (patient name, status, time)
- [ ] `priority: 'optional'` columns truncated with ellipsis + `Tooltip` in card body
- [ ] "Show more / Show less" toggle works correctly when optional column count > 3
- [ ] Scroll position is restored after browser window resize (manual test: scroll 500px, resize window, confirm position maintained)
- [ ] All `spacing`, `gap`, `px`, `py` values in layout components are multiples of 8px (audit logged in checklist item 6)

---

## Implementation Checklist

- [ ] **1.** Update `AuthenticatedLayout.tsx` main `<Box component="main">` `sx`: set `px: { xs: 2, sm: 3, md: 3 }`, `pt: { xs: 2, md: 3 }`, add `overflowX: 'hidden'` and `maxWidth: '100%'`; preserve existing `pb` logic for bottom nav clearance; call `useScrollRestoration()` at component top
- [ ] **2.** Extend `GlobalStyles` in `App.tsx`: add `'img, video': { maxWidth: '100%', height: 'auto' }` and `'table': { maxWidth: '100%' }` rules to prevent media and table overflow on narrow viewports
- [ ] **3.** Create `client/src/components/common/ResponsiveTable.tsx`: define `Column` type with `key`, `label`, `priority` fields; render MUI `Table` on md+ via `useMediaQuery(theme.breakpoints.up('md'))`; render `Stack` of `Card` items on xs/sm with `priority: 'always'` in card header and `priority: 'optional'` in collapsible card body
- [ ] **4.** Add Tooltip truncation to `ResponsiveTable` optional column cells: `sx={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}`; wrap in `<Tooltip title={String(cell value)}>` for full disclosure on hover/focus
- [ ] **5.** Add `<Collapse in={expanded}>` toggle with "Show more / Show less" `<Button variant="text" size="small">` when optional column count > 3 in mobile card mode (edge case from AC)
- [ ] **6.** Create `client/src/hooks/useScrollRestoration.ts`: on mount bind `resize` event listener; save `window.scrollY` to `useRef`; restore after 200ms debounced `setTimeout`; remove listener on unmount; export `useScrollRestoration()` with no arguments
- [ ] **7.** Audit and document 8px grid conformance: scan `AuthenticatedLayout.tsx`, `Sidebar.tsx`, `BottomNav.tsx` `sx` props; confirm all pixel values are multiples of 8 (`theme.spacing()` equivalents); replace any non-conforming hardcoded values; check ✅ or flag ⚠️ each file below:
  - `AuthenticatedLayout.tsx` spacing conformance: [ ] ✅ / [ ] ⚠️ (document violations)
  - `Sidebar.tsx` spacing conformance: [ ] ✅ / [ ] ⚠️ (document violations)
  - `BottomNav.tsx` spacing conformance: [ ] ✅ / [ ] ⚠️ (document violations)
- [ ] **[UI Tasks - MANDATORY]** Reference wireframe `.propel/context/wireframes/Hi-Fi/wireframe-SCR-025-header-navigation.html` and `design-tokens-applied.md` Section 8 during implementation
- [ ] **[UI Tasks - MANDATORY]** Validate UI matches wireframe and design tokens before marking task complete

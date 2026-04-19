# Task - TASK_001: FE Breadcrumb Navigation Component

## Requirement Reference

- **User Story:** us_040 — Navigation Optimization, Guidance & Semantic Colors
- **Story Location:** `.propel/context/tasks/EP-009-II/us_040/us_040.md`
- **Acceptance Criteria:**
  - AC-1: Given I navigate within the platform, When I am more than one level deep, Then a breadcrumb
    trail appears below the header showing the navigation path with clickable parent links per UXR-001.
  - AC-5: Given the navigation system, When I access any feature, Then the maximum click depth from
    the dashboard to any feature is 3 clicks or fewer per UXR-001.
- **Edge Cases:**
  - Breadcrumb trail too long for screen width → Collapse intermediate levels with "..." ellipsis;
    first and last 2 levels always visible (per AC edge case definition).

> ⚠️ **UXR-001 vs UXR-002 Discrepancy (flag for BRD revision):**
> US_040 AC-1 cites `UXR-001` for the breadcrumb requirement. However, `figma_spec.md` defines
> `UXR-001` as "System MUST provide navigation to any feature in max 3 clicks from authenticated
> dashboard" (the click-depth requirement). The breadcrumb requirement is `figma_spec.md UXR-002`:
> "System MUST display clear navigation hierarchy with breadcrumbs for multi-step workflows —
> Breadcrumb component visible on all nested screens — SCR-010, SCR-016, SCR-017, SCR-018,
> SCR-019, SCR-028." The story's Requirement Tags list only `UXR-001, UXR-003, UXR-303` and
> omit `UXR-002`. This task implements breadcrumbs as per UXR-002 intent and maps AC-5
> (click depth) to UXR-001. Recommend correcting US_040 AC-1 reference from `UXR-001` to
> `UXR-002` in a future BRD revision.

---

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | All wireframes in `.propel/context/wireframes/Hi-Fi/`; breadcrumb pattern referenced in `figma_spec.md` section 5 (Navigation Patterns: "Breadcrumbs for workflow depth: Staff Dashboard > Patient Chart > 360-View > Conflict Resolution") and component inventory (C/Navigation/Breadcrumbs — `figma_spec.md` line 780) |
| **Screen Spec** | All authenticated screens. Primary references: `figma_spec.md#SCR-010` (Staff Dashboard), `figma_spec.md#SCR-016` (Patient Chart Review), `figma_spec.md#SCR-017` (360-Degree Patient View), `figma_spec.md#SCR-018` (Conflict Resolution), `figma_spec.md#SCR-019` (Code Verification), `figma_spec.md#SCR-028` (Operational Metrics) — all have breadcrumbs per UXR-002 |
| **UXR Requirements** | UXR-001 (max 3 clicks), UXR-002 (breadcrumbs on nested screens) |
| **Design Tokens** | `designsystem.md#typography` (body/link font Roboto), `designsystem.md#colors` (`primary.dark` `#1565C0` for clickable links per WCAG 4.5:1 contrast), `designsystem.md#spacing` (8px grid — breadcrumb sits below Header at spacing(2) = 16px top padding) |

> **Wireframe Implementation Requirement:**
> MUST reference `figma_spec.md` Secondary Nav pattern: "Tabs for category switching. Breadcrumbs
> for workflow depth (Staff Dashboard > Patient Chart > 360-View > Conflict Resolution)."
> The breadcrumb renders below the `<Header>` and above the page `<main>` content area within
> `AuthenticatedLayout`. Validate breadcrumb rendering at 375px (mobile — may be hidden below
> md breakpoint as bottom nav is primary; but text-only compact form should still render), 768px,
> and 1440px.

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

Implement a `AppBreadcrumbs` component that auto-generates breadcrumb trails from React Router v6
`useMatches()` — each route can declare a `handle.crumb` metadata label. The breadcrumb renders
between the `<Header>` and `<main>` content slot in `AuthenticatedLayout`, visible whenever the
current match depth is greater than one level (AC-1). The component also implements smart
ellipsis collapse (edge case: >4 segments — show first 1 + "..." + last 2, all always showing
root and current page). AC-5 (max 3-click depth) is validated via documentation and navigation-map
analysis — no code change is required since `navigation-map.md` already confirms all P0/P1
features are reachable within 3 sidebar clicks from the dashboard.

---

## Dependent Tasks

- `EP-009-I/us_037/task_001_fe_responsive_navigation_shell.md` — `AuthenticatedLayout` must be
  in place with `<Header>` + `<Outlet>` structure before breadcrumbs can be slotted in.

---

## Impacted Components

| Component | Module | Action |
|-----------|--------|--------|
| `AppBreadcrumbs.tsx` | `client/src/components/navigation/` | CREATE — auto-generates crumbs from `useMatches()` route handles |
| `AuthenticatedLayout.tsx` | `client/src/components/layout/` | MODIFY — render `<AppBreadcrumbs />` between header and main |
| `App.tsx` | `client/src/` | MODIFY — add `handle: { crumb: 'Label' }` to route objects in `createBrowserRouter` |

---

## Implementation Plan

1. **Add `handle.crumb` to routes in `App.tsx`** — React Router v6 route `handle` is a custom
   metadata object; `useMatches()` returns all active matched routes including their `handle`.
   Add a `handle: { crumb: string }` field to each route object:
   ```ts
   { path: '/', element: <AuthenticatedLayout />, handle: { crumb: 'Dashboard' }, children: [
     { path: 'patients', element: <PatientListPage />, handle: { crumb: 'Patients' }, children: [
       { path: ':patientId', element: <PatientChartPage />, handle: { crumb: 'Patient Chart' }, children: [
         { path: '360', element: <PatientView360 />, handle: { crumb: '360° View' } },
         { path: 'conflicts', element: <ConflictResolutionPage />, handle: { crumb: 'Conflict Resolution' } },
       ]},
     ]},
     { path: 'verification', element: <CodeVerificationPage />, handle: { crumb: 'Code Verification' } },
     { path: 'metrics', element: <MetricsDashboardPage />, handle: { crumb: 'Metrics' } },
   ]}
   ```
   The `handle.crumb` is a `string`. Dynamic crumbs (e.g., patient name) can use a function
   `handle: { crumb: (data: RouteData) => string }` in a future iteration; Phase 1 uses
   static labels only.

2. **Create `AppBreadcrumbs.tsx`** — functional component with no props (reads from router context):
   ```ts
   const matches = useMatches() as RouteMatchWithHandle[];
   const crumbs = matches.filter(m => typeof m.handle?.crumb === 'string');
   ```
   - If `crumbs.length <= 1`: return `null` (no breadcrumbs on root/dashboard — AC-1 requires
     breadcrumbs only when "more than one level deep").
   - Map `crumbs` to MUI `<Link>` + `<Typography>` elements for MUI `<Breadcrumbs>` children.
   - Last crumb (current page): `<Typography color="text.primary">` (not a link).
   - Parent crumbs: `<Link component={RouterLink} to={match.pathname} color="inherit" underline="hover">`.
   - **Ellipsis collapse** for `crumbs.length > 4`:
     - Show `crumbs[0]` (root/Dashboard), `<BreadcrumbCollapse>` ("..." Tooltip with full path),
       `crumbs[n-2]`, `crumbs[n-1]` (current).
     - MUI `Breadcrumbs` has built-in `itemsBeforeCollapse` and `itemsAfterCollapse` props:
       `<Breadcrumbs itemsBeforeCollapse={1} itemsAfterCollapse={2} maxItems={4}>` achieves
       this automatically.
   - Accessibility: `<nav aria-label="breadcrumb">` wrapping MUI `<Breadcrumbs>` (MUI renders
     `<ol>` by default); `aria-current="page"` on the last crumb (MUI applies automatically to
     the last item when using `<Breadcrumbs>`).

3. **Add TypeScript type for route handle**:
   ```ts
   interface CrumbHandle { crumb?: string }
   type RouteMatchWithHandle = ReturnType<typeof useMatches>[number] & { handle: CrumbHandle }
   ```
   Exported from `AppBreadcrumbs.tsx` for reuse in `App.tsx` route declarations.

4. **Wire into `AuthenticatedLayout.tsx`**:
   - Import `AppBreadcrumbs`.
   - Render between the `<Header />` and `<Box component="main">` as a sibling (not inside main):
     ```tsx
     <Header ... />
     <AppBreadcrumbs />
     <Box component="main" id="main-content" ...>
       <Suspense fallback={<PageLoadingFallback />}>
         <Outlet />
       </Suspense>
     </Box>
     ```
   - The breadcrumbs `<Box>` uses `sx={{ px: { xs: 2, sm: 3, md: 3 }, py: 1 }}` to match
     content horizontal padding from US_037/task_002.

5. **AC-5 Click Depth Validation** — reference `navigation-map.md` section 4 (Flow Coverage
   Report) and section 2 (Patient/Staff/Admin navigation): all P0 and P1 features are reachable
   via sidebar single-click from the authenticated dashboard. No routing changes are needed.
   Document the depth analysis in a code comment above the route config in `App.tsx`:
   ```ts
   // Click depth audit (AC-5 / UXR-001): all features reachable from dashboard within 3 clicks.
   // Level 1 (1 click): direct sidebar routes — /patients, /verification, /metrics, /admin
   // Level 2 (2 clicks): sub-pages — /patients/:id, /patients/:id/360
   // Level 3 (3 clicks): deep screens — /patients/:id/360/conflicts (max depth = 3)
   // Source: navigation-map.md section 2 & 4 (all flows confirmed ≤ 3 navigation steps)
   ```

6. **Mobile behaviour**: On `xs`/`sm` breakpoints (bottom nav, no sidebar), the breadcrumb
   still renders but is compact. Use `sx={{ display: { xs: 'block', md: 'block' } }}` — always
   visible. MUI `Breadcrumbs` component handles wrapping gracefully on narrow viewports via
   `maxItems` collapsing. On mobile, the breadcrumb is the primary wayfinding aid since the
   sidebar is hidden.

7. **`primary.dark` for breadcrumb links** — apply `color="primary.dark"` (`#1565C0`, 5.9:1
   contrast on white) to breadcrumb `<Link>` items, consistent with the existing theme override
   for `MuiLink` set in US_036 (`healthcare-theme.ts`). No additional theme change needed.

---

## Current Project State

```
client/src/
├── App.tsx                             ← MODIFY (add handle.crumb to routes)
└── components/
    ├── layout/
    │   └── AuthenticatedLayout.tsx     ← MODIFY (render AppBreadcrumbs)
    └── navigation/                     ← CREATE (new folder)
        └── AppBreadcrumbs.tsx
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `client/src/components/navigation/AppBreadcrumbs.tsx` | Auto-breadcrumb from `useMatches()`; MUI `<Breadcrumbs maxItems={4} itemsBeforeCollapse={1} itemsAfterCollapse={2}`>; `aria-label="breadcrumb"` nav wrapper; returns `null` at depth ≤ 1 |
| MODIFY | `client/src/App.tsx` | Add `handle: { crumb: string }` to each route object; add click-depth audit comment |
| MODIFY | `client/src/components/layout/AuthenticatedLayout.tsx` | Import and render `<AppBreadcrumbs />` between Header and main Box |

---

## External References

- [React Router v6 — useMatches() and route handle for breadcrumbs](https://reactrouter.com/en/6.28.0/hooks/use-matches)
- [MUI Breadcrumbs API — maxItems, itemsBeforeCollapse, itemsAfterCollapse (MUI v5)](https://mui.com/material-ui/react-breadcrumbs/)
- [MUI Link API — component prop for RouterLink integration (MUI v5)](https://mui.com/material-ui/react-link/)
- [WCAG 2.4.8 Location — breadcrumbs as current page indicator](https://www.w3.org/WAI/WCAG22/Understanding/location.html)
- [ARIA — aria-label="breadcrumb" on nav, aria-current="page" on last item](https://www.w3.org/WAI/ARIA/apg/patterns/breadcrumb/)
- [UXR-002 figma_spec.md — breadcrumb screens: SCR-010, SCR-016, SCR-017, SCR-018, SCR-019, SCR-028](d:\Propal IQ\Appontment Booking and Clinical Intell Platform\PROPELIQ_APPOINTMENT_BOOKING\.propel\context\docs\figma_spec.md)
- [navigation-map.md section 4 — click depth audit evidence for AC-5](d:\Propal IQ\Appontment Booking and Clinical Intell Platform\PROPELIQ_APPOINTMENT_BOOKING\.propel\context\wireframes\navigation-map.md)

---

## Build Commands

```bash
cd client
npm run dev     # Navigate to nested routes; verify breadcrumbs appear at depth > 1
npm run build   # Confirm no TypeScript errors on RouteMatchWithHandle typing
npm run lint    # Confirm no a11y warnings on breadcrumb nav landmark
```

---

## Implementation Validation Strategy

- [ ] Unit tests pass
- [ ] Integration tests pass (if applicable)
- [ ] **[UI Tasks]** Visual comparison against wireframe completed at 375px, 768px, 1440px
- [ ] **[UI Tasks]** Run `/analyze-ux` to validate wireframe alignment
- [ ] Breadcrumbs render at `/patients/123` (depth 2) — shows "Dashboard > Patient Chart"
- [ ] Breadcrumbs do NOT render at `/` or `/verification` (depth 1 — only one route handle match)
- [ ] "Dashboard" breadcrumb link navigates to `/` on click
- [ ] Last crumb is plain `Typography` (not a link), current page correct text
- [ ] At depth > 4 (e.g., `/patients/123/360/conflicts`), MUI collapse fires: shows "Dashboard > ... > 360° View > Conflict Resolution"
- [ ] `aria-label="breadcrumb"` present on `<nav>` wrapper; `aria-current="page"` on last item
- [ ] Breadcrumb links use `primary.dark` (#1565C0) colour — WCAG 4.5:1 pass
- [ ] AC-5: add click depth audit comment to `App.tsx` route config documenting max depth ≤ 3

---

## Implementation Checklist

- [ ] **1.** Define `CrumbHandle` interface and `RouteMatchWithHandle` type in `AppBreadcrumbs.tsx`; export for reuse in `App.tsx`
- [ ] **2.** Create `AppBreadcrumbs.tsx`: `useMatches()` filtered to `handle.crumb` entries; return `null` when ≤ 1 crumb; wrap in `<Box component="nav" aria-label="breadcrumb">`; use MUI `<Breadcrumbs maxItems={4} itemsBeforeCollapse={1} itemsAfterCollapse={2}>`; render parent crumbs as `<Link component={RouterLink} color="primary.dark">` and last crumb as `<Typography color="text.primary">`
- [ ] **3.** Modify `App.tsx`: add `handle: { crumb: 'Label' }` to root `/` route and all nested route objects; add click-depth audit comment above route config
- [ ] **4.** Modify `AuthenticatedLayout.tsx`: import `AppBreadcrumbs`; render after `<Header />` and before `<Box component="main">`; apply `sx={{ px: { xs: 2, sm: 3, md: 3 }, py: 1 }}` on breadcrumb container Box
- [ ] **5.** Test ellipsis collapse: add a temporary 5-level deep route; verify `<BreadcrumbCollapsed>` renders with expandable full path on click; remove temporary route after test
- [ ] **6.** Verify `aria-label="breadcrumb"` on wrapper nav and `aria-current="page"` on last crumb using browser DevTools Accessibility panel
- [ ] **7.** Add navigation-map click depth audit comment to `App.tsx` confirming AC-5 compliance with reference to `navigation-map.md`
- [ ] **[UI Tasks - MANDATORY]** Reference `figma_spec.md` section 5 Secondary Nav pattern ("Breadcrumbs for workflow depth") and C/Navigation/Breadcrumbs component spec during implementation
- [ ] **[UI Tasks - MANDATORY]** Validate breadcrumb renders below Header and above main content at 375px, 768px, 1440px before marking task complete

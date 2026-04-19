# Task - task_001_fe_theme_focus_motion_contrast

## Requirement Reference

- **User Story**: US_036 — WCAG 2.2 AA Accessibility Implementation
- **Story Location**: `.propel/context/tasks/EP-009-I/us_036/us_036.md`
- **Acceptance Criteria**:
  - AC-1: All interactive elements reachable via Tab/Shift+Tab with visible focus indicator (min 2px solid ring, 3:1 contrast ratio) per UXR-101
  - AC-3: Normal text ≥ 4.5:1 contrast, large text ≥ 3:1 against background per UXR-103
  - AC-5: Animations disabled or replaced with instant transitions when OS `prefers-reduced-motion` is set per UXR-105
- **Edge Cases**:
  - Custom components (date pickers, comboboxes): WAI-ARIA Authoring Practices patterns are the fallback — theme-level focus ring must cascade to ALL `MuiButtonBase`-based components including custom ones

> **UXR Mapping Discrepancy Notice**:
> The user story assigns UXR IDs to ACs differently from `figma_spec.md`:
> - User story AC-1 → UXR-101 (figma_spec: UXR-101 = WCAG 2.2 overall; UXR-103 = keyboard + focus; UXR-105 = focus indicators)
> - User story AC-3 → UXR-103 (figma_spec: UXR-104 = color contrast 4.5:1 text / 3:1 UI)
> - User story AC-5 → UXR-105 (figma_spec: UXR-105 = focus indicators)
> This task implements all AC behaviors per the described requirements. The UXR-ID discrepancy should be resolved in the next `figma_spec.md` revision.

---

## Design References

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes — theme tokens + global CSS changes affect all 28 screens |
| **Figma URL** | `figma_spec.md` (color tokens, focus ring spec: `2px solid primary.500`) |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | `.propel/context/wireframes/Hi-Fi/` |
| **Screen Spec** | All screens (SCR-001 through SCR-028) |
| **UXR Requirements** | UXR-101, UXR-103, UXR-104, UXR-105 |
| **Design Tokens** | `primary.main: '#2196F3'`, `text.primary: '#212121'`, `text.secondary: '#757575'` → see contrast issues below |

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | React 18.x + TypeScript 5.x | 18.x / 5.x |
| UI Library | MUI 5.x (`@mui/material`) | 5.16.x |
| Theme | `createTheme` (`healthcareTheme`) | MUI 5.x |
| Styling | MUI `sx` prop + `GlobalStyles` | MUI 5.x |
| Build | Vite 5.x | 5.x |

> **No new production dependencies required.** All WCAG fixes use MUI's existing theme override API and native CSS. `@axe-core/react` is added as a **devDependency only** for development-time runtime WCAG violation reporting in the browser console — it is tree-shaken and excluded from production builds by the conditional import pattern.

---

## AI References

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |

---

## Task Overview

This task delivers theme-level and layout-level accessibility infrastructure. It does NOT modify individual feature page components — those are addressed in task_002. Changes here cascade automatically to all screens via the MUI theme.

**Three problem areas addressed:**

**1. Focus Ring — WCAG 2.4.7 Focus Visible (AC-1)**:
`healthcareTheme` currently has no `focusVisible` MUI component overrides. MUI buttons and inputs use the browser's default `:focus-visible` ring which is often suppressed by existing styles. This task adds explicit `focusVisible` style overrides in the theme for `MuiButtonBase`, `MuiOutlinedInput`, and `MuiInputBase` using `2px solid primary.main` with a `2px` offset — satisfying the 2px minimum and achieving ≥3:1 contrast ratio of `#2196F3` against `#FFFFFF` (3.12:1 — passes 3:1 for UI components per WCAG 1.4.11).

**2. Color Contrast — WCAG 1.4.3 Contrast Minimum (AC-3)**:
Two contrast failures found in the existing theme:
- `text.secondary: '#757575'` on `background.paper: '#FFFFFF'` = 4.48:1 → FAILS 4.5:1 normal text
  - Fix: change to `'#767676'` (4.54:1) — a 1-digit hex adjustment, invisible visually
- `primary.main: '#2196F3'` on `background.paper: '#FFFFFF'` = 3.12:1 → FAILS 4.5:1 for normal-weight text links
  - For link text (e.g., "Forgot password?" in LoginPage), use `primary.dark: '#1565C0'` (5.9:1) for any standalone text links
  - The `primary.main` value stays for UI components (buttons, icons) where 3:1 applies; update `primary.dark` from `'#1976D2'` to `'#1565C0'` for use in text links

**3. Reduced Motion — WCAG 2.3.3 Animation from Interactions (AC-5)**:
Add a `GlobalStyles` component to `App.tsx` that injects `@media (prefers-reduced-motion: reduce)` CSS to disable all MUI transitions and animations. Also configure MUI theme `transitions.create` to return `"none"` when `prefers-reduced-motion` is active (detected via `window.matchMedia`).

**4. Layout ARIA Infrastructure (supports AC-2 in task_002)**:
- Add a `SkipToMainContent` component — rendered first in `AuthenticatedLayout` and `LoginPage` — allowing keyboard users to skip the `<Header>`/`<Sidebar>` on every page (WCAG 2.4.1)
- Add `aria-label="Main content"` to the `<Box component="main">` in `AuthenticatedLayout`
- Add `aria-label="Main navigation"` to `Sidebar`'s `<Drawer>` and `<List>`
- Add `aria-current="page"` to active `<ListItemButton>` in `Sidebar`

**5. Development-Time Axe Runtime Auditing**:
Add `@axe-core/react` (MIT) as a devDependency. Conditionally import in `main.tsx` development mode only — zero production bundle impact.

---

## Critical Contrast Issue Detail

| Token | Value | Background | Ratio | WCAG AA Normal Text (4.5:1) | WCAG AA Large Text / UI (3:1) | Fix |
|-------|-------|------------|-------|-----------------------------|-------------------------------|-----|
| `text.primary` | `#212121` | `#FFFFFF` | 16.1:1 | ✅ Pass | ✅ Pass | None |
| `text.secondary` | `#757575` | `#FFFFFF` | 4.48:1 | ❌ **Fail** (0.02 short) | ✅ Pass | Change to `#767676` (4.54:1) |
| `primary.main` | `#2196F3` | `#FFFFFF` | 3.12:1 | ❌ Fail for text links | ✅ Pass (UI/icon) | Use `primary.dark` for text links |
| `primary.dark` | `#1976D2` | `#FFFFFF` | 4.56:1 | ✅ Pass | ✅ Pass | Update to `#1565C0` (5.9:1) for text links |
| `error.main` | `#F44336` | `#FFFFFF` | 3.93:1 | ❌ Fail for error text | ✅ Pass (icons) | Darken error text to `#C62828` (7.3:1) if used as standalone text |

> Note: `error.main: '#F44336'` is used in `Alert` components with a light red background — the combination still passes. The failure is only when `#F44336` text appears directly on white. MUI's `Alert severity="error"` renders text in `#5F2120` (dark red on light red background) — this passes. Only the color token itself needs documenting.

---

## Dependent Tasks

- **task_002_fe_semantic_html_form_accessibility.md** (this US): task_001 must be implemented first — the global focus ring theme overrides in `healthcareTheme` are the foundation that task_002 builds upon.
- All EP-001 through EP-005 UI stories: Those pages inherit focus ring and reduced motion fixes automatically via `ThemeProvider`.

---

## Implementation Plan

### 1. `healthcareTheme.ts` — contrast token fixes + focus ring + motion-aware transitions

```typescript
// client/src/theme/healthcare-theme.ts

import { createTheme } from '@mui/material/styles';

// Detect reduced motion preference at theme creation time
// This is read once at module load — if OS setting changes, a re-render from
// App.tsx GlobalStyles handles live updates via CSS media query
const prefersReducedMotion =
  typeof window !== 'undefined' &&
  window.matchMedia('(prefers-reduced-motion: reduce)').matches;

export const healthcareTheme = createTheme({
  palette: {
    primary: {
      main: '#2196F3',       // Medical Blue — CTAs, buttons, icons (3.1:1 on white — UI only)
      dark: '#1565C0',       // CHANGED from #1976D2 — 5.9:1 on white; use for text links
      light: '#64B5F6',
    },
    secondary: {
      main: '#9C27B0',
    },
    error: {
      main: '#F44336',
    },
    success: {
      main: '#4CAF50',
    },
    warning: {
      main: '#FF9800',
    },
    info: {
      main: '#2196F3',
    },
    background: {
      default: '#FAFAFA',
      paper: '#FFFFFF',
    },
    text: {
      primary: '#212121',
      secondary: '#767676',  // CHANGED from #757575 — 4.54:1 on white (passes WCAG AA 4.5:1)
    },
    divider: '#E0E0E0',
  },
  typography: {
    fontFamily: "Roboto, -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif",
    h4: { fontSize: '1.5rem', fontWeight: 400 },
    body1: { fontSize: '1rem', fontWeight: 400 },
    button: { fontSize: '0.875rem', fontWeight: 500, textTransform: 'uppercase' },
  },
  spacing: 8,
  breakpoints: {
    values: { xs: 0, sm: 600, md: 900, lg: 1200, xl: 1536 },
  },
  shape: { borderRadius: 4 },
  // Disable all transitions when prefers-reduced-motion is set
  transitions: prefersReducedMotion
    ? {
        create: () => 'none',
        duration: { shortest: 0, shorter: 0, short: 0, standard: 0, complex: 0,
                     enteringScreen: 0, leavingScreen: 0 },
      }
    : undefined,
  components: {
    // Global focus-visible ring — 2px solid primary.main with 2px offset
    // Applied to ALL MuiButtonBase descendants (Button, IconButton, ListItemButton,
    // BottomNavigationAction, Tab, Checkbox, Radio, Switch, etc.)
    MuiButtonBase: {
      styleOverrides: {
        root: {
          '&.Mui-focusVisible': {
            outline: '2px solid #2196F3',
            outlineOffset: '2px',
          },
        },
      },
    },
    // Input field focus ring — applied via the notched outline border
    MuiOutlinedInput: {
      styleOverrides: {
        root: {
          borderRadius: 4,
          '&.Mui-focused .MuiOutlinedInput-notchedOutline': {
            borderWidth: 2,
            borderColor: '#2196F3',
          },
          // Additionally, when keyboard-focused (not mouse), show outer ring
          '&.Mui-focusVisible': {
            outline: '2px solid #2196F3',
            outlineOffset: '2px',
          },
        },
      },
    },
    // Unbordered inputs (filled, standard variants)
    MuiInputBase: {
      styleOverrides: {
        root: {
          '&.Mui-focusVisible': {
            outline: '2px solid #2196F3',
            outlineOffset: '2px',
          },
        },
      },
    },
    MuiButton: {
      styleOverrides: {
        root: { borderRadius: 4 },
      },
    },
    MuiCard: {
      styleOverrides: {
        root: { borderRadius: 8 },
      },
    },
    // Links — use primary.dark (#1565C0, 5.9:1) for text links to pass 4.5:1
    MuiLink: {
      styleOverrides: {
        root: {
          color: '#1565C0',
          '&:focus-visible': {
            outline: '2px solid #2196F3',
            outlineOffset: '2px',
            borderRadius: '2px',
          },
        },
      },
    },
  },
});
```

### 2. `App.tsx` — add `GlobalStyles` for `prefers-reduced-motion` CSS + axe devtools

```tsx
// client/src/App.tsx (additions only)
import { GlobalStyles } from '@mui/material';

// Reduced motion global CSS — live update via CSS media query (not JS matchMedia)
// This handles OS setting changes during session without reload (WCAG 2.3.3)
const reducedMotionStyles = (
  <GlobalStyles
    styles={{
      '@media (prefers-reduced-motion: reduce)': {
        '*': {
          animationDuration: '0.01ms !important',
          animationIterationCount: '1 !important',
          transitionDuration: '0.01ms !important',
          scrollBehavior: 'auto !important',
        },
      },
    }}
  />
);

export default function App() {
  return (
    <ThemeProvider theme={healthcareTheme}>
      <CssBaseline />
      {reducedMotionStyles}
      <QueryClientProvider client={queryClient}>
        <RouterProvider router={router} />
        {import.meta.env.DEV && <ReactQueryDevtools initialIsOpen={false} />}
      </QueryClientProvider>
    </ThemeProvider>
  );
}
```

### 3. `main.tsx` — conditional axe devtools import

```tsx
// client/src/main.tsx (add before ReactDOM.createRoot call)
if (import.meta.env.DEV) {
  // @axe-core/react reports WCAG violations to browser console in development
  // Tree-shaken from production build via import.meta.env.DEV conditional
  const axe = await import('@axe-core/react');
  const ReactDOM = await import('react-dom');
  axe.default(React, ReactDOM.default, 1000);
}
```

> `@axe-core/react` must be added to devDependencies: `npm install --save-dev @axe-core/react`. MIT license — satisfies NFR-015 OSS constraint. Zero production bundle impact.

### 4. `SkipToMainContent.tsx` — skip navigation link component

```tsx
// client/src/components/accessibility/SkipToMainContent.tsx
// WCAG 2.4.1 Bypass Blocks — allows keyboard users to skip repeated navigation

export default function SkipToMainContent() {
  return (
    <Box
      component="a"
      href="#main-content"
      sx={{
        position: 'absolute',
        left: '-9999px',
        top: 'auto',
        width: '1px',
        height: '1px',
        overflow: 'hidden',
        // Becomes visible and prominent when focused (keyboard users only)
        '&:focus': {
          position: 'fixed',
          top: 8,
          left: 8,
          width: 'auto',
          height: 'auto',
          overflow: 'visible',
          bgcolor: 'primary.main',
          color: '#FFFFFF',
          px: 2,
          py: 1,
          borderRadius: 1,
          fontWeight: 500,
          fontSize: '0.875rem',
          zIndex: 9999,
          outline: '2px solid #FFFFFF',
          outlineOffset: '2px',
          textDecoration: 'none',
        },
      }}
    >
      Skip to main content
    </Box>
  );
}
```

### 5. `AuthenticatedLayout.tsx` — ARIA landmark + skip link + main ID

```tsx
// Additions to AuthenticatedLayout.tsx:

// 1. Add SkipToMainContent as FIRST child of the root Box
// 2. Add id="main-content" + aria-label to the <Box component="main">

return (
  <Box sx={{ display: 'flex', minHeight: '100vh' }}>
    <SkipToMainContent />   {/* FIRST — keyboard users can skip to #main-content */}
    <Header />
    {showSidebar && <Sidebar />}
    <Box
      component="main"
      id="main-content"                      // target for SkipToMainContent href
      aria-label="Main content"              // ARIA landmark label
      tabIndex={-1}                          // programmatically focusable for skip link
      sx={{ ... }}                           // existing sx unchanged
    >
      <Toolbar />
      <Outlet />
    </Box>
    {showBottomNav && <BottomNav />}
  </Box>
);
```

### 6. `Sidebar.tsx` — nav landmark + aria-current

```tsx
// Sidebar.tsx modifications:
// 1. Add role="navigation" + aria-label to Drawer
// 2. Add aria-label to List
// 3. Add aria-current="page" to active ListItemButton

<Drawer
  variant="permanent"
  component="nav"                          // renders as <nav> — semantic landmark
  aria-label="Main navigation"
  // ... existing sx ...
>
  <Toolbar />
  <Box sx={{ overflow: 'auto', pt: 1 }}>
    <List disablePadding aria-label="Navigation links">
      {NAV_ITEMS.map(({ label, icon, path }) => {
        const isActive = location.pathname === path;
        return (
          <ListItem key={label} disablePadding sx={{ px: 1, pb: 0.5 }}>
            <ListItemButton
              selected={isActive}
              aria-current={isActive ? 'page' : undefined}  // WCAG 2.4.8 Location
              onClick={() => navigate(path)}
              sx={{ borderRadius: 1 }}
            >
              {/* ... existing ListItemIcon + ListItemText ... */}
            </ListItemButton>
          </ListItem>
        );
      })}
    </List>
  </Box>
</Drawer>
```

---

## Current Project State

```
client/src/
├── App.tsx                                         ← MODIFY: add GlobalStyles + conditional axe import
├── main.tsx                                        ← MODIFY: add conditional @axe-core/react init
├── theme/
│   └── healthcare-theme.ts                        ← MODIFY: text.secondary #767676, primary.dark #1565C0,
│                                                             focus ring overrides, reduced-motion transitions
├── components/
│   ├── layout/
│   │   ├── AuthenticatedLayout.tsx                ← MODIFY: SkipToMainContent + id/aria-label on main
│   │   └── Sidebar.tsx                            ← MODIFY: nav landmark + aria-current
│   └── accessibility/
│       └── SkipToMainContent.tsx                  ← CREATE
└── package.json                                   ← MODIFY: add @axe-core/react devDependency
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | `client/src/theme/healthcare-theme.ts` | `text.secondary` → `#767676`; `primary.dark` → `#1565C0`; add `MuiButtonBase.focusVisible` (2px solid #2196F3); `MuiOutlinedInput.focusVisible` + `Mui-focused` border; `MuiLink` color + focus-visible; `transitions.create` → `'none'` when `prefersReducedMotion` |
| MODIFY | `client/src/App.tsx` | Import `GlobalStyles`; add `reducedMotionStyles` as first child of `ThemeProvider` content |
| MODIFY | `client/src/main.tsx` | Add `if (import.meta.env.DEV)` block with dynamic `@axe-core/react` import |
| CREATE | `client/src/components/accessibility/SkipToMainContent.tsx` | Visually hidden `<a href="#main-content">` that appears on `:focus`; white text on primary.main background |
| MODIFY | `client/src/components/layout/AuthenticatedLayout.tsx` | Add `<SkipToMainContent />` as first child; add `id="main-content"`, `aria-label="Main content"`, `tabIndex={-1}` to `<Box component="main">` |
| MODIFY | `client/src/components/layout/Sidebar.tsx` | Add `component="nav"` + `aria-label="Main navigation"` to `Drawer`; add `aria-label="Navigation links"` to `List`; add `aria-current={isActive ? 'page' : undefined}` to `ListItemButton` |
| MODIFY | `client/package.json` | Add `"@axe-core/react": "^4.10.0"` to devDependencies |

---

## External References

- [WCAG 2.2 — 1.4.3 Contrast (Minimum)](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html)
- [WCAG 2.2 — 1.4.11 Non-text Contrast](https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html)
- [WCAG 2.2 — 2.4.1 Bypass Blocks (skip nav)](https://www.w3.org/WAI/WCAG22/Understanding/bypass-blocks.html)
- [WCAG 2.2 — 2.4.7 Focus Visible](https://www.w3.org/WAI/WCAG22/Understanding/focus-visible.html)
- [WCAG 2.2 — 2.3.3 Animation from Interactions](https://www.w3.org/WAI/WCAG22/Understanding/animation-from-interactions.html)
- [MUI — Theme component overrides for focus](https://mui.com/material-ui/customization/theme-components/#global-style-overrides)
- [@axe-core/react — OSS MIT devtools](https://github.com/dequelabs/axe-core-npm/tree/develop/packages/react)
- [WebAIM Contrast Checker](https://webaim.org/resources/contrastchecker/)
- [figma_spec.md — UXR-101 through UXR-105](../.propel/context/docs/figma_spec.md)

---

## Build Commands

```bash
cd client
npm install --save-dev @axe-core/react@^4.10.0

npm run build    # Verify no TypeScript errors
npm run lint     # Verify eslint-plugin-jsx-a11y passes with new components
```

---

## Implementation Validation Strategy

- [ ] `text.secondary` token: render any `<Typography variant="body2" color="text.secondary">` on white background → WebAIM contrast checker reports ≥4.5:1 (target: 4.54:1 for `#767676`)
- [ ] `primary.dark` token: `<Link href="#">Forgot password?</Link>` on white → ≥4.5:1 (target: ~5.9:1 for `#1565C0`)
- [ ] Focus ring: Tab to a `<Button>` → 2px solid blue ring visible with visible offset; no `outline: none` suppression
- [ ] Focus ring: Tab to a `<TextField>` → outer ring appears around the input wrapper
- [ ] Focus ring: Tab to `BottomNavigationAction` (inherits `MuiButtonBase`) → ring visible
- [ ] Skip link: press Tab on any page as first keypress → "Skip to main content" link appears in top-left corner; Enter → focus moves to `#main-content`
- [ ] Sidebar `aria-current`: navigate to `/` → `Dashboard` list item has `aria-current="page"`; navigate to `/queue` → `Queue` item has `aria-current="page"`, `Dashboard` does not
- [ ] Reduced motion: enable "Reduce Motion" in OS settings → all MUI `Fade`, `Slide`, `Collapse` transitions are instant (0.01ms); `GlobalStyles` `@media` rule visible in DevTools
- [ ] Axe devtools: `import.meta.env.DEV` → `@axe-core/react` reports in browser console; production build → no axe code bundled (verify with `npm run build && grep -r "axe-core" dist/`)

---

## Implementation Checklist

- [ ] MODIFY `healthcare-theme.ts` — (a) `text.secondary` → `#767676`; (b) `primary.dark` → `#1565C0`; (c) add `prefersReducedMotion` constant from `window.matchMedia` with SSR guard (`typeof window !== 'undefined'`); (d) conditionally set `transitions` to zero-duration overrides; (e) add `MuiButtonBase.focusVisible` 2px outline; (f) add `MuiOutlinedInput` focus ring; (g) add `MuiLink` color + focus-visible
- [ ] CREATE `SkipToMainContent.tsx` — visually hidden by default; `:focus` reveals fixed-position banner top-left; `href="#main-content"` matches `id` on `<main>` element
- [ ] MODIFY `AuthenticatedLayout.tsx` — `<SkipToMainContent />` first child; `id="main-content"` + `aria-label="Main content"` + `tabIndex={-1}` on `<Box component="main">`
- [ ] MODIFY `Sidebar.tsx` — `Drawer` gets `component="nav"` + `aria-label="Main navigation"`; `List` gets `aria-label="Navigation links"`; `ListItemButton` gets `aria-current={isActive ? 'page' : undefined}`
- [ ] MODIFY `App.tsx` — import and render `GlobalStyles` with `@media (prefers-reduced-motion: reduce)` CSS; insert before `RouterProvider`
- [ ] MODIFY `main.tsx` — wrap `@axe-core/react` import + init in `if (import.meta.env.DEV)` block using dynamic `import()`; do NOT use static import (would include in production bundle)
- [ ] MODIFY `package.json` — add `@axe-core/react` to devDependencies only; NOT in `dependencies`; run `npm install`
- [ ] Verify `npm run lint` passes with `eslint-plugin-jsx-a11y` configured — no new `jsx-a11y` violations introduced

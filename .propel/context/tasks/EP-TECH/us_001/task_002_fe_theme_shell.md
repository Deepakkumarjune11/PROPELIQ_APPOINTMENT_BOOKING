# Task - task_002_fe_theme_shell

## Requirement Reference

- User Story: us_001
- Story Location: .propel/context/tasks/EP-TECH/us_001/us_001.md
- Acceptance Criteria:
  - AC-2: **Given** the React project is scaffolded, **When** inspecting the project configuration, **Then** Material-UI v5 component library is installed and a healthcare-appropriate theme provider wraps the application root.
  - AC-3: **Given** the frontend project structure exists, **When** reviewing the state management setup, **Then** React Query is configured for server state caching and Zustand is configured for client-side state management.
  - AC-4: **Given** the application shell is rendered, **When** navigating to the root URL, **Then** a login shell (SCR-024) and header/navigation shell (SCR-025) are rendered with placeholder content.
- Edge Cases:
  - How does the system handle missing environment variables? (Application displays configuration checklist on startup if required environment variables are missing)

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | .propel/context/wireframes/Hi-Fi/wireframe-SCR-024-login.html, .propel/context/wireframes/Hi-Fi/wireframe-SCR-025-header-navigation.html |
| **Screen Spec** | figma_spec.md#SCR-024, figma_spec.md#SCR-025 |
| **UXR Requirements** | UXR-203 (adaptive navigation: bottom nav mobile, sidebar desktop) |
| **Design Tokens** | designsystem.md#colors, designsystem.md#typography, designsystem.md#spacing, designsystem.md#breakpoints, designsystem.md#elevation, designsystem.md#component-specifications |

> **Wireframe Status Legend:**
> - **AVAILABLE**: Local file exists at specified path

### **CRITICAL: Wireframe Implementation Requirement (UI Tasks Only)**

**IF Wireframe Status = AVAILABLE or EXTERNAL:**
- **MUST** open and reference the wireframe file/URL during UI implementation
- **MUST** match layout, spacing, typography, and colors from the wireframe
- **MUST** implement all states shown in wireframe (default, hover, focus, error, loading)
- **MUST** validate implementation against wireframe at breakpoints: 375px, 768px, 1440px
- Run `/analyze-ux` after implementation to verify pixel-perfect alignment

### Screen State Requirements (from figma_spec.md)

| Screen | Default | Loading | Empty | Error | Validation |
|--------|---------|---------|-------|-------|------------|
| SCR-024 Login | Yes | Yes | N/A | Yes | Yes |
| SCR-025 Header/Navigation | Yes | N/A | N/A | N/A | N/A |

### Component Inventory (from figma_spec.md)

| Screen | Components | Details |
|--------|------------|---------|
| SCR-024 | TextField (2), Button (2), Link (1), Alert, Checkbox | Email/password fields, login button, "Forgot password?" link, remember me checkbox |
| SCR-025 | Header, Sidebar (desktop), BottomNav (mobile), Avatar, Badge, Dropdown | Responsive navigation shell, logo, user avatar dropdown (profile/settings/logout) |

### Design Token Summary (from designsystem.md)

```yaml
colors:
  primary.500: "#2196F3"  # Medical Blue - CTAs, active states, links
  primary.700: "#1976D2"  # Hover states
  error.main: "#F44336"   # Validation errors
  success.main: "#4CAF50" # Success states
  neutral.50: "#FAFAFA"   # App background
  neutral.300: "#E0E0E0"  # Borders
  neutral.600: "#757575"  # Body text
  neutral.900: "#212121"  # High contrast text

typography:
  fontFamily: "Roboto, -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif"
  h4: { size: "1.5rem", weight: 400 }    # Login card title
  body1: { size: "1rem", weight: 400 }    # Form labels
  button: { size: "0.875rem", weight: 500, transform: "uppercase" }

spacing:
  base: 8px
  card_padding: 48px (spacing-6)
  form_field_gap: 24px (spacing-3)
  button_gap: 16px (spacing-2)

breakpoints:
  xs: 0, sm: 600px, md: 900px, lg: 1200px, xl: 1536px

elevation:
  card: "0px 2px 1px -1px rgba(0,0,0,0.2), 0px 1px 1px 0px rgba(0,0,0,0.14), 0px 1px 3px 0px rgba(0,0,0,0.12)"

borderRadius:
  button: 4px (small)
  card: 8px (medium)
  textField: 4px (small)
  avatar: 9999px (full/circle)
```

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | React | 18.x |
| UI Components | Material-UI (MUI) | 5.x |
| State Management (Server) | @tanstack/react-query | 4.x |
| State Management (Client) | Zustand | 4.x |
| Routing | React Router | 6.x |
| Build Tool | Vite | 5.x |
| Language | TypeScript | 5.x |

**Note**: All code, and libraries, MUST be compatible with versions above.

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |
| **AIR Requirements** | N/A |
| **AI Pattern** | N/A |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | N/A |
| **Model Provider** | N/A |

## Mobile References (Mobile Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **Mobile Impact** | No |
| **Platform Target** | N/A |
| **Min OS Version** | N/A |
| **Mobile Framework** | N/A |

## Task Overview

Install Material-UI v5 with a healthcare-appropriate custom theme derived from the design system tokens, configure React Query for server state caching and Zustand for client-side state management, set up React Router v6 routing, and implement the Login page shell (SCR-024) and Header/Navigation shell (SCR-025) as placeholder UI matching the Hi-Fi wireframes. This task transforms the bare Vite scaffold into a themed, routed application with state management infrastructure and two functional screen shells.

## Dependent Tasks

- task_001_fe_project_scaffolding — React project must be initialized with Vite, TypeScript, and directory structure before UI work begins

## Impacted Components

- **NEW** `client/src/theme/healthcare-theme.ts` — Custom MUI theme with design tokens
- **NEW** `client/src/stores/auth-store.ts` — Zustand auth store (user, isAuthenticated, login/logout)
- **NEW** `client/src/pages/LoginPage.tsx` — Login page shell matching SCR-024 wireframe
- **NEW** `client/src/components/layout/Header.tsx` — Application header with logo and user menu
- **NEW** `client/src/components/layout/Sidebar.tsx` — Desktop sidebar navigation
- **NEW** `client/src/components/layout/BottomNav.tsx` — Mobile bottom navigation
- **NEW** `client/src/components/layout/AuthenticatedLayout.tsx` — Layout wrapper for authenticated routes
- **NEW** `client/src/App.tsx` — Root component with routing, ThemeProvider, QueryClientProvider
- **MODIFY** `client/src/main.tsx` — Update to render App component
- **MODIFY** `client/package.json` — Add MUI, React Query, Zustand, React Router dependencies

## Implementation Plan

1. **Install MUI dependencies**: Add `@mui/material@5`, `@mui/icons-material@5`, `@emotion/react`, `@emotion/styled` to package.json. Add `@fontsource/roboto` for self-hosted Roboto font
2. **Create healthcare theme**: Build `healthcare-theme.ts` using MUI's `createTheme()` with palette (primary #2196F3, secondary #9C27B0, error #F44336, success #4CAF50, warning #FF9800, info #2196F3), typography (Roboto, type scale from designsystem.md), spacing (8px base), breakpoints (xs/sm/md/lg/xl), shape (borderRadius 4px), and component overrides
3. **Configure application root**: Update `main.tsx` to import Roboto font weights (300,400,500,700) and Material Icons. Create `App.tsx` wrapping children in `ThemeProvider` → `CssBaseline` → `QueryClientProvider` → `RouterProvider`
4. **Set up React Query**: Install `@tanstack/react-query@4`. Create `QueryClient` with sensible defaults (`staleTime: 5 * 60 * 1000`, `retry: 1`, `refetchOnWindowFocus: false`)
5. **Set up Zustand**: Install `zustand@4`. Create `auth-store.ts` with `user: null | UserProfile`, `isAuthenticated: boolean`, `login(user)`, `logout()` actions
6. **Configure React Router v6**: Install `react-router-dom@6`. Define routes: `/login` → `LoginPage`, `/` → `AuthenticatedLayout` (with nested child routes and redirect logic)
7. **Implement Login shell (SCR-024)**: Build `LoginPage.tsx` matching wireframe — centered card (max-width 400px), logo/title "PropelIQ Healthcare", email TextField, password TextField, "Remember me" Checkbox, "Login" Button (primary, full-width), "Forgot password?" Link. Implement states: Default, Loading (button spinner), Error (Alert banner), Validation (field-level error messages)
8. **Implement Header/Navigation shell (SCR-025)**: Build responsive navigation — desktop: `Header` (logo, nav links, avatar dropdown) + `Sidebar` (persistent, nav items with icons). Mobile (< 900px): `Header` (logo, avatar) + `BottomNav` (bottom tab bar with icons). Avatar dropdown: Profile, Settings, Logout. Wrap in `AuthenticatedLayout` that combines Header + Sidebar/BottomNav + content outlet

## Current Project State

```
PropelIQ-Stub-Copilot/
├── client/                          # Created by task_001
│   ├── .eslintrc.cjs
│   ├── .prettierrc
│   ├── .nvmrc
│   ├── .env.example
│   ├── index.html
│   ├── package.json
│   ├── tsconfig.json
│   ├── tsconfig.node.json
│   ├── vite.config.ts
│   └── src/
│       ├── main.tsx
│       ├── vite-env.d.ts
│       ├── components/
│       │   └── layout/              # Empty, ready for shell components
│       ├── hooks/
│       ├── pages/                   # Empty, ready for LoginPage
│       ├── services/
│       ├── stores/                  # Empty, ready for auth store
│       ├── theme/                   # Empty, ready for healthcare theme
│       └── types/
└── ...
```

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | client/package.json | Add @mui/material@5, @mui/icons-material@5, @emotion/react, @emotion/styled, @fontsource/roboto, @tanstack/react-query@4, zustand@4, react-router-dom@6 |
| CREATE | client/src/theme/healthcare-theme.ts | Custom MUI theme with healthcare color palette, Roboto typography scale, 8px spacing grid, breakpoints, elevation, and component overrides |
| MODIFY | client/src/main.tsx | Import Roboto font weights (300/400/500/700), render App component |
| CREATE | client/src/App.tsx | Root component: ThemeProvider → CssBaseline → QueryClientProvider → RouterProvider with route definitions |
| CREATE | client/src/stores/auth-store.ts | Zustand store: user state, isAuthenticated flag, login/logout actions |
| CREATE | client/src/pages/LoginPage.tsx | Login page shell matching SCR-024 wireframe — centered card, email/password fields, remember me, login button, forgot password link, all 4 states |
| CREATE | client/src/components/layout/Header.tsx | App header with logo, navigation links (desktop), user avatar with dropdown menu |
| CREATE | client/src/components/layout/Sidebar.tsx | Desktop persistent sidebar with navigation items (Dashboard, Appointments, Documents, Patients) |
| CREATE | client/src/components/layout/BottomNav.tsx | Mobile bottom navigation bar with icon tabs |
| CREATE | client/src/components/layout/AuthenticatedLayout.tsx | Layout wrapper combining Header + Sidebar (desktop) or BottomNav (mobile) + Outlet for child routes |

## External References

- MUI v5 Theming: https://v5.mui.com/material-ui/customization/theming/
- MUI v5 createTheme API: https://v5.mui.com/material-ui/customization/theming/#createtheme-options-args-theme
- MUI v5 CssBaseline: https://v5.mui.com/material-ui/react-css-baseline/
- MUI v5 TextField: https://v5.mui.com/material-ui/react-text-field/
- MUI v5 Button: https://v5.mui.com/material-ui/react-button/
- MUI v5 AppBar: https://v5.mui.com/material-ui/react-app-bar/
- MUI v5 Drawer (Sidebar): https://v5.mui.com/material-ui/react-drawer/
- MUI v5 BottomNavigation: https://v5.mui.com/material-ui/react-bottom-navigation/
- React Query v4 Quick Start: https://tanstack.com/query/v4/docs/framework/react/quick-start
- Zustand v4 Getting Started: https://docs.pmnd.rs/zustand/getting-started/introduction
- React Router v6 Tutorial: https://reactrouter.com/en/6.x/start/tutorial
- Wireframe SCR-024: .propel/context/wireframes/Hi-Fi/wireframe-SCR-024-login.html
- Wireframe SCR-025: .propel/context/wireframes/Hi-Fi/wireframe-SCR-025-header-navigation.html
- Design System: .propel/context/docs/designsystem.md

## Build Commands

```bash
cd client
npm install
npm run dev          # Verify theme renders, login page loads at /login, nav shell visible
npm run build        # Ensure production build succeeds with all new dependencies
```

## Implementation Validation Strategy

- [x] `npm run dev` renders the login page at `/login` matching SCR-024 wireframe layout
- [x] `npm run dev` renders the header/navigation shell at `/` matching SCR-025 wireframe layout
- [x] **[UI Tasks]** Visual comparison against wireframe completed at 375px, 768px, 1440px
- [x] **[UI Tasks]** Run `/analyze-ux` to validate wireframe alignment
- [x] MUI ThemeProvider wraps the entire application with healthcare color palette
- [x] React Query `QueryClientProvider` is present in the component tree (verify via React DevTools)
- [x] Zustand auth store initializes with `isAuthenticated: false` and `user: null`
- [x] Login page displays all 4 states: Default, Loading (button spinner), Error (Alert), Validation (field errors)
- [x] Navigation adapts responsively: sidebar at >= 900px, bottom nav at < 900px
- [x] All new npm packages are OSS-licensed (MIT/Apache-2.0/BSD/ISC)

## Implementation Checklist

- [x] Install MUI dependencies: `npm install @mui/material@5 @mui/icons-material@5 @emotion/react @emotion/styled @fontsource/roboto`
- [x] Create `src/theme/healthcare-theme.ts` using `createTheme()` with full design token integration from designsystem.md (palette, typography, spacing, breakpoints, shape, shadows, component overrides for Button, TextField, Card)
- [x] Install and configure React Query: `npm install @tanstack/react-query@4`, create QueryClient with defaults (staleTime 5min, retry 1), wrap App in QueryClientProvider
- [x] Install and configure Zustand: `npm install zustand@4`, create `src/stores/auth-store.ts` with user/isAuthenticated state and login/logout actions
- [x] Install and configure React Router: `npm install react-router-dom@6`, create `src/App.tsx` with route definitions (public: /login, protected: / with AuthenticatedLayout)
- [x] Implement `src/pages/LoginPage.tsx` matching SCR-024 wireframe: centered Card (max-width 400px, elevation 1), PropelIQ logo/title, email TextField, password TextField, "Remember me" Checkbox, full-width primary Login Button, "Forgot password?" Link, Error Alert, field validation states
- [x] Implement Header (`src/components/layout/Header.tsx`) and responsive navigation: desktop Sidebar (`Sidebar.tsx`) with persistent Drawer + nav items, mobile BottomNav (`BottomNav.tsx`) with icon tabs, AuthenticatedLayout (`AuthenticatedLayout.tsx`) combining all with Outlet
- [x] **[UI Tasks - MANDATORY]** Validate Login page and Navigation shell match wireframes at 375px, 768px, 1440px breakpoints before marking task complete

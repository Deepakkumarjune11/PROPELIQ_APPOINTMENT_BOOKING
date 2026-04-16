# Task - task_001_fe_project_scaffolding

## Requirement Reference

- User Story: us_001
- Story Location: .propel/context/tasks/EP-TECH/us_001/us_001.md
- Acceptance Criteria:
  - AC-1: **Given** the project repository is cloned, **When** a developer runs `npm install` and `npm start`, **Then** the React 18 application starts on a local development server without errors.
  - AC-5: **Given** the project uses only free and open-source technology, **When** auditing all npm dependencies, **Then** no paid or proprietary packages are included per NFR-015.
- Edge Cases:
  - What happens when Node.js version is incompatible? (Project includes .nvmrc specifying minimum Node 18.x and displays clear error if version mismatch detected)
  - How does the system handle missing environment variables? (Application displays configuration checklist on startup if required environment variables are missing)

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | No |
| **Figma URL** | N/A |
| **Wireframe Status** | N/A |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | N/A |
| **Screen Spec** | N/A |
| **UXR Requirements** | N/A |
| **Design Tokens** | N/A |

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | React | 18.x |
| Build Tool | Vite | 5.x |
| Language | TypeScript | 5.x |
| UI Components | Material-UI (MUI) | 5.x |
| State Management | React Query + Zustand | 4.x / 4.x |
| Backend | .NET 8 | 8.0 LTS |
| Database | PostgreSQL | 15.x |

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

Initialize the greenfield React 18 frontend project using Vite with TypeScript. This task establishes the project scaffolding, build toolchain, linting/formatting configuration, directory structure, and environment variable management. It produces a runnable `npm start` dev server with zero errors, satisfying AC-1. The `.nvmrc` and environment validation address both edge cases. All dependencies are audited for OSS compliance per AC-5 and NFR-015.

## Dependent Tasks

- None (this is the first task in the greenfield project)

## Impacted Components

- **NEW** `client/` — Root directory for the React frontend SPA
- **NEW** `client/src/` — Source directory with entry point and type definitions
- **NEW** `client/package.json` — NPM manifest with all dependencies
- **NEW** `client/vite.config.ts` — Vite build and dev server configuration
- **NEW** `client/tsconfig.json` — TypeScript compiler configuration (strict mode)

## Implementation Plan

1. **Create project directory**: Initialize `client/` directory at the repository root as the frontend workspace
2. **Scaffold Vite project**: Use `npm create vite@latest` with the `react-ts` template to generate the React 18 + TypeScript scaffold, then adjust to match the required directory structure
3. **Configure TypeScript**: Set `strict: true`, target `ES2022`, module `ESNext`, paths alias `@/*` → `src/*` in `tsconfig.json`
4. **Configure Vite**: Set up `vite.config.ts` with React plugin, path aliases matching tsconfig, dev server port 3000, and API proxy to `http://localhost:5000/api`
5. **Set up ESLint**: Configure `.eslintrc.cjs` with `eslint:recommended`, `plugin:react/recommended`, `plugin:@typescript-eslint/recommended`, React 18 JSX transform settings
6. **Set up Prettier**: Configure `.prettierrc` with consistent formatting rules (single quotes, trailing commas, 2-space indent, 100 print width)
7. **Create directory structure**: Establish `src/pages/`, `src/components/layout/`, `src/stores/`, `src/hooks/`, `src/services/`, `src/theme/`, `src/types/` directories
8. **Configure environment handling**: Create `.env.example` with documented variables, `src/vite-env.d.ts` with typed `ImportMetaEnv`, and add `.nvmrc` with `18` for Node version pinning

## Current Project State

```
PropelIQ-Stub-Copilot/
├── .github/
├── .propel/
│   ├── context/
│   │   ├── docs/
│   │   ├── tasks/
│   │   │   └── EP-TECH/
│   │   │       └── us_001/
│   │   │           └── us_001.md
│   │   └── wireframes/
│   └── templates/
├── BRD - Appointment Booking and Clinical Intell-platform.md
└── README.md
```

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | client/package.json | NPM manifest with React 18, Vite, TypeScript, and dev tool dependencies |
| CREATE | client/vite.config.ts | Vite configuration with React plugin, path aliases, dev server (port 3000), API proxy |
| CREATE | client/tsconfig.json | TypeScript strict config targeting ES2022 with path alias `@/*` |
| CREATE | client/tsconfig.node.json | TypeScript config for Vite config file (Node environment) |
| CREATE | client/.nvmrc | Node version pin: `18` |
| CREATE | client/.eslintrc.cjs | ESLint configuration for React 18 + TypeScript |
| CREATE | client/.prettierrc | Prettier formatting rules |
| CREATE | client/index.html | HTML entry point with root div and Vite script tag |
| CREATE | client/src/main.tsx | React 18 entry point using `createRoot` |
| CREATE | client/src/vite-env.d.ts | Vite client types + typed `ImportMetaEnv` interface |
| CREATE | client/.env.example | Documented environment variable template |

## External References

- Vite React+TS setup: https://vitejs.dev/guide/#scaffolding-your-first-vite-project
- Vite config reference: https://vitejs.dev/config/
- TypeScript strict mode: https://www.typescriptlang.org/tsconfig#strict
- ESLint React plugin: https://github.com/jsx-eslint/eslint-plugin-react
- React 18 createRoot API: https://react.dev/reference/react-dom/client/createRoot

## Build Commands

```bash
cd client
npm install
npm run dev          # Start Vite dev server on port 3000
npm run build        # Production build to client/dist/
npm run lint         # Run ESLint
npm run format       # Run Prettier
```

## Implementation Validation Strategy

- [x] `npm install` completes without errors or warnings for missing peer deps
- [x] `npm run dev` starts Vite dev server on `http://localhost:3000` and renders root component
- [x] `npm run build` produces optimized output in `client/dist/` without TypeScript errors
- [x] `npm run lint` passes with zero errors
- [x] `.nvmrc` contains `18` and `nvm use` selects correct Node version
- [x] All npm dependencies are MIT, Apache-2.0, BSD, or ISC licensed (no proprietary packages)

## Implementation Checklist

- [x] Create `client/` directory and initialize with `npm create vite@latest . -- --template react-ts`
- [x] Configure `tsconfig.json` with `strict: true`, `target: "ES2022"`, `moduleResolution: "bundler"`, path alias `"@/*": ["./src/*"]`
- [x] Configure `vite.config.ts` with `@vitejs/plugin-react`, path alias resolve, dev server port 3000, API proxy to `localhost:5000`
- [x] Create `.eslintrc.cjs` with React 18, TypeScript, and accessibility rules (jsx-a11y plugin)
- [x] Create `.prettierrc` with `singleQuote: true`, `trailingComma: "all"`, `tabWidth: 2`, `printWidth: 100`
- [x] Create directory scaffold: `src/pages/`, `src/components/layout/`, `src/stores/`, `src/hooks/`, `src/services/`, `src/theme/`, `src/types/`
- [x] Create `.nvmrc` with `18`, `.env.example` with `VITE_API_BASE_URL=http://localhost:5000`, and typed `vite-env.d.ts`
- [x] Verify `npm install && npm run dev` starts successfully and `npm run build` completes without errors

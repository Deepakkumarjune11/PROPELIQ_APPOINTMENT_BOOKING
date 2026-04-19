# Task - TASK_003: FE Semantic Color System & Color-Blind Indicators

## Requirement Reference

- **User Story:** us_040 — Navigation Optimization, Guidance & Semantic Colors
- **Story Location:** `.propel/context/tasks/EP-009-II/us_040/us_040.md`
- **Acceptance Criteria:**
  - AC-3: Given the platform uses colors for meaning, When status indicators, alerts, or badges
    are displayed, Then they follow the semantic color system: success (green #4CAF50), warning
    (amber #FF9800), error (red #F44336), info (blue #2196F3) consistently across all screens
    per UXR-303.
  - AC-4: Given colors convey meaning, When a color-blind user views the interface, Then all
    color-coded information also includes a non-color indicator (icon, pattern, or text label)
    per UXR-303 and UXR-103.
- **Edge Cases:** N/A (semantic colors are a system-wide concern; individual screen edge cases
  for color-coded content — e.g., "no-show risk > 70%" orange badge — are handled within
  those specific feature tasks).

> ⚠️ **UXR-303 Scope Discrepancy (flag for BRD revision):**
> US_040 AC-3 defines `UXR-303` as covering standard semantic status colors
> (success/warning/error/info) across "all screens." However, `figma_spec.md` defines `UXR-303`
> as: "System MUST use healthcare-appropriate semantic colors (clinical vitals pink, medications
> orange, etc.) — Fact category color coding validated — SCR-017, SCR-019." The figma_spec.md
> definition is specifically about healthcare-domain category colors in the 360° clinical view
> (vitals=pink, medications=deep orange, etc.), not the general success/warning/error/info
> semantic palette. This task covers both interpretations: (1) the standard semantic status
> palette (AC-3, cross-cutting) and (2) the healthcare clinical category colors (UXR-303
> figma_spec.md intent, SCR-017/SCR-019). Recommend splitting into UXR-303a (standard status
> colors) and UXR-303b (clinical category colors) in a future BRD revision.
>
> ⚠️ **UXR-103 Scope Discrepancy (flag for BRD revision):**
> US_040 AC-4 cites `UXR-103` for the color-blind non-color indicator requirement. However,
> `figma_spec.md` defines `UXR-103` as: "System MUST support full keyboard navigation with
> visible focus indicators." Keyboard navigation ≠ color-blind accessibility. The color-blind
> (non-color indicator) requirement maps to WCAG 1.4.1 Use of Color, which has no dedicated UXR
> ID in the current spec. Recommend adding `UXR-104a` (or similar) for WCAG 1.4.1 color-blind
> compliance in a future BRD revision.

---

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | AVAILABLE |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | All wireframes in `.propel/context/wireframes/Hi-Fi/`; semantic colors defined in `designsystem.md#Semantic Colors` section; healthcare clinical colors in `designsystem.md#Healthcare-Specific Colors`; component inventory: Badge (7 variants), StatusIndicator (Dot + Label), Alert (4 variants) from `designsystem.md#components` |
| **Screen Spec** | All authenticated screens for status colors (AC-3); specifically `figma_spec.md#SCR-017` (360° Patient View — clinical category color coding) and `figma_spec.md#SCR-019` (Code Verification — fact category badges) per UXR-303 |
| **UXR Requirements** | UXR-303 (semantic + healthcare colors), UXR-103 (referenced for color-blind — see discrepancy note) |
| **Design Tokens** | `designsystem.md#Semantic Colors` (success `#4CAF50`, warning `#FF9800`, error `#F44336`, info `#2196F3`); `designsystem.md#Healthcare-Specific Colors` (vitals `#E91E63`, medications `#FF5722`, history `#795548`, diagnoses `#673AB7`, procedures `#009688`); `designsystem.md#Badge` (size, borderRadius, fontWeight specs) |

> **Wireframe Implementation Requirement:**
> MUST reference `designsystem.md` Badge component spec (7 variants: primary, secondary, success,
> warning, error, info, neutral) and StatusIndicator (Dot + Label × Success/Warning/Error/Info).
> All semantic color applications MUST pair the colour with an icon: `CheckCircle` for success,
> `Warning` for warning, `Error` for error, `Info` for info. Text labels must also accompany
> all colour-coded status badges (never colour-only). Validate at 375px and 1440px for colour-
> only vs colour+icon+label rendering.

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | React | 18.x |
| Frontend Framework | TypeScript | 5.x |
| UI Library | Material-UI (MUI) | 5.x |
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

Create a `SemanticStatusChip` component and a `StatusDot` component as the canonical
implementations for all status/colour-coded information across the platform. These replace
ad-hoc `<Chip color="...">` and `<Badge>` usages with a single consistent abstraction that
enforces WCAG 1.4.1 (colour is never the sole means of conveying information — always
paired with an icon and text label). Additionally, update `healthcare-theme.ts` with MUI theme
overrides for `MuiChip` and `MuiAlert` to ensure the semantic colour palette is consistently
applied when using the standard `color` prop. The healthcare clinical category colour tokens
(vitals, medications, history, diagnoses, procedures) are added to the MUI theme as custom
palette extensions for use in the 360° clinical view (SCR-017/SCR-019).

---

## Dependent Tasks

- `EP-009-I/us_036/task_001_fe_wcag_color_contrast_tokens.md` — `healthcare-theme.ts` must
  already have `text.secondary: #767676` and `primary.dark: #1565C0` applied (done in US_036).
  This task extends the same theme file.

---

## Impacted Components

| Component | Module | Action |
|-----------|--------|--------|
| `SemanticStatusChip.tsx` | `client/src/components/status/` | CREATE — Chip with mandatory icon + label; never colour-only |
| `StatusDot.tsx` | `client/src/components/status/` | CREATE — compact dot + label indicator (inline with text) |
| `index.ts` | `client/src/components/status/` | CREATE — barrel export |
| `healthcare-theme.ts` | `client/src/theme/` | MODIFY — add `MuiChip` semantic overrides; add clinical palette extension to `theme.palette` |

---

## Implementation Plan

1. **Define `SemanticStatusChip` props type**:
   ```ts
   type SemanticStatus = 'success' | 'warning' | 'error' | 'info';

   const STATUS_CONFIG: Record<SemanticStatus, {
     icon: React.ReactElement;
     defaultLabel: string;
     color: ChipProps['color'];
   }> = {
     success: { icon: <CheckCircleIcon />, defaultLabel: 'Success', color: 'success' },
     warning: { icon: <WarningIcon />,     defaultLabel: 'Warning', color: 'warning' },
     error:   { icon: <ErrorIcon />,       defaultLabel: 'Error',   color: 'error' },
     info:    { icon: <InfoIcon />,        defaultLabel: 'Info',    color: 'info' },
   };

   interface SemanticStatusChipProps {
     status: SemanticStatus;
     label?: string;              // If omitted, uses STATUS_CONFIG defaultLabel
     size?: 'small' | 'medium';  // Default: 'small'
   }
   ```
   The `icon` is mandatory and included automatically based on `status` — callers cannot omit
   it. This enforces WCAG 1.4.1 by design: the chip always has both colour AND an icon.

2. **Implement `SemanticStatusChip`**:
   ```tsx
   export function SemanticStatusChip({ status, label, size = 'small' }: SemanticStatusChipProps) {
     const { icon, defaultLabel, color } = STATUS_CONFIG[status];
     return (
       <Chip
         icon={icon}
         label={label ?? defaultLabel}
         color={color}
         size={size}
         variant="filled"
       />
     );
   }
   ```
   MUI `Chip` with `icon` prop renders the icon inside the chip alongside the label. The
   `color` prop maps to the MUI theme's `success`, `warning`, `error`, `info` palette entries
   defined in `healthcare-theme.ts`. Callers use `<SemanticStatusChip status="success" label="Booking Confirmed" />` — never raw `<Chip color="success">`.

3. **Implement `StatusDot`** — for compact inline status indicators (e.g., appointment status
   dots in table rows, queue status):
   ```tsx
   interface StatusDotProps {
     status: SemanticStatus;
     label: string;    // MANDATORY — text label always required (WCAG 1.4.1)
   }

   export function StatusDot({ status, label }: StatusDotProps) {
     const { color } = STATUS_CONFIG[status];
     return (
       <Box sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.5 }}>
         <Box
           sx={{
             width: 8, height: 8, borderRadius: '50%',
             bgcolor: `${color}.main`,
             flexShrink: 0,
           }}
           role="img"
           aria-label={`${status} status`}
         />
         <Typography variant="body2" component="span">{label}</Typography>
       </Box>
     );
   }
   ```
   The `role="img"` + `aria-label` on the dot ensures screen readers announce it. The mandatory
   `label` prop enforces that the dot is never rendered without text.

4. **Add clinical palette extension to `healthcare-theme.ts`**:
   Extend the MUI theme's `palette` with the healthcare-specific clinical category colours from
   `designsystem.md#Healthcare-Specific Colors`:
   ```ts
   declare module '@mui/material/styles' {
     interface Palette {
       clinical: {
         vitals: string;
         medications: string;
         history: string;
         diagnoses: string;
         procedures: string;
       };
     }
     interface PaletteOptions {
       clinical?: {
         vitals?: string;
         medications?: string;
         history?: string;
         diagnoses?: string;
         procedures?: string;
       };
     }
   }
   // In createTheme:
   palette: {
     // ... existing semantic palette ...
     clinical: {
       vitals:      '#E91E63',   // Pink — blood pressure, heart rate, temperature
       medications: '#FF5722',   // Deep Orange — prescriptions, dosages
       history:     '#795548',   // Brown — medical history, allergies
       diagnoses:   '#673AB7',   // Deep Purple — ICD-10 codes
       procedures:  '#009688',   // Teal — CPT codes, surgical history
     },
   }
   ```
   Used in SCR-017 fact category Tabs and Badge components.

5. **Add MUI `MuiChip` and `MuiAlert` theme overrides** — ensure that even when the raw MUI
   components are used with `color="success"` etc., they reflect the correct design token
   values. The MUI theme already maps `palette.success.main = #4CAF50` etc. (from
   `designsystem.md`). Add `MuiChip` override to enforce `fontWeight: 500` and correct
   border-radius (`4px = small`) across all Chip variants:
   ```ts
   MuiChip: {
     styleOverrides: {
       root: { borderRadius: 4, fontWeight: 500 },
       sizeSmall: { height: 20, '& .MuiChip-label': { px: '6px', fontSize: '0.75rem' } },
     },
   },
   ```
   MUI's `Alert` component automatically uses `palette.success/warning/error/info` — no
   additional override is needed if the palette is set correctly.

6. **Create barrel export `client/src/components/status/index.ts`**:
   ```ts
   export { SemanticStatusChip } from './SemanticStatusChip';
   export { StatusDot } from './StatusDot';
   export type { SemanticStatus } from './SemanticStatusChip';
   ```

7. **WCAG 1.4.1 compliance rule comment** — add a JSDoc block comment at the top of
   `SemanticStatusChip.tsx`:
   ```ts
   /**
    * SemanticStatusChip — canonical status indicator for the platform.
    *
    * WCAG 1.4.1 Use of Color: Color MUST NOT be the only visual means of conveying information.
    * This component enforces compliance by ALWAYS rendering an icon alongside the color.
    * Do NOT use raw <Chip color="success"> without an icon; use <SemanticStatusChip> instead.
    *
    * UXR-303: healthcare semantic color palette (success #4CAF50, warning #FF9800,
    * error #F44336, info #2196F3). Clinical category colours in theme.palette.clinical.*
    */
   ```

---

## Current Project State

```
client/src/
├── theme/
│   └── healthcare-theme.ts            ← MODIFY (clinical palette extension, MuiChip override)
└── components/
    └── status/                        ← CREATE (new folder)
        ├── SemanticStatusChip.tsx
        ├── StatusDot.tsx
        └── index.ts
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `client/src/components/status/SemanticStatusChip.tsx` | `SemanticStatus` type; `STATUS_CONFIG` map (status → icon + color + defaultLabel); `SemanticStatusChip` component with mandatory icon; WCAG 1.4.1 JSDoc |
| CREATE | `client/src/components/status/StatusDot.tsx` | 8px dot + `Typography` label; `role="img"` + `aria-label` on dot; `label` prop is mandatory |
| CREATE | `client/src/components/status/index.ts` | Barrel export for `SemanticStatusChip`, `StatusDot`, `SemanticStatus` type |
| MODIFY | `client/src/theme/healthcare-theme.ts` | Add `clinical` palette extension with `module augmentation`; add `MuiChip` style overrides (borderRadius 4px, fontWeight 500, sizeSmall heights) |

---

## External References

- [MUI Chip API — icon prop, color prop, size (MUI v5)](https://mui.com/material-ui/react-chip/)
- [MUI Theme Palette — extending palette with custom keys using module augmentation (MUI v5)](https://mui.com/material-ui/customization/palette/#adding-new-colors)
- [MUI Theme Component Overrides — MuiChip styleOverrides (MUI v5)](https://mui.com/material-ui/customization/theme-components/)
- [WCAG 1.4.1 Use of Color — color must not be the only visual means of conveying information](https://www.w3.org/WAI/WCAG22/Understanding/use-of-color.html)
- [WCAG 1.4.3 Contrast Minimum — 4.5:1 for normal text in badges and chips](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html)
- [designsystem.md#Semantic Colors — success #4CAF50, warning #FF9800, error #F44336, info #2196F3](d:\Propal IQ\Appontment Booking and Clinical Intell Platform\PROPELIQ_APPOINTMENT_BOOKING\.propel\context\docs\designsystem.md)
- [designsystem.md#Healthcare-Specific Colors — clinical vitals/medications/history/diagnoses/procedures](d:\Propal IQ\Appontment Booking and Clinical Intell Platform\PROPELIQ_APPOINTMENT_BOOKING\.propel\context\docs\designsystem.md)

---

## Build Commands

```bash
cd client
npm run dev     # Render <SemanticStatusChip status="success" label="Confirmed" /> in dev; verify icon+color+label
npm run build   # Confirm no TypeScript errors on module augmentation for clinical palette
npm run lint    # Confirm no a11y warnings on StatusDot role="img" usage
```

---

## Implementation Validation Strategy

- [ ] Unit tests pass
- [ ] Integration tests pass (if applicable)
- [ ] **[UI Tasks]** Visual comparison against wireframe completed at 375px, 1440px
- [ ] **[UI Tasks]** Run `/analyze-ux` to validate wireframe alignment
- [ ] `<SemanticStatusChip status="success" label="Verified" />` renders with `CheckCircle` icon + green chip + "Verified" text
- [ ] `<SemanticStatusChip status="warning" />` renders with `Warning` icon + amber chip + "Warning" defaultLabel
- [ ] `<SemanticStatusChip status="error" />` renders with `Error` icon + red chip
- [ ] `<SemanticStatusChip status="info" />` renders with `Info` icon + blue chip
- [ ] `<StatusDot status="success" label="Active" />` renders with green dot + "Active" text; dot has `role="img"` + `aria-label`
- [ ] TypeScript: using `<SemanticStatusChip status="unknown" />` causes a compile error (union type enforced)
- [ ] `theme.palette.clinical.vitals` resolves to `#E91E63` — no TS error
- [ ] `MuiChip` override: raw `<Chip size="small">` has `height: 20px`, `borderRadius: 4px`, `fontWeight: 500`
- [ ] Colour contrast: white text on `success.main` (#4CAF50) — verify 4.5:1 or adjust `contrastText` if needed

---

## Implementation Checklist

- [ ] **1.** Create `client/src/components/status/SemanticStatusChip.tsx`: define `SemanticStatus` union type; `STATUS_CONFIG` object with icon/color/defaultLabel per status; `SemanticStatusChip` component rendering MUI `<Chip icon={icon} label={label ?? defaultLabel} color={color} size={size}>`; add WCAG 1.4.1 JSDoc comment
- [ ] **2.** Create `client/src/components/status/StatusDot.tsx`: inline `<Box>` with 8×8px dot (`bgcolor: \`${color}.main\``, `borderRadius: '50%'`, `role="img"`, `aria-label`); mandatory `label` prop rendered as `<Typography variant="body2">`
- [ ] **3.** Create `client/src/components/status/index.ts`: barrel export for `SemanticStatusChip`, `StatusDot`, `SemanticStatus` type
- [ ] **4.** Modify `healthcare-theme.ts`: add TypeScript `declare module '@mui/material/styles'` augmentation for `clinical` palette key; add `clinical: { vitals, medications, history, diagnoses, procedures }` colour values to `createTheme` palette; add `MuiChip.styleOverrides` (borderRadius 4, fontWeight 500, sizeSmall dimensions)
- [ ] **5.** Verify contrast ratios — `contrastText: '#FFFFFF'` on success.main (#4CAF50): check at https://webaim.org/resources/contrastchecker/ — if < 4.5:1, use `success.dark` (#388E3C) as chip background for text content; `contrastText: '#000000'` on warning.main (#FF9800) is already configured per designsystem.md
- [ ] **6.** Import and use `<SemanticStatusChip>` in at least one existing page/component as proof-of-integration (e.g., replace any ad-hoc `<Chip color="success">` found in codebase with `<SemanticStatusChip status="success">`)
- [ ] **7.** Add `theme.palette.clinical` usage example comment in `healthcare-theme.ts` showing how SCR-017 fact category Tabs can use `theme.palette.clinical.vitals` for category colouring
- [ ] **[UI Tasks - MANDATORY]** Reference `designsystem.md#Badge` and `designsystem.md#Semantic Colors` spec during implementation; validate all 4 semantic status states (success/warning/error/info) visually before marking task complete
- [ ] **[UI Tasks - MANDATORY]** Confirm all colour-coded elements have an icon + label alongside (never colour-only); test by temporarily viewing in grayscale browser mode

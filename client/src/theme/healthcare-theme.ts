import { createTheme } from '@mui/material/styles';

/**
 * TypeScript module augmentation — adds `clinical` palette to MUI's Palette interfaces.
 * Enables type-safe access: `theme.palette.clinical.vitals` in `sx` and `styled`.
 * Clinical category colours sourced from designsystem.md#Healthcare-Specific Colors (UXR-303).
 *
 * Usage in SCR-017 fact category Tabs:
 *   `sx={{ borderLeft: `3px solid ${theme.palette.clinical.vitals}` }}`
 */
declare module '@mui/material/styles' {
  interface Palette {
    clinical: {
      /** Pink #E91E63 — blood pressure, heart rate, temperature */
      vitals: string;
      /** Deep Orange #FF5722 — prescriptions, dosages */
      medications: string;
      /** Brown #795548 — medical history, allergies */
      history: string;
      /** Deep Purple #673AB7 — ICD-10 codes */
      diagnoses: string;
      /** Teal #009688 — CPT codes, surgical history */
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

/**
 * Healthcare fact category colour tokens — sourced from designsystem.md#healthcare-specific-colors.
 * Used by FactCard left-border and any category-level UI elements (UXR-303).
 */
export const FACT_CATEGORY_COLORS: Record<string, string> = {
  Vitals:      '#E91E63',   // pink
  Medications: '#FF5722',   // deep orange
  History:     '#795548',   // brown
  Diagnoses:   '#673AB7',   // deep purple
  Procedures:  '#009688',   // teal
};

// Detect reduced-motion preference at theme creation time (WCAG 2.3.3, AC-5).
// SSR-safe: guard prevents access to window in non-browser environments.
// Live OS-setting changes during a session are handled by the GlobalStyles @media
// query in App.tsx — no page reload required.
const prefersReducedMotion =
  typeof window !== 'undefined' &&
  window.matchMedia('(prefers-reduced-motion: reduce)').matches;

// Design tokens sourced from designsystem.md — Medical Blue healthcare palette
export const healthcareTheme = createTheme({
  palette: {
    primary: {
      main: '#2196F3', // Medical Blue — CTAs, buttons, icons (3.1:1 on white — UI only, WCAG 1.4.11)
      dark: '#1565C0', // CHANGED from #1976D2 — 5.9:1 on white; use for text links (WCAG 1.4.3)
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
      default: '#FAFAFA', // App background (neutral.50)
      paper: '#FFFFFF',
    },
    text: {
      primary: '#212121',  // neutral.900 — 16.1:1 on white ✅
      secondary: '#767676', // CHANGED from #757575 — 4.54:1 on white, passes WCAG AA 4.5:1 (AC-3)
    },
    divider: '#E0E0E0', // neutral.300 — borders
    // Healthcare clinical category colours (UXR-303 / figma_spec.md#SCR-017, SCR-019).
    // Used for fact-category left-border accents in 360° Patient View and Code Verification.
    clinical: {
      vitals:      '#E91E63',   // Pink — blood pressure, heart rate, temperature
      medications: '#FF5722',   // Deep Orange — prescriptions, dosages
      history:     '#795548',   // Brown — medical history, allergies
      diagnoses:   '#673AB7',   // Deep Purple — ICD-10 codes
      procedures:  '#009688',   // Teal — CPT codes, surgical history
    },
  },
  typography: {
    fontFamily: "Roboto, -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif",
    h4: {
      fontSize: '1.5rem',
      fontWeight: 400,
    },
    body1: {
      fontSize: '1rem',
      fontWeight: 400,
    },
    button: {
      fontSize: '0.875rem',
      fontWeight: 500,
      textTransform: 'uppercase',
    },
  },
  // 8px base spacing grid
  spacing: 8,
  breakpoints: {
    values: {
      xs: 0,
      sm: 600,
      md: 900,
      lg: 1200,
      xl: 1536,
    },
  },
  shape: {
    borderRadius: 4,
  },
  // Zero-duration transitions when OS prefers-reduced-motion (WCAG 2.3.3, AC-5).
  // GlobalStyles @media rule in App.tsx handles live updates without reload.
  ...(prefersReducedMotion && {
    transitions: {
      create: () => 'none',
      duration: {
        shortest: 0, shorter: 0, short: 0, standard: 0, complex: 0,
        enteringScreen: 0, leavingScreen: 0,
      },
    },
  }),
  components: {
    // Global focus-visible ring for ALL MuiButtonBase descendants:
    // Button, IconButton, ListItemButton, BottomNavigationAction, Tab,
    // Checkbox, Radio, Switch — 2px solid, 2px offset, ≥3:1 on white (WCAG 2.4.7, AC-1)
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
    // Outlined input: thicker focused border + outer ring on keyboard focus (AC-1)
    MuiOutlinedInput: {
      styleOverrides: {
        root: {
          borderRadius: 4,
          '&.Mui-focused .MuiOutlinedInput-notchedOutline': {
            borderWidth: 2,
            borderColor: '#2196F3',
          },
          '&.Mui-focusVisible': {
            outline: '2px solid #2196F3',
            outlineOffset: '2px',
          },
        },
      },
    },
    // Standard/filled variant inputs (AC-1)
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
        root: {
          borderRadius: 4,
        },
      },
    },
    MuiCard: {
      styleOverrides: {
        root: {
          borderRadius: 8,
        },
      },
    },
    // Text links use primary.dark (#1565C0, 5.9:1 on white) to pass WCAG 1.4.3 4.5:1 (AC-3)
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
    // WCAG 2.5.5 Touch Target (Minimum) — 44×44px on all ListItemButton instances (AC-4)
    // Applies globally; individual sx overrides in Sidebar.tsx for belt-and-suspenders.
    MuiListItemButton: {
      styleOverrides: {
        root: {
          minHeight: 44,
        },
      },
    },
    // WCAG 2.5.5 — BottomNavigationAction minimum touch width across all app instances (AC-4)
    MuiBottomNavigationAction: {
      styleOverrides: {
        root: {
          minWidth: 44,
        },
      },
    },
    // WCAG 2.3.3 Animation from Interactions — disable shimmer wave when OS prefers reduced motion.
    // Uses the `prefersReducedMotion` constant established in US_036 (same file, top of module).
    // `false` disables animation entirely (no pulse fallback) — safest for vestibular disorders.
    MuiSkeleton: {
      defaultProps: {
        animation: prefersReducedMotion ? false : 'wave',
      },
    },
    // UXR-303 / WCAG 1.4.1 — SemanticStatusChip enforces icon+colour+label at component level.
    // This override ensures all raw <Chip> instances also have consistent borderRadius and
    // fontWeight even when used outside the SemanticStatusChip wrapper.
    MuiChip: {
      styleOverrides: {
        root: {
          borderRadius: 4,
          fontWeight: 500,
        },
        sizeSmall: {
          height: 20,
          '& .MuiChip-label': {
            paddingLeft: '6px',
            paddingRight: '6px',
            fontSize: '0.75rem',
          },
        },
      },
    },
  },
});

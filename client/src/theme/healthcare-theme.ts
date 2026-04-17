import { createTheme } from '@mui/material/styles';

// Design tokens sourced from designsystem.md — Medical Blue healthcare palette
export const healthcareTheme = createTheme({
  palette: {
    primary: {
      main: '#2196F3', // Medical Blue — CTAs, active states, links
      dark: '#1976D2', // Hover states
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
      primary: '#212121', // neutral.900 — high-contrast text
      secondary: '#757575', // neutral.600 — body text
    },
    divider: '#E0E0E0', // neutral.300 — borders
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
  components: {
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
    MuiOutlinedInput: {
      styleOverrides: {
        root: {
          borderRadius: 4,
        },
      },
    },
  },
});

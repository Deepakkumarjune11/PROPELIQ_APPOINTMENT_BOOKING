import { Box } from '@mui/material';

/**
 * WCAG 2.4.1 Bypass Blocks — skip navigation link.
 *
 * Visually hidden until focused (keyboard-only users). When Tab is pressed
 * as the first interaction on any page, this link appears as a prominent
 * banner in the top-left corner. Pressing Enter moves focus to
 * `#main-content`, skipping the repeated Header / Sidebar navigation.
 *
 * Placement: render as the FIRST child of any layout that contains repeated
 * navigation (AuthenticatedLayout, LoginPage wrapping shell).
 *
 * The target element must have `id="main-content"` and `tabIndex={-1}` so
 * that programmatic focus via the anchor navigates correctly.
 */
export default function SkipToMainContent() {
  return (
    <Box
      component="a"
      href="#main-content"
      sx={{
        // Visually hidden by default — off-screen, 1×1 px, overflow clipped
        position: 'absolute',
        left: '-9999px',
        top: 'auto',
        width: '1px',
        height: '1px',
        overflow: 'hidden',
        // Becomes visible and prominent on keyboard focus (WCAG 2.4.1)
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
          // White outline ring on blue background — ≥3:1 contrast for focus indicator
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

import ErrorOutlineIcon from '@mui/icons-material/ErrorOutline';
import { Box, Button, Typography } from '@mui/material';
import { useRouteError } from 'react-router-dom';

interface GlobalErrorPageProps {
  /**
   * Custom error title. Defaults to `"Something went wrong"` (or `"Page not found"` for 404s).
   * Provided by `AppErrorBoundary` when rendering as a crash fallback.
   */
  title?: string;
  /** Custom body message. Defaults to a generic recovery prompt. */
  message?: string;
  /**
   * Called when the user clicks "Try Again".
   * `AppErrorBoundary` passes `() => this.setState({ hasError: false })` to reset the boundary.
   * When absent (e.g. as a route `errorElement`), defaults to `window.location.reload()`.
   */
  onRetry?: () => void;
}

/**
 * User-friendly error page (US_039 AC-4 / UXR-503).
 *
 * Used in two contexts:
 * 1. **`AppErrorBoundary` fallback** — catches component-tree crashes; rendered outside Router.
 * 2. **React Router `errorElement`** — catches loader/action failures and 404s; rendered inside Router.
 *
 * Navigation always uses `window.location.href` so the component works in both contexts without
 * requiring a React Router context (which is unavailable when `AppErrorBoundary` has caught a crash).
 *
 * OWASP A09: Stack traces are logged to `console.error` by `AppErrorBoundary` only —
 * never rendered in the UI here.
 */
export function GlobalErrorPage({ title, message, onRetry }: GlobalErrorPageProps) {
  // Safe read of React Router error — IIFE try/catch handles the case where this component
  // renders outside a Router context (AppErrorBoundary fallback). useRouteError() throws
  // synchronously when no Router context is present; catching it returns undefined.
  // eslint-disable-next-line react-hooks/rules-of-hooks
  const routeError = (() => { try { return useRouteError(); } catch { return undefined; } })();

  const is404 = (routeError as { status?: number } | undefined)?.status === 404;

  const resolvedTitle = title ?? (is404 ? 'Page not found' : 'Something went wrong');
  const resolvedMessage =
    message ??
    (is404
      ? "The page you're looking for doesn't exist."
      : 'An unexpected error occurred. Please try again or return to the dashboard.');

  const handleRetry = onRetry ?? (() => window.location.reload());

  // Always use window.location for "Go to Dashboard" — ensures navigation works both
  // inside and outside Router context (AppErrorBoundary fallback has no Router context).
  const handleDashboard = () => {
    window.location.href = '/';
  };

  return (
    <Box
      role="main"
      aria-labelledby="error-heading"
      sx={{
        minHeight: '100vh',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        gap: 3,
        px: 3,
        bgcolor: 'background.default',
      }}
    >
      {/* Error icon — 64px, error.main colour (--color-error-500) */}
      <ErrorOutlineIcon sx={{ fontSize: 64, color: 'error.main' }} />

      <Typography
        id="error-heading"
        variant="h5"
        component="h1"
        align="center"
        gutterBottom
      >
        {resolvedTitle}
      </Typography>

      <Typography
        variant="body1"
        color="text.secondary"
        align="center"
        sx={{ maxWidth: 400 }}
      >
        {resolvedMessage}
      </Typography>

      {/* Action buttons — "Try Again" only shown for non-404 errors (no point retrying a 404) */}
      <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap', justifyContent: 'center' }}>
        {!is404 && (
          <Button variant="contained" onClick={handleRetry}>
            Try Again
          </Button>
        )}
        <Button variant="outlined" onClick={handleDashboard}>
          Go to Dashboard
        </Button>
      </Box>
    </Box>
  );
}

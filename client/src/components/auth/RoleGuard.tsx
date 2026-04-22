import { Box, Button, Typography } from '@mui/material';
import { Navigate, Outlet } from 'react-router-dom';

import { useAuthStore, type UserProfile } from '@/stores/auth-store';

interface Props {
  /** Roles permitted to render the child routes. */
  roles: ReadonlyArray<UserProfile['role']>;
}

/**
 * Role-based route guard wrapping `<Outlet>` (US_024, AC-3, NFR-004).
 *
 * - Unauthenticated → redirect to `/login` (AC-1).
 * - Authenticated but wrong role → inline 403 state; does NOT expose child route (AC-3).
 * - Correct role → render `<Outlet>` so nested routes display normally.
 *
 * Used in `App.tsx` as the `element` of route groups so every nested child
 * inherits the same role check without wrapping each route individually.
 */
export function RoleGuard({ roles }: Props) {
  const { user, isAuthenticated } = useAuthStore();

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  if (!user || !roles.includes(user.role)) {
    return (
      <Box
        display="flex"
        flexDirection="column"
        alignItems="center"
        justifyContent="center"
        minHeight="60vh"
        role="main"
        aria-labelledby="access-denied-heading"
      >
        <Typography
          id="access-denied-heading"
          variant="h5"
          color="error"
          gutterBottom
        >
          403 — Access Denied
        </Typography>
        <Typography color="text.secondary">
          You do not have permission to view this page.
        </Typography>
        <Button
          sx={{ mt: 2 }}
          variant="contained"
          onClick={() => window.history.back()}
          aria-label="Go back to the previous page"
        >
          Go Back
        </Button>
      </Box>
    );
  }

  return <Outlet />;
}

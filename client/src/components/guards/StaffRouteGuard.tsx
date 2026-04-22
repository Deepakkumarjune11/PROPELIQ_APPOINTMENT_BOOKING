// RBAC route guard — permits only authenticated users with role 'staff' or 'admin' (US_016, AC-1).
// Unauthorized access redirects to '/' with an error snackbar (OWASP A01).
import { type ReactNode, useState } from 'react';
import { Navigate } from 'react-router-dom';
import { Alert, Snackbar } from '@mui/material';

import { useAuthStore } from '@/stores/auth-store';

interface StaffRouteGuardProps {
  children: ReactNode;
}

export function StaffRouteGuard({ children }: StaffRouteGuardProps) {
  const user = useAuthStore((s) => s.user);
  const [showDenied, setShowDenied] = useState(true);

  const isAuthorized = user?.role === 'staff' || user?.role === 'admin';

  if (!isAuthorized) {
    return (
      <>
        <Snackbar
          open={showDenied}
          autoHideDuration={4000}
          onClose={() => setShowDenied(false)}
          anchorOrigin={{ vertical: 'top', horizontal: 'center' }}
        >
          <Alert severity="error" onClose={() => setShowDenied(false)}>
            Access denied. Staff role required.
          </Alert>
        </Snackbar>
        <Navigate to="/" replace />
      </>
    );
  }

  return <>{children}</>;
}

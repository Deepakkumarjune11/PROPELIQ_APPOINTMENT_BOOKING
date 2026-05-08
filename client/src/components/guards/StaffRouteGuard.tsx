// RBAC route guard — permits only authenticated users with role 'staff' or 'admin' (US_016, AC-1).
// Optional `allowedStaffRoles` further restricts to specific sub-roles (OWASP A01).
// Unauthorized access redirects to '/staff/dashboard' with an error snackbar.
import { type ReactNode, useState } from 'react';
import { Navigate } from 'react-router-dom';
import { Alert, Snackbar } from '@mui/material';

import { useAuthStore } from '@/stores/auth-store';

interface StaffRouteGuardProps {
  children: ReactNode;
  /**
   * When provided, only the listed staff sub-roles may access this route.
   * Admins always bypass this check.
   */
  allowedStaffRoles?: Array<'FrontDesk' | 'CallCenter' | 'ClinicalReviewer'>;
}

export function StaffRouteGuard({ children, allowedStaffRoles }: StaffRouteGuardProps) {
  const user = useAuthStore((s) => s.user);
  const [showDenied, setShowDenied] = useState(true);

  const isAuthorized = user?.role === 'staff' || user?.role === 'admin';

  // Sub-role check — admins bypass; only enforced when allowedStaffRoles is specified.
  const subRoleAllowed =
    !allowedStaffRoles ||
    user?.role === 'admin' ||
    (user?.staffRole != null && allowedStaffRoles.includes(user.staffRole));

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

  if (!subRoleAllowed) {
    return (
      <>
        <Snackbar
          open={showDenied}
          autoHideDuration={4000}
          onClose={() => setShowDenied(false)}
          anchorOrigin={{ vertical: 'top', horizontal: 'center' }}
        >
          <Alert severity="error" onClose={() => setShowDenied(false)}>
            Access denied. Your staff role does not have permission to view this page.
          </Alert>
        </Snackbar>
        <Navigate to="/staff/dashboard" replace />
      </>
    );
  }

  return <>{children}</>;
}

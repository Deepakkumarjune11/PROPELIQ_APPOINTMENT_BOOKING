import { CssBaseline, GlobalStyles, ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import { Navigate, RouterProvider, createBrowserRouter } from 'react-router-dom';

import AuthenticatedLayout from '@/components/layout/AuthenticatedLayout';
import { ToastProvider } from '@/components/feedback/ToastProvider';
import { AppErrorBoundary } from '@/components/errors/AppErrorBoundary';
import { GlobalErrorPage } from '@/components/errors/GlobalErrorPage';
import { RoleGuard } from '@/components/auth/RoleGuard';
import { StaffRouteGuard } from '@/components/guards/StaffRouteGuard';
import { useAuthStore } from '@/stores/auth-store';

/** Redirects to the role-appropriate home page. Staff → /staff/dashboard, patient → /appointments. */
function RoleHomeRedirect() {
  const role = useAuthStore((s) => s.user?.role);
  if (role === 'patient') return <Navigate to="/appointments" replace />;
  return <Navigate to="/staff/dashboard" replace />;
}
import LoginPage from '@/pages/LoginPage';
import AvailabilitySearchPage from '@/pages/availability/AvailabilitySearchPage';
import BookingErrorPage from '@/pages/booking-error/BookingErrorPage';
import BookingConfirmationPage from '@/pages/confirmation/BookingConfirmationPage';
import ConversationalIntakePage from '@/pages/intake/ConversationalIntakePage';
import ManualIntakeFormPage from '@/pages/intake/ManualIntakeFormPage';
import MyAppointmentsPage from '@/pages/my-appointments/MyAppointmentsPage';
import PatientDetailsFormPage from '@/pages/patient-details/PatientDetailsFormPage';
import PreferredSlotSelectionPage from '@/pages/preferred-slot/PreferredSlotSelectionPage';
import SlotSelectionPage from '@/pages/slot-selection/SlotSelectionPage';
import StaffDashboardPage from '@/pages/staff/dashboard/StaffDashboardPage';
import WalkInBookingPage from '@/pages/staff/walk-in/WalkInBookingPage';
import SameDayQueuePage from '@/pages/staff/queue/SameDayQueuePage';
import PatientChartReviewPage from '@/pages/staff/PatientChartReviewPage';
import PatientView360Page from '@/pages/staff/PatientView360Page';
import ConflictResolutionPage from '@/pages/staff/ConflictResolutionPage';
import CodeVerificationPage from '@/pages/staff/CodeVerificationPage';
import VerificationCompletePage from '@/pages/staff/VerificationCompletePage';
import DocumentListPage from '@/pages/documents/DocumentListPage';
import DocumentUploadPage from '@/pages/documents/DocumentUploadPage';
import UserManagementPage from '@/pages/admin/UserManagementPage';
import AnalyticsDashboardPage from '@/pages/AnalyticsDashboardPage';
import PatientProfilePage from '@/pages/profile/PatientProfilePage';
import { healthcareTheme } from '@/theme/healthcare-theme';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // Cache server data for 5 minutes before marking stale
      staleTime: 5 * 60 * 1000,
      retry: 1,
      // Avoid re-fetching on window focus in clinical workflows where data freshness is managed deliberately
      refetchOnWindowFocus: false,
    },
  },
});

// Click depth audit (AC-5 / UXR-001): all features reachable from dashboard within 3 clicks.
// Level 1 (1 click): direct sidebar routes — /appointments, /documents, /staff/dashboard, /analytics, /admin
// Level 2 (2 clicks): sub-pages — /appointments/search, /staff/patients, /staff/walk-in, /admin/users
// Level 3 (3 clicks): deep screens — /staff/patients/:id/360-view, /staff/patients/:id/conflict-resolution (max depth = 3)
// Source: navigation-map.md section 2 & 4 (all flows confirmed ≤ 3 navigation steps)
const router = createBrowserRouter([
  {
    path: '/login',
    element: <LoginPage />,
  },
  {
    path: '/',
    element: <AuthenticatedLayout />,
    errorElement: <GlobalErrorPage />,
    handle: { crumb: 'Dashboard' },
    children: [
      // SCR-001: Availability search — step 1 of the booking flow
      { path: 'appointments/search', element: <AvailabilitySearchPage />, handle: { crumb: 'Search Availability' } },
      // '/book' alias — matches BottomNav "Book" tab on mobile
      { path: 'book', element: <Navigate to="/appointments/search" replace /> },
      // SCR-002: Slot selection / confirmation — step 2 of the booking flow
      { path: 'appointments/slot-selection', element: <SlotSelectionPage />, handle: { crumb: 'Select Slot' } },
      // SCR-003: Patient details form — step 3 of the booking flow
      { path: 'appointments/patient-details', element: <PatientDetailsFormPage />, handle: { crumb: 'Patient Details' } },
      // SCR-004: Manual intake form — step 4 of the booking flow
      { path: 'appointments/intake/manual', element: <ManualIntakeFormPage />, handle: { crumb: 'Intake Form' } },
      // SCR-005: Conversational intake stub (US_012) — prevents 404 when mode-switch fires
      { path: 'appointments/intake/conversational', element: <ConversationalIntakePage />, handle: { crumb: 'Intake' } },
      // SCR-006: Booking confirmation (US_014) — success state with PDF + calendar sync
      { path: 'appointments/confirmation', element: <BookingConfirmationPage />, handle: { crumb: 'Confirmation' } },
      // SCR-007: Booking error (US_014) — non-409 failure with retry + slot link
      { path: 'appointments/error', element: <BookingErrorPage />, handle: { crumb: 'Booking Error' } },
      // SCR-008: My Appointments (US_015) — appointment list with watchlist badge
      { path: 'appointments', element: <MyAppointmentsPage />, handle: { crumb: 'My Appointments' } },
      // SCR-009: Preferred Slot Selection (US_015) — slot calendar for watchlist registration
      { path: 'appointments/:appointmentId/preferred-slot', element: <PreferredSlotSelectionPage />, handle: { crumb: 'Preferred Slot' } },
      // '/' index — role-aware redirect: patient → /appointments, staff/admin → /staff/dashboard
      { index: true, element: <RoleHomeRedirect /> },
      {
        path: 'staff/dashboard',
        handle: { crumb: 'Staff Dashboard' },
        element: (
          <StaffRouteGuard>
            <StaffDashboardPage />
          </StaffRouteGuard>
        ),
      },
      // SCR-011: Walk-In Booking (US_016) — patient search, inline create, booking submit
      // '/walkin' alias matches Sidebar & BottomNav links
      {
        path: 'staff/walk-in',
        handle: { crumb: 'Walk-In Booking' },
        element: (
          <StaffRouteGuard>
            <WalkInBookingPage />
          </StaffRouteGuard>
        ),
      },
      { path: 'walkin', element: <Navigate to="/staff/walk-in" replace /> },
      // SCR-012 + SCR-013: Same-Day Queue & Arrival Marking (US_017)
      // '/queue' alias matches Sidebar & BottomNav links
      {
        path: 'staff/queue',
        handle: { crumb: 'Same-Day Queue' },
        element: (
          <StaffRouteGuard>
            <SameDayQueuePage />
          </StaffRouteGuard>
        ),
      },
      { path: 'queue', element: <Navigate to="/staff/queue" replace /> },
      // SCR-016: Patient Chart Review (US_021) — verification queue
      // '/verify' alias matches Sidebar & BottomNav links
      {
        path: 'staff/patients',
        handle: { crumb: 'Patient Charts' },
        element: (
          <StaffRouteGuard>
            <PatientChartReviewPage />
          </StaffRouteGuard>
        ),
      },
      { path: 'verify', element: <Navigate to="/staff/patients" replace /> },
      // SCR-017: 360-Degree Patient View (US_021) — consolidated fact summary
      {
        path: 'staff/patients/:patientId/360-view',
        handle: { crumb: '360° View' },
        element: (
          <StaffRouteGuard>
            <PatientView360Page />
          </StaffRouteGuard>
        ),
      },
      // SCR-018: Conflict Resolution (US_022) — side-by-side source comparison + resolution
      {
        path: 'staff/patients/:patientId/conflict-resolution',
        handle: { crumb: 'Conflict Resolution' },
        element: (
          <StaffRouteGuard>
            <ConflictResolutionPage />
          </StaffRouteGuard>
        ),
      },
      // SCR-019: Code Verification (US_023) — ICD-10/CPT code review with evidence breadcrumbs
      {
        path: 'staff/patients/:patientId/code-verification',
        handle: { crumb: 'Code Verification' },
        element: (
          <StaffRouteGuard>
            <CodeVerificationPage />
          </StaffRouteGuard>
        ),
      },
      // SCR-020: Verification Complete (US_023) — chart summary + timing stats + next patient CTA
      {
        path: 'staff/patients/:patientId/verification-complete',
        handle: { crumb: 'Verification Complete' },
        element: (
          <StaffRouteGuard>
            <VerificationCompletePage />
          </StaffRouteGuard>
        ),
      },
      // SCR-014: Document Upload (US_018) — drag-drop zone, per-file progress
      { path: 'documents/upload', element: <DocumentUploadPage />, handle: { crumb: 'Upload Documents' } },
      // SCR-015: Document List (US_018) — status badges, conditional polling, delete dialog
      { path: 'documents', element: <DocumentListPage />, handle: { crumb: 'Documents' } },
      // SCR-P01: Patient Profile — account details + logout
      { path: 'profile', element: <PatientProfilePage />, handle: { crumb: 'Profile' } },

      // Admin routes — US_024 AC-3: only 'admin' role permitted (RoleGuard renders 403 for others)
      {
        path: 'admin',
        element: <RoleGuard roles={['admin']} />,
        handle: { crumb: 'Admin' },
        children: [
          // Placeholder — dedicated AdminDashboard component added in admin epic tasks
          { path: 'dashboard', element: <Navigate to="/" replace /> },
          // SCR-021: User Management (US_025)
          { path: 'users', element: <UserManagementPage />, handle: { crumb: 'User Management' } },
        ],
      },
      // SCR-028: Analytics Dashboard (US_033) — staff and admin only
      // Path 'metrics' matches the Sidebar and BottomNav links; 'analytics' kept as alias.
      {
        path: 'metrics',
        element: <RoleGuard roles={['staff', 'admin']} />,
        handle: { crumb: 'Metrics' },
        children: [
          { index: true, element: <AnalyticsDashboardPage /> },
        ],
      },
      {
        path: 'analytics',
        element: <Navigate to="/metrics" replace />,
      },
    ],
  },
  {
    // Catch-all: render GlobalErrorPage for unknown paths (404 detected via useRouteError())
    path: '*',
    element: <GlobalErrorPage />,
  },
]);

// WCAG 2.3.3 Animation from Interactions — live CSS media query handles OS setting
// changes during session without a page reload. Theme-level transitions.create also
// returns 'none' when the preference was set at page-load time (see healthcare-theme.ts).
const reducedMotionStyles = (
  <GlobalStyles
    styles={{
      '@media (prefers-reduced-motion: reduce)': {
        '*': {
          animationDuration: '0.01ms !important',
          animationIterationCount: '1 !important',
          transitionDuration: '0.01ms !important',
          scrollBehavior: 'auto !important',
        },
      },
    }}
  />
);

export default function App() {
  return (
    <AppErrorBoundary>
    <ThemeProvider theme={healthcareTheme}>
      <CssBaseline />
      {reducedMotionStyles}
      <QueryClientProvider client={queryClient}>
        <RouterProvider router={router} />
        {/* ToastProvider renders app-global toast stack (US_038 AC-2, AC-3, AC-5) */}
        <ToastProvider />
        {import.meta.env.DEV && <ReactQueryDevtools initialIsOpen={false} />}
      </QueryClientProvider>
    </ThemeProvider>
    </AppErrorBoundary>
  );
}

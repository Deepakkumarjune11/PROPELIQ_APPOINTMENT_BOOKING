import { Box, Drawer, Toolbar, useMediaQuery } from '@mui/material';
import { useTheme } from '@mui/material/styles';
import { Suspense, useState } from 'react';
import { Navigate, Outlet } from 'react-router-dom';

import { CardSkeleton } from '@/components/skeletons';

/**
 * Default Suspense fallback for lazily-loaded page routes.
 * Renders four summary card skeletons inside the main content area
 * so the layout shell (Header, Sidebar/BottomNav) remains visible during navigation.
 */
function PageLoadingFallback() {
  return (
    <Box sx={{ pt: 3 }}>
      <CardSkeleton count={4} />
    </Box>
  );
}


import BottomNav from './BottomNav';
import Header from './Header';
import Sidebar from './Sidebar';
import { ICON_RAIL_WIDTH, SIDEBAR_WIDTH } from './Sidebar';
import SkipToMainContent from '@/components/accessibility/SkipToMainContent';
import { AppBreadcrumbs } from '@/components/navigation/AppBreadcrumbs';
import { OnboardingTour } from '@/components/onboarding/OnboardingTour';

import { SessionTimeoutModal } from '@/components/auth/SessionTimeoutModal';
import { useSessionTimeout } from '@/hooks/useSessionTimeout';
import { useSwipeGesture } from '@/hooks/useSwipeGesture';
import { useAuthStore } from '@/stores/auth-store';

// SCR-025: Authenticated layout shell — three-tier adaptive navigation
// UXR-201: Desktop (lg+, ≥1200px) — full 240px sidebar
// UXR-202: Tablet (md–lg, 900–1199px) — 64px icon-rail sidebar
// UXR-203: Mobile (<md, <900px) — bottom nav + swipe-to-open overlay drawer
export default function AuthenticatedLayout() {
  const theme = useTheme();

  // Three-tier breakpoint detection (design-tokens-applied.md#8-responsive-breakpoint-token-application)
  const isDesktop = useMediaQuery(theme.breakpoints.up('lg'));   // ≥ 1200px — full sidebar
  const isTablet  = useMediaQuery(theme.breakpoints.between('md', 'lg')); // 900–1199px — icon rail

  const { isAuthenticated, user } = useAuthStore();

  // Swipe-to-open overlay drawer state (mobile only — supplementary to bottom nav)
  const [mobileDrawerOpen, setMobileDrawerOpen] = useState(false);

  // Session timeout: warns at 14 min idle; auto-logs out at 15 min (AC-4, AC-5)
  const { showModal, setShowModal, countdown } = useSessionTimeout();

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  const isStaffOrAdmin = user?.role === 'staff' || user?.role === 'admin';

  // Derive display flags from breakpoint + role
  const showFullSidebar = isDesktop && isStaffOrAdmin;   // persistent 240px drawer
  const showIconRail    = isTablet  && isStaffOrAdmin;   // persistent 64px icon-rail
  const showBottomNav   = !isDesktop && !isTablet;        // < 900px — bottom tabs

  // Swipe gesture spread props — wired to main content box on mobile (AC-4 UXR-203)
  const swipeHandlers = useSwipeGesture({
    onSwipeRight: () => isStaffOrAdmin && setMobileDrawerOpen(true),
    onSwipeLeft:  () => setMobileDrawerOpen(false),
  });

  // Content left margin = sidebar width when a sidebar is present, else 0
  const contentMarginLeft = showFullSidebar
    ? SIDEBAR_WIDTH
    : showIconRail
      ? ICON_RAIL_WIDTH
      : 0;

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh' }}>
      {/* WCAG 2.4.1: Skip link must be the FIRST focusable element so Tab as first
          keypress immediately reveals the "Skip to main content" banner */}
      <SkipToMainContent />
      <Header />

      {/* Breadcrumb navigation — renders when user is more than 1 level deep (AC-1 / UXR-002) */}
      <AppBreadcrumbs />

      {/* Persistent full sidebar — desktop (≥ 1200px) */}
      {showFullSidebar && <Sidebar />}

      {/* Persistent icon rail — tablet (900–1199px) */}
      {showIconRail && <Sidebar iconRail />}

      {/* Swipe-to-open overlay drawer — mobile (< 900px), staff/admin only */}
      {showBottomNav && isStaffOrAdmin && (
        <Drawer
          variant="temporary"
          open={mobileDrawerOpen}
          onClose={() => setMobileDrawerOpen(false)}
          ModalProps={{ keepMounted: true }} // better mobile performance
          sx={{
            '& .MuiDrawer-paper': {
              width: SIDEBAR_WIDTH,
              boxSizing: 'border-box',
            },
          }}
        >
          <Sidebar />
        </Drawer>
      )}

      <Box
        component="main"
        id="main-content"
        aria-label="Main content"
        tabIndex={-1}
        // Spread swipe handlers on mobile so swipe-right opens the overlay drawer
        {...(showBottomNav ? swipeHandlers : {})}
        sx={{
          flexGrow: 1,
          p: 3,
          // Extra bottom padding prevents last content element hiding behind BottomNav
          pb: showBottomNav ? 9 : 3,
          // Margin left accounts for persistent sidebar/icon-rail width
          ml: `${contentMarginLeft}px`,
          // Explicit width prevents content from overflowing horizontally (WCAG 1.4.4 Reflow)
          width: `calc(100% - ${contentMarginLeft}px)`,
          // Remove default outline on programmatic focus from skip link (visual focus
          // is already communicated by the SkipToMainContent banner)
          '&:focus': { outline: 'none' },
        }}
      >
        {/* Spacer element equals AppBar height — required because AppBar is position:fixed */}
        <Toolbar />
        {/* Suspense boundary: shows PageLoadingFallback for lazily-loaded page chunks (US_038 AC-1) */}
        <Suspense fallback={<PageLoadingFallback />}>
          <Outlet />
        </Suspense>
      </Box>

      {showBottomNav && <BottomNav />}

      {/* Global session timeout modal — rendered at layout root so it overlays all child routes */}
      <SessionTimeoutModal
        open={showModal}
        countdown={countdown}
        onClose={() => setShowModal(false)}
      />

      {/* Guided onboarding tour — auto-starts on first login; self-manages via onboarding-store (AC-2) */}
      <OnboardingTour />
    </Box>
  );
}

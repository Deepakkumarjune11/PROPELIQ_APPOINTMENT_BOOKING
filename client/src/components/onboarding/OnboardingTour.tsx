import { useEffect } from 'react';

import { useAuthStore } from '@/stores/auth-store';
import { useOnboardingStore } from '@/stores/onboarding-store';
import { TourStep, TourStepTooltip } from './TourStepTooltip';

/**
 * Tour steps for the guided onboarding flow (US_040 AC-2 / UXR-003).
 *
 * `targetId` maps to `id` attributes on Sidebar nav items (desktop).
 * `mobileTargetId` maps to `id` attributes on BottomNav actions (mobile).
 *
 * Static labels only in Phase 1. Dynamic crumbs (e.g., patient name) can be added
 * in a future iteration when loader data is available.
 *
 * Click depth audit (AC-5): all 3 features are reachable in 1 sidebar click from Dashboard.
 */
const TOUR_STEPS: TourStep[] = [
  {
    id: 'book-appointment',
    targetId: 'nav-book',
    mobileTargetId: 'nav-book-mobile',
    title: 'Book an Appointment',
    body: 'Search available slots and book your appointment in just a few clicks.',
    placement: 'right',
  },
  {
    id: 'upload-document',
    targetId: 'nav-documents',
    mobileTargetId: 'nav-documents-mobile',
    title: 'Upload Documents',
    body: 'Upload insurance cards, referrals, and medical records securely.',
    placement: 'right',
  },
  {
    id: 'patient-profile',
    targetId: 'nav-profile',
    mobileTargetId: 'nav-profile-mobile',
    title: 'Your Profile',
    body: 'View and update your personal details and appointment history.',
    placement: 'right',
  },
];

/**
 * Guided onboarding tour orchestrator (US_040 AC-2).
 *
 * Auto-starts on the first authenticated render when `hasCompletedOnboarding` is `false`.
 * Returns `null` when the tour is inactive (`currentStep === -1`), imposing zero overhead
 * on users who have already completed or skipped the tour.
 *
 * Reads `isAuthenticated` from `auth-store` so the tour never fires on the login page
 * or before credentials are established.
 */
export function OnboardingTour() {
  const { isAuthenticated } = useAuthStore();
  const { currentStep, hasCompletedOnboarding, startTour } = useOnboardingStore();

  // Auto-start: fires once after the authenticated layout mounts for the first time.
  // Dependency array includes `isAuthenticated` — tour only activates when logged in.
  useEffect(() => {
    if (isAuthenticated && !hasCompletedOnboarding) {
      startTour();
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isAuthenticated]);

  // Tour inactive — render nothing
  if (currentStep < 0 || currentStep >= TOUR_STEPS.length) return null;

  return (
    <TourStepTooltip
      step={TOUR_STEPS[currentStep]}
      stepIndex={currentStep}
      totalSteps={TOUR_STEPS.length}
    />
  );
}

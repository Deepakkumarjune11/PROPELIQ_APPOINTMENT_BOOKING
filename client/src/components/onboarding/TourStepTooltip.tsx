import { Backdrop, Box, Button, Chip, Paper, Popover, Typography } from '@mui/material';
import { useEffect, useRef, useState } from 'react';
import { useMediaQuery } from '@mui/material';

import { useOnboardingStore } from '@/stores/onboarding-store';

export interface TourStep {
  /** Unique step identifier. */
  id: string;
  /** `id` attribute of the desktop (sidebar) target element. */
  targetId: string;
  /** `id` attribute of the mobile (bottom nav) target element — used as fallback. */
  mobileTargetId?: string;
  /** Popover heading. */
  title: string;
  /** Popover body copy. */
  body: string;
  /** MUI Popover anchor origin vertical position. */
  placement?: 'right' | 'bottom' | 'top' | 'left';
}

interface TourStepTooltipProps {
  step: TourStep;
  stepIndex: number;
  totalSteps: number;
}

/**
 * Single onboarding tour step rendered as a MUI Popover anchored to a DOM element by id.
 *
 * Uses `Popover` rather than `Tooltip` because `Tooltip` is hover-activated only;
 * `Popover` supports programmatic `open` control with proper focus management.
 *
 * **Accessibility (WAI-ARIA):**
 * - `aria-live="polite"` on content — screen readers announce each new step.
 * - "Skip Tour" has `aria-label="Skip onboarding tour"` for assistive tech.
 * - `autoFocus` on "Next" / "Done" button — focus lands on the primary action on open.
 *
 * **Reduced motion**: `transitionDuration={0}` when OS reduced-motion preference is set,
 * consistent with the `prefersReducedMotion` guard in `healthcare-theme.ts` (US_036).
 */
export function TourStepTooltip({ step, stepIndex, totalSteps }: TourStepTooltipProps) {
  const { nextStep, skipTour } = useOnboardingStore();
  const [anchorEl, setAnchorEl] = useState<HTMLElement | null>(null);
  const prefersReducedMotion = useMediaQuery('(prefers-reduced-motion: reduce)');
  // Track whether we have located the target element; avoids re-querying on every render
  const resolvedRef = useRef(false);

  useEffect(() => {
    resolvedRef.current = false;

    // Try desktop target first, fall back to mobile target
    const el =
      document.getElementById(step.targetId) ??
      document.getElementById(step.mobileTargetId ?? '');

    setAnchorEl(el);
    resolvedRef.current = el !== null;
  }, [step.targetId, step.mobileTargetId]);

  if (!anchorEl) return null;

  const isLastStep = stepIndex === totalSteps - 1;

  const anchorOrigin =
    step.placement === 'right'
      ? { vertical: 'center' as const, horizontal: 'right' as const }
      : step.placement === 'bottom'
        ? { vertical: 'bottom' as const, horizontal: 'center' as const }
        : step.placement === 'top'
          ? { vertical: 'top' as const, horizontal: 'center' as const }
          : { vertical: 'center' as const, horizontal: 'left' as const };

  const transformOrigin =
    step.placement === 'right'
      ? { vertical: 'center' as const, horizontal: 'left' as const }
      : step.placement === 'bottom'
        ? { vertical: 'top' as const, horizontal: 'center' as const }
        : step.placement === 'top'
          ? { vertical: 'bottom' as const, horizontal: 'center' as const }
          : { vertical: 'center' as const, horizontal: 'right' as const };

  return (
    <>
      {/* Semi-transparent backdrop draws attention to the highlighted element */}
      <Backdrop
        open
        sx={{
          zIndex: 1400,
          bgcolor: 'rgba(0, 0, 0, 0.45)',
          // Backdrop sits below the Popover (zIndex 1500) and highlighted target
        }}
        onClick={skipTour}
      />

      <Popover
        open
        anchorEl={anchorEl}
        anchorOrigin={anchorOrigin}
        transformOrigin={transformOrigin}
        disablePortal={false}
        // Prevent closing on backdrop click — handled by Backdrop above
        onClose={() => {}}
        disableEscapeKeyDown
        transitionDuration={prefersReducedMotion ? 0 : 150}
        sx={{ zIndex: 1500 }}
        PaperProps={{
          sx: { maxWidth: 320, p: 0, overflow: 'visible' },
          component: Paper,
        }}
      >
        <Box
          sx={{ p: 2.5 }}
          // aria-live announces new step content to screen readers on each step advance
          aria-live="polite"
          aria-atomic="true"
        >
          {/* Step counter chip */}
          <Chip
            label={`${stepIndex + 1} / ${totalSteps}`}
            size="small"
            color="primary"
            sx={{ mb: 1.5, fontSize: '0.7rem' }}
          />

          <Typography variant="subtitle2" gutterBottom sx={{ fontWeight: 600 }}>
            {step.title}
          </Typography>

          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            {step.body}
          </Typography>

          <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <Button
              size="small"
              color="inherit"
              onClick={skipTour}
              aria-label="Skip onboarding tour"
              sx={{ color: 'text.secondary', fontSize: '0.75rem' }}
            >
              Skip Tour
            </Button>
            <Button
              size="small"
              variant="contained"
              // eslint-disable-next-line jsx-a11y/no-autofocus
              autoFocus
              onClick={() => nextStep(totalSteps)}
            >
              {isLastStep ? 'Done ✓' : 'Next →'}
            </Button>
          </Box>
        </Box>
      </Popover>
    </>
  );
}

import { create } from 'zustand';
import { persist } from 'zustand/middleware';

interface OnboardingState {
  /** `true` after the user completes or skips the tour. Persisted in localStorage. */
  hasCompletedOnboarding: boolean;
  /**
   * Index of the currently active tour step.
   * `-1` means the tour is not active.
   * Persisted so the tour resumes at the same step after a page refresh.
   */
  currentStep: number;
  /**
   * Starts the tour from step 0. Guards against re-activation when the user has
   * already completed or skipped the tour.
   */
  startTour: () => void;
  /** Advances to the next step. Calls `skipTour` when the last step is reached. */
  nextStep: (totalSteps: number) => void;
  /** Navigates back to the previous step (minimum 0). */
  prevStep: () => void;
  /**
   * Marks the tour as completed and deactivates it.
   * Called by "Skip Tour" and automatically after the last step.
   */
  skipTour: () => void;
  /**
   * Resets the tour so it will auto-start again on the next authenticated render.
   * Exposed for the "Restart Tour" help-menu action (edge case — AC-2).
   * Security note: only persists a boolean + integer — no PII (OWASP A02 safe).
   */
  resetOnboarding: () => void;
}

export const useOnboardingStore = create<OnboardingState>()(
  persist(
    (set, get) => ({
      hasCompletedOnboarding: false,
      currentStep: -1,

      startTour: () => {
        if (!get().hasCompletedOnboarding) {
          set({ currentStep: 0 });
        }
      },

      nextStep: (totalSteps: number) => {
        const next = get().currentStep + 1;
        if (next >= totalSteps) {
          // Last step done — treat as completion
          set({ hasCompletedOnboarding: true, currentStep: -1 });
        } else {
          set({ currentStep: next });
        }
      },

      prevStep: () => {
        set((s) => ({ currentStep: Math.max(0, s.currentStep - 1) }));
      },

      skipTour: () => {
        set({ hasCompletedOnboarding: true, currentStep: -1 });
      },

      resetOnboarding: () => {
        set({ hasCompletedOnboarding: false, currentStep: -1 });
      },
    }),
    {
      name: 'propeliq-onboarding',
      // Persist only the two state fields — actions are never serialised
      partialize: (s) => ({
        hasCompletedOnboarding: s.hasCompletedOnboarding,
        currentStep: s.currentStep,
      }),
    },
  ),
);

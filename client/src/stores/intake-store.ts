// Zustand store for the intake flow — shared between manual (SCR-004) and conversational (SCR-005) modes.
// Persisted to sessionStorage so partial answers survive page refresh / back-navigation (AC-2, AC-3).
// Cleared on tab close; never written to localStorage to avoid PHI persisting across sessions.
import { create } from 'zustand';
import { createJSONStorage, persist } from 'zustand/middleware';

export type IntakeMode = 'manual' | 'conversational';

interface IntakeState {
  /** Canonical answer map: questionId → free-text answer. Shared across both intake modes (AC-2, AC-3). */
  answers: Record<string, string>;
  /** Currently active intake mode; null until the patient first enters an intake route. */
  mode: IntakeMode | null;
  setAnswer: (questionId: string, value: string) => void;
  setMode: (mode: IntakeMode) => void;
  /**
   * Merges structured answers gathered by the conversational AI into the shared answers map (AC-3).
   * Existing answers are NOT cleared — conversational answers are shallow-merged on top so that
   * switching back to manual form pre-populates all fields correctly.
   */
  mergeAnswers: (incoming: Record<string, string>) => void;
  clearIntake: () => void;
}

export const useIntakeStore = create<IntakeState>()(
  persist(
    (set) => ({
      answers: {},
      mode: null,

      setAnswer: (questionId, value) =>
        set((state) => ({
          answers: { ...state.answers, [questionId]: value },
        })),

      setMode: (mode) => set({ mode }),

      mergeAnswers: (incoming) =>
        set((state) => ({
          answers: { ...state.answers, ...incoming },
        })),

      clearIntake: () => set({ answers: {}, mode: null }),
    }),
    {
      name: 'propeliq-intake',
      storage: createJSONStorage(() => sessionStorage),
    },
  ),
);

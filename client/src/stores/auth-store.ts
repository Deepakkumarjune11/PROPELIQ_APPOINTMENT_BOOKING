import { create } from 'zustand';
import { persist } from 'zustand/middleware';

export interface UserProfile {
  id: string;
  email: string;
  name: string;
  role: 'patient' | 'staff' | 'admin';
}

interface AuthState {
  user: UserProfile | null;
  /** JWT access token — short-lived (15 min per NFR-005). Stored in localStorage via persist. */
  token: string | null;
  /** Unix timestamp (ms) when the current token expires. */
  expiresAt: number | null;
  isAuthenticated: boolean;
  /** Unix timestamp (ms) of the last recorded user activity — used by useSessionTimeout. */
  lastActivity: number;
  /** Called on successful login or token refresh. */
  setAuth: (user: UserProfile, token: string, expiresAt: number) => void;
  logout: () => void;
  /** Resets lastActivity to now — called by useSessionTimeout activity listeners. */
  resetActivity: () => void;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      user: null,
      token: null,
      expiresAt: null,
      isAuthenticated: false,
      lastActivity: Date.now(),

      setAuth: (user, token, expiresAt) =>
        set({ user, token, expiresAt, isAuthenticated: true, lastActivity: Date.now() }),

      logout: () =>
        set({ user: null, token: null, expiresAt: null, isAuthenticated: false }),

      resetActivity: () => set({ lastActivity: Date.now() }),
    }),
    {
      name: 'propeliq-auth',
      // Only persist the fields required to restore session on page reload (OWASP A02).
      // lastActivity is intentionally excluded — resets per-session as expected.
      partialize: (s) => ({ token: s.token, user: s.user, expiresAt: s.expiresAt }),
    },
  ),
);


import { useMutation } from '@tanstack/react-query';

import api from '@/lib/api';
import { useAuthStore } from '@/stores/auth-store';

interface RefreshResponse {
  user: import('@/stores/auth-store').UserProfile;
  token: string;
  expiresAt: number;
}

/**
 * React Query mutation hook for `POST /api/v1/auth/refresh` (US_039 AC-5, NFR-005).
 *
 * On success: updates `auth-store` with the new token and expiry.
 * On 401: the Axios response interceptor in `lib/api.ts` calls `logout()` automatically,
 * preventing an infinite refresh loop (OWASP A07 — Authentication Failures).
 *
 * The existing Bearer token is attached by the Axios request interceptor — no manual
 * `Authorization` header management is required here.
 *
 * @example
 * ```tsx
 * const { mutate: refreshToken, isPending } = useRefreshToken();
 * // Call on "Stay Logged In" action:
 * refreshToken();
 * ```
 */
export function useRefreshToken() {
  const { setAuth } = useAuthStore();

  return useMutation<RefreshResponse, Error>({
    mutationFn: () =>
      api.post<RefreshResponse>('/api/v1/auth/refresh').then((r) => r.data),

    onSuccess: (data) => {
      setAuth(data.user, data.token, data.expiresAt);
    },
    // No onError handler — the Axios 401 interceptor calls logout() which clears
    // isAuthenticated, causing AuthenticatedLayout to redirect to /login.
  });
}

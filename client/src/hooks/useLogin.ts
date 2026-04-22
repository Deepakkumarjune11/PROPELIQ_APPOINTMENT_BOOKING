import { useMutation } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';

import api from '@/lib/api';
import { useAuthStore, type UserProfile } from '@/stores/auth-store';

export interface LoginRequest {
  email: string;
  password: string;
  rememberMe?: boolean;
}

/** Shape returned by POST /api/v1/auth/login */
interface ApiLoginResponse {
  token: string;
  userId: string;
  username: string;
  role: string;       // PascalCase from backend: "FrontDesk" | "CallCenter" | "ClinicalReviewer" | "Admin"
  expiresAt: number;  // Unix ms
}

/** Map backend role string to the frontend union expected by UserProfile. */
function normalizeRole(role: string): UserProfile['role'] {
  if (role === 'Admin') return 'admin';
  if (role === 'Patient') return 'patient';
  // FrontDesk, CallCenter, ClinicalReviewer all map to 'staff'
  return 'staff';
}

/**
 * Mutation wrapper for `POST /api/v1/auth/login` (US_024, AC-2).
 *
 * On success:
 * - Stores token + user + expiresAt via `setAuth`.
 * - Navigates to the role-appropriate dashboard (TR-010):
 *   staff → `/staff/dashboard`, admin → `/admin/dashboard`, patient → `/`.
 *
 * On error: propagates the Axios error to the component so the Alert banner
 * can display `error.response?.data?.message`.
 */
export function useLogin() {
  const setAuth = useAuthStore((s) => s.setAuth);
  const navigate = useNavigate();

  return useMutation<ApiLoginResponse, Error, LoginRequest>({
    mutationFn: (credentials) =>
      api.post<ApiLoginResponse>('/api/v1/auth/login', credentials).then((r) => r.data),

    onSuccess: (data) => {
      const role = normalizeRole(data.role);
      const user: UserProfile = {
        id: data.userId,
        email: data.username,
        name: data.username,
        role,
      };
      setAuth(user, data.token, data.expiresAt);
      if (role === 'staff') navigate('/staff/dashboard', { replace: true });
      else if (role === 'admin') navigate('/admin/dashboard', { replace: true });
      else if (role === 'patient') navigate('/appointments', { replace: true });
      else navigate('/', { replace: true });
    },
  });
}

/**
 * Shared Axios instance for all PropelIQ API calls (US_024).
 *
 * - Base URL sourced from VITE_API_URL env var (falls back to same-origin).
 * - Authorization header injected from auth-store on every request.
 * - 401 response interceptor clears the auth store (OWASP A07 — session invalidation
 *   on expired/invalid token). Import this instance anywhere you need authenticated calls.
 */
import axios from 'axios';

import { useAuthStore } from '@/stores/auth-store';

const BASE_URL = (import.meta.env.VITE_API_URL as string | undefined) ?? '';

const api = axios.create({
  baseURL: BASE_URL,
  headers: { 'Content-Type': 'application/json' },
});

// Request interceptor — attach Bearer token from store (OWASP A07)
api.interceptors.request.use((config) => {
  const token = useAuthStore.getState().token;
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  // For FormData (multipart/form-data) requests, remove the default
  // application/json Content-Type so Axios can auto-set the correct
  // multipart boundary required by the server ([Consumes("multipart/form-data")]).
  if (config.data instanceof FormData) {
    delete config.headers['Content-Type'];
  }
  return config;
});

// Response interceptor — clear session on 401 (token expired / revoked)
// per NFR-005 and FR-017. Does NOT navigate — navigation is handled by
// AuthenticatedLayout's redirect when isAuthenticated becomes false.
api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (axios.isAxiosError(error) && error.response?.status === 401) {
      useAuthStore.getState().logout();
    }
    return Promise.reject(error);
  },
);

export default api;

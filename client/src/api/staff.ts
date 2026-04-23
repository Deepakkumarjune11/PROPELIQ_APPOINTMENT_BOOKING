// API client for staff-facing endpoints (US_016, US_017).
// GET  /api/v1/patients/search             — autocomplete patient search
// POST /api/v1/patients                    — create minimal patient profile
// POST /api/v1/appointments/walk-in        — book walk-in / add to wait queue
// GET  /api/v1/staff/dashboard/summary     — summary card counts
// GET  /api/v1/staff/queue                 — today's same-day queue
// PATCH /api/v1/staff/queue/reorder        — reorder queue by drag-and-drop
// PATCH /api/v1/appointments/{id}/status   — update individual appointment status
import { useAuthStore } from '@/stores/auth-store';

import api from '@/lib/api';

// ── Domain types ────────────────────────────────────────────────────────────

export interface PatientSearchResult {
  id: string;
  fullName: string;
  email: string;
  phone: string;
}

export interface CreatePatientRequest {
  fullName: string;
  email: string;
  phone: string;
}

export interface DashboardSummary {
  walkInsToday: number;
  queueLength: number;
  verificationPending: number;
  criticalConflicts: number;
}

export interface QueueEntry {
  appointmentId: string;
  queuePosition: number;
  patientName: string;
  appointmentTime: string; // ISO-8601
  status: 'waiting' | 'arrived' | 'in-room' | 'completed' | 'left';
  visitType: string;
}

export interface DashboardData {
  summary: DashboardSummary;
  queue: QueueEntry[];
}

export interface WalkInBookingRequest {
  patientId: string;
  visitType: string;
}

export interface WalkInBookingResult {
  appointmentId: string;
  queuePosition: number;
  /** true when no slots are available — patient placed on wait queue (AC-5). */
  waitQueue: boolean;
}

/** Thrown on any non-2xx response from a staff API call. */
export class StaffApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
  ) {
    super(message);
    this.name = 'StaffApiError';
  }
}

// ── API functions ─────────────────────────────────────────────────────────────

/**
 * Searches patients by email or phone (min 2-char query enforced by caller).
 * GET /api/v1/patients/search?q={query}
 */
export async function searchPatients(query: string): Promise<PatientSearchResult[]> {
  const res = await api.get<PatientSearchResult[]>('/api/v1/patients/search', { params: { q: query } });
  return res.data;
}

/**
 * Creates a minimal patient profile.
 * POST /api/v1/patients
 */
export async function createPatient(data: CreatePatientRequest): Promise<PatientSearchResult> {
  const res = await api.post<PatientSearchResult>('/api/v1/patients', data);
  return res.data;
}

/**
 * Books a walk-in appointment or adds the patient to the wait queue.
 * POST /api/v1/appointments/walk-in
 */
export async function bookWalkIn(data: WalkInBookingRequest): Promise<WalkInBookingResult> {
  const res = await api.post<WalkInBookingResult>('/api/v1/appointments/walk-in', data);
  return res.data;
}

/**
 * Fetches the staff dashboard summary counts and same-day queue.
 * Calls /api/v1/staff/dashboard/summary (flat summary) and /api/v1/staff/queue in parallel,
 * then composes them into the DashboardData shape the page expects.
 */
export async function getDashboardData(): Promise<DashboardData> {
  const [summaryRes, queueRes] = await Promise.all([
    api.get<DashboardSummary>('/api/v1/staff/dashboard/summary'),
    api.get<QueueEntry[]>('/api/v1/staff/queue'),
  ]);
  return { summary: summaryRes.data, queue: queueRes.data };
}

/**
 * Fetches today's same-day queue ordered by queue_position.
 * GET /api/v1/staff/queue
 */
export async function getSameDayQueue(): Promise<QueueEntry[]> {
  const res = await api.get<QueueEntry[]>('/api/v1/staff/queue');
  return res.data;
}

/**
 * Persists a drag-and-drop reorder of the same-day queue.
 * PATCH /api/v1/staff/queue/reorder
 */
export async function reorderQueue(orderedIds: string[]): Promise<void> {
  await api.patch('/api/v1/staff/queue/reorder', { orderedAppointmentIds: orderedIds });
}

/**
 * Updates a single appointment's arrival status.
 * PATCH /api/v1/appointments/{appointmentId}/status
 */
export async function updateAppointmentStatus(
  appointmentId: string,
  status: 'arrived' | 'in-room' | 'left',
): Promise<void> {
  await api.patch(`/api/v1/appointments/${encodeURIComponent(appointmentId)}/status`, { status });
}

/**
 * Returns the auth token string for SignalR accessTokenFactory.
 */
export function getAuthToken(): string {
  return useAuthStore.getState().token ?? '';
}

// API client for GET /api/v1/appointments/availability
// Accepts ISO-8601 date strings (YYYY-MM-DD); returns typed AvailabilitySlot array.
import api from '@/lib/api';

export interface AvailabilitySlot {
  id: string;
  /** ISO-8601 datetime e.g. "2026-04-19T09:00:00" */
  datetime: string;
  provider: string;
  specialty?: string;
  visitType: 'in-person' | 'telehealth';
  /** Probability 0–1 from the clinical intelligence model. Badge shown when > 0.7. */
  noShowRisk?: number;
  /** Human-readable contributing factor strings rendered in the no-show risk tooltip (AC-2). */
  riskContributingFactors?: string[];
  /** True when patient signals were unavailable at search time; shown as italic footer in tooltip. */
  isPartialScoring?: boolean;
  /** Free-form location string (clinic address or "Telehealth"). */
  location?: string;
  /** Appointment duration in minutes. */
  durationMinutes?: number;
}

export async function fetchAvailability(
  startDate: string,
  endDate: string,
): Promise<AvailabilitySlot[]> {
  const response = await api.get<AvailabilitySlot[]>('/api/v1/appointments/availability', {
    params: { startDate, endDate },
  });
  return response.data;
}

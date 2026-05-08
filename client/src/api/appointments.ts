// API client for appointment list and preferred-slot watchlist endpoints.
// GET  /api/v1/appointments                                — authenticated patient's bookings
// GET  /api/v1/slots/availability                         — per-provider slot availability for calendar
// POST /api/v1/appointments/{appointmentId}/preferred-slot — watchlist registration (US_015)
import api from '@/lib/api';

// ── Domain types ────────────────────────────────────────────────────────────────

export interface AppointmentDto {
  id: string;
  /** ISO-8601 datetime e.g. "2026-04-19T09:00:00" */
  slotDatetime: string;
  providerName: string | null;
  /** Opaque provider identifier used for slot-availability lookups. Null when not stored on entity. */
  providerId: string | null;
  visitType: string | null;
  status: 'booked' | 'arrived' | 'completed' | 'cancelled' | 'no-show';
  /** null = not on the swap watchlist; non-null = preferred slot datetime registered. */
  preferredSlotDatetime: string | null;
}

export interface SlotAvailabilityEntry {
  /** ISO-8601 datetime of the slot. */
  datetime: string;
  /**
   * true = slot is open for direct booking (disabled in SCR-009 calendar).
   * false = slot is already booked and eligible for watchlist enrollment.
   * Matches the backend's camelCase serialised field name `isAvailable`.
   */
  isAvailable: boolean;
}

/** Error shape thrown by all appointment API functions on non-2xx responses. */
export class AppointmentsApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
    public readonly body?: unknown,
  ) {
    super(message);
    this.name = 'AppointmentsApiError';
  }
}

// ── API functions ───────────────────────────────────────────────────────────────

/**
 * Fetches all appointments for the authenticated patient.
 * GET /api/v1/appointments
 */
export async function getAppointments(): Promise<AppointmentDto[]> {
  const response = await api.get<AppointmentDto[]>('/api/v1/appointments');
  return response.data;
}

/**
 * Fetches per-provider slot availability for a given month.
 * GET /api/v1/slots/availability?providerId=&year=&month=
 *
 * Returns all slots (both open and booked) so SCR-009 can distinguish
 * watchlist-eligible (booked) from directly-bookable (available) slots.
 */
export async function getSlotAvailability(
  year: number,
  month: number,
  providerId?: string | null,
): Promise<SlotAvailabilityEntry[]> {
  const response = await api.get<SlotAvailabilityEntry[]>('/api/v1/slots/availability', {
    params: { year, month, ...(providerId ? { providerId } : {}) },
  });
  return response.data;
}

/**
 * Registers a preferred slot on the swap watchlist.
 * POST /api/v1/appointments/{appointmentId}/preferred-slot
 * Body: { preferredSlotDatetime: string }
 *
 * Throws AppointmentsApiError with status 422 when the slot is no longer
 * eligible (already claimed or no longer booked). Booking is not blocked.
 */
export async function registerPreferredSlot(
  appointmentId: string,
  preferredSlotDatetime: string,
): Promise<void> {
  await api.post(
    `/api/v1/appointments/${encodeURIComponent(appointmentId)}/preferred-slot`,
    { preferredSlotDatetime },
  );
}



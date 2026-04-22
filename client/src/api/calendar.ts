// API client for GET /api/v1/calendar/{provider}/init
// Returns the OAuth authorization URL for calendar sync (TR-012).
import api from '@/lib/api';

export type CalendarProvider = 'google' | 'outlook';

export interface CalendarInitResponse {
  /** OAuth 2.0 authorization URL — the FE redirects the browser to this URL to begin consent flow. */
  authUrl: string;
}

/**
 * Fetches the OAuth authorization URL for the given calendar provider.
 * The caller should redirect via `window.location.href = authUrl` to initiate the OAuth flow.
 * On completion, the backend callback redirects back to `/appointments/confirmation?calendarSynced={provider}`.
 */
export async function getCalendarInitUrl(
  provider: CalendarProvider,
  appointmentId: string,
): Promise<CalendarInitResponse> {
  const res = await api.get<CalendarInitResponse>(
    `/api/v1/calendar/${encodeURIComponent(provider)}/init`,
    { params: { appointmentId } },
  );
  return res.data;
}

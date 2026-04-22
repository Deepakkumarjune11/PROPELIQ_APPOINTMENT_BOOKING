// React Query hook — fetches the authenticated patient's appointment list.
// staleTime: 30 s — appointments rarely change mid-session (AC-4 freshness requirement).
import { useQuery } from '@tanstack/react-query';

import { type AppointmentDto, getAppointments } from '@/api/appointments';

const STALE_TIME_MS = 30_000;

export function useAppointments() {
  const { data, isLoading, isError, refetch } = useQuery<AppointmentDto[], Error>({
    queryKey: ['appointments'],
    queryFn: getAppointments,
    staleTime: STALE_TIME_MS,
  });

  return {
    appointments: data ?? [],
    isLoading,
    isError,
    refetch,
  };
}

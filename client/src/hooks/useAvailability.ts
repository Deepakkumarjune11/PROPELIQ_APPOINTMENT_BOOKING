import { useQuery } from '@tanstack/react-query';

import { type AvailabilitySlot, fetchAvailability } from '@/api/availability';

// staleTime matches Redis TTL (60 s) per AC-2 — prevents redundant re-fetches within cache window.
const STALE_TIME_MS = 60_000;

export function useAvailability(startDate: string, endDate: string) {
  const { data, isLoading, isError, refetch } = useQuery<AvailabilitySlot[], Error>({
    queryKey: ['availability', startDate, endDate],
    queryFn: () => fetchAvailability(startDate, endDate),
    enabled: Boolean(startDate && endDate),
    staleTime: STALE_TIME_MS,
  });

  return {
    slots: data ?? [],
    isLoading,
    isError,
    refetch,
  };
}

import { useQuery } from '@tanstack/react-query';

import { type AvailabilitySlot, fetchAvailability } from '@/api/availability';

// staleTime: 0 — always fetch fresh availability; slots change in real time.
const STALE_TIME_MS = 0;

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

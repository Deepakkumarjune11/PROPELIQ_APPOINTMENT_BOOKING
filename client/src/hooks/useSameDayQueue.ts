// React Query hook for the same-day staff queue (US_017, AC-1, AC-5).
// staleTime: 30s matches the Redis TTL on the backend so the UI avoids redundant refetches
// while still reflecting slot changes within the cache window.
import { useQuery } from '@tanstack/react-query';

import { type QueueEntry, getSameDayQueue } from '@/api/staff';

export const QUEUE_QUERY_KEY = ['queue'] as const;

export function useSameDayQueue() {
  return useQuery<QueueEntry[], Error>({
    queryKey: QUEUE_QUERY_KEY,
    queryFn: getSameDayQueue,
    staleTime: 30_000, // 30s — aligns with Redis cache TTL (NFR-001)
    retry: 1,
  });
}

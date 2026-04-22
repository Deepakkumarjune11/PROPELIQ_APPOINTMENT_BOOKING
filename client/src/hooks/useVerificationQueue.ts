// React Query hook for the SCR-016 verification queue (US_021).
import { useQuery } from '@tanstack/react-query';

import { type VerificationQueueEntry, getVerificationQueue } from '@/api/patientView360';

export const VERIFICATION_QUEUE_QUERY_KEY = ['verificationQueue'] as const;

export function useVerificationQueue() {
  return useQuery<VerificationQueueEntry[], Error>({
    queryKey: VERIFICATION_QUEUE_QUERY_KEY,
    queryFn: getVerificationQueue,
    staleTime: 30_000,
    retry: 1,
  });
}

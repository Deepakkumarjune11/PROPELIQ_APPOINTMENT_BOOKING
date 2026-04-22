// React Query hook for the 360-degree patient view (US_021, AC-1).
// Fetches consolidated, de-duplicated facts grouped by category.
import { useQuery } from '@tanstack/react-query';

import { type PatientView360Dto, getPatientView360 } from '@/api/patientView360';

export const PATIENT_VIEW_360_QUERY_KEY = (patientId: string) =>
  ['patientView360', patientId] as const;

/**
 * Fetches the 360-degree patient view for the given patient.
 *
 * staleTime: 60 s — view assemblies are expensive; tolerate slight staleness in staff review.
 * retry: 2 — transient failures common on first-time assembly.
 */
export function usePatientView360(patientId: string) {
  return useQuery<PatientView360Dto, Error>({
    queryKey: PATIENT_VIEW_360_QUERY_KEY(patientId),
    queryFn: () => getPatientView360(patientId),
    staleTime: 60_000,
    retry: 2,
    enabled: patientId.length > 0,
  });
}

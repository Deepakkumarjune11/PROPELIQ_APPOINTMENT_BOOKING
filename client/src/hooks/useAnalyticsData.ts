// React Query hooks for analytics dashboard data (US_033, SCR-028).
import { useQuery } from '@tanstack/react-query';

import { fetchKpi, fetchSystemHealth, fetchTrends } from '@/api/analytics';
import type { DateRange } from '@/types/analytics';

export function useKpiMetrics(range: DateRange) {
  return useQuery({
    queryKey: ['analytics', 'kpi', range.startDate.toISOString(), range.endDate.toISOString()],
    queryFn: () => fetchKpi(range),
    staleTime: 30_000, // 30s — KPIs tolerate slightly stale data
    retry: 2,
  });
}

export function useTrendData(range: DateRange) {
  return useQuery({
    queryKey: ['analytics', 'trends', range.startDate.toISOString(), range.endDate.toISOString()],
    queryFn: () => fetchTrends(range),
    staleTime: 0, // AC-2: always re-fetch on date range change
    retry: 2,
  });
}

export function useSystemHealth() {
  return useQuery({
    queryKey: ['analytics', 'systemHealth'],
    queryFn: fetchSystemHealth,
    staleTime: 30_000,
    refetchInterval: 60_000, // Auto-refresh every 60s for live health status
  });
}

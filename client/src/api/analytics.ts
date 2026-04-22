// API client for /api/v1/analytics endpoints (US_033, SCR-028).
// Uses the shared Axios instance so the Bearer token interceptor applies (OWASP A07).
import type { DateRange, KpiMetrics, SystemHealth, TrendData } from '@/types/analytics';

import api from '@/lib/api';

const BASE = '/api/v1/analytics';

function toDateParam(date: Date): string {
  return date.toISOString().split('T')[0];
}

export async function fetchKpi(range: DateRange): Promise<KpiMetrics> {
  const res = await api.get<KpiMetrics>(`${BASE}/kpi`, {
    params: {
      startDate: toDateParam(range.startDate),
      endDate: toDateParam(range.endDate),
    },
  });
  return res.data;
}

export async function fetchTrends(range: DateRange): Promise<TrendData> {
  const res = await api.get<TrendData>(`${BASE}/trends`, {
    params: {
      startDate: toDateParam(range.startDate),
      endDate: toDateParam(range.endDate),
    },
  });
  return res.data;
}

export async function fetchSystemHealth(): Promise<SystemHealth> {
  const res = await api.get<SystemHealth>(`${BASE}/system-health`);
  return res.data;
}

export async function downloadExport(
  format: 'pdf' | 'csv',
  range: DateRange,
): Promise<void> {
  const res = await api.get(`${BASE}/export`, {
    params: {
      format,
      startDate: toDateParam(range.startDate),
      endDate: toDateParam(range.endDate),
    },
    responseType: 'blob',
  });
  const url = URL.createObjectURL(res.data as Blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `propeliq-metrics.${format}`;
  a.click();
  URL.revokeObjectURL(url);
}

# Task - task_001_fe_analytics_dashboard

## Requirement Reference

- **User Story**: US_033 — Operational Metrics Dashboard & Reporting
- **Story Location**: `.propel/context/tasks/EP-008/us_033/us_033.md`
- **Acceptance Criteria**:
  - AC-1: Given authenticated as staff or admin, when navigating to analytics dashboard (SCR-028), then I see real-time KPI cards for today's appointment count, no-show rate, average wait time, and AI suggestion acceptance rate per FR-018.
  - AC-2: Given the dashboard is loaded, when I select a date range filter, then all charts and metrics update to reflect the selected period within 2 seconds per NFR-018.
  - AC-3: Given operational data is available, when I view the dashboard, then I see trend charts for daily appointment volumes, weekly no-show rates, document processing throughput, and AI response latencies per FR-018.
  - AC-4: Given system health metrics are collected, when I view the system health panel, then I see API response time p50/p95/p99, database connection pool usage, cache hit ratios, and AI gateway status per TR-017.
  - AC-5: Given I need to share metrics, when I click "Export", then the system generates a PDF or CSV report of the currently displayed metrics and date range per FR-018.
- **Edge Cases**:
  - Delayed metrics collection: Dashboard shows last-known values with a "data delayed" indicator and timestamp of last refresh. Implemented via `dataFreshness` field in API response — if `> 60s` stale, render `<Chip label="Data delayed" color="warning">` with `lastRefreshed` timestamp.
  - High-load access: Metrics are pre-aggregated; dashboard queries read materialized views, not live OLTP tables — component has no special handling needed beyond standard loading states.

---

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | Yes |
| **Figma URL** | N/A |
| **Wireframe Status** | PENDING |
| **Wireframe Type** | HTML |
| **Wireframe Path/URL** | TODO: Upload to `.propel/context/wireframes/Hi-Fi/wireframe-SCR-028-analytics-dashboard.html` (referenced in us_033.md as AVAILABLE but file not found on disk) |
| **Screen Spec** | .propel/context/docs/figma_spec.md#SCR-028 |
| **UXR Requirements** | UXR-002 (breadcrumb navigation visible) |
| **Design Tokens** | .propel/context/docs/designsystem.md#colors, #typography, #spacing, #component-card |

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend Framework | React | 18.x |
| UI Components | Material-UI (MUI) | 5.x |
| State / Data Fetching | React Query (`@tanstack/react-query`) | 4.x |
| Global State | Zustand | 4.x |
| Charting | Recharts | 2.x (ADD to `package.json` — OSS MIT, satisfies NFR-015) |
| Language | TypeScript | 5.x |
| Build | Vite | 5.x |
| Routing | React Router DOM | 6.x |

> **`recharts` is not yet in `client/package.json`** — add `"recharts": "^2.12.x"` and `"@types/recharts"` devDependency. Recharts is built on React + SVG with responsive container support; no canvas or WebGL — ideal for clinical data dashboard without external rendering risk. Satisfies NFR-015 (free OSS).

---

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |

---

## Mobile References (Mobile Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **Mobile Impact** | No (responsive layout required per NFR-001; same React SPA serves mobile via MUI breakpoints) |

---

## Task Overview

Implement the Analytics Dashboard page (SCR-028) as a React SPA page accessible to Staff and Admin roles. The dashboard consists of four visual sections:

1. **KPI Cards row** (AC-1) — Four `<MetricKpiCard>` components displaying today's appointment count, no-show rate, average wait time, and AI suggestion acceptance rate. Each card has a Skeleton loading state, a numeric value, a label, and a trend badge (up/down/neutral `<Chip>`).

2. **Date Range Filter** (AC-2) — A MUI `DatePicker` pair (start + end date) at the top of the chart section. On change, all queries are invalidated and refetched. Must complete within 2 seconds — ensured by `staleTime: 0` on trend/health queries and materialized view queries on the backend.

3. **Trend Charts panel** (AC-3) — Recharts `<ResponsiveContainer>` wrapping:
   - `<BarChart>`: daily appointment volumes (x-axis: date, y-axis: count)
   - `<LineChart>`: weekly no-show rates + AI response latency p95 (dual y-axis via `<YAxis yAxisId>`)
   - `<PieChart>`: document processing throughput by status (Completed/ManualReview/Pending)

4. **System Health panel** (AC-4) — Linear gauges (MUI `<LinearProgress>`) for DB connection pool usage (%) and cache hit ratio (%), API latency p50/p95/p99 as a `<Chip>` group, AI gateway status as a color-coded `<Chip>` (green=Available, orange=Degraded, red=Unavailable).

5. **Export button** (AC-5) — `<Button variant="outlined" startIcon={<DownloadIcon />}>Export</Button>` with a `<Menu>` popup offering PDF and CSV options. Clicking either triggers a GET request to `/api/v1/analytics/export?format=pdf|csv&startDate=...&endDate=...` and downloads the blob response.

**Screen states** per figma_spec.md: Default, Loading (Skeleton), Empty (no data message), Error (Alert).

---

## Dependent Tasks

- **task_002_be_metrics_api_export.md** (US_033) — Backend API endpoints must exist for all `useQuery` calls in this task.
- **task_001_be_azure_openai_gateway_hardening.md** (US_030) — `latency_ms` field surfaced via the metrics API (sourced from Redis latency recorder from US_032).
- **task_001_be_latency_sla_schema_validation.md** (US_032) — `ILatencyRecorder.GetP95Async` used by BE metrics endpoint to serve AI latency data to this dashboard.

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | `client/package.json` | Add `"recharts": "^2.12.0"` to dependencies (charting library, MIT, NFR-015) |
| CREATE | `client/src/pages/AnalyticsDashboardPage.tsx` | Top-level page component (route `/analytics`); composes all dashboard sections; handles date range state |
| CREATE | `client/src/components/analytics/MetricKpiCard.tsx` | KPI card: title, value, unit, trend chip, Skeleton loading state; MUI `Card` + `CardContent` |
| CREATE | `client/src/components/analytics/TrendChartsPanel.tsx` | Recharts BarChart + LineChart + PieChart in responsive containers; accepts `data` prop + `isLoading` |
| CREATE | `client/src/components/analytics/SystemHealthPanel.tsx` | API latency chips, DB pool gauge, cache hit gauge, AI gateway status chip |
| CREATE | `client/src/components/analytics/DateRangeFilter.tsx` | MUI DatePicker pair (start/end); `onRangeChange(start, end)` callback; defaults today-30d to today |
| CREATE | `client/src/components/analytics/ExportButton.tsx` | MUI Button + Menu with PDF/CSV options; triggers blob download |
| CREATE | `client/src/hooks/useAnalyticsData.ts` | React Query hooks: `useKpiMetrics`, `useTrendData`, `useSystemHealth` — all accept `{ startDate, endDate }` |
| CREATE | `client/src/api/analytics.ts` | API client functions: `fetchKpi`, `fetchTrends`, `fetchSystemHealth`, `downloadExport` |
| MODIFY | `client/src/App.tsx` | Add `/analytics` route behind `<AuthenticatedLayout>` requiring `staff` or `admin` role |

---

## Implementation Plan

### 1. `package.json` — add Recharts

```json
// Add to "dependencies":
"recharts": "^2.12.0"
```

### 2. `api/analytics.ts` — typed API functions

```typescript
// client/src/api/analytics.ts
import type { DateRange, KpiMetrics, TrendData, SystemHealth } from '../types/analytics';

const BASE = '/api/v1/analytics';

export async function fetchKpi(range: DateRange): Promise<KpiMetrics> {
  const params = new URLSearchParams({
    startDate: range.startDate.toISOString().split('T')[0],
    endDate: range.endDate.toISOString().split('T')[0],
  });
  const res = await fetch(`${BASE}/kpi?${params}`, { credentials: 'include' });
  if (!res.ok) throw new Error(`KPI fetch failed: ${res.status}`);
  return res.json();
}

export async function fetchTrends(range: DateRange): Promise<TrendData> {
  const params = new URLSearchParams({
    startDate: range.startDate.toISOString().split('T')[0],
    endDate: range.endDate.toISOString().split('T')[0],
  });
  const res = await fetch(`${BASE}/trends?${params}`, { credentials: 'include' });
  if (!res.ok) throw new Error(`Trends fetch failed: ${res.status}`);
  return res.json();
}

export async function fetchSystemHealth(): Promise<SystemHealth> {
  const res = await fetch(`${BASE}/system-health`, { credentials: 'include' });
  if (!res.ok) throw new Error(`Health fetch failed: ${res.status}`);
  return res.json();
}

export async function downloadExport(
  format: 'pdf' | 'csv',
  range: DateRange
): Promise<void> {
  const params = new URLSearchParams({
    format,
    startDate: range.startDate.toISOString().split('T')[0],
    endDate: range.endDate.toISOString().split('T')[0],
  });
  const res = await fetch(`${BASE}/export?${params}`, { credentials: 'include' });
  if (!res.ok) throw new Error(`Export failed: ${res.status}`);
  const blob = await res.blob();
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `propeliq-metrics-${format === 'pdf' ? '.pdf' : '.csv'}`;
  a.click();
  URL.revokeObjectURL(url);
}
```

### 3. `useAnalyticsData.ts` — React Query hooks

```typescript
// client/src/hooks/useAnalyticsData.ts
import { useQuery } from '@tanstack/react-query';
import { fetchKpi, fetchTrends, fetchSystemHealth } from '../api/analytics';
import type { DateRange } from '../types/analytics';

export function useKpiMetrics(range: DateRange) {
  return useQuery({
    queryKey: ['analytics', 'kpi', range.startDate, range.endDate],
    queryFn: () => fetchKpi(range),
    staleTime: 30_000,    // 30s — KPIs tolerate slightly stale data
    retry: 2,
  });
}

export function useTrendData(range: DateRange) {
  return useQuery({
    queryKey: ['analytics', 'trends', range.startDate, range.endDate],
    queryFn: () => fetchTrends(range),
    staleTime: 0,         // AC-2: always re-fetch on date range change
    retry: 2,
  });
}

export function useSystemHealth() {
  return useQuery({
    queryKey: ['analytics', 'systemHealth'],
    queryFn: fetchSystemHealth,
    staleTime: 30_000,
    refetchInterval: 60_000,   // Auto-refresh every 60s for live health status
  });
}
```

### 4. `MetricKpiCard.tsx` — design token conformant

```tsx
// Design tokens applied:
// - Card elevation 1, borderRadius 8px (medium), padding 24px (spacing 3)
// - Typography h4 (1.5rem) for value, body2 (14px) for label, caption for trend
// - Colors: primary.500 for up-trend chip, error.main for down, neutral.500 for neutral

import { Card, CardContent, Skeleton, Chip, Typography, Box } from '@mui/material';
import ArrowUpwardIcon from '@mui/icons-material/ArrowUpward';
import ArrowDownwardIcon from '@mui/icons-material/ArrowDownward';

interface MetricKpiCardProps {
  title: string;
  value: string | number;
  unit?: string;
  trend?: 'up' | 'down' | 'neutral';
  trendLabel?: string;
  isLoading?: boolean;
}

export default function MetricKpiCard({
  title, value, unit, trend, trendLabel, isLoading
}: MetricKpiCardProps) {
  if (isLoading) {
    return (
      <Card elevation={1} sx={{ borderRadius: 1, p: 3, height: 140 }}>
        <Skeleton variant="text" width="60%" sx={{ mb: 1 }} />
        <Skeleton variant="text" width="40%" height={40} />
        <Skeleton variant="text" width="50%" />
      </Card>
    );
  }
  const trendColor = trend === 'up' ? 'primary' : trend === 'down' ? 'error' : 'default';

  return (
    <Card elevation={1} sx={{ borderRadius: 1, p: 3 }}>
      <CardContent sx={{ p: 0 }}>
        <Typography variant="body2" color="text.secondary">{title}</Typography>
        <Box sx={{ display: 'flex', alignItems: 'baseline', gap: 0.5, my: 1 }}>
          <Typography variant="h4" fontWeight={600}>{value}</Typography>
          {unit && <Typography variant="body2" color="text.secondary">{unit}</Typography>}
        </Box>
        {trend && trendLabel && (
          <Chip
            label={trendLabel}
            color={trendColor}
            size="small"
            icon={trend === 'up' ? <ArrowUpwardIcon /> : trend === 'down' ? <ArrowDownwardIcon /> : undefined}
          />
        )}
      </CardContent>
    </Card>
  );
}
```

### 5. `TrendChartsPanel.tsx` — Recharts charts

```tsx
// Uses Recharts 2.x (to be added to package.json)
// ResponsiveContainer ensures charts fill parent width at all breakpoints
import {
  BarChart, Bar, LineChart, Line, PieChart, Pie, Cell,
  XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer
} from 'recharts';
import { Skeleton, Typography, Box } from '@mui/material';
import type { TrendData } from '../../types/analytics';

// Color palette from designsystem.md
const CHART_COLORS = {
  primary: '#2196F3',    // primary.500
  warning: '#FF9800',    // warning.main
  success: '#4CAF50',    // success.main
  error: '#F44336',      // error.main
  secondary: '#9C27B0',  // secondary.500
};

interface Props { data: TrendData | undefined; isLoading: boolean; }

export default function TrendChartsPanel({ data, isLoading }: Props) {
  if (isLoading) return <Skeleton variant="rectangular" height={340} sx={{ borderRadius: 1 }} />;

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 3 }}>
      {/* Daily Appointment Volumes — BarChart */}
      <Box>
        <Typography variant="h6" sx={{ mb: 1 }}>Daily Appointment Volumes</Typography>
        <ResponsiveContainer width="100%" height={200}>
          <BarChart data={data?.dailyVolumes ?? []}>
            <CartesianGrid strokeDasharray="3 3" />
            <XAxis dataKey="date" />
            <YAxis />
            <Tooltip />
            <Bar dataKey="count" fill={CHART_COLORS.primary} name="Appointments" />
          </BarChart>
        </ResponsiveContainer>
      </Box>

      {/* No-show Rate + AI Latency — LineChart with dual axis */}
      <Box>
        <Typography variant="h6" sx={{ mb: 1 }}>No-Show Rate & AI Response Latency</Typography>
        <ResponsiveContainer width="100%" height={200}>
          <LineChart data={data?.weeklyTrends ?? []}>
            <CartesianGrid strokeDasharray="3 3" />
            <XAxis dataKey="week" />
            <YAxis yAxisId="left" unit="%" />
            <YAxis yAxisId="right" orientation="right" unit="ms" />
            <Tooltip />
            <Legend />
            <Line yAxisId="left" type="monotone" dataKey="noShowRate" stroke={CHART_COLORS.warning} name="No-show Rate (%)" />
            <Line yAxisId="right" type="monotone" dataKey="aiLatencyP95Ms" stroke={CHART_COLORS.secondary} name="AI p95 Latency (ms)" />
          </LineChart>
        </ResponsiveContainer>
      </Box>

      {/* Document Processing Throughput — PieChart */}
      <Box>
        <Typography variant="h6" sx={{ mb: 1 }}>Document Processing Status</Typography>
        <ResponsiveContainer width="100%" height={200}>
          <PieChart>
            <Pie data={data?.documentThroughput ?? []} dataKey="count" nameKey="status" cx="50%" cy="50%" outerRadius={80}>
              {(data?.documentThroughput ?? []).map((_, i) => (
                <Cell key={i} fill={[CHART_COLORS.success, CHART_COLORS.warning, CHART_COLORS.error][i % 3]} />
              ))}
            </Pie>
            <Tooltip />
            <Legend />
          </PieChart>
        </ResponsiveContainer>
      </Box>
    </Box>
  );
}
```

### 6. `SystemHealthPanel.tsx` — p50/p95/p99 + gauges

```tsx
import { Box, Chip, LinearProgress, Typography, Stack } from '@mui/material';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import WarningIcon from '@mui/icons-material/Warning';
import type { SystemHealth } from '../../types/analytics';

interface Props { data: SystemHealth | undefined; isLoading: boolean; }

export default function SystemHealthPanel({ data, isLoading }: Props) {
  const gatewayStatusColor = data?.aiGatewayStatus === 'Available' ? 'success'
    : data?.aiGatewayStatus === 'Degraded' ? 'warning' : 'error';

  return (
    <Box>
      <Typography variant="h6" sx={{ mb: 2 }}>System Health</Typography>
      <Stack direction="row" spacing={2} flexWrap="wrap" sx={{ mb: 3 }}>
        <Chip label={`p50: ${data?.apiLatencyP50Ms ?? '–'}ms`} variant="outlined" size="small" />
        <Chip label={`p95: ${data?.apiLatencyP95Ms ?? '–'}ms`} variant="outlined" size="small" />
        <Chip label={`p99: ${data?.apiLatencyP99Ms ?? '–'}ms`} variant="outlined" size="small" />
        <Chip
          label={`AI Gateway: ${data?.aiGatewayStatus ?? 'Unknown'}`}
          color={gatewayStatusColor as any}
          icon={gatewayStatusColor === 'success' ? <CheckCircleIcon /> : <WarningIcon />}
          size="small"
        />
      </Stack>
      <Box sx={{ mb: 2 }}>
        <Box sx={{ display: 'flex', justifyContent: 'space-between' }}>
          <Typography variant="body2">DB Connection Pool</Typography>
          <Typography variant="body2">{data?.dbPoolUsagePct ?? 0}%</Typography>
        </Box>
        <LinearProgress variant="determinate" value={data?.dbPoolUsagePct ?? 0}
          color={data?.dbPoolUsagePct && data.dbPoolUsagePct > 80 ? 'error' : 'primary'} />
      </Box>
      <Box>
        <Box sx={{ display: 'flex', justifyContent: 'space-between' }}>
          <Typography variant="body2">Cache Hit Ratio</Typography>
          <Typography variant="body2">{data?.cacheHitRatioPct ?? 0}%</Typography>
        </Box>
        <LinearProgress variant="determinate" value={data?.cacheHitRatioPct ?? 0} color="success" />
      </Box>
    </Box>
  );
}
```

### 7. `AnalyticsDashboardPage.tsx` — page assembly

```tsx
// Assembles all sections; manages date range state; composes queries
// Breadcrumb: Staff Dashboard > Analytics (UXR-002)
// States: Default, Loading (Skeleton), Empty (no data), Error (Alert)
```

### 8. Screen States (per figma_spec.md SCR-028)

| State | Implementation |
|-------|---------------|
| **Default** | All sections rendered with data |
| **Loading** | `<Skeleton>` on each card + chart (MUI Skeleton, 300ms pulse animation per designsystem.md) |
| **Empty** | MUI `<Alert severity="info">No metrics available for the selected date range.</Alert>` + `<Typography>` prompt |
| **Error** | MUI `<Alert severity="error">Unable to load metrics. Please try again.</Alert>` with Retry button |

---

## Current Project State

```
client/src/
├── App.tsx                              ← MODIFY: add /analytics route
├── pages/
│   └── AnalyticsDashboardPage.tsx       ← CREATE
├── components/
│   └── analytics/                       ← CREATE all
│       ├── MetricKpiCard.tsx
│       ├── TrendChartsPanel.tsx
│       ├── SystemHealthPanel.tsx
│       ├── DateRangeFilter.tsx
│       └── ExportButton.tsx
├── hooks/
│   └── useAnalyticsData.ts              ← CREATE
├── api/
│   └── analytics.ts                     ← CREATE
└── types/
    └── analytics.ts                     ← CREATE (KpiMetrics, TrendData, SystemHealth DTOs)

client/package.json                      ← MODIFY: add recharts ^2.12.0
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | `client/package.json` | Add `"recharts": "^2.12.0"` |
| CREATE | `client/src/types/analytics.ts` | TypeScript interfaces: `KpiMetrics`, `TrendData`, `SystemHealth`, `DateRange` |
| CREATE | `client/src/api/analytics.ts` | `fetchKpi`, `fetchTrends`, `fetchSystemHealth`, `downloadExport` typed API functions |
| CREATE | `client/src/hooks/useAnalyticsData.ts` | `useKpiMetrics`, `useTrendData`, `useSystemHealth` React Query hooks |
| CREATE | `client/src/components/analytics/MetricKpiCard.tsx` | KPI card; elevation 1; 24px padding; Skeleton loading; trend Chip |
| CREATE | `client/src/components/analytics/TrendChartsPanel.tsx` | Recharts BarChart + LineChart (dual axis) + PieChart; ResponsiveContainer; Skeleton fallback |
| CREATE | `client/src/components/analytics/SystemHealthPanel.tsx` | Latency p50/p95/p99 chips; DB pool + cache hit LinearProgress gauges; AI gateway status Chip |
| CREATE | `client/src/components/analytics/DateRangeFilter.tsx` | MUI DatePicker pair; `onRangeChange` callback; default range = today-30d to today |
| CREATE | `client/src/components/analytics/ExportButton.tsx` | MUI Button + Menu; PDF/CSV options; blob download via `downloadExport` |
| CREATE | `client/src/pages/AnalyticsDashboardPage.tsx` | Page assembly; date range state; all 4 sections; breadcrumb; 4 screen states |
| MODIFY | `client/src/App.tsx` | Add `<Route path="/analytics" element={<AnalyticsDashboardPage />}>` within authenticated routes |

---

## External References

- [Recharts 2.x documentation — BarChart, LineChart, PieChart, ResponsiveContainer](https://recharts.org/en-US/api)
- [MUI LinearProgress determinate variant](https://mui.com/material-ui/react-progress/#linear-determinate)
- [React Query useQuery — staleTime and refetchInterval](https://tanstack.com/query/v4/docs/react/reference/useQuery)
- [MUI DatePicker — @mui/x-date-pickers (note: NOT in package.json; use MUI TextField type="date" pair as alternative if @mui/x-date-pickers not installed)](https://mui.com/x/react-date-pickers/date-picker/)
- [figma_spec.md SCR-028 component list](../.propel/context/docs/figma_spec.md#SCR-028)
- [designsystem.md — Card, colors, typography, spacing](../.propel/context/docs/designsystem.md)

> **MUI DatePicker dependency note**: `@mui/x-date-pickers` is NOT in `client/package.json`. Use two MUI `<TextField type="date">` inputs as a zero-dependency alternative for the `DateRangeFilter`, or add `"@mui/x-date-pickers": "^6.x"` + `"date-fns": "^2.x"` to package.json. The task prefers `TextField type="date"` (no new dependency) unless the team prefers the richer picker UX.

---

## Build Commands

```powershell
# Install new dependency
cd client ; npm install recharts@^2.12.0

# Development server
cd client ; npm run dev

# Type-check
cd client ; npx tsc --noEmit
```

---

## Implementation Validation Strategy

- [ ] All 4 screen states render correctly: Default, Loading (Skeleton), Empty (no data), Error (Alert with Retry)
- [ ] Date range change → all queries refetch → charts update; visually validates within 2 seconds on local dev with mock data
- [ ] KPI cards render with correct titles: "Today's Appointments", "No-show Rate", "Avg Wait Time", "AI Acceptance Rate"
- [ ] BarChart renders `dailyVolumes[].date` on x-axis, `count` on y-axis
- [ ] LineChart dual y-axis: left = `noShowRate %`, right = `aiLatencyP95Ms ms`
- [ ] PieChart renders 3 slices for Completed/ManualReview/Pending with correct colors
- [ ] SystemHealthPanel renders 3 latency chips + DB pool gauge + cache hit gauge + AI gateway chip
- [ ] Export button renders PDF/CSV menu; clicking CSV triggers `downloadExport('csv', range)`; mock network returns blob → anchor click simulated
- [ ] `/analytics` route accessible to staff and admin roles; redirected to login if unauthenticated
- [ ] Breadcrumb (UXR-002): "Staff Dashboard > Analytics" visible on desktop layout

---

## Implementation Checklist

- [ ] ADD `"recharts": "^2.12.0"` to `client/package.json` dependencies
- [ ] CREATE `client/src/types/analytics.ts` — TypeScript interfaces: `KpiMetrics` (appointmentCount, noShowRate, avgWaitTimeMin, aiAcceptanceRate, dataFreshnessSec, lastRefreshedAt), `TrendData` (dailyVolumes, weeklyTrends, documentThroughput), `SystemHealth` (apiLatencyP50/P95/P99Ms, dbPoolUsagePct, cacheHitRatioPct, aiGatewayStatus), `DateRange`
- [ ] CREATE `api/analytics.ts` + `hooks/useAnalyticsData.ts` — typed fetch functions + React Query hooks with `staleTime: 0` on date-range-dependent queries
- [ ] CREATE `MetricKpiCard.tsx` — MUI Card elevation=1, borderRadius=1, p=3; Skeleton loading; value typography h4; trend Chip; aria-label on card (WCAG)
- [ ] CREATE `TrendChartsPanel.tsx` — Recharts `<ResponsiveContainer>` + `<BarChart>` (daily volumes) + `<LineChart>` (no-show + AI latency, dual YAxis) + `<PieChart>` (doc throughput) + Skeleton fallback
- [ ] CREATE `SystemHealthPanel.tsx` — p50/p95/p99 Chips + LinearProgress for DB pool + cache hit + AI gateway Chip with color-coded status
- [ ] CREATE `DateRangeFilter.tsx` — two MUI `<TextField type="date">` with start/end validation; `onChange` triggers `onRangeChange` callback; aria-labels for accessibility
- [ ] CREATE `ExportButton.tsx` — MUI Button + Menu; calls `downloadExport(format, range)`; shows loading spinner during download; `aria-haspopup` set on button
- [ ] CREATE `AnalyticsDashboardPage.tsx` — compose all sections; `useState<DateRange>` for filter; `useMemo` for query keys; 4 screen states; Breadcrumb (UXR-002)
- [ ] MODIFY `App.tsx` — add `/analytics` route requiring authentication (check `useAuthStore` token or redirect to login)

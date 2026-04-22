// Analytics domain types for SCR-028 Operational Metrics Dashboard (US_033).

export interface DateRange {
  startDate: Date;
  endDate: Date;
}

// ── KPI Metrics (AC-1) ─────────────────────────────────────────────────────

export interface KpiMetrics {
  /** Total appointments for the selected date range. */
  appointmentCount: number;
  /** No-show rate as a percentage (0–100). */
  noShowRate: number;
  /** Average patient wait time in minutes. */
  avgWaitTimeMin: number;
  /** AI suggestion acceptance rate as a percentage (0–100). */
  aiAcceptanceRate: number;
  /** Seconds since metrics were last collected. If > 60, data is considered delayed. */
  dataFreshnessSec: number;
  /** ISO 8601 timestamp of last successful metrics collection. */
  lastRefreshedAt: string;
}

// ── Trend Data (AC-3) ──────────────────────────────────────────────────────

export interface DailyVolume {
  /** YYYY-MM-DD date label for the x-axis. */
  date: string;
  /** Total appointment count for that day. */
  count: number;
}

export interface WeeklyTrend {
  /** Week label for the x-axis (e.g. "2026-W15"). */
  week: string;
  /** No-show rate percentage for the week. */
  noShowRate: number;
  /** AI response p95 latency in milliseconds for the week. */
  aiLatencyP95Ms: number;
}

export interface DocumentThroughput {
  /** Processing status label (e.g. "Completed", "ManualReview", "Pending"). */
  status: string;
  /** Document count with this status. */
  count: number;
}

export interface TrendData {
  dailyVolumes: DailyVolume[];
  weeklyTrends: WeeklyTrend[];
  documentThroughput: DocumentThroughput[];
}

// ── System Health (AC-4) ───────────────────────────────────────────────────

export type AiGatewayStatus = 'Available' | 'Degraded' | 'Unavailable';

export interface SystemHealth {
  /** API response time at the 50th percentile in ms. */
  apiLatencyP50Ms: number;
  /** API response time at the 95th percentile in ms. */
  apiLatencyP95Ms: number;
  /** API response time at the 99th percentile in ms. */
  apiLatencyP99Ms: number;
  /** Database connection pool usage as a percentage (0–100). */
  dbPoolUsagePct: number;
  /** Redis cache hit ratio as a percentage (0–100). */
  cacheHitRatioPct: number;
  /** Current AI gateway operational status. */
  aiGatewayStatus: AiGatewayStatus;
}

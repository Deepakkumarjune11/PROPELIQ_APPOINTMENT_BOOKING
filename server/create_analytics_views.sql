-- Analytics materialized views for PropelIQ dashboard (US_033, FR-018)
-- Run once against the postgres database.

-- 1. Freshness tracking table
CREATE TABLE IF NOT EXISTS mv_last_refresh (
    view_name         TEXT        PRIMARY KEY,
    last_refreshed_at TIMESTAMPTZ NOT NULL DEFAULT '1970-01-01T00:00:00Z'
);
INSERT INTO mv_last_refresh (view_name) VALUES
    ('mv_daily_appointment_volumes'),
    ('mv_weekly_noshow_rates'),
    ('mv_daily_kpi'),
    ('mv_document_processing_throughput')
ON CONFLICT DO NOTHING;

-- 2. Daily appointment volumes
CREATE MATERIALIZED VIEW IF NOT EXISTS mv_daily_appointment_volumes AS
SELECT
    DATE("SlotDatetime" AT TIME ZONE 'UTC') AS metric_date,
    COUNT(*)::int                           AS appointment_count
FROM appointment
GROUP BY DATE("SlotDatetime" AT TIME ZONE 'UTC')
WITH DATA;

CREATE UNIQUE INDEX IF NOT EXISTS ux_mv_daily_apt_vol_date
    ON mv_daily_appointment_volumes (metric_date);

-- 3. Weekly no-show rates
CREATE MATERIALIZED VIEW IF NOT EXISTS mv_weekly_noshow_rates AS
SELECT
    TO_CHAR(DATE_TRUNC('week', "SlotDatetime" AT TIME ZONE 'UTC'), 'IYYY-"W"IW')
                                                        AS week_label,
    DATE_TRUNC('week', "SlotDatetime" AT TIME ZONE 'UTC')::date
                                                        AS week_start_date,
    ROUND(
        COUNT(*) FILTER (WHERE "Status" = 'NoShow')::NUMERIC /
        NULLIF(COUNT(*), 0),
        4
    )::DOUBLE PRECISION                                 AS noshow_rate,
    0.0::DOUBLE PRECISION                               AS ai_latency_p95_ms
FROM appointment
GROUP BY DATE_TRUNC('week', "SlotDatetime" AT TIME ZONE 'UTC')
WITH DATA;

CREATE UNIQUE INDEX IF NOT EXISTS ux_mv_weekly_noshow_week
    ON mv_weekly_noshow_rates (week_label);

-- 4. Daily KPI summary
CREATE MATERIALIZED VIEW IF NOT EXISTS mv_daily_kpi AS
SELECT
    DATE("SlotDatetime" AT TIME ZONE 'UTC') AS metric_date,
    COUNT(*)::int                           AS appointment_count,
    ROUND(
        COUNT(*) FILTER (WHERE "Status" = 'NoShow')::NUMERIC /
        NULLIF(COUNT(*), 0),
        4
    )::DOUBLE PRECISION                    AS noshow_rate,
    0.0::DOUBLE PRECISION                  AS avg_wait_time_min,
    0.0::DOUBLE PRECISION                  AS ai_acceptance_rate
FROM appointment
GROUP BY DATE("SlotDatetime" AT TIME ZONE 'UTC')
WITH DATA;

CREATE UNIQUE INDEX IF NOT EXISTS ux_mv_daily_kpi_date
    ON mv_daily_kpi (metric_date);

-- 5. Document processing throughput
CREATE MATERIALIZED VIEW IF NOT EXISTS mv_document_processing_throughput AS
SELECT
    DATE("UploadedAt" AT TIME ZONE 'UTC') AS metric_date,
    "ExtractionStatus"                    AS processing_status,
    COUNT(*)::int                         AS document_count
FROM clinical_document
GROUP BY DATE("UploadedAt" AT TIME ZONE 'UTC'), "ExtractionStatus"
WITH DATA;

CREATE UNIQUE INDEX IF NOT EXISTS ux_mv_doc_throughput_date_status
    ON mv_document_processing_throughput (metric_date, processing_status);

-- 6. Base table indexes for fast refresh
CREATE INDEX IF NOT EXISTS ix_appointments_scheduled_at_status
    ON appointment ("SlotDatetime", "Status");

CREATE INDEX IF NOT EXISTS ix_clinical_documents_status_created_at
    ON clinical_document ("ExtractionStatus", "UploadedAt");

-- Mark as refreshed now
UPDATE mv_last_refresh SET last_refreshed_at = NOW();

SELECT 'Analytics views created successfully.' AS result;

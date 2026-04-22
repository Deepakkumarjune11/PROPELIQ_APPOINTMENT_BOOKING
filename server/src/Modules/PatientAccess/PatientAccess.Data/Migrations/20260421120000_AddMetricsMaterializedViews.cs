using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PatientAccess.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMetricsMaterializedViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. mv_last_refresh tracking table ──────────────────────────────────
            migrationBuilder.Sql("""
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
                """);

            // ── 2. mv_daily_appointment_volumes ────────────────────────────────────
            // column appointment_count matches PostgresMetricsQueryService.DailyRow binding
            migrationBuilder.Sql("""
                CREATE MATERIALIZED VIEW IF NOT EXISTS mv_daily_appointment_volumes AS
                SELECT
                    DATE("SlotDatetime" AT TIME ZONE 'UTC') AS metric_date,
                    COUNT(*)::int                           AS appointment_count
                FROM appointment
                GROUP BY DATE("SlotDatetime" AT TIME ZONE 'UTC')
                WITH DATA;
                """);

            // Unique index required for CONCURRENTLY refresh (PG 15 requirement)
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS ux_mv_daily_apt_vol_date " +
                "ON mv_daily_appointment_volumes (metric_date);",
                suppressTransaction: true);

            // ── 3. mv_weekly_noshow_rates ──────────────────────────────────────────
            // week_label + week_start_date match PostgresMetricsQueryService.WeeklyRow binding.
            // ai_latency_p95_ms is sourced from Redis by the service; 0.0 placeholder in view.
            migrationBuilder.Sql("""
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
                    )::DOUBLE PRECISION                         AS noshow_rate,
                    0.0::DOUBLE PRECISION                       AS ai_latency_p95_ms
                FROM appointment
                GROUP BY DATE_TRUNC('week', "SlotDatetime" AT TIME ZONE 'UTC')
                WITH DATA;
                """);

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS ux_mv_weekly_noshow_week " +
                "ON mv_weekly_noshow_rates (week_label);",
                suppressTransaction: true);

            // ── 4. mv_daily_kpi ────────────────────────────────────────────────────
            // avg_wait_time_min: 0.0 placeholder (no started_at column in current schema).
            // ai_acceptance_rate: 0.0 placeholder (ai_suggested/verified_as_accepted not yet added).
            migrationBuilder.Sql("""
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
                """);

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS ux_mv_daily_kpi_date " +
                "ON mv_daily_kpi (metric_date);",
                suppressTransaction: true);

            // ── 5. mv_document_processing_throughput ───────────────────────────────
            // processing_status + document_count match PostgresMetricsQueryService.ThroughputRow binding.
            migrationBuilder.Sql("""
                CREATE MATERIALIZED VIEW IF NOT EXISTS mv_document_processing_throughput AS
                SELECT
                    DATE("UploadedAt" AT TIME ZONE 'UTC') AS metric_date,
                    "ExtractionStatus"                    AS processing_status,
                    COUNT(*)::int                         AS document_count
                FROM clinical_document
                GROUP BY DATE("UploadedAt" AT TIME ZONE 'UTC'), "ExtractionStatus"
                WITH DATA;
                """);

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS ux_mv_doc_throughput_date_status " +
                "ON mv_document_processing_throughput (metric_date, processing_status);",
                suppressTransaction: true);

            // ── 6. Supporting indexes on base tables (CONCURRENTLY) ────────────────
            // Speeds up the SELECT re-executed on each REFRESH MATERIALIZED VIEW CONCURRENTLY.
            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_appointments_scheduled_at_status " +
                "ON appointment (\"SlotDatetime\", \"Status\");",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_clinical_documents_status_created_at " +
                "ON clinical_document (\"ExtractionStatus\", \"UploadedAt\");",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_extracted_facts_accepted_at " +
                "ON extracted_fact (\"ExtractedAt\", \"FactType\");",
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop CONCURRENTLY indexes first — suppressTransaction: true required (PG 15)
            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS ix_extracted_facts_accepted_at;",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS ix_clinical_documents_status_created_at;",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS ix_appointments_scheduled_at_status;",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS ux_mv_doc_throughput_date_status;",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS ux_mv_daily_kpi_date;",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS ux_mv_weekly_noshow_week;",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS ux_mv_daily_apt_vol_date;",
                suppressTransaction: true);

            // Drop views, then tracking table
            migrationBuilder.Sql("DROP MATERIALIZED VIEW IF EXISTS mv_document_processing_throughput CASCADE;");
            migrationBuilder.Sql("DROP MATERIALIZED VIEW IF EXISTS mv_daily_kpi CASCADE;");
            migrationBuilder.Sql("DROP MATERIALIZED VIEW IF EXISTS mv_weekly_noshow_rates CASCADE;");
            migrationBuilder.Sql("DROP MATERIALIZED VIEW IF EXISTS mv_daily_appointment_volumes CASCADE;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS mv_last_refresh;");
        }
    }
}

# Task - task_003_db_metrics_materialized_views

## Requirement Reference

- **User Story**: US_033 — Operational Metrics Dashboard & Reporting
- **Story Location**: `.propel/context/tasks/EP-008/us_033/us_033.md`
- **Acceptance Criteria**:
  - AC-2: Metrics update within 2 seconds on date range change — satisfied by pre-aggregated materialized views (query time < 50ms vs multi-second OLTP aggregate queries).
  - AC-3: Trend charts require `mv_daily_appointment_volumes`, `mv_weekly_noshow_rates`, `mv_document_processing_throughput`.
  - AC-1/KPI: `mv_daily_kpi` pre-aggregates today's appointment count, no-show rate, avg wait time, AI acceptance rate.
  - Edge case: "Metrics are pre-aggregated via materialized views; dashboard queries don't impact operational performance."
- **Edge Cases**:
  - Materialized view refresh during query: PostgreSQL `REFRESH MATERIALIZED VIEW CONCURRENTLY` allows reads during refresh (no table lock). This is the ONLY refresh strategy — non-concurrent `REFRESH` is forbidden as it takes an exclusive lock.
  - Refresh failure: Hangfire job retries 3 times. If all retries fail, `mv_last_refresh` is NOT updated → `dataFreshnessSec` grows → dashboard shows "data delayed" indicator (handled in FE task_001).

---

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | No |

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Database | PostgreSQL 15 | 15.x |
| ORM / Migrations | EF Core 8.0 Code-First | 8.0 |
| Background Jobs | Hangfire | 1.8.x |
| CI/CD Lint | GitHub Actions `has-pending-model-changes` + CONCURRENTLY index lint (US_028) | - |

---

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |

---

## Task Overview

Create the pre-aggregation layer that powers the analytics dashboard. All materialized views are created via EF Core migration using `migrationBuilder.Sql(...)`. CONCURRENTLY refresh requires a unique index on each view.

**Materialized views to create:**

1. **`mv_daily_appointment_volumes`** — Aggregates `appointments` by date:
   `metric_date DATE, count INT`. Unique index on `metric_date` (required for CONCURRENTLY refresh).

2. **`mv_weekly_noshow_rates`** — Aggregates by ISO week:
   `metric_week TEXT (ISO format 'YYYY-WXX'), noshow_rate DOUBLE PRECISION`. Unique index on `metric_week`.

3. **`mv_daily_kpi`** — Combines appointment count, no-show rate, avg wait time, AI acceptance rate per day:
   `metric_date DATE, appointment_count INT, noshow_rate DOUBLE PRECISION, avg_wait_time_min DOUBLE PRECISION, ai_acceptance_rate DOUBLE PRECISION`. Unique index on `metric_date`.

4. **`mv_document_processing_throughput`** — Aggregates `clinical_documents` by status per day:
   `metric_date DATE, status TEXT, count INT`. Unique index on `(metric_date, status)`.

5. **`mv_last_refresh`** tracking table — `view_name TEXT PRIMARY KEY, last_refreshed_at TIMESTAMPTZ` — updated by Hangfire refresh job after each successful CONCURRENTLY refresh.

**Hangfire refresh job:**
- `RefreshMetricsMaterializedViewsJob` — registered as `AddOrUpdateRecurringJob<>` every 1 hour.
- Refreshes all 4 views in order using `REFRESH MATERIALIZED VIEW CONCURRENTLY`.
- Updates `mv_last_refresh` for each view after success.
- `[AutomaticRetry(Attempts = 3)]` + `[DisableConcurrentExecution(timeoutInSeconds: 300)]`.

**Supporting indexes on base tables** to keep refresh fast (each `REFRESH` re-runs the view's SELECT):
- `CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_appointments_scheduled_at_status ON appointments (scheduled_at, status)` — for daily volume and KPI aggregation.
- `CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_clinical_documents_status_created_at ON clinical_documents (status, created_at)` — for document throughput.
- `CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_extracted_facts_accepted_at ON extracted_facts (accepted_at, ai_suggested)` — for AI acceptance rate.

---

## Dependent Tasks

- **task_002_be_metrics_api_export.md** (US_033) — `PostgresMetricsQueryService` queries these views; must exist before BE task can be implemented.
- No migration dependency on US_029–US_032 (metrics views are independent, read from existing base tables created in prior migrations).

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/PropelIQ.Api/Migrations/<timestamp>_AddMetricsMaterializedViews.cs` | EF Core migration: 4 materialized views + 4 unique indexes on views + `mv_last_refresh` table + 3 supporting indexes on base tables (all CONCURRENTLY) |
| CREATE | `server/src/Modules/Admin/Admin.Application/Analytics/RefreshMetricsMaterializedViewsJob.cs` | Hangfire recurring job: REFRESH CONCURRENTLY × 4; update `mv_last_refresh`; `[AutomaticRetry(3)]` |
| MODIFY | `server/src/Modules/Admin/Admin.Presentation/ServiceCollectionExtensions.cs` | Register `RefreshMetricsMaterializedViewsJob`; add `RecurringJob.AddOrUpdate` in startup |

---

## Implementation Plan

### 1. EF Core Migration — `AddMetricsMaterializedViews`

```csharp
public partial class AddMetricsMaterializedViews : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── 1. mv_last_refresh tracking table ──────────────────────────────
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS mv_last_refresh (
                view_name       TEXT        PRIMARY KEY,
                last_refreshed_at TIMESTAMPTZ NOT NULL DEFAULT '1970-01-01T00:00:00Z'
            );
            INSERT INTO mv_last_refresh (view_name) VALUES
                ('mv_daily_appointment_volumes'),
                ('mv_weekly_noshow_rates'),
                ('mv_daily_kpi'),
                ('mv_document_processing_throughput')
            ON CONFLICT DO NOTHING;
            """);

        // ── 2. mv_daily_appointment_volumes ────────────────────────────────
        migrationBuilder.Sql("""
            CREATE MATERIALIZED VIEW IF NOT EXISTS mv_daily_appointment_volumes AS
            SELECT
                DATE(scheduled_at AT TIME ZONE 'UTC') AS metric_date,
                COUNT(*)                              AS count
            FROM appointments
            GROUP BY DATE(scheduled_at AT TIME ZONE 'UTC')
            WITH DATA;
            """);

        // Unique index required for CONCURRENTLY refresh
        migrationBuilder.Sql(
            "CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS ux_mv_daily_apt_vol_date " +
            "ON mv_daily_appointment_volumes (metric_date);",
            suppressTransaction: true);

        // ── 3. mv_weekly_noshow_rates ──────────────────────────────────────
        migrationBuilder.Sql("""
            CREATE MATERIALIZED VIEW IF NOT EXISTS mv_weekly_noshow_rates AS
            SELECT
                TO_CHAR(DATE_TRUNC('week', scheduled_at AT TIME ZONE 'UTC'), 'IYYY-"W"IW') AS metric_week,
                ROUND(
                    COUNT(*) FILTER (WHERE status = 'NoShow')::NUMERIC /
                    NULLIF(COUNT(*), 0),
                    4
                )::DOUBLE PRECISION AS noshow_rate
            FROM appointments
            GROUP BY DATE_TRUNC('week', scheduled_at AT TIME ZONE 'UTC')
            WITH DATA;
            """);

        migrationBuilder.Sql(
            "CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS ux_mv_weekly_noshow_week " +
            "ON mv_weekly_noshow_rates (metric_week);",
            suppressTransaction: true);

        // ── 4. mv_daily_kpi ────────────────────────────────────────────────
        migrationBuilder.Sql("""
            CREATE MATERIALIZED VIEW IF NOT EXISTS mv_daily_kpi AS
            SELECT
                DATE(a.scheduled_at AT TIME ZONE 'UTC')                     AS metric_date,
                COUNT(*)                                                     AS appointment_count,
                ROUND(
                    COUNT(*) FILTER (WHERE a.status = 'NoShow')::NUMERIC /
                    NULLIF(COUNT(*), 0),
                    4
                )::DOUBLE PRECISION                                          AS noshow_rate,
                COALESCE(AVG(
                    EXTRACT(EPOCH FROM (a.started_at - a.scheduled_at)) / 60
                ) FILTER (WHERE a.started_at IS NOT NULL), 0)               AS avg_wait_time_min,
                COALESCE(
                    (
                        SELECT ROUND(
                            COUNT(*) FILTER (WHERE ef.verified_as_accepted = true)::NUMERIC /
                            NULLIF(COUNT(*), 0), 4
                        )::DOUBLE PRECISION
                        FROM extracted_facts ef
                        WHERE DATE(ef.created_at AT TIME ZONE 'UTC') =
                              DATE(a.scheduled_at AT TIME ZONE 'UTC')
                    ), 0
                )                                                            AS ai_acceptance_rate
            FROM appointments a
            GROUP BY DATE(a.scheduled_at AT TIME ZONE 'UTC')
            WITH DATA;
            """);

        migrationBuilder.Sql(
            "CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS ux_mv_daily_kpi_date " +
            "ON mv_daily_kpi (metric_date);",
            suppressTransaction: true);

        // ── 5. mv_document_processing_throughput ───────────────────────────
        migrationBuilder.Sql("""
            CREATE MATERIALIZED VIEW IF NOT EXISTS mv_document_processing_throughput AS
            SELECT
                DATE(created_at AT TIME ZONE 'UTC') AS metric_date,
                status,
                COUNT(*)                            AS count
            FROM clinical_documents
            GROUP BY DATE(created_at AT TIME ZONE 'UTC'), status
            WITH DATA;
            """);

        migrationBuilder.Sql(
            "CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS ux_mv_doc_throughput_date_status " +
            "ON mv_document_processing_throughput (metric_date, status);",
            suppressTransaction: true);

        // ── 6. Supporting indexes on base tables (CONCURRENTLY) ────────────
        migrationBuilder.Sql(
            "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_appointments_scheduled_at_status " +
            "ON appointments (scheduled_at, status);",
            suppressTransaction: true);

        migrationBuilder.Sql(
            "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_clinical_documents_status_created_at " +
            "ON clinical_documents (status, created_at);",
            suppressTransaction: true);

        migrationBuilder.Sql(
            "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_extracted_facts_accepted_at " +
            "ON extracted_facts (accepted_at, ai_suggested);",
            suppressTransaction: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Drop CONCURRENTLY indexes first, then views, then tracking table
        migrationBuilder.Sql(
            "DROP INDEX CONCURRENTLY IF EXISTS ix_extracted_facts_accepted_at;",
            suppressTransaction: true);
        migrationBuilder.Sql(
            "DROP INDEX CONCURRENTLY IF EXISTS ix_clinical_documents_status_created_at;",
            suppressTransaction: true);
        migrationBuilder.Sql(
            "DROP INDEX CONCURRENTLY IF EXISTS ix_appointments_scheduled_at_status;",
            suppressTransaction: true);

        migrationBuilder.Sql("DROP MATERIALIZED VIEW IF EXISTS mv_document_processing_throughput CASCADE;");
        migrationBuilder.Sql("DROP MATERIALIZED VIEW IF EXISTS mv_daily_kpi CASCADE;");
        migrationBuilder.Sql("DROP MATERIALIZED VIEW IF EXISTS mv_weekly_noshow_rates CASCADE;");
        migrationBuilder.Sql("DROP MATERIALIZED VIEW IF EXISTS mv_daily_appointment_volumes CASCADE;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS mv_last_refresh;");
    }
}
```

> **CRITICAL — `suppressTransaction: true`**: ALL `CREATE INDEX CONCURRENTLY` and `DROP INDEX CONCURRENTLY` calls MUST use `suppressTransaction: true`. This is enforced by the CI lint job from US_028. Failure to include it will cause the migration to fail in a transaction context.

### 2. `RefreshMetricsMaterializedViewsJob`

```csharp
[AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
[DisableConcurrentExecution(timeoutInSeconds: 300)]
public sealed class RefreshMetricsMaterializedViewsJob(
    PropelIQDbContext db,
    ILogger<RefreshMetricsMaterializedViewsJob> logger)
{
    private static readonly string[] Views =
    [
        "mv_daily_appointment_volumes",
        "mv_weekly_noshow_rates",
        "mv_daily_kpi",
        "mv_document_processing_throughput"
    ];

    public async Task ExecuteAsync(CancellationToken ct)
    {
        foreach (var view in Views)
        {
            try
            {
                // REFRESH MATERIALIZED VIEW CONCURRENTLY allows reads during refresh
                await db.Database.ExecuteSqlRawAsync(
                    $"REFRESH MATERIALIZED VIEW CONCURRENTLY {view}", ct);

                await db.Database.ExecuteSqlRawAsync(
                    """
                    INSERT INTO mv_last_refresh (view_name, last_refreshed_at)
                    VALUES ({0}, NOW() AT TIME ZONE 'UTC')
                    ON CONFLICT (view_name) DO UPDATE
                        SET last_refreshed_at = EXCLUDED.last_refreshed_at
                    """, view);

                logger.LogInformation("Materialized view refreshed: {View}", view);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to refresh materialized view: {View}", view);
                throw; // Allow Hangfire AutomaticRetry to handle
            }
        }
    }
}
```

### 3. Service registration in `ServiceCollectionExtensions.cs`

```csharp
// In Admin.Presentation or PropelIQ.Api startup:
RecurringJob.AddOrUpdate<RefreshMetricsMaterializedViewsJob>(
    "refresh-metrics-views",
    job => job.ExecuteAsync(CancellationToken.None),
    Cron.Hourly);
```

---

## Current Project State

```
server/src/
├── PropelIQ.Api/
│   └── Migrations/
│       └── <timestamp>_AddMetricsMaterializedViews.cs   ← CREATE
└── Modules/
    └── Admin/
        ├── Admin.Application/
        │   └── Analytics/
        │       └── RefreshMetricsMaterializedViewsJob.cs ← CREATE
        └── Admin.Presentation/
            └── ServiceCollectionExtensions.cs            ← MODIFY: register recurring job
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/PropelIQ.Api/Migrations/<timestamp>_AddMetricsMaterializedViews.cs` | 4 materialized views + unique indexes (CONCURRENTLY, suppressTransaction) + `mv_last_refresh` table + 3 base-table supporting indexes; `Down()` drops indexes before views |
| CREATE | `server/src/Modules/Admin/Admin.Application/Analytics/RefreshMetricsMaterializedViewsJob.cs` | Hangfire job: `REFRESH MATERIALIZED VIEW CONCURRENTLY` × 4; update `mv_last_refresh`; `[AutomaticRetry(3)]`; `[DisableConcurrentExecution(300)]` |
| MODIFY | `server/src/Modules/Admin/Admin.Presentation/ServiceCollectionExtensions.cs` | `RecurringJob.AddOrUpdate<RefreshMetricsMaterializedViewsJob>("refresh-metrics-views", ..., Cron.Hourly)` |

---

## External References

- [PostgreSQL MATERIALIZED VIEW — CREATE MATERIALIZED VIEW](https://www.postgresql.org/docs/15/sql-creatematerializedview.html)
- [PostgreSQL REFRESH MATERIALIZED VIEW CONCURRENTLY](https://www.postgresql.org/docs/15/sql-refreshmaterializedview.html)
- [EF Core — migrationBuilder.Sql suppressTransaction](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/operations#arbitrary-changes-via-raw-sql)
- [Hangfire — AutomaticRetry + DisableConcurrentExecution attributes](https://docs.hangfire.io/en/latest/background-processing/dealing-with-exceptions.html)
- [US_028 CI lint — CONCURRENTLY must be paired with suppressTransaction:true](../.propel/context/tasks/EP-006/us_028/)

---

## Build Commands

```powershell
# Generate migration (after DbContext updated if needed — these are raw SQL only)
cd server ; dotnet ef migrations add AddMetricsMaterializedViews --project src/PropelIQ.Api

# Apply migration
cd server ; dotnet ef database update --project src/PropelIQ.Api

# Verify views exist in PostgreSQL
# psql: SELECT matviewname, ispopulated FROM pg_matviews WHERE schemaname = 'public';
```

---

## Implementation Validation Strategy

- [ ] Migration applies without error on a clean database: `dotnet ef database update` succeeds
- [ ] All CONCURRENTLY index statements use `suppressTransaction: true` — CI lint job (`has-pending-model-changes`) from US_028 validates this
- [ ] `mv_daily_appointment_volumes` populated after migration: `SELECT COUNT(*) FROM mv_daily_appointment_volumes` > 0 on seeded DB
- [ ] `mv_daily_kpi` columns match `KpiMetricsDto` field names used in `PostgresMetricsQueryService`
- [ ] `REFRESH MATERIALIZED VIEW CONCURRENTLY mv_daily_kpi` executes without error (unique index present)
- [ ] `RefreshMetricsMaterializedViewsJob` registered as hourly recurring job in Hangfire dashboard
- [ ] `mv_last_refresh` row updated after job execution: `SELECT last_refreshed_at FROM mv_last_refresh WHERE view_name = 'mv_daily_kpi'` shows recent timestamp
- [ ] `Down()` migration drops CONCURRENTLY indexes before views — CI validates this ordering

---

## Implementation Checklist

- [ ] CREATE migration `AddMetricsMaterializedViews` with 4 materialized views (`mv_daily_appointment_volumes`, `mv_weekly_noshow_rates`, `mv_daily_kpi`, `mv_document_processing_throughput`); each with a UNIQUE INDEX CONCURRENTLY + `suppressTransaction: true`
- [ ] CREATE `mv_last_refresh` table in same migration for freshness tracking; pre-insert 4 rows
- [ ] CREATE 3 supporting CONCURRENTLY indexes on base tables (`appointments`, `clinical_documents`, `extracted_facts`) with `suppressTransaction: true`
- [ ] Ensure `Down()` drops CONCURRENTLY indexes before materialized views (CONCURRENTLY drops also require `suppressTransaction: true`)
- [ ] CREATE `RefreshMetricsMaterializedViewsJob` with `[AutomaticRetry(Attempts=3)]` + `[DisableConcurrentExecution(300)]`; refreshes 4 views in order; updates `mv_last_refresh` for each; logs success + re-throws on failure
- [ ] MODIFY `ServiceCollectionExtensions.cs` — `RecurringJob.AddOrUpdate` hourly for `RefreshMetricsMaterializedViewsJob`
- [ ] Validate migration via `dotnet ef database update` on local PostgreSQL 15 instance

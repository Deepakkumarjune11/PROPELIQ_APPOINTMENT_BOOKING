using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PatientAccess.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogComplianceSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── New compliance columns (AC-1, DR-012) ────────────────────────────────
            // All use ADD COLUMN — no table rewrite, zero-downtime (NFR-012).

            migrationBuilder.AddColumn<string>(
                name: "target_entity_type",
                table: "audit_log",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ip_address",
                table: "audit_log",
                type: "character varying(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "old_values",
                table: "audit_log",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "new_values",
                table: "audit_log",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "previous_hash",
                table: "audit_log",
                type: "text",
                nullable: true);

            // chain_hash is NOT NULL; existing rows get '' — AuditLogger populates on all new inserts (AC-4).
            migrationBuilder.AddColumn<string>(
                name: "chain_hash",
                table: "audit_log",
                type: "text",
                nullable: false,
                defaultValue: "");

            // ── DB-level immutability triggers (AC-2 / NFR-007) ──────────────────────
            // These fire even when the application layer is bypassed (direct psql, migrations).
            // The EF Core SaveChangesInterceptor remains as the in-process guard.

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION fn_audit_log_immutable()
                RETURNS TRIGGER
                LANGUAGE plpgsql AS
                $$
                BEGIN
                    RAISE EXCEPTION 'audit_log records are immutable: UPDATE and DELETE are prohibited per NFR-007 and DR-008';
                END;
                $$;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER trg_audit_log_no_update
                    BEFORE UPDATE ON audit_log
                    FOR EACH ROW
                    EXECUTE FUNCTION fn_audit_log_immutable();
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER trg_audit_log_no_delete
                    BEFORE DELETE ON audit_log
                    FOR EACH ROW
                    EXECUTE FUNCTION fn_audit_log_immutable();
                """);

            // ── Performance indexes — CONCURRENTLY to avoid table lock (NFR-012) ─────
            // suppressTransaction: true required — CREATE INDEX CONCURRENTLY cannot run
            // inside a transaction block (PostgreSQL restriction).

            // ix_audit_log_action_type — supports actionType filter in compliance queries (AC-3)
            // Quotes required: existing columns were created with PascalCase names.
            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_audit_log_action_type " +
                @"ON audit_log(""ActionType"");",
                suppressTransaction: true);

            // ix_audit_log_created_at_actor — composite for date-range + actor_id filter (most frequent pattern)
            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_audit_log_created_at_actor " +
                @"ON audit_log(""OccurredAt"" DESC, ""ActorId"");",
                suppressTransaction: true);

            // ix_audit_log_entity_type_created_at — composite for entity-type filter + date sort
            // target_entity_type is the new snake_case column; OccurredAt is PascalCase (existing).
            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_audit_log_entity_type_created_at " +
                @"ON audit_log(target_entity_type, ""OccurredAt"" DESC);",
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop indexes FIRST — CONCURRENTLY avoids lock contention during rollback (NFR-012).
            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS ix_audit_log_entity_type_created_at;",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS ix_audit_log_created_at_actor;",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS ix_audit_log_action_type;",
                suppressTransaction: true);

            // Drop triggers before function (PostgreSQL dependency ordering).
            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS trg_audit_log_no_delete ON audit_log;");

            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS trg_audit_log_no_update ON audit_log;");

            migrationBuilder.Sql(
                "DROP FUNCTION IF EXISTS fn_audit_log_immutable();");

            // Drop columns last.
            migrationBuilder.DropColumn(
                name: "chain_hash",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "previous_hash",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "new_values",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "old_values",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "ip_address",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "target_entity_type",
                table: "audit_log");
        }
    }
}

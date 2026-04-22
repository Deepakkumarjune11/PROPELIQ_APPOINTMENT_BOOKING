using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PatientAccess.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentAccessGrantSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Add department column to patient — nullable, zero-downtime (brief ACCESS EXCLUSIVE lock)
            migrationBuilder.AddColumn<string>(
                name: "department",
                table: "patient",
                type: "varchar(100)",
                nullable: true);

            // Step 2: Create document_access_grants table
            migrationBuilder.CreateTable(
                name: "document_access_grants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grantee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grantee_type = table.Column<string>(type: "varchar(10)", nullable: false),
                    granted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_access_grants", x => x.id);

                    // FK to clinical_document — cross-module; cascade on document delete cleans up grants
                    table.ForeignKey(
                        name: "fk_document_access_grants_document_id",
                        column: x => x.document_id,
                        principalTable: "clinical_document",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);

                    table.CheckConstraint("ck_document_access_grants_grantee_type", "grantee_type IN ('staff', 'dept')");
                });

            // Step 3a: Index on (grantee_id, grantee_type) — RagAccessFilter staff lookup (AIR-S02)
            // suppressTransaction: true required — CONCURRENTLY cannot run inside a transaction
            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_dag_grantee " +
                "ON document_access_grants (grantee_id, grantee_type);",
                suppressTransaction: true);

            // Step 3b: Index on document_id — document-scoped grant queries
            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_dag_document_id " +
                "ON document_access_grants (document_id);",
                suppressTransaction: true);

            // Step 3c: Unique index — prevents duplicate grants (idempotent grant application)
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS ux_dag_unique " +
                "ON document_access_grants (document_id, grantee_id, grantee_type);",
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop indexes CONCURRENTLY before dropping table (PostgreSQL dependency order)
            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS ux_dag_unique;",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS ix_dag_document_id;",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS ix_dag_grantee;",
                suppressTransaction: true);

            migrationBuilder.DropTable(
                name: "document_access_grants");

            migrationBuilder.DropColumn(
                name: "department",
                table: "patient");
        }
    }
}

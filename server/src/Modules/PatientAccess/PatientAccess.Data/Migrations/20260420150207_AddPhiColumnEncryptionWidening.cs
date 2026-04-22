using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PatientAccess.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPhiColumnEncryptionWidening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── patient table ─────────────────────────────────────────────────────
            // character varying(N) → text: catalogue-only in PostgreSQL 15, no row rewrite (NFR-012).

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "patient",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "patient",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "InsuranceProvider",
                table: "patient",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "InsuranceMemberId",
                table: "patient",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            // ── clinical_document table ───────────────────────────────────────
            // character varying(4096) → text: catalogue-only, no row rewrite.

            migrationBuilder.AlterColumn<string>(
                name: "FileReference",
                table: "clinical_document",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(4096)",
                oldMaxLength: 4096);

            // ── extracted_fact table ─────────────────────────────────────────
            // character varying(2000) → text: catalogue-only, no row rewrite.

            migrationBuilder.AlterColumn<string>(
                name: "FactText",
                table: "extracted_fact",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);

            // ── intake_response table ─────────────────────────────────────────
            // jsonb → text: REQUIRES explicit USING cast; EF-generated AlterColumn does not
            // emit USING for jsonb, so a raw Sql() call is used instead.
            // On greenfield DB (no rows): instant. On existing data: triggers a full
            // sequential scan — schedule in a maintenance window if table is non-empty.
            // Existing plaintext rows will need manual re-encryption (ops runbook item, DR-015).

            migrationBuilder.Sql(
                "ALTER TABLE intake_response ALTER COLUMN \"Answers\" TYPE text USING \"Answers\"::text;");

            // ── patient_view_360 table ──────────────────────────────────────
            // jsonb → text: same USING cast required.

            migrationBuilder.Sql(
                "ALTER TABLE patient_view_360 ALTER COLUMN \"ConsolidatedFacts\" TYPE text USING \"ConsolidatedFacts\"::text;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // WARNING: Down() will fail if any encrypted ciphertext is present in jsonb-reverted
            // columns — ciphertext is not valid JSON. Only apply rollback before any encrypted
            // data has been written (DR-015 / ops runbook).

            // Reverse patient_view_360 jsonb cast first
            migrationBuilder.Sql(
                "ALTER TABLE patient_view_360 ALTER COLUMN \"ConsolidatedFacts\" TYPE jsonb USING \"ConsolidatedFacts\"::jsonb;");

            // Reverse intake_response jsonb cast
            migrationBuilder.Sql(
                "ALTER TABLE intake_response ALTER COLUMN \"Answers\" TYPE jsonb USING \"Answers\"::jsonb;");

            // Reverse extracted_fact text → varchar(2000)
            migrationBuilder.AlterColumn<string>(
                name: "FactText",
                table: "extracted_fact",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            // Reverse clinical_document text → varchar(4096)
            migrationBuilder.AlterColumn<string>(
                name: "FileReference",
                table: "clinical_document",
                type: "character varying(4096)",
                maxLength: 4096,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            // Reverse patient columns text → original varchar(N)
            migrationBuilder.AlterColumn<string>(
                name: "InsuranceMemberId",
                table: "patient",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "InsuranceProvider",
                table: "patient",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "patient",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "patient",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");
        }
    }
}

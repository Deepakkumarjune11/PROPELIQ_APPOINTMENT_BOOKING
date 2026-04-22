using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PatientAccess.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicalDocumentColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_clinical_document_PatientId",
                table: "clinical_document",
                newName: "ix_clinical_document_patient_id");

            migrationBuilder.AlterColumn<string>(
                name: "FileReference",
                table: "clinical_document",
                type: "character varying(4096)",
                maxLength: 4096,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2048)",
                oldMaxLength: 2048);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "clinical_document",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FileSizeBytes",
                table: "clinical_document",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "clinical_document",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OriginalFileName",
                table: "clinical_document",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "clinical_document",
                type: "timestamp with time zone",
                nullable: false,
                // Default NOW() so existing rows get a meaningful timestamp on migration
                defaultValueSql: "NOW()");

            // Partial index for Hangfire extraction job polling — active docs only (NFR-012).
            // CONCURRENTLY avoids a table-level lock on the live clinical_document table.
            // suppressTransaction: true is mandatory — CREATE INDEX CONCURRENTLY cannot run
            // inside a transaction block.
            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_clinical_document_extraction_status " +
                "ON clinical_document (\"ExtractionStatus\") WHERE \"IsDeleted\" = false;",
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop partial index before removing the column it references
            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS ix_clinical_document_extraction_status;",
                suppressTransaction: true);

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "clinical_document");

            migrationBuilder.DropColumn(
                name: "FileSizeBytes",
                table: "clinical_document");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "clinical_document");

            migrationBuilder.DropColumn(
                name: "OriginalFileName",
                table: "clinical_document");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "clinical_document");

            migrationBuilder.RenameIndex(
                name: "ix_clinical_document_patient_id",
                table: "clinical_document",
                newName: "IX_clinical_document_PatientId");

            migrationBuilder.AlterColumn<string>(
                name: "FileReference",
                table: "clinical_document",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(4096)",
                oldMaxLength: 4096);
        }
    }
}

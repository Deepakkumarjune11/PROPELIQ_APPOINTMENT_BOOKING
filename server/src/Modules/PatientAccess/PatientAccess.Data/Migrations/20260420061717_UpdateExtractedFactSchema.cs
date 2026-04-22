using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PatientAccess.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateExtractedFactSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_extracted_fact_clinical_document_DocumentId",
                table: "extracted_fact");

            migrationBuilder.AlterColumn<int>(
                name: "SourceCharOffset",
                table: "extracted_fact",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "SourceCharLength",
                table: "extracted_fact",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "FactType",
                table: "extracted_fact",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "FactText",
                table: "extracted_fact",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "extracted_fact",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "extracted_fact",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddForeignKey(
                name: "FK_extracted_fact_clinical_document_DocumentId",
                table: "extracted_fact",
                column: "DocumentId",
                principalTable: "clinical_document",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Partial index: fast fact_type filter for active (non-deleted) facts (staff review queries).
            // CONCURRENTLY: zero-downtime per NFR-012. suppressTransaction: true required by PG.
            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_extracted_fact_fact_type_active " +
                "ON extracted_fact (\"FactType\") WHERE \"IsDeleted\" = false;",
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_extracted_fact_clinical_document_DocumentId",
                table: "extracted_fact");

            // Drop the partial index added in Up() before reverting column changes
            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS ix_extracted_fact_fact_type_active;",
                suppressTransaction: true);

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "extracted_fact");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "extracted_fact");

            migrationBuilder.AlterColumn<int>(
                name: "SourceCharOffset",
                table: "extracted_fact",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "SourceCharLength",
                table: "extracted_fact",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FactType",
                table: "extracted_fact",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "FactText",
                table: "extracted_fact",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddForeignKey(
                name: "FK_extracted_fact_clinical_document_DocumentId",
                table: "extracted_fact",
                column: "DocumentId",
                principalTable: "clinical_document",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

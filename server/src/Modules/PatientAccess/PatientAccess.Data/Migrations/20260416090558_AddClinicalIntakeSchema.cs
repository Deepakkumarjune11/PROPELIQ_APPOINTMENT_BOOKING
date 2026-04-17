using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PatientAccess.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicalIntakeSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClinicalDocuments_IntakeResponses_IntakeResponseId",
                table: "ClinicalDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_ExtractedFacts_ClinicalDocuments_DocumentId",
                table: "ExtractedFacts");

            migrationBuilder.DropForeignKey(
                name: "FK_IntakeResponses_appointment_AppointmentId",
                table: "IntakeResponses");

            migrationBuilder.DropForeignKey(
                name: "FK_IntakeResponses_patient_PatientId",
                table: "IntakeResponses");

            migrationBuilder.DropPrimaryKey(
                name: "PK_IntakeResponses",
                table: "IntakeResponses");

            migrationBuilder.DropIndex(
                name: "IX_IntakeResponses_AppointmentId",
                table: "IntakeResponses");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ExtractedFacts",
                table: "ExtractedFacts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ClinicalDocuments",
                table: "ClinicalDocuments");

            migrationBuilder.DropColumn(
                name: "AppointmentId",
                table: "IntakeResponses");

            migrationBuilder.DropColumn(
                name: "ExtractionStatus",
                table: "IntakeResponses");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "IntakeResponses");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "IntakeResponses");

            migrationBuilder.DropColumn(
                name: "FactValue",
                table: "ExtractedFacts");

            migrationBuilder.DropColumn(
                name: "DocumentContent",
                table: "ClinicalDocuments");

            migrationBuilder.DropColumn(
                name: "DocumentType",
                table: "ClinicalDocuments");

            migrationBuilder.DropColumn(
                name: "GeneratedAt",
                table: "ClinicalDocuments");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ClinicalDocuments");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ClinicalDocuments");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "ClinicalDocuments");

            migrationBuilder.RenameTable(
                name: "IntakeResponses",
                newName: "intake_response");

            migrationBuilder.RenameTable(
                name: "ExtractedFacts",
                newName: "extracted_fact");

            migrationBuilder.RenameTable(
                name: "ClinicalDocuments",
                newName: "clinical_document");

            migrationBuilder.RenameIndex(
                name: "IX_IntakeResponses_PatientId",
                table: "intake_response",
                newName: "IX_intake_response_PatientId");

            migrationBuilder.RenameColumn(
                name: "IntakeResponseId",
                table: "clinical_document",
                newName: "PatientId");

            migrationBuilder.RenameIndex(
                name: "ix_clinical_document_intake_response_id",
                table: "clinical_document",
                newName: "IX_clinical_document_PatientId");

            migrationBuilder.AlterColumn<string>(
                name: "Mode",
                table: "intake_response",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "intake_response",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "intake_response",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()",
                oldClrType: typeof(Guid),
                oldType: "uuid");

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

            migrationBuilder.AlterColumn<DateTime>(
                name: "ExtractedAt",
                table: "extracted_fact",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "extracted_fact",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "Value",
                table: "extracted_fact",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "clinical_document",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "ExtractionStatus",
                table: "clinical_document",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FileReference",
                table: "clinical_document",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessedAt",
                table: "clinical_document",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UploadedAt",
                table: "clinical_document",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.AddPrimaryKey(
                name: "PK_intake_response",
                table: "intake_response",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_extracted_fact",
                table: "extracted_fact",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_clinical_document",
                table: "clinical_document",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "ix_clinical_document_encounter_id",
                table: "clinical_document",
                column: "EncounterId");

            migrationBuilder.AddForeignKey(
                name: "FK_clinical_document_patient_PatientId",
                table: "clinical_document",
                column: "PatientId",
                principalTable: "patient",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_extracted_fact_clinical_document_DocumentId",
                table: "extracted_fact",
                column: "DocumentId",
                principalTable: "clinical_document",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_intake_response_patient_PatientId",
                table: "intake_response",
                column: "PatientId",
                principalTable: "patient",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_clinical_document_patient_PatientId",
                table: "clinical_document");

            migrationBuilder.DropForeignKey(
                name: "FK_extracted_fact_clinical_document_DocumentId",
                table: "extracted_fact");

            migrationBuilder.DropForeignKey(
                name: "FK_intake_response_patient_PatientId",
                table: "intake_response");

            migrationBuilder.DropPrimaryKey(
                name: "PK_intake_response",
                table: "intake_response");

            migrationBuilder.DropPrimaryKey(
                name: "PK_extracted_fact",
                table: "extracted_fact");

            migrationBuilder.DropPrimaryKey(
                name: "PK_clinical_document",
                table: "clinical_document");

            migrationBuilder.DropIndex(
                name: "ix_clinical_document_encounter_id",
                table: "clinical_document");

            migrationBuilder.DropColumn(
                name: "Value",
                table: "extracted_fact");

            migrationBuilder.DropColumn(
                name: "ExtractionStatus",
                table: "clinical_document");

            migrationBuilder.DropColumn(
                name: "FileReference",
                table: "clinical_document");

            migrationBuilder.DropColumn(
                name: "ProcessedAt",
                table: "clinical_document");

            migrationBuilder.DropColumn(
                name: "UploadedAt",
                table: "clinical_document");

            migrationBuilder.RenameTable(
                name: "intake_response",
                newName: "IntakeResponses");

            migrationBuilder.RenameTable(
                name: "extracted_fact",
                newName: "ExtractedFacts");

            migrationBuilder.RenameTable(
                name: "clinical_document",
                newName: "ClinicalDocuments");

            migrationBuilder.RenameIndex(
                name: "IX_intake_response_PatientId",
                table: "IntakeResponses",
                newName: "IX_IntakeResponses_PatientId");

            migrationBuilder.RenameColumn(
                name: "PatientId",
                table: "ClinicalDocuments",
                newName: "IntakeResponseId");

            migrationBuilder.RenameIndex(
                name: "IX_clinical_document_PatientId",
                table: "ClinicalDocuments",
                newName: "ix_clinical_document_intake_response_id");

            migrationBuilder.AlterColumn<string>(
                name: "Mode",
                table: "IntakeResponses",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "IntakeResponses",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "NOW()");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "IntakeResponses",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<Guid>(
                name: "AppointmentId",
                table: "IntakeResponses",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "ExtractionStatus",
                table: "IntakeResponses",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "IntakeResponses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "IntakeResponses",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<int>(
                name: "SourceCharOffset",
                table: "ExtractedFacts",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "SourceCharLength",
                table: "ExtractedFacts",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "FactType",
                table: "ExtractedFacts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ExtractedAt",
                table: "ExtractedFacts",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "NOW()");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "ExtractedFacts",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<string>(
                name: "FactValue",
                table: "ExtractedFacts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "ClinicalDocuments",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<string>(
                name: "DocumentContent",
                table: "ClinicalDocuments",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DocumentType",
                table: "ClinicalDocuments",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "GeneratedAt",
                table: "ClinicalDocuments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ClinicalDocuments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ClinicalDocuments",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "ClinicalDocuments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddPrimaryKey(
                name: "PK_IntakeResponses",
                table: "IntakeResponses",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ExtractedFacts",
                table: "ExtractedFacts",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ClinicalDocuments",
                table: "ClinicalDocuments",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeResponses_AppointmentId",
                table: "IntakeResponses",
                column: "AppointmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_ClinicalDocuments_IntakeResponses_IntakeResponseId",
                table: "ClinicalDocuments",
                column: "IntakeResponseId",
                principalTable: "IntakeResponses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExtractedFacts_ClinicalDocuments_DocumentId",
                table: "ExtractedFacts",
                column: "DocumentId",
                principalTable: "ClinicalDocuments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_IntakeResponses_appointment_AppointmentId",
                table: "IntakeResponses",
                column: "AppointmentId",
                principalTable: "appointment",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IntakeResponses_patient_PatientId",
                table: "IntakeResponses",
                column: "PatientId",
                principalTable: "patient",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}

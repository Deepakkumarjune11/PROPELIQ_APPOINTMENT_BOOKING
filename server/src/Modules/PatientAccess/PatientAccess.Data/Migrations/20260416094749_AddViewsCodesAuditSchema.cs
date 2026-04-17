using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PatientAccess.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddViewsCodesAuditSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CodeSuggestions_PatientViews360_PatientView360Id",
                table: "CodeSuggestions");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientViews360_patient_PatientId",
                table: "PatientViews360");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PatientViews360",
                table: "PatientViews360");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CodeSuggestions",
                table: "CodeSuggestions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AuditLogs",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "ix_audit_log_entity_id",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "ix_audit_log_occurred_at",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "ComputedAt",
                table: "PatientViews360");

            migrationBuilder.DropColumn(
                name: "Summary",
                table: "PatientViews360");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "PatientViews360");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "PatientViews360");

            migrationBuilder.DropColumn(
                name: "ConfidenceScore",
                table: "CodeSuggestions");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "CodeSuggestions");

            migrationBuilder.DropColumn(
                name: "ReviewedByStaffId",
                table: "CodeSuggestions");

            migrationBuilder.DropColumn(
                name: "SuggestedAt",
                table: "CodeSuggestions");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "CodeSuggestions");

            migrationBuilder.DropColumn(
                name: "VerificationStatus",
                table: "CodeSuggestions");

            migrationBuilder.DropColumn(
                name: "IpAddress",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "OccurredAt",
                table: "AuditLogs");

            migrationBuilder.RenameTable(
                name: "PatientViews360",
                newName: "patient_view_360");

            migrationBuilder.RenameTable(
                name: "CodeSuggestions",
                newName: "code_suggestion");

            migrationBuilder.RenameTable(
                name: "AuditLogs",
                newName: "audit_log");

            migrationBuilder.RenameColumn(
                name: "PatientView360Id",
                table: "code_suggestion",
                newName: "PatientId");

            migrationBuilder.RenameColumn(
                name: "Code",
                table: "code_suggestion",
                newName: "CodeValue");

            migrationBuilder.RenameIndex(
                name: "IX_CodeSuggestions_PatientView360Id",
                table: "code_suggestion",
                newName: "ix_code_suggestion_patient_id");

            migrationBuilder.RenameColumn(
                name: "EntityName",
                table: "audit_log",
                newName: "TargetEntityType");

            migrationBuilder.RenameColumn(
                name: "EntityId",
                table: "audit_log",
                newName: "TargetEntityId");

            migrationBuilder.RenameColumn(
                name: "Changes",
                table: "audit_log",
                newName: "Payload");

            migrationBuilder.AlterColumn<int>(
                name: "Version",
                table: "patient_view_360",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "patient_view_360",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "ConsolidatedFacts",
                table: "patient_view_360",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUpdated",
                table: "patient_view_360",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.AddColumn<string>(
                name: "VerificationStatus",
                table: "patient_view_360",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "CodeType",
                table: "code_suggestion",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "code_suggestion",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "code_suggestion",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.AddColumn<string>(
                name: "ReviewOutcome",
                table: "code_suggestion",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "code_suggestion",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "StaffReviewed",
                table: "code_suggestion",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "ActionType",
                table: "audit_log",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "audit_log",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "audit_log",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.AddPrimaryKey(
                name: "PK_patient_view_360",
                table: "patient_view_360",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_code_suggestion",
                table: "code_suggestion",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_audit_log",
                table: "audit_log",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_created_at",
                table: "audit_log",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_target",
                table: "audit_log",
                columns: new[] { "TargetEntityType", "TargetEntityId" });

            migrationBuilder.AddForeignKey(
                name: "FK_code_suggestion_patient_PatientId",
                table: "code_suggestion",
                column: "PatientId",
                principalTable: "patient",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_patient_view_360_patient_PatientId",
                table: "patient_view_360",
                column: "PatientId",
                principalTable: "patient",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_code_suggestion_patient_PatientId",
                table: "code_suggestion");

            migrationBuilder.DropForeignKey(
                name: "FK_patient_view_360_patient_PatientId",
                table: "patient_view_360");

            migrationBuilder.DropPrimaryKey(
                name: "PK_patient_view_360",
                table: "patient_view_360");

            migrationBuilder.DropPrimaryKey(
                name: "PK_code_suggestion",
                table: "code_suggestion");

            migrationBuilder.DropPrimaryKey(
                name: "PK_audit_log",
                table: "audit_log");

            migrationBuilder.DropIndex(
                name: "ix_audit_log_created_at",
                table: "audit_log");

            migrationBuilder.DropIndex(
                name: "ix_audit_log_target",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "ConsolidatedFacts",
                table: "patient_view_360");

            migrationBuilder.DropColumn(
                name: "LastUpdated",
                table: "patient_view_360");

            migrationBuilder.DropColumn(
                name: "VerificationStatus",
                table: "patient_view_360");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "code_suggestion");

            migrationBuilder.DropColumn(
                name: "ReviewOutcome",
                table: "code_suggestion");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "code_suggestion");

            migrationBuilder.DropColumn(
                name: "StaffReviewed",
                table: "code_suggestion");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "audit_log");

            migrationBuilder.RenameTable(
                name: "patient_view_360",
                newName: "PatientViews360");

            migrationBuilder.RenameTable(
                name: "code_suggestion",
                newName: "CodeSuggestions");

            migrationBuilder.RenameTable(
                name: "audit_log",
                newName: "AuditLogs");

            migrationBuilder.RenameColumn(
                name: "PatientId",
                table: "CodeSuggestions",
                newName: "PatientView360Id");

            migrationBuilder.RenameColumn(
                name: "CodeValue",
                table: "CodeSuggestions",
                newName: "Code");

            migrationBuilder.RenameIndex(
                name: "ix_code_suggestion_patient_id",
                table: "CodeSuggestions",
                newName: "IX_CodeSuggestions_PatientView360Id");

            migrationBuilder.RenameColumn(
                name: "TargetEntityType",
                table: "AuditLogs",
                newName: "EntityName");

            migrationBuilder.RenameColumn(
                name: "TargetEntityId",
                table: "AuditLogs",
                newName: "EntityId");

            migrationBuilder.RenameColumn(
                name: "Payload",
                table: "AuditLogs",
                newName: "Changes");

            migrationBuilder.AlterColumn<long>(
                name: "Version",
                table: "PatientViews360",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "PatientViews360",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<DateTime>(
                name: "ComputedAt",
                table: "PatientViews360",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "PatientViews360",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "PatientViews360",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "PatientViews360",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AlterColumn<string>(
                name: "CodeType",
                table: "CodeSuggestions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "CodeSuggestions",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<float>(
                name: "ConfidenceScore",
                table: "CodeSuggestions",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "CodeSuggestions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedByStaffId",
                table: "CodeSuggestions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SuggestedAt",
                table: "CodeSuggestions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "CodeSuggestions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "VerificationStatus",
                table: "CodeSuggestions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "ActionType",
                table: "AuditLogs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "AuditLogs",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<string>(
                name: "IpAddress",
                table: "AuditLogs",
                type: "character varying(45)",
                maxLength: 45,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "OccurredAt",
                table: "AuditLogs",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddPrimaryKey(
                name: "PK_PatientViews360",
                table: "PatientViews360",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CodeSuggestions",
                table: "CodeSuggestions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AuditLogs",
                table: "AuditLogs",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_entity_id",
                table: "AuditLogs",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_occurred_at",
                table: "AuditLogs",
                column: "OccurredAt");

            migrationBuilder.AddForeignKey(
                name: "FK_CodeSuggestions_PatientViews360_PatientView360Id",
                table: "CodeSuggestions",
                column: "PatientView360Id",
                principalTable: "PatientViews360",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PatientViews360_patient_PatientId",
                table: "PatientViews360",
                column: "PatientId",
                principalTable: "patient",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}

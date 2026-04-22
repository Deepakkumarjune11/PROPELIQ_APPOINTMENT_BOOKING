using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PatientAccess.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAIPromptLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_appointments_slot_datetime_status_active",
                table: "appointment");

            migrationBuilder.CreateTable(
                name: "ai_prompt_log",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    ModelProvider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DeploymentName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RequestSummary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ResponseSummary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsComplete = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_prompt_log", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_appointments_slot_datetime_status_active",
                table: "appointment",
                columns: new[] { "SlotDatetime", "Status" },
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_prompt_log");

            migrationBuilder.DropIndex(
                name: "ix_appointments_slot_datetime_status_active",
                table: "appointment");

            migrationBuilder.CreateIndex(
                name: "ix_appointments_slot_datetime_status_active",
                table: "appointment",
                columns: new[] { "SlotDatetime", "Status" },
                filter: "is_deleted = false");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PatientAccess.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentSlotMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "PatientId",
                table: "appointment",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "DurationMinutes",
                table: "appointment",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "appointment",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "appointment",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VisitType",
                table: "appointment",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DurationMinutes",
                table: "appointment");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "appointment");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "appointment");

            migrationBuilder.DropColumn(
                name: "VisitType",
                table: "appointment");

            migrationBuilder.AlterColumn<Guid>(
                name: "PatientId",
                table: "appointment",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}

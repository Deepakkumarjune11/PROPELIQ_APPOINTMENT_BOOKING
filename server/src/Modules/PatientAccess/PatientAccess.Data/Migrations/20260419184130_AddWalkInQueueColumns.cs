using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PatientAccess.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWalkInQueueColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsWalkIn",
                table: "appointment",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "QueuePosition",
                table: "appointment",
                type: "integer",
                nullable: true);

            // Partial index for same-day ordered queue reads (US_016, NFR-012).
            // CONCURRENTLY — no table lock, satisfies zero-downtime requirement.
            // IF NOT EXISTS — guards against re-run after a partial failure.
            // Must run OUTSIDE a transaction; suppressTransaction: true opts this statement out
            // of EF Core's default per-migration transaction wrapper.
            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_appointments_queue_position " +
                "ON appointment(\"QueuePosition\") WHERE \"QueuePosition\" IS NOT NULL;",
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the partial index before removing the column it references (dependency order).
            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS ix_appointments_queue_position;",
                suppressTransaction: true);

            migrationBuilder.DropColumn(
                name: "QueuePosition",
                table: "appointment");

            migrationBuilder.DropColumn(
                name: "IsWalkIn",
                table: "appointment");
        }
    }
}

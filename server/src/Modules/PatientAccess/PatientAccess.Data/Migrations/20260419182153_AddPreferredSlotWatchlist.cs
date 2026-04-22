using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PatientAccess.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPreferredSlotWatchlist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the plain auto-generated FK index so the partial index can replace it.
            // Executed inside a transaction by EF Core — DROP INDEX does not require CONCURRENTLY.
            migrationBuilder.DropIndex(
                name: "IX_appointment_PreferredSlotId",
                table: "appointment");

            // CREATE INDEX CONCURRENTLY does not hold a table lock, satisfying NFR-012 (zero-downtime).
            // IF NOT EXISTS guards against duplicate execution (e.g., re-run after a partial failure).
            // Must be executed OUTSIDE a transaction — EF Core wraps migrations in a transaction by
            // default; the Sql() call with suppressTransaction: true opts out for this statement only.
            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_appointments_preferred_slot_id " +
                "ON appointment(\"PreferredSlotId\") WHERE \"PreferredSlotId\" IS NOT NULL;",
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // DROP INDEX CONCURRENTLY — no table lock during rollback (NFR-012).
            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS ix_appointments_preferred_slot_id;",
                suppressTransaction: true);

            // Restore the plain FK index that EF Core originally auto-generated.
            migrationBuilder.CreateIndex(
                name: "IX_appointment_PreferredSlotId",
                table: "appointment",
                column: "PreferredSlotId");
        }
    }
}

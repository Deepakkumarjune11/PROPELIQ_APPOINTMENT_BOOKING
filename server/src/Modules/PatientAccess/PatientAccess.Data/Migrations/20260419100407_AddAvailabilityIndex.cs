using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PatientAccess.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAvailabilityIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // DR-014: Zero-downtime index creation.
            // CREATE INDEX CONCURRENTLY does not hold a table-level lock, so the appointments
            // table remains fully accessible during the operation on a live database.
            // suppressTransaction: true is mandatory — CONCURRENTLY cannot run inside a transaction.
            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_appointments_slot_datetime_status_active " +
                "ON appointment (\"SlotDatetime\", \"Status\") WHERE \"IsDeleted\" = false;",
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS ix_appointments_slot_datetime_status_active;",
                suppressTransaction: true);
        }
    }
}

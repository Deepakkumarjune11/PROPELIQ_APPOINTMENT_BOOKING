using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PatientAccess.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Composite index for slot availability search — most frequent read pattern:
            // "Find all appointments for provider {staffId} in date range [start, end] by status"
            // Used by availability search and slot conflict detection (AC-2).
            // suppressTransaction: true required — CONCURRENTLY cannot run inside a transaction (US_028 CI lint).
            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_appointment_staff_slot_status " +
                "ON appointment (\"StaffId\", \"SlotDatetime\", \"Status\") " +
                "WHERE \"IsDeleted\" = false;",
                suppressTransaction: true);

            // Partial index for active (Booked/Arrived) future appointments per patient.
            // Used by patient dashboard, no-show risk scoring, and conflict detection queries.
            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_appointment_patient_future_active " +
                "ON appointment (\"PatientId\", \"SlotDatetime\") " +
                "WHERE \"Status\" IN ('Booked', 'Arrived') AND \"IsDeleted\" = false;",
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS ix_appointment_staff_slot_status;",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS ix_appointment_patient_future_active;",
                suppressTransaction: true);
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PatientAccess.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCodeSuggestionStaffReviewedIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // AC-2 (US_023): partial index optimises the "unreviewed codes" 422-gate query.
            // CONCURRENTLY avoids table locks in production; IF NOT EXISTS prevents re-run errors.
            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_code_suggestion_staff_reviewed " +
                "ON code_suggestion (patient_id) " +
                "WHERE is_deleted = false AND staff_reviewed = false;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP INDEX IF EXISTS ix_code_suggestion_staff_reviewed;");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PatientAccess.Data.Migrations
{
    /// <inheritdoc />
    public partial class CreatePatientView360Table : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Use raw SQL with USING cast: PostgreSQL requires explicit USING when converting
            // jsonb → text (even though the cast exists, the DDL planner demands it for ALTER TYPE).
            // This makes the column able to store encrypted ciphertext from .NET Data Protection API
            // (DR-015). The USING clause preserves existing jsonb values as their text representation.
            //
            // Note: uix_patient_view_360_patient_id UNIQUE index already exists (created in
            // AddViewsCodesAuditSchema migration). No zero-downtime CONCURRENTLY re-creation needed
            // here — the existing unique B-tree index on (patient_id) satisfies the one-view-per-patient
            // constraint (DR-018, NFR-012).
            migrationBuilder.Sql(
                @"ALTER TABLE patient_view_360
                  ALTER COLUMN ""ConsolidatedFacts"" TYPE text
                  USING ""ConsolidatedFacts""::text;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert text → jsonb (only safe when column still contains valid JSON; ciphertext rows
            // must be cleared before running Down() in production — document in deployment runbook).
            migrationBuilder.Sql(
                @"ALTER TABLE patient_view_360
                  ALTER COLUMN ""ConsolidatedFacts"" TYPE jsonb
                  USING ""ConsolidatedFacts""::jsonb;");
        }
    }
}

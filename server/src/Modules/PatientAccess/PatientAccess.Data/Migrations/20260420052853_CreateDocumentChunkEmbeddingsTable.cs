using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace PatientAccess.Data.Migrations
{
    /// <inheritdoc />
    public partial class CreateDocumentChunkEmbeddingsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "document_chunk_embeddings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chunk_index = table.Column<int>(type: "integer", nullable: false),
                    chunk_text = table.Column<string>(type: "text", nullable: false),
                    token_count = table.Column<int>(type: "integer", nullable: false),
                    embedding = table.Column<Vector>(type: "vector(1536)", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_chunk_embeddings", x => x.id);
                    table.ForeignKey(
                        name: "fk_dce_document_id",
                        column: x => x.document_id,
                        principalTable: "clinical_document",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ivfflat cosine index — approximate nearest-neighbour for <10ms p95 (TR-015, AIR-R02)
            // Standard CREATE (no CONCURRENTLY) because the table is new and empty at this point.
            migrationBuilder.Sql(
                "CREATE INDEX ix_dce_embedding_cosine " +
                "ON document_chunk_embeddings USING ivfflat (embedding vector_cosine_ops) " +
                "WITH (lists = 100);");

            // Composite unique — CONCURRENTLY for zero-downtime on future data migrations
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS ix_dce_document_chunk_unique " +
                "ON document_chunk_embeddings (document_id, chunk_index);",
                suppressTransaction: true);

            // Partial index: fast retrieval of non-null embeddings only (used by EmbeddingGenerationJob)
            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_dce_document_id_non_null " +
                "ON document_chunk_embeddings (document_id) WHERE embedding IS NOT NULL;",
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS ix_dce_document_id_non_null;",
                suppressTransaction: true);
            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS ix_dce_document_chunk_unique;",
                suppressTransaction: true);
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_dce_embedding_cosine;");

            migrationBuilder.DropTable(
                name: "document_chunk_embeddings");
            // NOTE: pgvector extension is NOT dropped here — it may be used by other tables (DR-016).
        }
    }
}

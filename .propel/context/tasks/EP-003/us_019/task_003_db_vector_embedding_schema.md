# Task - task_003_db_vector_embedding_schema

## Requirement Reference

- **User Story**: US_019 — Document Chunking & Embedding Pipeline
- **Story Location**: `.propel/context/tasks/EP-003/us_019/us_019.md`
- **Acceptance Criteria**:
  - AC-2: Each chunk is stored in the pgvector table with `document_id`, `chunk_index`, `chunk_text`, and `token_count` per DR-016; embedding column is `vector(1536)`.
  - AC-3: Cosine distance similarity search returns results within <10ms at p95 per TR-015 — requires `ivfflat` index with `vector_cosine_ops`.
- **Edge Cases**:
  - Very large documents (100+ pages) → `chunk_index` is sequential starting at 0; no uniqueness constraint on `chunk_index` alone (unique on `(document_id, chunk_index)` pair).
  - `embedding` column is nullable during staging (chunking job stores rows with `null` embedding first; embedding job fills them) — requires `nullable: true` on the EF Core configuration.

---

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | No |
| **Figma URL** | N/A |
| **Wireframe Status** | N/A |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | N/A |
| **Screen Spec** | N/A |
| **UXR Requirements** | N/A |
| **Design Tokens** | N/A |

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| ORM | Entity Framework Core + `Pgvector.EntityFrameworkCore` | 8.0 / 0.2.x |
| Database | PostgreSQL | 15.x |
| Vector Extension | pgvector | 0.5.x |
| Language | C# | 12 (.NET 8) |
| Migration Tool | EF Core Migrations | 8.0 |

> All code and libraries MUST be compatible with versions above. `Pgvector.EntityFrameworkCore` (MIT, free/OSS) provides `Vector` type for EF Core — satisfies NFR-015. The `ivfflat` index must be created via `migrationBuilder.Sql()` (not standard EF Core index API) as pgvector uses a non-standard index method.

---

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |
| **AIR Requirements** | DR-016, TR-015 |
| **AI Pattern** | N/A |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | N/A |
| **Model Provider** | N/A |

---

## Mobile References (Mobile Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **Mobile Impact** | No |
| **Platform Target** | N/A |
| **Min OS Version** | N/A |
| **Mobile Framework** | N/A |

---

## Task Overview

Create the `DocumentChunkEmbedding` domain entity, its EF Core configuration, and the zero-downtime PostgreSQL migration establishing the `document_chunk_embeddings` table with a pgvector `ivfflat` index for cosine similarity search meeting the <10ms p95 target (TR-015).

Key design decisions:
- **`vector(1536)` column** — stores 1536-dimensional float arrays for `text-embedding-3-small` output per DR-016. EF Core uses `Pgvector.EntityFrameworkCore`'s `Vector` value type; column type mapped as `"vector(1536)"`.
- **Nullable embedding** — chunking job stages rows with `embedding = NULL` before the AI job fills them. Partial index excludes null-embedding rows from retrieval queries.
- **`ivfflat` index with `vector_cosine_ops`** — fastest approximate cosine search for phase-1 scale (hundreds of documents). `lists = 100` is appropriate for up to ~1 million vectors. Must use `migrationBuilder.Sql()` because EF Core's `HasIndex()` does not support custom index methods.
- **pgvector extension** — `CREATE EXTENSION IF NOT EXISTS vector` in `Up()` migration ensures the extension is available before the table is created.
- **Composite unique index** on `(document_id, chunk_index)` — prevents duplicate chunks from re-processed documents.
- **Zero-downtime** — `CREATE INDEX CONCURRENTLY` for all indexes per NFR-012.

---

## Dependent Tasks

- **task_003_db_clinical_document_schema.md** (US_018) — `clinical_documents` table and `patients` table must exist (FK constraint from `document_chunk_embeddings.document_id → clinical_documents.id`).

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Domain/Entities/DocumentChunkEmbedding.cs` | Domain entity: `Id`, `DocumentId`, `ChunkIndex`, `ChunkText`, `TokenCount`, `Embedding` (nullable `Vector`), `CreatedAt`; `SetEmbedding(Vector)` domain method |
| CREATE | `server/src/PropelIQ.Api/Infrastructure/Persistence/Configurations/DocumentChunkEmbeddingConfiguration.cs` | EF Core `IEntityTypeConfiguration<DocumentChunkEmbedding>` — `vector(1536)` type, nullable embedding, composite unique index |
| MODIFY | `server/src/PropelIQ.Api/Infrastructure/Persistence/AppDbContext.cs` | Add `DbSet<DocumentChunkEmbedding> DocumentChunkEmbeddings`; call `modelBuilder.HasPostgresExtension("vector")` |
| CREATE | `server/src/PropelIQ.Api/Migrations/[timestamp]_CreateDocumentChunkEmbeddingsTable.cs` | Migration: pgvector extension + table + 3 indexes (ivfflat, composite unique, partial on null embedding) |

---

## Implementation Plan

1. **`DocumentChunkEmbedding` domain entity**:
   ```csharp
   public class DocumentChunkEmbedding
   {
       public Guid Id { get; private set; }
       public Guid DocumentId { get; private set; }
       public int ChunkIndex { get; private set; }
       public string ChunkText { get; private set; }
       public int TokenCount { get; private set; }
       public Pgvector.Vector? Embedding { get; private set; }  // nullable until AI job fills it
       public DateTimeOffset CreatedAt { get; private set; }

       public DocumentChunkEmbedding(Guid documentId, int chunkIndex,
           string chunkText, int tokenCount, Pgvector.Vector? embedding = null)
       { ... CreatedAt = DateTimeOffset.UtcNow; }

       public void SetEmbedding(Pgvector.Vector vector) => Embedding = vector;
   }
   ```

2. **`DocumentChunkEmbeddingConfiguration`** — EF Core type config:
   ```csharp
   builder.ToTable("document_chunk_embeddings");
   builder.HasKey(e => e.Id);
   builder.Property(e => e.DocumentId).HasColumnName("document_id");
   builder.Property(e => e.ChunkIndex).HasColumnName("chunk_index");
   builder.Property(e => e.ChunkText).HasColumnName("chunk_text").HasColumnType("text");
   builder.Property(e => e.TokenCount).HasColumnName("token_count");
   builder.Property(e => e.Embedding)
       .HasColumnName("embedding")
       .HasColumnType("vector(1536)")   // pgvector type
       .IsRequired(false);              // nullable — staged before AI job
   builder.Property(e => e.CreatedAt).HasColumnName("created_at");

   // FK to clinical_documents (cascade delete — if document deleted, remove embeddings)
   builder.HasOne<ClinicalDocument>()
       .WithMany()
       .HasForeignKey(e => e.DocumentId)
       .OnDelete(DeleteBehavior.Cascade);
   ```

3. **`AppDbContext` additions**:
   ```csharp
   public DbSet<DocumentChunkEmbedding> DocumentChunkEmbeddings => Set<DocumentChunkEmbedding>();

   protected override void OnModelCreating(ModelBuilder modelBuilder)
   {
       base.OnModelCreating(modelBuilder);
       modelBuilder.HasPostgresExtension("vector");  // ensures pgvector is activated
       // ... existing configurations
   }
   ```

4. **EF Core migration** — extension + table + indexes:
   ```csharp
   // Up()

   // Enable pgvector extension FIRST (must precede vector column creation)
   migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS vector;");

   migrationBuilder.CreateTable("document_chunk_embeddings", columns => new
   {
       id = columns.Column<Guid>(nullable: false),
       document_id = columns.Column<Guid>(nullable: false),
       chunk_index = columns.Column<int>(nullable: false),
       chunk_text = columns.Column<string>(type: "text", nullable: false),
       token_count = columns.Column<int>(nullable: false),
       embedding = columns.Column<Pgvector.Vector>(type: "vector(1536)", nullable: true),
       created_at = columns.Column<DateTimeOffset>(nullable: false)
   }, constraints: table => {
       table.PrimaryKey("pk_document_chunk_embeddings", x => x.id);
       table.ForeignKey("fk_dce_document_id",
           x => x.document_id, "clinical_documents", "id",
           onDelete: ReferentialAction.Cascade);
   });

   // ivfflat cosine index — approximate nearest-neighbour for <10ms p95 (TR-015)
   // CONCURRENTLY not supported on new/empty table; use standard CREATE for this migration
   // NOTE: Run CONCURRENTLY manually on production after data load if table is non-empty
   migrationBuilder.Sql(
       "CREATE INDEX ix_dce_embedding_cosine " +
       "ON document_chunk_embeddings USING ivfflat (embedding vector_cosine_ops) " +
       "WITH (lists = 100);");

   // Composite unique index — prevents duplicate chunks per document
   migrationBuilder.Sql(
       "CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS ix_dce_document_chunk_unique " +
       "ON document_chunk_embeddings (document_id, chunk_index);");

   // Partial index on document_id for retrieval queries filtering NULL embeddings
   migrationBuilder.Sql(
       "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_dce_document_id_non_null " +
       "ON document_chunk_embeddings (document_id) WHERE embedding IS NOT NULL;");

   // Down()
   migrationBuilder.Sql("DROP INDEX IF EXISTS ix_dce_document_id_non_null;");
   migrationBuilder.Sql("DROP INDEX IF EXISTS ix_dce_document_chunk_unique;");
   migrationBuilder.Sql("DROP INDEX IF EXISTS ix_dce_embedding_cosine;");
   migrationBuilder.DropTable("document_chunk_embeddings");
   // NOTE: Do NOT drop the 'vector' extension in Down() — may be used by other tables
   ```

---

## Current Project State

```
server/src/
  Modules/
    ClinicalIntelligence/
      ClinicalIntelligence.Domain/
        Entities/
          ClinicalDocument.cs                    ← us_018/task_003
          DocumentChunkEmbedding.cs              ← THIS TASK (create)
  PropelIQ.Api/
    Infrastructure/
      Persistence/
        AppDbContext.cs                          ← add DbSet + HasPostgresExtension("vector")
        Configurations/
          ClinicalDocumentConfiguration.cs       ← us_018/task_003
          DocumentChunkEmbeddingConfiguration.cs ← THIS TASK (create)
    Migrations/
      [timestamp]_CreateClinicalDocumentsTable.cs ← us_018/task_003
      [timestamp]_CreateDocumentChunkEmbeddingsTable.cs ← THIS TASK (generate)
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/ClinicalIntelligence/ClinicalIntelligence.Domain/Entities/DocumentChunkEmbedding.cs` | Entity: nullable `Vector?` embedding, `SetEmbedding()` domain method, cascade FK |
| CREATE | `server/src/PropelIQ.Api/Infrastructure/Persistence/Configurations/DocumentChunkEmbeddingConfiguration.cs` | EF Core config: `vector(1536)` column type, nullable, FK cascade, composite unique constraint |
| MODIFY | `server/src/PropelIQ.Api/Infrastructure/Persistence/AppDbContext.cs` | Add `DbSet<DocumentChunkEmbedding>`; add `modelBuilder.HasPostgresExtension("vector")` |
| CREATE | `server/src/PropelIQ.Api/Migrations/[timestamp]_CreateDocumentChunkEmbeddingsTable.cs` | pgvector extension + table + ivfflat cosine index + composite unique index + partial non-null index |

---

## External References

- [pgvector — `ivfflat` index with `vector_cosine_ops` and `lists` parameter](https://github.com/pgvector/pgvector#indexing)
- [pgvector — cosine distance operator `<=>` in SQL](https://github.com/pgvector/pgvector#querying)
- [Pgvector.EntityFrameworkCore — `Vector` type mapping, `CosineDistance()`](https://github.com/pgvector/pgvector-dotnet#entity-framework-core)
- [EF Core 8 — `HasPostgresExtension("vector")` via Npgsql](https://www.npgsql.org/efcore/mapping/full-text-search.html)
- [PostgreSQL 15 — `CREATE INDEX CONCURRENTLY`](https://www.postgresql.org/docs/15/sql-createindex.html#SQL-CREATEINDEX-CONCURRENTLY)
- [pgvector — performance tuning: `ivfflat` lists selection guidance](https://github.com/pgvector/pgvector#ivfflat)
- [DR-016 — pgvector vector embedding storage requirement](../.propel/context/docs/design.md#DR-016)
- [TR-015 — cosine distance <10ms p95 requirement](../.propel/context/docs/design.md#TR-015)
- [NFR-012 — zero-downtime migration requirement](../.propel/context/docs/design.md#NFR-012)

---

## Build Commands

```bash
cd server
dotnet add src/PropelIQ.Api package Pgvector.EntityFrameworkCore
dotnet ef migrations add CreateDocumentChunkEmbeddingsTable --project src/PropelIQ.Api
dotnet ef database update --project src/PropelIQ.Api
dotnet build
dotnet test --filter "Category=Unit"
```

---

## Implementation Validation Strategy

- [ ] `dotnet ef migrations add` generates correct `Up()` with `CREATE EXTENSION IF NOT EXISTS vector` before table creation
- [ ] `dotnet ef database update` applies cleanly to PostgreSQL 15 with pgvector 0.5.x installed
- [ ] `embedding` column type in DB is `vector(1536)` (verify with `\d document_chunk_embeddings`)
- [ ] Inserting a row with `embedding = null` succeeds; inserting with a 1536-dim `Vector` succeeds
- [ ] `ivfflat` index created with `lists = 100` and `vector_cosine_ops` (verify with `\d+ document_chunk_embeddings`)
- [ ] `Down()` drops all 3 indexes before `DropTable` without errors
- [ ] `EXPLAIN ANALYZE` on cosine distance query uses `ix_dce_embedding_cosine` index and returns within <10ms on test data

---

## Implementation Checklist

- [ ] Add `Pgvector.EntityFrameworkCore` NuGet to `PropelIQ.Api`; verify `Vector` type available in C# project
- [ ] Create `DocumentChunkEmbedding` entity: `Vector?` nullable embedding, `SetEmbedding(Vector)` method, cascade FK constructor
- [ ] Create `DocumentChunkEmbeddingConfiguration`: `HasColumnType("vector(1536)")` on `Embedding`, `IsRequired(false)`, `OnDelete(DeleteBehavior.Cascade)` FK
- [ ] Add `DbSet<DocumentChunkEmbedding>` and `modelBuilder.HasPostgresExtension("vector")` to `AppDbContext`
- [ ] Generate EF migration; verify scaffold; replace default index calls with `migrationBuilder.Sql()` for `ivfflat` and composite unique indexes
- [ ] Implement `Down()`: drop all 3 indexes in reverse dependency order before `DropTable`; do NOT drop `vector` extension
- [ ] Validate `(document_id, chunk_index)` unique constraint prevents duplicate chunk staging on re-processed documents

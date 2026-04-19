# Task - task_002_db_document_acl_schema

## Requirement Reference

- **User Story**: US_029 — AI Prompt Safety & RAG Access Controls
- **Story Location**: `.propel/context/tasks/EP-006/us_029/us_029.md`
- **Acceptance Criteria**:
  - AC-3: RAG retrieval enforces document-level access — requires `document_access_grants` table for explicit ACL rows per AIR-S02.
  - AC-4: Staff-scoped queries filter by department or care team — requires `patients.department` column to enable the staff-department JOIN in `RagAccessFilter` per AIR-S02.
- **Edge Cases**:
  - Patient has no department assigned (NULL): department-based JOIN excludes the patient's documents; staff must obtain an explicit grant via `document_access_grants` to access those docs. Safe default — no data leakage.
  - `document_access_grants` table is empty (fresh install): staff access falls through entirely to the department JOIN. Correct behaviour — no spurious access.

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
| Backend | .NET 8 ASP.NET Core | 8.0 LTS |
| ORM | Entity Framework Core | 8.0 LTS |
| Database | PostgreSQL | 15.x |
| Migration Tool | `dotnet-ef` | 8.0.0 (pinned in `.config/dotnet-tools.json` per US_028) |
| Testing - Unit | xUnit | 2.x |

> All `CREATE INDEX CONCURRENTLY` statements MUST use `migrationBuilder.Sql(..., suppressTransaction: true)` — enforced by CI lint step added in US_028/task_001. The `dotnet ef migrations has-pending-model-changes` CI gate also enforces that the ModelSnapshot is updated by this migration.

---

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |
| **AIR Requirements** | AIR-S02 (data foundation only — runtime logic in task_001) |
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

Add the `document_access_grants` table and a `department` column on `patients` to the PostgreSQL schema via an EF Core 8 migration. These two schema elements are the data foundation consumed by `RagAccessFilter` (implemented in task_001) to enforce AIR-S02 row-level access control on RAG retrieval.

**`document_access_grants` table** — explicit per-document ACL grants:
- Stores individual grant rows linking a document to a grantee (staff member or department string).
- Used by `RagAccessFilter` Branch A: explicit individual staff grants.
- `grantee_type` discriminates between `'staff'` (individual grant) and `'dept'` (department-level grant, reserved for future batch-grant workflows).

**`patients.department` column** — department linkage for department-scoped staff access:
- Nullable `VARCHAR(100)` — backward compatible (existing patients retain `NULL`, not included in dept-based RAG results until populated).
- Used by `RagAccessFilter` Branch B: `staff.department = patient.department` join.
- NOT PHI (department name is administrative metadata, not personal health information — no encryption required).

**Migration name**: `AddDocumentAccessGrantSchema`
**Sequence**: Follows `AddPhiColumnEncryptionWidening` (US_027), which is the current latest migration.

---

## Dependent Tasks

- **task_003_db_vector_embedding_schema.md** (US_019) — `document_chunk_embeddings` table must exist; `clinical_documents` FK must exist (referenced by `document_access_grants.document_id`).
- **task_001_ai_prompt_safety_rag_acl.md** (US_029, this sprint) — `RagAccessFilter` implementation references `DocumentAccessGrant` entity and `Patient.Department` property. Both DB task and backend task can proceed in parallel; the backend compiles against entity definitions and migration is applied at integration test time.
- **US_027/task_001** — `AddPhiColumnEncryptionWidening` migration must be the latest applied migration before this one.

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Data/Entities/Patient.cs` | Add `Department` property: `public string? Department { get; private set; }` with `SetDepartment(string?)` domain method |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Data/Configurations/PatientConfiguration.cs` | Map `Department` as `VARCHAR(100)` nullable; no PHI encryption (administrative metadata) |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Data/Entities/DocumentAccessGrant.cs` | Domain entity: `Id`, `DocumentId`, `GranteeId`, `GranteeType`, `GrantedAt`; no navigation properties |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Data/Configurations/DocumentAccessGrantConfiguration.cs` | EF `IEntityTypeConfiguration<DocumentAccessGrant>`: `ToTable("document_access_grants")`, FK to `clinical_documents`, `GranteeType` check constraint |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Data/PropelIQDbContext.cs` | Add `DbSet<DocumentAccessGrant> DocumentAccessGrants` |
| CREATE | `server/src/PropelIQ.Api/Infrastructure/Migrations/[timestamp]_AddDocumentAccessGrantSchema.cs` | EF Core migration: `document_access_grants` table + `ALTER TABLE patients ADD COLUMN department` + 3 CONCURRENTLY indexes via `suppressTransaction: true` |

---

## Implementation Plan

### 1. `Patient.cs` — add `Department` property

```csharp
// Add to existing Patient entity (PatientAccess.Data/Entities/Patient.cs)
// Place after existing properties (Name, Phone, Email, etc.)

/// <summary>Administrative department assignment for RAG access scoping (AIR-S02). NOT PHI.</summary>
public string? Department { get; private set; }

public void SetDepartment(string? department)
    => Department = department is { Length: > 100 }
        ? throw new ArgumentException("Department name exceeds 100 characters.", nameof(department))
        : department;
```

### 2. `PatientConfiguration.cs` — map `Department`

```csharp
// Add inside Configure(EntityTypeBuilder<Patient> builder) after existing property mappings
builder.Property(p => p.Department)
    .HasColumnName("department")
    .HasColumnType("varchar(100)")
    .IsRequired(false);

// NOTE: No PhiEncryptedConverter — department is administrative metadata, not PHI
// NOTE: No HasMaxLength(100) on entity — constraint enforced by column type only
```

### 3. `DocumentAccessGrant.cs` — domain entity

```csharp
// PatientAccess.Data/Entities/DocumentAccessGrant.cs
namespace PropelIQ.PatientAccess.Data.Entities;

/// <summary>
/// Explicit per-document access grant for RAG retrieval access control (AIR-S02).
/// Immutable after creation — no update/delete operations permitted in application code.
/// </summary>
public sealed class DocumentAccessGrant
{
    public Guid Id { get; private set; }

    /// <summary>FK to clinical_documents.id (ClinicalIntelligence domain).</summary>
    public Guid DocumentId { get; private set; }

    /// <summary>Staff member ID (grantee_type = 'staff') or department identifier (grantee_type = 'dept').</summary>
    public Guid GranteeId { get; private set; }

    /// <summary>'staff' = individual grant; 'dept' = department-level grant (reserved for future use).</summary>
    public string GranteeType { get; private set; } = default!;

    public DateTimeOffset GrantedAt { get; private set; }

    // EF Core constructor
    private DocumentAccessGrant() { }

    public static DocumentAccessGrant Create(Guid documentId, Guid granteeId, string granteeType)
    {
        if (granteeType is not ("staff" or "dept"))
            throw new ArgumentException("grantee_type must be 'staff' or 'dept'.", nameof(granteeType));

        return new DocumentAccessGrant
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            GranteeId = granteeId,
            GranteeType = granteeType,
            GrantedAt = DateTimeOffset.UtcNow
        };
    }
}
```

> **Security note (OWASP A01):** `DocumentAccessGrant` has no public setters. `Create(...)` is the only entry point and validates `grantee_type`. The `AuditLogImmutabilityInterceptor` pattern established in US_026 should NOT be applied here (grants can be revoked in future workflows); however, no `DELETE` endpoint is implemented in this sprint — grants are append-only for now.

### 4. `DocumentAccessGrantConfiguration.cs` — EF configuration

```csharp
public sealed class DocumentAccessGrantConfiguration : IEntityTypeConfiguration<DocumentAccessGrant>
{
    public void Configure(EntityTypeBuilder<DocumentAccessGrant> builder)
    {
        builder.ToTable("document_access_grants");

        builder.HasKey(g => g.Id);

        builder.Property(g => g.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(g => g.DocumentId)
            .HasColumnName("document_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(g => g.GranteeId)
            .HasColumnName("grantee_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(g => g.GranteeType)
            .HasColumnName("grantee_type")
            .HasColumnType("varchar(10)")
            .IsRequired();

        builder.Property(g => g.GrantedAt)
            .HasColumnName("granted_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("NOW()");

        // Check constraint — enforces 'staff' | 'dept' at DB level (OWASP A03 defense-in-depth)
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_document_access_grants_grantee_type",
            "grantee_type IN ('staff', 'dept')"));

        // FK to clinical_documents — no navigation property (cross-module reference)
        // Enforced via raw FK in migration SQL (see migration file)
        // DO NOT add HasOne<ClinicalDocument>() — would create cross-module coupling

        // Indexes defined in migration via migrationBuilder.Sql() CONCURRENTLY
        // (HasIndex here would create non-concurrent indexes in EF migration scaffold)
    }
}
```

### 5. Migration — `AddDocumentAccessGrantSchema`

The migration `Up()` performs four operations in this order:

1. `ALTER TABLE patients ADD COLUMN department VARCHAR(100)` — zero-downtime column add (nullable).
2. `CREATE TABLE document_access_grants` — with check constraint on `grantee_type`.
3. Three `CREATE INDEX CONCURRENTLY` statements (each `suppressTransaction: true`):
   - `ix_dag_grantee` on `(grantee_id, grantee_type)` — supports Branch A staff lookup in `RagAccessFilter`.
   - `ix_dag_document_id` on `document_id` — supports document-scoped grant lookups.
   - `ux_dag_unique` UNIQUE on `(document_id, grantee_id, grantee_type)` — prevents duplicate grants (idempotent grant application).

```csharp
public partial class AddDocumentAccessGrantSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Step 1: Add department column to patients (nullable, zero-downtime)
        migrationBuilder.AddColumn<string>(
            name: "department",
            table: "patients",
            type: "varchar(100)",
            nullable: true);

        // Step 2: Create document_access_grants table
        migrationBuilder.CreateTable(
            name: "document_access_grants",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false,
                    defaultValueSql: "gen_random_uuid()"),
                document_id = table.Column<Guid>(type: "uuid", nullable: false),
                grantee_id = table.Column<Guid>(type: "uuid", nullable: false),
                grantee_type = table.Column<string>(type: "varchar(10)", nullable: false),
                granted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false,
                    defaultValueSql: "NOW()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_document_access_grants", x => x.id);

                // FK to clinical_documents (cross-module; no EF navigation property)
                table.ForeignKey(
                    name: "fk_document_access_grants_document_id",
                    column: x => x.document_id,
                    principalTable: "clinical_documents",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade); // cascade: grant deleted when document deleted

                table.CheckConstraint(
                    name: "ck_document_access_grants_grantee_type",
                    sql: "grantee_type IN ('staff', 'dept')");
            });

        // Step 3a: Index on (grantee_id, grantee_type) — RagAccessFilter Branch A staff lookup
        // MUST use suppressTransaction: true — CONCURRENTLY cannot run inside a transaction
        migrationBuilder.Sql(
            "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_dag_grantee " +
            "ON document_access_grants (grantee_id, grantee_type);",
            suppressTransaction: true);

        // Step 3b: Index on document_id — document-scoped grant queries
        migrationBuilder.Sql(
            "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_dag_document_id " +
            "ON document_access_grants (document_id);",
            suppressTransaction: true);

        // Step 3c: Unique index — prevents duplicate grants
        migrationBuilder.Sql(
            "CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS ux_dag_unique " +
            "ON document_access_grants (document_id, grantee_id, grantee_type);",
            suppressTransaction: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Drop indexes CONCURRENTLY before dropping table (PostgreSQL dependency order)
        migrationBuilder.Sql(
            "DROP INDEX CONCURRENTLY IF EXISTS ux_dag_unique;",
            suppressTransaction: true);

        migrationBuilder.Sql(
            "DROP INDEX CONCURRENTLY IF EXISTS ix_dag_document_id;",
            suppressTransaction: true);

        migrationBuilder.Sql(
            "DROP INDEX CONCURRENTLY IF EXISTS ix_dag_grantee;",
            suppressTransaction: true);

        migrationBuilder.DropTable(name: "document_access_grants");

        migrationBuilder.DropColumn(name: "department", table: "patients");
    }
}
```

> **Migration ordering note:** This migration uses `AddColumn` (not `AlterColumn`) for `patients.department` — adding a nullable column to an existing table acquires only a brief `ACCESS EXCLUSIVE` lock in PostgreSQL 15 (column metadata update only). No table rewrite. Safe for production with active connections.

> **`suppressTransaction: true` reminder (CI lint):** The CONCURRENTLY lint step in CI (`ci.yml`, US_028/task_001) will FAIL the PR if any `CONCURRENTLY` keyword appears in a `migrationBuilder.Sql()` call without a corresponding `suppressTransaction: true` argument. All three indexes above comply.

---

## Current Project State

```
server/src/Modules/PatientAccess/
└── PatientAccess.Data/
    ├── Entities/
    │   ├── Patient.cs                              ← MODIFY: add Department property
    │   ├── AuditLog.cs                             ← no change (US_026 columns already added)
    │   ├── Staff.cs                                ← no change (Department string already exists from US_025)
    │   └── DocumentAccessGrant.cs                  ← CREATE this task
    ├── Configurations/
    │   ├── PatientConfiguration.cs                 ← MODIFY: map Department column
    │   └── DocumentAccessGrantConfiguration.cs     ← CREATE this task
    └── PropelIQDbContext.cs                        ← MODIFY: add DbSet<DocumentAccessGrant>

server/src/PropelIQ.Api/Infrastructure/Migrations/
    ├── ...AddStaffAdminSchema.cs                   ← US_025 latest
    ├── ...AddAuditLogComplianceSchema.cs           ← US_026
    ├── ...AddPhiColumnEncryptionWidening.cs        ← US_027 (current latest)
    └── [timestamp]_AddDocumentAccessGrantSchema.cs ← CREATE this task (next in sequence)
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Data/Entities/Patient.cs` | Add `Department` nullable string property with `SetDepartment()` domain method |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Data/Configurations/PatientConfiguration.cs` | Map `Department` as `varchar(100)` nullable; no PHI encryption |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Data/Entities/DocumentAccessGrant.cs` | `Id`, `DocumentId`, `GranteeId`, `GranteeType`, `GrantedAt`; factory `Create()`; no nav props |
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Data/Configurations/DocumentAccessGrantConfiguration.cs` | EF config: `document_access_grants` table, check constraint on `grantee_type`, no cross-module navigation |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Data/PropelIQDbContext.cs` | Add `public DbSet<DocumentAccessGrant> DocumentAccessGrants { get; set; }` |
| CREATE | `server/src/PropelIQ.Api/Infrastructure/Migrations/[timestamp]_AddDocumentAccessGrantSchema.cs` | Migration: `ALTER TABLE patients ADD COLUMN department`; `CREATE TABLE document_access_grants`; 3 CONCURRENTLY indexes with `suppressTransaction: true`; full `Down()` reversing all in dependency order |

---

## External References

- [EF Core 8 — IEntityTypeConfiguration](https://learn.microsoft.com/en-us/ef/core/modeling/#grouping-configuration)
- [PostgreSQL 15 — ALTER TABLE ADD COLUMN lock behaviour](https://www.postgresql.org/docs/15/sql-altertable.html)
- [PostgreSQL 15 — CREATE INDEX CONCURRENTLY](https://www.postgresql.org/docs/15/sql-createindex.html#SQL-CREATEINDEX-CONCURRENTLY)
- [EF Core migrations — suppressTransaction](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/operations#using-migrationbuildersql)
- [OWASP A01 — Broken Access Control](https://owasp.org/Top10/A01_2021-Broken_Access_Control/)

---

## Build Commands

```powershell
# Scaffold migration (from server/ folder)
dotnet tool restore
dotnet ef migrations add AddDocumentAccessGrantSchema `
  --project src/Modules/PatientAccess/PatientAccess.Data `
  --startup-project src/PropelIQ.Api `
  --output-dir Infrastructure/Migrations

# Verify no pending model changes after scaffolding
dotnet ef migrations has-pending-model-changes `
  --project src/Modules/PatientAccess/PatientAccess.Data `
  --startup-project src/PropelIQ.Api

# Apply migration (local dev)
dotnet ef database update `
  --project src/Modules/PatientAccess/PatientAccess.Data `
  --startup-project src/PropelIQ.Api
```

---

## Implementation Validation Strategy

- [ ] Unit tests pass
- [ ] `dotnet ef migrations has-pending-model-changes` exits 0 after migration scaffold
- [ ] Migration `Up()` applies cleanly on a fresh schema (PostgreSQL 15 Docker container)
- [ ] Migration `Down()` rolls back cleanly — `document_access_grants` table dropped, `patients.department` column dropped, indexes dropped CONCURRENTLY in correct order
- [ ] All three CONCURRENTLY index statements have `suppressTransaction: true` — CI lint step must pass
- [ ] `Patient.Department = null` (existing rows): `RagAccessFilter` dept-join returns no rows for such patients — confirmed by integration test
- [ ] `DocumentAccessGrant.Create(docId, granteeId, "invalid")` throws `ArgumentException` — confirmed by unit test

---

## Implementation Checklist

- [ ] MODIFY `Patient.cs` — add `Department` nullable string property + `SetDepartment(string?)` domain method with 100-char guard
- [ ] MODIFY `PatientConfiguration.cs` — map `Department` as `varchar(100)` nullable (no PHI encryption; no `HasMaxLength` on entity property — type constraint only)
- [ ] CREATE `DocumentAccessGrant.cs` — `Id`, `DocumentId`, `GranteeId`, `GranteeType` (check 'staff'|'dept' in factory), `GrantedAt`; private EF Core constructor; `static Create()` factory; no navigation properties (cross-module FK)
- [ ] CREATE `DocumentAccessGrantConfiguration.cs` — `ToTable("document_access_grants")`, PK, check constraint `ck_document_access_grants_grantee_type`, FK to `clinical_documents` (cascade delete), `HasDefaultValueSql("NOW()")` for `granted_at`; no `HasIndex()` calls (indexes created CONCURRENTLY in migration SQL)
- [ ] MODIFY `PropelIQDbContext.cs` — add `public DbSet<DocumentAccessGrant> DocumentAccessGrants { get; set; }` and apply `DocumentAccessGrantConfiguration` in `OnModelCreating`
- [ ] CREATE `[timestamp]_AddDocumentAccessGrantSchema` migration — `ALTER TABLE patients ADD COLUMN department varchar(100)` → `CREATE TABLE document_access_grants` with PK + FK + check constraint → 3× `Sql(..., suppressTransaction: true)` for CONCURRENTLY indexes; `Down()` drops indexes CONCURRENTLY before `DropTable` before `DropColumn`

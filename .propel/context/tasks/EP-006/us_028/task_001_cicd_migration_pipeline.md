# Task - task_001_cicd_migration_pipeline

## Requirement Reference

- **User Story**: US_028 — Backup Strategy & Migration Patterns
- **Story Location**: `.propel/context/tasks/EP-006/us_028/us_028.md`
- **Acceptance Criteria**:
  - AC-4: Every EF Core migration follows a versioned, idempotent pattern (already enforced by EF tooling). This task adds a CI gate that verifies no uncommitted pending migrations exist, and a CD migration step that applies the latest migration to the target database **before** deploying the new application binary, ensuring the schema is always ahead of the code during rolling update per DR-014.
  - AC-5: If `dotnet ef database update` fails during CD, the pipeline automatically rolls back to the previous EF Core migration snapshot (`dotnet ef database update <PreviousMigration>`) and sends a Slack/email alert via GitHub Actions `on.failure` notification step. The application binary is NOT deployed after a migration failure — the old binary continues to serve traffic (zero-downtime per NFR-012).
- **Edge Cases**:
  - `dotnet ef database update` on PostgreSQL 15 with `CREATE INDEX CONCURRENTLY` inside migration SQL must run **outside** a transaction block — EF Core wraps migrations in transactions by default. Migrations containing `CONCURRENTLY` must set `migrationBuilder.Sql(..., suppressTransaction: true)` (already noted in prior task conventions; this task adds a CI check for it).
  - If no new migrations are pending, the CD migration step is a no-op (`dotnet ef database update` returns 0 with no-op message) — this is correct behaviour and must not fail the pipeline.
  - The `PreviousMigration` target for rollback is determined at runtime by calling `dotnet ef migrations list` and selecting the second-to-last entry.

---

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | No |
| **Figma URL** | N/A |
| **Wireframe Status** | N/A |
| **Wireframe Path/URL** | N/A |
| **Screen Spec** | N/A |
| **UXR Requirements** | N/A |
| **Design Tokens** | N/A |

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| CI/CD | GitHub Actions | — |
| ORM / Migrations | Entity Framework Core | 8.0 |
| Runtime | .NET | 8 LTS |
| Database | PostgreSQL | 15.x |

> **Existing workflows**: `ci.yml` builds, formats, and tests on every PR. `cd.yml` builds a web deploy ZIP artifact on every merge to `main`. Neither currently runs EF Core migrations. This task extends both.

---

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |
| **AIR Requirements** | N/A |
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

Extend the existing GitHub Actions CI and CD workflows to enforce migration correctness and enable automated rollback on migration failure.

### Part A — CI Gate: Pending Migration Check

Add a step to `ci.yml` (`dotnet-ci` job) that verifies the migration snapshot is up-to-date with the entity model. A drift would indicate a developer added/modified an entity property without generating the corresponding migration — catching this in PR review prevents silent schema drift in production.

```yaml
- name: Check for pending EF Core migrations
  run: |
    dotnet tool restore
    dotnet ef migrations has-pending-model-changes \
      --project server/src/Modules/PatientAccess/PatientAccess.Data \
      --startup-project server/src/PropelIQ.Api
  # Exits non-zero if model has changes not reflected in latest migration — fails PR
```

> `dotnet ef migrations has-pending-model-changes` was introduced in EF Core 8.0 and returns exit code 1 if the compiled model differs from the last migration snapshot.

### Part B — CD: Pre-Deployment Migration Step with Auto-Rollback

Add a `migrate` job to `cd.yml` that runs **before** the artifact is deployed to IIS. The job:
1. Applies the latest migration to the target database.
2. On failure: determines the previous migration name, rolls back, and alerts.
3. The `build-and-package` job only uploads the IIS artifact if `migrate` succeeds.

```yaml
# New job in cd.yml — must run before the IIS artifact is deployed
migrate:
  name: Apply EF Core Migrations
  runs-on: ubuntu-latest
  needs: build-and-package   # artifact must be built before migration metadata is available
  environment: production    # uses production environment secrets

  steps:
    - name: Checkout
      uses: actions/checkout@v5

    - name: Setup .NET 8
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '8.0.x'

    - name: Restore tools
      run: dotnet tool restore

    - name: Install EF tool (idempotent)
      run: dotnet tool install --global dotnet-ef --version 8.* || true

    # Apply all pending migrations — no-op if already at latest
    - name: Apply EF Core migrations
      id: migrate
      env:
        ConnectionStrings__DefaultConnection: ${{ secrets.PROD_DB_CONNECTION_STRING }}
        DataProtection__KeysPath: /tmp/keys/phi     # ephemeral; keys not needed for schema changes
      run: |
        dotnet ef database update \
          --project server/src/Modules/PatientAccess/PatientAccess.Data \
          --startup-project server/src/PropelIQ.Api \
          --connection "${{ secrets.PROD_DB_CONNECTION_STRING }}"

    # Rollback to previous migration if apply failed
    - name: Rollback migration on failure
      if: failure() && steps.migrate.outcome == 'failure'
      env:
        ConnectionStrings__DefaultConnection: ${{ secrets.PROD_DB_CONNECTION_STRING }}
      run: |
        # Determine the second-to-last applied migration name
        PREV=$(dotnet ef migrations list \
          --project server/src/Modules/PatientAccess/PatientAccess.Data \
          --startup-project server/src/PropelIQ.Api \
          --connection "${{ secrets.PROD_DB_CONNECTION_STRING }}" \
          --json | jq -r '.[-2].name')

        echo "Rolling back to: $PREV"
        dotnet ef database update "$PREV" \
          --project server/src/Modules/PatientAccess/PatientAccess.Data \
          --startup-project server/src/PropelIQ.Api \
          --connection "${{ secrets.PROD_DB_CONNECTION_STRING }}"

    # Alert DevOps on failure — uses GitHub Actions default failure notification
    # or optionally POST to Slack webhook (secret: SLACK_WEBHOOK_URL)
    - name: Alert on migration failure
      if: failure()
      run: |
        curl -s -X POST "${{ secrets.SLACK_WEBHOOK_URL }}" \
          -H 'Content-type: application/json' \
          --data '{
            "text": ":rotating_light: *PropelIQ* EF Core migration failed on `${{ github.ref_name }}` (run #${{ github.run_number }}). Schema rolled back. Application NOT deployed. <${{ github.server_url }}/${{ github.repository }}/actions/runs/${{ github.run_id }}|View run>"
          }' || true    # non-blocking — alert failure must not mask the real failure
```

> **Security**: `PROD_DB_CONNECTION_STRING` and `SLACK_WEBHOOK_URL` are stored as GitHub Encrypted Secrets in the `production` environment. Never echoed in logs. Connection string must use a **migration-only** DB user with DDL privileges but no DML access to patient data tables (OWASP A01 — least privilege).

### Part C — `dotnet-tools.json` (`.config/dotnet-tools.json`)

Ensure `dotnet-ef` is pinned as a local tool to guarantee version consistency between developers and CI:

```json
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "dotnet-ef": {
      "version": "8.0.0",
      "commands": ["dotnet-ef"]
    }
  }
}
```

> File path: `.config/dotnet-tools.json` (repository root). Run `dotnet tool restore` at the start of any workflow step that calls `dotnet ef`.

### Part D — Migration Convention Enforcement: `CONCURRENTLY` Lint Check

Add a step to `ci.yml` that rejects any migration file containing `CREATE INDEX CONCURRENTLY` without `suppressTransaction: true` via a simple `grep` check:

```yaml
- name: Lint migration CONCURRENTLY usage
  run: |
    # Reject: CONCURRENTLY inside migrationBuilder.Sql(...) without suppressTransaction
    # Pattern: migrationBuilder.Sql("...CONCURRENTLY...") with no second parameter
    if grep -rn "CONCURRENTLY" server/src/Modules/PatientAccess/PatientAccess.Data/Migrations/ \
       | grep -v "suppressTransaction: true" \
       | grep -v "\.Designer\.cs" \
       | grep -v "Snapshot\.cs" \
       | grep -q .; then
      echo "ERROR: Found CONCURRENTLY index in migration without suppressTransaction: true."
      echo "Add second parameter: migrationBuilder.Sql(\"...\", suppressTransaction: true)"
      exit 1
    fi
    echo "Migration CONCURRENTLY lint: OK"
```

---

## Dependent Tasks

- No blocking dependencies — this task modifies only CI/CD workflow YAML files and adds a tooling manifest.

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | `.github/workflows/ci.yml` | Add `has-pending-model-changes` step to `dotnet-ci` job; add CONCURRENTLY lint step |
| MODIFY | `.github/workflows/cd.yml` | Add `migrate` job with `apply → rollback-on-fail → alert` steps; gate `build-and-package` artifact upload on migrate success |
| CREATE | `.config/dotnet-tools.json` | Pin `dotnet-ef@8.0.0` as local tool for deterministic version across dev and CI |

---

## Current Project State

```
.github/
  workflows/
    ci.yml         ← MODIFY — add migration lint steps to dotnet-ci job
    cd.yml         ← MODIFY — add migrate job with rollback and alert
.config/
  dotnet-tools.json  ← THIS TASK (create)
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | `.github/workflows/ci.yml` | Add `has-pending-model-changes` check + CONCURRENTLY lint to `dotnet-ci` job after Build step |
| MODIFY | `.github/workflows/cd.yml` | Add `migrate` job with 4 steps (setup, apply, rollback-on-fail, alert); gate IIS artifact upload to run only after `migrate` succeeds |
| CREATE | `.config/dotnet-tools.json` | `dotnet-ef 8.0.0` local tool manifest — ensures `dotnet tool restore` fetches the exact version |

---

## External References

- [dotnet ef migrations has-pending-model-changes — EF Core 8.0](https://learn.microsoft.com/en-us/ef/core/cli/dotnet#dotnet-ef-migrations-has-pending-model-changes)
- [dotnet ef database update — rollback via target migration](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying#command-line)
- [GitHub Actions — environment secrets](https://docs.github.com/en/actions/deployment/targeting-different-environments/using-environments-for-deployment)
- [GitHub Actions — jobs.needs (dependency ordering)](https://docs.github.com/en/actions/writing-workflows/choosing-what-your-workflow-does/using-jobs-in-a-workflow)
- [EF Core migrations — suppressTransaction for CONCURRENTLY](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/operations#arbitrary-changes-via-raw-sql)
- [OWASP A01 — least-privilege DB migration user](https://owasp.org/Top10/A01_2021-Broken_Access_Control/)
- [DR-014 — Zero-downtime migration strategy; blue-green or rolling update](../.propel/context/docs/design.md)
- [NFR-012 — Zero-downtime database schema migrations](../.propel/context/docs/design.md)
- [TR-016 — GitHub Actions for CI/CD with zero-downtime deployment](../.propel/context/docs/design.md)

---

## Build Commands

```bash
# Install local tools (run from repository root)
dotnet tool restore

# Manually check pending model changes
dotnet ef migrations has-pending-model-changes \
  --project server/src/Modules/PatientAccess/PatientAccess.Data \
  --startup-project server/src/PropelIQ.Api

# List all migrations (for determining rollback target)
dotnet ef migrations list \
  --project server/src/Modules/PatientAccess/PatientAccess.Data \
  --startup-project server/src/PropelIQ.Api
```

---

## Implementation Validation Strategy

- [ ] CI test: modify an entity property without generating a migration → `has-pending-model-changes` step fails the PR check
- [ ] CI test: add a migration containing `CREATE INDEX CONCURRENTLY` without `suppressTransaction: true` → lint step fails
- [ ] CD test: introduce a deliberate bad migration SQL → `migrate` job fails → rollback step executes → `Alert on migration failure` sends Slack notification → IIS artifact upload is skipped
- [ ] CD test: valid migration → `migrate` job succeeds → IIS artifact upload proceeds normally
- [ ] CD test: no pending migrations (schema already up-to-date) → `dotnet ef database update` exits 0 → pipeline green
- [ ] Security test: `PROD_DB_CONNECTION_STRING` is never echoed in any step's stdout; verify via workflow run logs

---

## Implementation Checklist

- [ ] Create `.config/dotnet-tools.json` pinning `dotnet-ef@8.0.0`; add `dotnet tool restore` as the first step in any workflow job that calls `dotnet ef`
- [ ] Modify `ci.yml`: add `has-pending-model-changes` step after the `Build` step in `dotnet-ci` job; add `CONCURRENTLY lint` step after it
- [ ] Modify `cd.yml`: add `migrate` job with `needs: build-and-package`; 4 steps: setup .NET + restore tools, apply migration (id: `migrate`), rollback on failure (`if: failure() && steps.migrate.outcome == 'failure'`), Slack alert (`if: failure()`)
- [ ] Update `cd.yml` `build-and-package` job: change `upload-artifact` step to only run `if: success()` and add `needs: migrate` to the job so the artifact is only uploaded after migration succeeds
- [ ] Add `production` environment to GitHub repository settings with `PROD_DB_CONNECTION_STRING` and `SLACK_WEBHOOK_URL` secrets — document in README under "Deployment Secrets"
- [ ] Verify rollback step uses `dotnet ef migrations list --json | jq -r '.[-2].name'` to dynamically determine the previous migration name rather than a hardcoded value
- [ ] Security review: confirm connection string is only passed via `--connection` flag (not echoed); confirm `DataProtection__KeysPath` for CD runner points to ephemeral `/tmp` (no PHI keys needed for schema migration)
- [ ] Document `suppressTransaction: true` requirement for all CONCURRENTLY index migrations in the project CONTRIBUTING.md or code comment in `AuditLogConfiguration.cs`

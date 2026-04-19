# Task - task_002_db_backup_strategy

## Requirement Reference

- **User Story**: US_028 — Backup Strategy & Migration Patterns
- **Story Location**: `.propel/context/tasks/EP-006/us_028/us_028.md`
- **Acceptance Criteria**:
  - AC-1: An incremental backup is created every 4 hours using `pg_basebackup` (full) and WAL archiving (incremental); backups are encrypted with AES-256 before upload to geographically redundant cold storage per DR-013. Full backup runs weekly (Sunday 02:00 UTC); incremental WAL segments archive continuously.
  - AC-2: Point-in-time recovery (PITR) to any second within the last 7 days is enabled by continuous WAL archiving to backup storage. Recovery procedure uses `pg_restore` + WAL replay to the target timestamp per DR-013 and NFR-017 (RPO 1 hour, RTO 4 hours).
  - AC-3: After a restore, a validation script runs `pg_dump --schema-only` and checks key foreign-key and index integrity via SQL assertions — exits non-zero on any failure, alerting the ops team.
- **Edge Cases**:
  - If backup storage is full or unavailable, the backup script exits non-zero, triggers a Slack alert, and falls back to a local compressed archive in `/var/backups/propeliq/` (retained 48 hours only — not a permanent substitute per DR-013).
  - Backup encryption key must be the same AES-256 key material used by the `.NET Data Protection API` (US_027/task_001) — the backup decryptor must be able to decrypt column-level ciphertext during test restores. The backup-level encryption (file-level `openssl enc -aes-256-gcm`) is separate from the column-level EF encryption — both layers must be present.
  - The `pg_basebackup` full backup creates a tar snapshot of `PGDATA`; the encrypted archive is uploaded. The WAL archive script encrypts each WAL segment before uploading. Decryption reverses this on restore.
  - Production runs on Windows Server / IIS (no Docker). All scripts must be PowerShell-compatible for Windows task scheduler, with Bash equivalents for Docker Compose development environment.

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
| Database | PostgreSQL | 15.x |
| Scripting (dev) | Bash | — |
| Scripting (prod) | PowerShell | 7.x |
| Backup tool | pg_basebackup + pg_dump | PostgreSQL 15 native |
| Encryption | openssl (AES-256-CBC) | 3.x |
| Storage (prod) | Azure Blob Storage (geographically redundant) | — |
| Storage (dev) | Local filesystem | — |
| Scheduler (prod) | Windows Task Scheduler | — |
| Scheduler (dev) | Docker Compose cron sidecar | — |

> **Storage choice**: Azure Blob Storage with GRS (Geo-Redundant Storage) is the zero-incremental-cost option compatible with IIS/Windows production hosting and NFR-015. Alternatively any S3-compatible storage (Backblaze B2) works with `az storage blob upload` replaced by the provider's CLI.

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

Implement the automated backup, WAL archiving, and restore validation system for PropelIQ's PostgreSQL 15 database. Three artefacts are produced:

1. **`scripts/backup/backup.sh`** (Bash) + **`scripts/backup/backup.ps1`** (PowerShell): scheduled backup runner — full weekly + WAL-archive-based incremental every 4 hours.
2. **`scripts/backup/restore-validate.sh`** (Bash) + `restore-validate.ps1`: PITR restore procedure + integrity validation.
3. **`server/docker-compose.yml` sidecar**: `backup-cron` container for development environment.

### Architecture Summary

```
PostgreSQL 15
  ├── WAL archiving (archive_mode=on, archive_command)
  │     └── Every WAL segment (~16MB) → encrypt → upload → Azure Blob / local
  ├── Full weekly backup (pg_basebackup → tar → encrypt → upload)
  └── Recovery (restore base + replay WAL to target timestamp)
```

---

## Dependent Tasks

- **US_027/task_001** — Data Protection API keys path (`DataProtection:KeysPath`) is needed at runtime by the application. The backup encryption key (separate, file-level AES key) is stored as an environment variable / secret — not the same material as the EF column encryption key.
- No other blocking dependencies.

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `scripts/backup/backup.sh` | Bash backup runner: full (pg_basebackup) or incremental (WAL); openssl encrypt; upload to Azure Blob; local fallback |
| CREATE | `scripts/backup/backup.ps1` | PowerShell equivalent for Windows Server production scheduling via Task Scheduler |
| CREATE | `scripts/backup/restore-validate.sh` | PITR restore procedure + integrity validation assertions |
| CREATE | `scripts/backup/.env.example` | Template for required environment variables — never commit actual values |
| MODIFY | `server/docker-compose.yml` | Add `backup-cron` sidecar service using `postgres:15` image + cron for dev-environment backup testing |
| MODIFY | `server/docker-compose.override.yml` | Expose `backup-cron` volume mount binding `scripts/backup/` → `/scripts/` |

---

## Implementation Plan

### Part A — PostgreSQL WAL Archiving Configuration

WAL archiving is the foundation for PITR (AC-2). It must be enabled in `postgresql.conf`:

```ini
# postgresql.conf additions (apply via ALTER SYSTEM or direct file edit)
wal_level = replica                 # minimum for archiving
archive_mode = on                   # enable WAL archiving
archive_command = '/scripts/archive_wal.sh %p %f'  # %p = path, %f = filename
archive_timeout = 300               # force WAL segment switch every 5 minutes (RPO = 5 min)
```

> **Development**: inject `POSTGRES_INITDB_WALDIR` in docker-compose and mount a custom `postgresql.conf` via the `db` service's `command` override.
> **Production**: apply `ALTER SYSTEM SET archive_mode = 'on';` + `ALTER SYSTEM SET wal_level = 'replica';` and restart PostgreSQL. Document in production ops runbook.

### Part B — `scripts/backup/backup.sh`

```bash
#!/usr/bin/env bash
# backup.sh — PropelIQ PostgreSQL backup runner
# Runs full backup weekly (FULL_BACKUP=1) or incremental WAL archive check
# Environment variables (from .env or Task Scheduler):
#   PGHOST, PGPORT, PGUSER, PGPASSWORD, PGDATABASE
#   BACKUP_ENCRYPTION_PASSPHRASE  — AES-256 file-level encryption key
#   AZURE_STORAGE_ACCOUNT, AZURE_STORAGE_KEY, AZURE_CONTAINER
#   LOCAL_FALLBACK_DIR            — local path for fallback if Azure unavailable
#   FULL_BACKUP                   — set to 1 for full backup mode
set -euo pipefail

TIMESTAMP=$(date -u +"%Y%m%dT%H%M%SZ")
BACKUP_DIR="/tmp/propeliq_backup_${TIMESTAMP}"
mkdir -p "${BACKUP_DIR}"

trap 'rm -rf "${BACKUP_DIR}"' EXIT   # cleanup on exit

if [[ "${FULL_BACKUP:-0}" == "1" ]]; then
    echo "[backup.sh] Starting FULL pg_basebackup — ${TIMESTAMP}"
    pg_basebackup \
        -h "${PGHOST}" -p "${PGPORT}" -U "${PGUSER}" \
        -D "${BACKUP_DIR}/base" \
        -Ft -z --checkpoint=fast --wal-method=stream

    ARCHIVE_NAME="propeliq-full-${TIMESTAMP}.tar.gz"
    tar -czf "${BACKUP_DIR}/${ARCHIVE_NAME}" -C "${BACKUP_DIR}" base/

    # Encrypt with AES-256-CBC — passphrase from environment (never hardcoded)
    openssl enc -aes-256-cbc -pbkdf2 -iter 100000 \
        -pass "env:BACKUP_ENCRYPTION_PASSPHRASE" \
        -in "${BACKUP_DIR}/${ARCHIVE_NAME}" \
        -out "${BACKUP_DIR}/${ARCHIVE_NAME}.enc"

    UPLOAD_FILE="${BACKUP_DIR}/${ARCHIVE_NAME}.enc"
    BLOB_PATH="full/${ARCHIVE_NAME}.enc"
else
    # Incremental: force WAL segment switch and verify archive_command ran
    echo "[backup.sh] Incremental WAL segment switch — ${TIMESTAMP}"
    psql -h "${PGHOST}" -p "${PGPORT}" -U "${PGUSER}" -d "${PGDATABASE}" \
        -c "SELECT pg_switch_wal();" > /dev/null
    echo "[backup.sh] WAL segment switched — archive_command will handle upload"
    exit 0
fi

# Upload to Azure Blob Storage — geographically redundant (GRS tier)
if az storage blob upload \
    --account-name "${AZURE_STORAGE_ACCOUNT}" \
    --account-key "${AZURE_STORAGE_KEY}" \
    --container-name "${AZURE_CONTAINER}" \
    --name "${BLOB_PATH}" \
    --file "${UPLOAD_FILE}" \
    --overwrite false 2>/dev/null; then
    echo "[backup.sh] Uploaded to Azure Blob: ${BLOB_PATH}"
else
    # Fallback: local storage (48-hour retention only — not permanent per DR-013)
    echo "[backup.sh] WARNING: Azure upload failed — falling back to local storage"
    mkdir -p "${LOCAL_FALLBACK_DIR}"
    cp "${UPLOAD_FILE}" "${LOCAL_FALLBACK_DIR}/${ARCHIVE_NAME}.enc"

    # Alert DevOps — non-blocking
    if [[ -n "${SLACK_WEBHOOK_URL:-}" ]]; then
        curl -s -X POST "${SLACK_WEBHOOK_URL}" \
            -H 'Content-type: application/json' \
            --data "{\"text\":\":warning: PropelIQ backup Azure upload FAILED at ${TIMESTAMP}. Local fallback at ${LOCAL_FALLBACK_DIR}.\"}" || true
    fi
    exit 1   # exit non-zero so Task Scheduler / cron marks run as failed
fi
```

### Part C — WAL Archive Command Script (`scripts/backup/archive_wal.sh`)

Referenced by `postgresql.conf archive_command`:

```bash
#!/usr/bin/env bash
# archive_wal.sh — called by PostgreSQL for each completed WAL segment
# $1 = full path to WAL file, $2 = WAL filename
set -euo pipefail

WAL_PATH="$1"
WAL_NAME="$2"
ENCRYPTED="${WAL_PATH}.enc"

openssl enc -aes-256-cbc -pbkdf2 -iter 100000 \
    -pass "env:BACKUP_ENCRYPTION_PASSPHRASE" \
    -in "${WAL_PATH}" \
    -out "${ENCRYPTED}"

az storage blob upload \
    --account-name "${AZURE_STORAGE_ACCOUNT}" \
    --account-key "${AZURE_STORAGE_KEY}" \
    --container-name "${AZURE_CONTAINER}" \
    --name "wal/${WAL_NAME}.enc" \
    --file "${ENCRYPTED}" \
    --overwrite false

rm -f "${ENCRYPTED}"
```

### Part D — `scripts/backup/restore-validate.sh`

Point-in-time recovery procedure + integrity assertions (AC-2 + AC-3):

```bash
#!/usr/bin/env bash
# restore-validate.sh — PITR restore + integrity validation
# Usage: ./restore-validate.sh <TARGET_TIMESTAMP> <RESTORE_DIR>
# TARGET_TIMESTAMP: ISO-8601 UTC e.g. "2026-04-18T14:30:00Z"
# RESTORE_DIR:      directory to restore into (empty)
set -euo pipefail

TARGET_TS="${1:?TARGET_TIMESTAMP required}"
RESTORE_DIR="${2:?RESTORE_DIR required}"

echo "[restore] Starting PITR restore to: ${TARGET_TS}"

# Step 1: Download most recent full backup before TARGET_TS
FULL_BLOB=$(az storage blob list \
    --account-name "${AZURE_STORAGE_ACCOUNT}" \
    --account-key "${AZURE_STORAGE_KEY}" \
    --container-name "${AZURE_CONTAINER}" \
    --prefix "full/" --query "[?properties.lastModified < '${TARGET_TS}']|[-1].name" \
    -o tsv)

[[ -z "${FULL_BLOB}" ]] && { echo "ERROR: No full backup found before ${TARGET_TS}"; exit 1; }

az storage blob download \
    --account-name "${AZURE_STORAGE_ACCOUNT}" \
    --account-key "${AZURE_STORAGE_KEY}" \
    --container-name "${AZURE_CONTAINER}" \
    --name "${FULL_BLOB}" --file "/tmp/base.tar.gz.enc"

# Step 2: Decrypt and extract base backup
openssl enc -d -aes-256-cbc -pbkdf2 -iter 100000 \
    -pass "env:BACKUP_ENCRYPTION_PASSPHRASE" \
    -in "/tmp/base.tar.gz.enc" -out "/tmp/base.tar.gz"
mkdir -p "${RESTORE_DIR}"
tar -xzf "/tmp/base.tar.gz" -C "${RESTORE_DIR}"

# Step 3: Create recovery.conf (PostgreSQL 15 uses recovery_target_time in postgresql.conf)
cat > "${RESTORE_DIR}/postgresql.conf.recovery" << EOF
restore_command = '/scripts/restore_wal.sh %f %p'
recovery_target_time = '${TARGET_TS}'
recovery_target_action = 'promote'
EOF

# Step 4: Start PostgreSQL in recovery mode (caller starts the server with this config)
echo "[restore] Base backup extracted. Start PostgreSQL with:"
echo "  pg_ctl start -D ${RESTORE_DIR} -o '-c config_file=${RESTORE_DIR}/postgresql.conf.recovery'"

# Step 5: Integrity validation (run AFTER PostgreSQL recovers and promotes)
echo "[validate] Running integrity assertions..."
VALIDATE_SQL="
  -- FK constraint check: appointments must reference existing patients
  DO \$\$ BEGIN
    IF EXISTS (
      SELECT 1 FROM appointments a
      LEFT JOIN patient p ON p.id = a.patient_id
      WHERE p.id IS NULL
    ) THEN RAISE EXCEPTION 'FK violation: orphaned appointments found'; END IF;
  END \$\$;

  -- Index existence checks for critical performance indexes
  DO \$\$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'uix_patient_email')
    THEN RAISE EXCEPTION 'Missing index: uix_patient_email'; END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'ix_audit_log_created_at')
    THEN RAISE EXCEPTION 'Missing index: ix_audit_log_created_at'; END IF;
  END \$\$;

  -- Confirm audit_log triggers exist (NFR-007 immutability)
  DO \$\$ BEGIN
    IF NOT EXISTS (
      SELECT 1 FROM information_schema.triggers
      WHERE trigger_name = 'trg_audit_log_no_update'
    ) THEN RAISE EXCEPTION 'Missing trigger: trg_audit_log_no_update'; END IF;
  END \$\$;

  SELECT 'Integrity validation PASSED' AS result;
"

psql -h localhost -p 5433 -U "${PGUSER}" -d "${PGDATABASE}" -c "${VALIDATE_SQL}"
echo "[validate] All integrity assertions passed."
```

### Part E — `scripts/backup/.env.example`

```bash
# PropelIQ Backup Environment Variables — NEVER commit actual values
PGHOST=localhost
PGPORT=5432
PGUSER=propeliq_backup          # migration-only DB user with pg_basebackup privilege
PGPASSWORD=CHANGE_ME
PGDATABASE=propeliq

BACKUP_ENCRYPTION_PASSPHRASE=CHANGE_ME   # min 32 chars; store in Azure Key Vault / Windows Credential Store

AZURE_STORAGE_ACCOUNT=propeliqbackups
AZURE_STORAGE_KEY=CHANGE_ME
AZURE_CONTAINER=db-backups              # GRS tier recommended

LOCAL_FALLBACK_DIR=/var/backups/propeliq

SLACK_WEBHOOK_URL=https://hooks.slack.com/services/CHANGE_ME
```

### Part F — Docker Compose `backup-cron` Sidecar (development)

Add to `docker-compose.yml` `services:` section:

```yaml
  # ── Backup cron sidecar — development only ───────────────────────────────────
  backup-cron:
    image: postgres:15-alpine
    container_name: propeliq_backup_cron
    entrypoint: >
      sh -c "
        apk add --no-cache openssl bash curl &&
        echo '0 */4 * * * /scripts/backup.sh' | crontab - &&
        echo '0 2 * * 0 FULL_BACKUP=1 /scripts/backup.sh' | crontab - &&
        crond -f -l 2
      "
    environment:
      PGHOST: db
      PGPORT: 5432
      PGUSER: ${POSTGRES_USER}
      PGPASSWORD: ${POSTGRES_PASSWORD}
      PGDATABASE: ${POSTGRES_DB}
      BACKUP_ENCRYPTION_PASSPHRASE: ${BACKUP_ENCRYPTION_PASSPHRASE}
      LOCAL_FALLBACK_DIR: /backups
    volumes:
      - ../scripts/backup:/scripts:ro
      - backup-data:/backups
    depends_on:
      db:
        condition: service_healthy
    networks:
      - propeliq-net
    profiles:
      - backup   # only starts when: docker compose --profile backup up
```

Add `backup-data` to the `volumes:` section at the bottom.

### Part G — Windows Task Scheduler Setup (`backup.ps1` outline)

```powershell
# backup.ps1 — Windows Server equivalent of backup.sh
# Register with Task Scheduler:
#   schtasks /create /tn "PropelIQ Full Backup" /tr "pwsh.exe -File C:\propeliq\scripts\backup.ps1 -Full" /sc WEEKLY /d SUN /st 02:00
#   schtasks /create /tn "PropelIQ WAL Rotate"  /tr "pwsh.exe -File C:\propeliq\scripts\backup.ps1"       /sc HOURLY /mo 4

param([switch]$Full)

$Timestamp    = (Get-Date -Format "yyyyMMddTHHmmssZ")
$BackupDir    = "C:\propeliq\backup\$Timestamp"
New-Item -ItemType Directory -Path $BackupDir -Force | Out-Null

$env:PGPASSWORD = $env:PGPASSWORD  # loaded from Windows Credential Manager / environment

if ($Full) {
    Write-Host "[backup.ps1] Starting FULL pg_basebackup — $Timestamp"
    & pg_basebackup -h $env:PGHOST -p $env:PGPORT -U $env:PGUSER `
        -D "$BackupDir\base" -Ft -z --checkpoint=fast --wal-method=stream

    $ArchiveName = "propeliq-full-$Timestamp.tar.gz"
    Compress-Archive -Path "$BackupDir\base\*" -DestinationPath "$BackupDir\$ArchiveName"

    # Encrypt — openssl must be installed (Chocolatey: choco install openssl)
    & openssl enc -aes-256-cbc -pbkdf2 -iter 100000 `
        -pass "env:BACKUP_ENCRYPTION_PASSPHRASE" `
        -in "$BackupDir\$ArchiveName" `
        -out "$BackupDir\$ArchiveName.enc"

    # Upload to Azure Blob (requires Az module: Install-Module Az.Storage)
    $ctx = New-AzStorageContext -StorageAccountName $env:AZURE_STORAGE_ACCOUNT `
                                -StorageAccountKey $env:AZURE_STORAGE_KEY
    Set-AzStorageBlobContent -File "$BackupDir\$ArchiveName.enc" `
        -Container $env:AZURE_CONTAINER `
        -Blob "full/$ArchiveName.enc" `
        -Context $ctx -Force

    Write-Host "[backup.ps1] Full backup uploaded: full/$ArchiveName.enc"
} else {
    # Incremental — force WAL segment switch via psql
    & psql -h $env:PGHOST -p $env:PGPORT -U $env:PGUSER -d $env:PGDATABASE `
        -c "SELECT pg_switch_wal();" | Out-Null
    Write-Host "[backup.ps1] WAL segment switch triggered — archive_command handles upload"
}

Remove-Item -Recurse -Force $BackupDir
```

---

## Current Project State

```
scripts/
  backup/                            ← THIS TASK (create directory + all scripts)
    backup.sh
    backup.ps1
    archive_wal.sh
    restore-validate.sh
    .env.example
server/
  docker-compose.yml                 ← MODIFY — add backup-cron sidecar service + backup-data volume
  docker-compose.override.yml        ← MODIFY — mount scripts/backup volume into backup-cron
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `scripts/backup/backup.sh` | Full + incremental backup runner (Bash); openssl encrypt; Azure Blob upload; local fallback |
| CREATE | `scripts/backup/backup.ps1` | PowerShell equivalent for Windows Task Scheduler |
| CREATE | `scripts/backup/archive_wal.sh` | WAL archive command script (called by `archive_command` in `postgresql.conf`) |
| CREATE | `scripts/backup/restore-validate.sh` | PITR restore procedure + SQL integrity assertions |
| CREATE | `scripts/backup/.env.example` | Environment variable template — no actual secrets |
| MODIFY | `server/docker-compose.yml` | Add `backup-cron` sidecar + `backup-data` volume |
| MODIFY | `server/docker-compose.override.yml` | Mount `../scripts/backup:/scripts:ro` into `backup-cron` |

---

## External References

- [PostgreSQL 15 — WAL archiving configuration](https://www.postgresql.org/docs/15/continuous-archiving.html)
- [PostgreSQL 15 — pg_basebackup](https://www.postgresql.org/docs/15/app-pgbasebackup.html)
- [PostgreSQL 15 — Point-in-Time Recovery (PITR)](https://www.postgresql.org/docs/15/continuous-archiving.html#BACKUP-PITR-RECOVERY)
- [openssl enc — AES-256-CBC with pbkdf2](https://www.openssl.org/docs/man3.0/man1/openssl-enc.html)
- [Azure Blob Storage — az storage blob upload](https://learn.microsoft.com/en-us/cli/azure/storage/blob#az-storage-blob-upload)
- [Azure Blob Storage — GRS (geo-redundant storage)](https://learn.microsoft.com/en-us/azure/storage/common/storage-redundancy#geo-redundant-storage)
- [DR-013 — Daily incremental + full weekly backups; 7-year retention](../.propel/context/docs/design.md)
- [NFR-017 — RPO 1 hour, RTO 4 hours for PITR](../.propel/context/docs/design.md)
- [NFR-003 — AES-256 encryption for backups at rest](../.propel/context/docs/design.md)

---

## Build Commands

```bash
# Make scripts executable
chmod +x scripts/backup/backup.sh scripts/backup/archive_wal.sh scripts/backup/restore-validate.sh

# Run backup sidecar in development
docker compose --profile backup up backup-cron

# Manual full backup test (with .env sourced)
source scripts/backup/.env
FULL_BACKUP=1 ./scripts/backup/backup.sh

# Manual restore test
./scripts/backup/restore-validate.sh "2026-04-18T14:30:00Z" /tmp/propeliq-restore
```

---

## Implementation Validation Strategy

- [ ] Backup test (dev): run `FULL_BACKUP=1 backup.sh` → encrypted `.enc` file written to `LOCAL_FALLBACK_DIR` (Azure unavailable in dev) → exit code 0
- [ ] WAL archive test: trigger `pg_switch_wal()` → `archive_wal.sh` encrypts and uploads WAL segment → exit code 0
- [ ] PITR test: write a known row, record timestamp T, write another row, run `restore-validate.sh T /tmp/restore` → restored database contains the first row but NOT the second
- [ ] Integrity validation test: corrupt a FK reference in the restored test database → `restore-validate.sh` exits non-zero with the FK violation message
- [ ] Fallback test: unset `AZURE_STORAGE_ACCOUNT` → `backup.sh` falls back to local dir, sends Slack alert, exits non-zero
- [ ] Windows test: run `backup.ps1 -Full` on a Windows Server with `openssl` and `Az.Storage` installed → full backup `.enc` uploaded to Azure Blob
- [ ] Security test: verify `BACKUP_ENCRYPTION_PASSPHRASE` never appears in script stdout or log output (passed via `env:` flag, not echoed)

---

## Implementation Checklist

- [ ] Create `scripts/backup/` directory; create `backup.sh` with full backup (pg_basebackup + tar + openssl encrypt + Azure upload) and incremental mode (pg_switch_wal); add `LOCAL_FALLBACK_DIR` fallback with Slack alert on Azure failure; `set -euo pipefail`
- [ ] Create `archive_wal.sh`: encrypt WAL segment with `openssl enc -aes-256-cbc -pbkdf2`; upload to Azure `wal/` prefix; delete local `.enc` temp file; mark executable
- [ ] Create `restore-validate.sh`: download + decrypt base backup; generate `recovery_target_time` snippet; run SQL integrity assertions (FK orphan check, critical index existence, audit trigger existence); print PASSED or exit non-zero
- [ ] Create `backup.ps1`: PowerShell equivalent of `backup.sh`; uses `Set-AzStorageBlobContent`; documented Task Scheduler `schtasks` registration commands in header comments
- [ ] Create `.env.example` with all required variables; add `scripts/backup/.env` to `.gitignore`
- [ ] Modify `docker-compose.yml`: add `backup-cron` sidecar under `profiles: [backup]`; add `backup-data` named volume; add WAL archiving `command` override to `db` service to enable `archive_mode`
- [ ] Document `postgresql.conf` WAL archiving settings in production ops runbook section of README (or inline comment in `docker-compose.yml`) — `wal_level=replica`, `archive_mode=on`, `archive_timeout=300`
- [ ] Add `BACKUP_ENCRYPTION_PASSPHRASE` to `.env.example` at repository root; ensure `scripts/backup/.env` is in `.gitignore`

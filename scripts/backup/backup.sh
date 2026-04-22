#!/usr/bin/env bash
# backup.sh — PropelIQ PostgreSQL backup runner
#
# Modes:
#   FULL_BACKUP=1 ./backup.sh  — full pg_basebackup + encrypt + Azure upload
#   ./backup.sh                — incremental: force WAL segment switch only
#
# Scheduling (production via cron / Windows Task Scheduler — see backup.ps1):
#   Full:        0 2 * * 0  FULL_BACKUP=1 /scripts/backup.sh   (Sunday 02:00 UTC)
#   Incremental: 0 */4 * * *              /scripts/backup.sh   (every 4 hours)
#
# Required environment variables (source from .env or inject via scheduler):
#   PGHOST, PGPORT, PGUSER, PGPASSWORD, PGDATABASE
#   BACKUP_ENCRYPTION_PASSPHRASE  — AES-256 file-level key (min 32 chars)
#   AZURE_STORAGE_ACCOUNT, AZURE_STORAGE_KEY, AZURE_CONTAINER
#   LOCAL_FALLBACK_DIR            — local path for fallback if Azure unavailable
#
# SECURITY: BACKUP_ENCRYPTION_PASSPHRASE is passed via openssl 'env:' flag —
# never echoed to stdout or written to disk unencrypted. DR-013 / NFR-003.
#
# OWASP A02: AES-256-CBC with PBKDF2 (100,000 iterations) provides encryption
# at rest for backup archives. Column-level EF encryption is a separate layer.

set -euo pipefail

# ── Validate required environment variables ────────────────────────────────────
: "${PGHOST:?PGHOST is required}"
: "${PGPORT:?PGPORT is required}"
: "${PGUSER:?PGUSER is required}"
: "${PGPASSWORD:?PGPASSWORD is required}"
: "${PGDATABASE:?PGDATABASE is required}"
: "${BACKUP_ENCRYPTION_PASSPHRASE:?BACKUP_ENCRYPTION_PASSPHRASE is required}"
: "${LOCAL_FALLBACK_DIR:?LOCAL_FALLBACK_DIR is required}"

TIMESTAMP=$(date -u +"%Y%m%dT%H%M%SZ")
BACKUP_DIR="/tmp/propeliq_backup_${TIMESTAMP}"
mkdir -p "${BACKUP_DIR}"

# Always remove the temp dir on exit (success or failure) — no plaintext residue
trap 'rm -rf "${BACKUP_DIR}"' EXIT

# ── Incremental mode: force WAL segment switch ─────────────────────────────────
if [[ "${FULL_BACKUP:-0}" != "1" ]]; then
    echo "[backup.sh] Incremental WAL segment switch — ${TIMESTAMP}"
    PGPASSWORD="${PGPASSWORD}" psql \
        -h "${PGHOST}" -p "${PGPORT}" -U "${PGUSER}" -d "${PGDATABASE}" \
        -c "SELECT pg_switch_wal();" > /dev/null
    echo "[backup.sh] WAL segment switched — archive_command will handle upload"
    exit 0
fi

# ── Full backup mode ───────────────────────────────────────────────────────────
echo "[backup.sh] Starting FULL pg_basebackup — ${TIMESTAMP}"

export PGPASSWORD
pg_basebackup \
    -h "${PGHOST}" -p "${PGPORT}" -U "${PGUSER}" \
    -D "${BACKUP_DIR}/base" \
    -Ft -z --checkpoint=fast --wal-method=stream

ARCHIVE_NAME="propeliq-full-${TIMESTAMP}.tar.gz"
tar -czf "${BACKUP_DIR}/${ARCHIVE_NAME}" -C "${BACKUP_DIR}" base/

# Encrypt with AES-256-CBC + PBKDF2 (100,000 iterations) per NFR-003 / DR-013.
# Passphrase sourced from environment variable — never hardcoded or echoed.
openssl enc -aes-256-cbc -pbkdf2 -iter 100000 \
    -pass "env:BACKUP_ENCRYPTION_PASSPHRASE" \
    -in "${BACKUP_DIR}/${ARCHIVE_NAME}" \
    -out "${BACKUP_DIR}/${ARCHIVE_NAME}.enc"

UPLOAD_FILE="${BACKUP_DIR}/${ARCHIVE_NAME}.enc"
BLOB_PATH="full/${ARCHIVE_NAME}.enc"

# ── Upload to Azure Blob Storage (GRS — geographically redundant per DR-013) ──
if az storage blob upload \
    --account-name "${AZURE_STORAGE_ACCOUNT}" \
    --account-key "${AZURE_STORAGE_KEY}" \
    --container-name "${AZURE_CONTAINER}" \
    --name "${BLOB_PATH}" \
    --file "${UPLOAD_FILE}" \
    --overwrite false 2>/dev/null; then

    echo "[backup.sh] Uploaded to Azure Blob: ${BLOB_PATH}"
else
    # Fallback: local storage (48-hour retention ONLY — not a permanent substitute per DR-013)
    echo "[backup.sh] WARNING: Azure upload failed — falling back to local storage"
    mkdir -p "${LOCAL_FALLBACK_DIR}"
    cp "${UPLOAD_FILE}" "${LOCAL_FALLBACK_DIR}/${ARCHIVE_NAME}.enc"

    # Remove local backups older than 48 hours to prevent disk exhaustion
    find "${LOCAL_FALLBACK_DIR}" -name "*.enc" -mmin +2880 -delete || true

    # Alert DevOps — non-blocking (|| true so alert failure does not mask backup failure)
    if [[ -n "${SLACK_WEBHOOK_URL:-}" ]]; then
        curl -s -X POST "${SLACK_WEBHOOK_URL}" \
            -H 'Content-type: application/json' \
            --data "{\"text\":\":warning: *PropelIQ* backup Azure upload FAILED at ${TIMESTAMP}. Local fallback written to ${LOCAL_FALLBACK_DIR}. Check storage connectivity immediately.\"}" || true
    fi

    exit 1   # non-zero so Task Scheduler / cron marks run as failed (AC-1 edge case)
fi

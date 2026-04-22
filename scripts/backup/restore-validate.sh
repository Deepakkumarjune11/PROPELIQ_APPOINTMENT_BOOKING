#!/usr/bin/env bash
# restore-validate.sh — PropelIQ PITR restore + integrity validation
#
# Usage:
#   ./restore-validate.sh <TARGET_TIMESTAMP> <RESTORE_DIR>
#
#   TARGET_TIMESTAMP : ISO-8601 UTC e.g. "2026-04-18T14:30:00Z"
#   RESTORE_DIR      : empty directory to restore PostgreSQL PGDATA into
#
# Steps:
#   1. Download the most recent full backup created before TARGET_TIMESTAMP
#   2. Decrypt and extract base backup into RESTORE_DIR
#   3. Write postgresql.conf recovery snippet (recovery_target_time)
#   4. Print start command — caller starts PostgreSQL in recovery mode
#   5. After PostgreSQL promotes, run SQL integrity assertions (AC-3)
#
# Required environment variables:
#   PGUSER, PGPASSWORD, PGDATABASE
#   BACKUP_ENCRYPTION_PASSPHRASE
#   AZURE_STORAGE_ACCOUNT, AZURE_STORAGE_KEY, AZURE_CONTAINER
#   RESTORE_PG_PORT (default 5433 — avoids conflict with running production instance)
#
# SECURITY: Passphrase passed via openssl 'env:' — never echoed. NFR-003 / DR-013.
# NFR-017: RPO 1 hour, RTO 4 hours — this script automates the restore portion.

set -euo pipefail

TARGET_TS="${1:?Usage: restore-validate.sh <TARGET_TIMESTAMP> <RESTORE_DIR>}"
RESTORE_DIR="${2:?Usage: restore-validate.sh <TARGET_TIMESTAMP> <RESTORE_DIR>}"

: "${PGUSER:?PGUSER is required}"
: "${PGPASSWORD:?PGPASSWORD is required}"
: "${PGDATABASE:?PGDATABASE is required}"
: "${BACKUP_ENCRYPTION_PASSPHRASE:?BACKUP_ENCRYPTION_PASSPHRASE is required}"
: "${AZURE_STORAGE_ACCOUNT:?AZURE_STORAGE_ACCOUNT is required}"
: "${AZURE_STORAGE_KEY:?AZURE_STORAGE_KEY is required}"
: "${AZURE_CONTAINER:?AZURE_CONTAINER is required}"

RESTORE_PG_PORT="${RESTORE_PG_PORT:-5433}"

echo "[restore] Starting PITR restore to: ${TARGET_TS}"
echo "[restore] Restore directory: ${RESTORE_DIR}"

# ── Step 1: Download most recent full backup created before TARGET_TS ──────────
echo "[restore] Locating most recent full backup before ${TARGET_TS}..."
FULL_BLOB=$(az storage blob list \
    --account-name "${AZURE_STORAGE_ACCOUNT}" \
    --account-key "${AZURE_STORAGE_KEY}" \
    --container-name "${AZURE_CONTAINER}" \
    --prefix "full/" \
    --query "[?properties.lastModified < '${TARGET_TS}'] | [-1].name" \
    -o tsv)

if [[ -z "${FULL_BLOB}" ]]; then
    echo "ERROR: No full backup found before ${TARGET_TS}. Cannot restore."
    exit 1
fi

echo "[restore] Downloading base backup: ${FULL_BLOB}"
az storage blob download \
    --account-name "${AZURE_STORAGE_ACCOUNT}" \
    --account-key "${AZURE_STORAGE_KEY}" \
    --container-name "${AZURE_CONTAINER}" \
    --name "${FULL_BLOB}" \
    --file "/tmp/propeliq_base.tar.gz.enc"

# ── Step 2: Decrypt and extract base backup ────────────────────────────────────
echo "[restore] Decrypting base backup..."
openssl enc -d -aes-256-cbc -pbkdf2 -iter 100000 \
    -pass "env:BACKUP_ENCRYPTION_PASSPHRASE" \
    -in "/tmp/propeliq_base.tar.gz.enc" \
    -out "/tmp/propeliq_base.tar.gz"

rm -f "/tmp/propeliq_base.tar.gz.enc"   # remove ciphertext residue

echo "[restore] Extracting base backup into: ${RESTORE_DIR}"
mkdir -p "${RESTORE_DIR}"
tar -xzf "/tmp/propeliq_base.tar.gz" -C "${RESTORE_DIR}"
rm -f "/tmp/propeliq_base.tar.gz"

# ── Step 3: Write recovery configuration (PostgreSQL 15 style) ─────────────────
# PostgreSQL 15 uses recovery parameters in postgresql.conf (not recovery.conf).
# restore_wal.sh must be present at /scripts/restore_wal.sh and decrypt + fetch WAL.
cat > "${RESTORE_DIR}/postgresql.auto.conf.recovery_append" << EOF
# PropelIQ PITR recovery settings — append to postgresql.auto.conf before starting
restore_command = '/scripts/restore_wal.sh %f %p'
recovery_target_time = '${TARGET_TS}'
recovery_target_action = 'promote'
EOF

# Create recovery signal file — PostgreSQL 15 requires this to enter recovery mode
touch "${RESTORE_DIR}/recovery.signal"

echo ""
echo "[restore] Base backup extracted and recovery config written."
echo "[restore] To start PostgreSQL in recovery mode, run:"
echo ""
echo "  cat '${RESTORE_DIR}/postgresql.auto.conf.recovery_append' >> '${RESTORE_DIR}/postgresql.auto.conf'"
echo "  pg_ctl start -D '${RESTORE_DIR}' -l '${RESTORE_DIR}/pg_restore.log'"
echo ""
echo "[restore] Wait for 'database system is ready' then run this script again with --validate flag,"
echo "         or proceed to Step 5 below once PostgreSQL has promoted."
echo ""

# ── Step 5: Integrity validation (AC-3) ───────────────────────────────────────
# Run after PostgreSQL has promoted the restored cluster.
# Exits non-zero if any assertion fails, alerting the ops team.
if [[ "${3:-}" == "--validate" ]]; then
    echo "[validate] Running post-restore integrity assertions..."

    VALIDATE_SQL="
-- AC-3: FK constraint check — appointments must reference existing patients
DO \$\$ BEGIN
  IF EXISTS (
    SELECT 1 FROM appointments a
    LEFT JOIN patient p ON p.id = a.patient_id
    WHERE p.id IS NULL AND a.patient_id IS NOT NULL
  ) THEN
    RAISE EXCEPTION 'FK violation: orphaned appointments found (patient_id references missing patient)';
  END IF;
END \$\$;

-- AC-3: Critical index existence checks (performance + uniqueness guarantees)
DO \$\$ BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_indexes WHERE indexname = 'uix_patient_email'
  ) THEN
    RAISE EXCEPTION 'Missing index: uix_patient_email — patient uniqueness constraint at risk';
  END IF;
END \$\$;

DO \$\$ BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_indexes WHERE indexname = 'ix_audit_log_occurred_at'
  ) THEN
    RAISE EXCEPTION 'Missing index: ix_audit_log_occurred_at — audit log query performance at risk';
  END IF;
END \$\$;

-- AC-3: Audit log immutability triggers must exist (US_026 NFR-007)
DO \$\$ BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.triggers
    WHERE trigger_name = 'trg_audit_log_no_update'
  ) THEN
    RAISE EXCEPTION 'Missing trigger: trg_audit_log_no_update — audit immutability not enforced';
  END IF;

  IF NOT EXISTS (
    SELECT 1 FROM information_schema.triggers
    WHERE trigger_name = 'trg_audit_log_no_delete'
  ) THEN
    RAISE EXCEPTION 'Missing trigger: trg_audit_log_no_delete — audit immutability not enforced';
  END IF;
END \$\$;

-- AC-3: Confirm __EFMigrationsHistory table is present and non-empty
DO \$\$ BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.tables
    WHERE table_name = '__EFMigrationsHistory'
  ) THEN
    RAISE EXCEPTION 'Missing table: __EFMigrationsHistory — EF migrations not applied';
  END IF;

  IF (SELECT COUNT(*) FROM \"__EFMigrationsHistory\") = 0 THEN
    RAISE EXCEPTION '__EFMigrationsHistory is empty — no migrations recorded';
  END IF;
END \$\$;

SELECT 'Integrity validation PASSED' AS result;
"

    PGPASSWORD="${PGPASSWORD}" psql \
        -h localhost \
        -p "${RESTORE_PG_PORT}" \
        -U "${PGUSER}" \
        -d "${PGDATABASE}" \
        -c "${VALIDATE_SQL}"

    echo "[validate] All integrity assertions passed. Restore is valid."
fi

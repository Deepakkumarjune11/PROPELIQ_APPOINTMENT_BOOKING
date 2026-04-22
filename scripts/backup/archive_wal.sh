#!/usr/bin/env bash
# archive_wal.sh — PostgreSQL WAL archive command script
#
# Called by PostgreSQL for each completed WAL segment via postgresql.conf:
#   archive_command = '/scripts/archive_wal.sh %p %f'
#     %p = full path to WAL file on the PostgreSQL server
#     %f = WAL segment filename only
#
# This script encrypts the WAL segment and uploads it to Azure Blob Storage.
# Returns exit code 0 on success, non-zero on failure (PostgreSQL will retry on failure).
#
# Required environment variables:
#   BACKUP_ENCRYPTION_PASSPHRASE  — AES-256 file-level key (same as backup.sh)
#   AZURE_STORAGE_ACCOUNT, AZURE_STORAGE_KEY, AZURE_CONTAINER
#
# SECURITY: Passphrase passed via openssl 'env:' flag — never echoed. NFR-003 / DR-013.

set -euo pipefail

WAL_PATH="${1:?WAL path (%p) is required}"
WAL_NAME="${2:?WAL filename (%f) is required}"

: "${BACKUP_ENCRYPTION_PASSPHRASE:?BACKUP_ENCRYPTION_PASSPHRASE is required}"
: "${AZURE_STORAGE_ACCOUNT:?AZURE_STORAGE_ACCOUNT is required}"
: "${AZURE_STORAGE_KEY:?AZURE_STORAGE_KEY is required}"
: "${AZURE_CONTAINER:?AZURE_CONTAINER is required}"

ENCRYPTED="/tmp/${WAL_NAME}.enc"

# Encrypt WAL segment with AES-256-CBC + PBKDF2 before upload (NFR-003)
openssl enc -aes-256-cbc -pbkdf2 -iter 100000 \
    -pass "env:BACKUP_ENCRYPTION_PASSPHRASE" \
    -in "${WAL_PATH}" \
    -out "${ENCRYPTED}"

# Upload to Azure Blob Storage under 'wal/' prefix for PITR replay (AC-2 / DR-013)
az storage blob upload \
    --account-name "${AZURE_STORAGE_ACCOUNT}" \
    --account-key "${AZURE_STORAGE_KEY}" \
    --container-name "${AZURE_CONTAINER}" \
    --name "wal/${WAL_NAME}.enc" \
    --file "${ENCRYPTED}" \
    --overwrite false

# Remove temp encrypted file — do not leave plaintext or ciphertext residue in /tmp
rm -f "${ENCRYPTED}"

echo "[archive_wal.sh] Archived WAL segment: wal/${WAL_NAME}.enc"

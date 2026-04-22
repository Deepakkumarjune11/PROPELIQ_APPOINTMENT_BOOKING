#Requires -Version 7.0
<#
.SYNOPSIS
    PropelIQ PostgreSQL backup runner — Windows Server production equivalent of backup.sh.

.DESCRIPTION
    Full weekly backup (pg_basebackup + AES-256 encrypt + Azure Blob upload) or
    incremental WAL segment switch (pg_switch_wal via psql).

    Register with Windows Task Scheduler:
      Full backup (Sunday 02:00 UTC):
        schtasks /create /tn "PropelIQ Full Backup" `
          /tr "pwsh.exe -NonInteractive -File C:\propeliq\scripts\backup\backup.ps1 -Full" `
          /sc WEEKLY /d SUN /st 02:00 /ru SYSTEM

      Incremental WAL rotate (every 4 hours):
        schtasks /create /tn "PropelIQ WAL Rotate" `
          /tr "pwsh.exe -NonInteractive -File C:\propeliq\scripts\backup\backup.ps1" `
          /sc HOURLY /mo 4 /ru SYSTEM

    Prerequisites on Windows Server:
      - OpenSSL 3.x  : choco install openssl
      - Az PowerShell: Install-Module Az.Storage -Scope AllUsers
      - PostgreSQL client tools (psql, pg_basebackup) in PATH

.PARAMETER Full
    When specified, performs a full pg_basebackup. Without this flag, performs
    an incremental WAL segment switch only.

.NOTES
    SECURITY: BACKUP_ENCRYPTION_PASSPHRASE loaded from environment variable only —
    never hardcoded or written to disk unencrypted. NFR-003 / DR-013 / OWASP A02.
#>

param([switch]$Full)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ── Validate required environment variables ────────────────────────────────────
$required = @(
    'PGHOST', 'PGPORT', 'PGUSER', 'PGPASSWORD', 'PGDATABASE',
    'BACKUP_ENCRYPTION_PASSPHRASE', 'LOCAL_FALLBACK_DIR'
)
foreach ($var in $required) {
    if (-not [Environment]::GetEnvironmentVariable($var)) {
        Write-Error "Required environment variable '$var' is not set."
        exit 1
    }
}

$Timestamp  = (Get-Date -Format "yyyyMMddTHHmmssZ" -AsUTC)
$BackupBase = "C:\propeliq\backup"
$BackupDir  = Join-Path $BackupBase $Timestamp
New-Item -ItemType Directory -Path $BackupDir -Force | Out-Null

# Cleanup temp dir on script exit (success or failure)
try {

# ── Incremental: force WAL segment switch ─────────────────────────────────────
if (-not $Full) {
    Write-Host "[backup.ps1] Incremental WAL segment switch — $Timestamp"
    $env:PGPASSWORD = $env:PGPASSWORD
    & psql -h $env:PGHOST -p $env:PGPORT -U $env:PGUSER -d $env:PGDATABASE `
        -c "SELECT pg_switch_wal();" | Out-Null
    Write-Host "[backup.ps1] WAL segment switched — archive_command handles upload"
    exit 0
}

# ── Full backup ────────────────────────────────────────────────────────────────
Write-Host "[backup.ps1] Starting FULL pg_basebackup — $Timestamp"

$env:PGPASSWORD = $env:PGPASSWORD
& pg_basebackup `
    -h $env:PGHOST -p $env:PGPORT -U $env:PGUSER `
    -D "$BackupDir\base" `
    -Ft -z --checkpoint=fast --wal-method=stream

$ArchiveName = "propeliq-full-$Timestamp.tar.gz"
# Compress the base backup directory (pg_basebackup -Ft already creates .tar files)
Compress-Archive -Path "$BackupDir\base\*" -DestinationPath "$BackupDir\$ArchiveName" -Force

# Encrypt with AES-256-CBC + PBKDF2 (100,000 iterations) per NFR-003 / DR-013.
# Passphrase sourced from environment variable — never hardcoded.
$EncryptedFile = "$BackupDir\$ArchiveName.enc"
& openssl enc -aes-256-cbc -pbkdf2 -iter 100000 `
    -pass "env:BACKUP_ENCRYPTION_PASSPHRASE" `
    -in "$BackupDir\$ArchiveName" `
    -out $EncryptedFile

# ── Upload to Azure Blob Storage (GRS — geographically redundant per DR-013) ──
$uploaded = $false
if ($env:AZURE_STORAGE_ACCOUNT -and $env:AZURE_STORAGE_KEY -and $env:AZURE_CONTAINER) {
    try {
        $ctx = New-AzStorageContext `
            -StorageAccountName $env:AZURE_STORAGE_ACCOUNT `
            -StorageAccountKey  $env:AZURE_STORAGE_KEY

        Set-AzStorageBlobContent `
            -File      $EncryptedFile `
            -Container $env:AZURE_CONTAINER `
            -Blob      "full/$ArchiveName.enc" `
            -Context   $ctx `
            -Force | Out-Null

        Write-Host "[backup.ps1] Uploaded to Azure Blob: full/$ArchiveName.enc"
        $uploaded = $true
    } catch {
        Write-Warning "[backup.ps1] Azure upload failed: $_"
    }
}

if (-not $uploaded) {
    # Fallback: local storage (48-hour retention ONLY — not permanent per DR-013)
    Write-Warning "[backup.ps1] Falling back to local storage: $env:LOCAL_FALLBACK_DIR"
    New-Item -ItemType Directory -Path $env:LOCAL_FALLBACK_DIR -Force | Out-Null
    Copy-Item $EncryptedFile -Destination "$env:LOCAL_FALLBACK_DIR\$ArchiveName.enc"

    # Remove local backups older than 48 hours
    Get-ChildItem -Path $env:LOCAL_FALLBACK_DIR -Filter "*.enc" |
        Where-Object { $_.LastWriteTimeUtc -lt (Get-Date).ToUniversalTime().AddHours(-48) } |
        Remove-Item -Force

    # Alert DevOps via Slack — non-blocking
    if ($env:SLACK_WEBHOOK_URL) {
        $body = "{`"text`":`":warning: *PropelIQ* backup Azure upload FAILED at $Timestamp. Local fallback: $env:LOCAL_FALLBACK_DIR`"}"
        try {
            Invoke-RestMethod -Uri $env:SLACK_WEBHOOK_URL -Method Post `
                -ContentType 'application/json' -Body $body | Out-Null
        } catch {
            Write-Warning "[backup.ps1] Slack alert failed: $_"
        }
    }

    exit 1   # non-zero so Task Scheduler marks run as failed (AC-1 edge case)
}

} finally {
    # Always remove temp backup dir — no plaintext archive residue on disk
    if (Test-Path $BackupDir) {
        Remove-Item -Recurse -Force $BackupDir -ErrorAction SilentlyContinue
    }
}

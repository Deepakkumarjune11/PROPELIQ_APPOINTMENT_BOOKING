# restart-server.ps1
# Usage: Right-click in Explorer > "Run with PowerShell"
#        OR in any terminal: .\restart-server.ps1

Write-Host "Stopping any running server processes..." -ForegroundColor Yellow
Get-Process -Name "dotnet","iisexpress" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

$apiPath = Join-Path $PSScriptRoot "server\src\PropelIQ.Api"
Set-Location $apiPath

Write-Host "Building..." -ForegroundColor Yellow
dotnet build --configuration Debug -v quiet
if ($LASTEXITCODE -ne 0) { Write-Host "Build FAILED" -ForegroundColor Red; Read-Host "Press Enter to exit"; exit 1 }

Write-Host "Starting server at http://localhost:8080 ..." -ForegroundColor Green
Write-Host "Press Ctrl+C here to stop the server before rebuilding." -ForegroundColor Cyan
dotnet run --configuration Debug --no-build

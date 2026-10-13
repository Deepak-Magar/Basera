# Starts local dependencies and runs the web app with hot reload.
# Usage (from the repository root): ./scripts/dev.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

Write-Host 'Starting postgres, mailhog and minio...'
docker compose up -d --wait
if ($LASTEXITCODE -ne 0) { throw 'docker compose up failed' }

Write-Host 'Mailhog UI: http://localhost:8025   MinIO console: http://localhost:9001'
Write-Host 'Running Basera.Web with dotnet watch (http://localhost:5080)...'
dotnet watch --project src/Basera.Web run --launch-profile http

# Prepares the local database and runs the web app with hot reload.
# Usage (from the repository root): ./scripts/dev.ps1 [-SkipDocker]
#
# Docker is optional. With docker on PATH (and no -SkipDocker), docker compose starts postgres,
# mailhog and minio. Without it, a locally installed PostgreSQL is used and mailhog/minio are skipped.
param(
    [switch]$SkipDocker
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

if (-not $SkipDocker -and (Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Host 'Starting postgres, mailhog and minio...'
    docker compose up -d --wait
    if ($LASTEXITCODE -ne 0) { throw 'docker compose up failed' }
    Write-Host 'Mailhog UI: http://localhost:8025   MinIO console: http://localhost:9001'
}
else {
    Write-Host 'Docker not used: expecting a local PostgreSQL on the server in ConnectionStrings:Default.'
}

& (Join-Path $PSScriptRoot 'create-db.ps1')
& (Join-Path $PSScriptRoot 'migrate.ps1')

Write-Host 'Running Basera.Web with dotnet watch (http://localhost:5080)...'
dotnet watch --project src/Basera.Web run --launch-profile http

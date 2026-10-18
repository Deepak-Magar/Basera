# Creates the development database if it does not exist yet. Safe to run repeatedly.
# Usage (from the repository root): ./scripts/create-db.ps1
#
# The connection string comes from Basera.Web's user secrets ("ConnectionStrings:Default"), the same
# place the app reads it from in Development. Pass -ConnectionString to use a different server.
param(
    [string]$ConnectionString
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$webProject = Join-Path $root 'src/Basera.Web'

function Get-DevConnectionString {
    $secrets = dotnet user-secrets list --project $webProject 2>$null
    $line = $secrets | Where-Object { $_ -like 'ConnectionStrings:Default = *' } | Select-Object -First 1
    if (-not $line) {
        throw ('No "ConnectionStrings:Default" user secret. Run:' + [Environment]::NewLine +
            '  dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=basera;Username=postgres;Password=<your postgres password>" --project src/Basera.Web')
    }
    return $line.Substring('ConnectionStrings:Default = '.Length)
}

function ConvertFrom-ConnectionString([string]$value) {
    $parts = @{}
    foreach ($pair in $value.Split(';')) {
        $index = $pair.IndexOf('=')
        if ($index -gt 0) { $parts[$pair.Substring(0, $index).Trim().ToLowerInvariant()] = $pair.Substring($index + 1).Trim() }
    }
    return $parts
}

function Find-Psql {
    $command = Get-Command psql -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    $installed = Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\psql.exe' -ErrorAction SilentlyContinue |
        Sort-Object { [int]$_.Directory.Parent.Name } -Descending | Select-Object -First 1
    if ($installed) { return $installed.FullName }
    throw 'psql not found. Add the PostgreSQL bin folder (e.g. C:\Program Files\PostgreSQL\18\bin) to PATH.'
}

if (-not $ConnectionString) { $ConnectionString = Get-DevConnectionString }
$cs = ConvertFrom-ConnectionString $ConnectionString
$database = $cs['database']
if ($database -notmatch '^[A-Za-z_][A-Za-z0-9_]*$') { throw "Unexpected database name '$database'." }
if ($cs['password'] -eq '__SET_IN_USER_SECRETS__') { throw 'The password is still the placeholder; set the user secret first.' }

$psql = Find-Psql
$hostName = if ($cs['host']) { $cs['host'] } else { 'localhost' }
$port = if ($cs['port']) { $cs['port'] } else { '5432' }
$user = if ($cs['username']) { $cs['username'] } else { 'postgres' }

$previousPassword = $env:PGPASSWORD
$env:PGPASSWORD = $cs['password']
try {
    # Connect to the maintenance database "postgres", because the target may not exist yet.
    $exists = & $psql -h $hostName -p $port -U $user -d postgres -w -tAc "SELECT 1 FROM pg_database WHERE datname = '$database'"
    if ($LASTEXITCODE -ne 0) { throw "Could not connect to PostgreSQL at ${hostName}:$port as $user." }
    if ($exists -eq '1') {
        Write-Host "Database '$database' already exists."
    }
    else {
        & $psql -h $hostName -p $port -U $user -d postgres -w -c "CREATE DATABASE `"$database`" ENCODING 'UTF8'"
        if ($LASTEXITCODE -ne 0) { throw "CREATE DATABASE $database failed." }
        Write-Host "Created database '$database'."
    }
}
finally {
    $env:PGPASSWORD = $previousPassword
}

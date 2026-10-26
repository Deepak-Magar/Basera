# Applies EF Core migrations for every module DbContext (ARCHITECTURE section 6: never on app start).
# Usage (from the repository root): ./scripts/migrate.ps1
# The connection string comes from Basera.Web's configuration in Development (user secrets).
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

# One entry per module DbContext, in dependency order. Add a line when a module gets its first migration.
$contexts = @(
    @{ Name = 'DataProtectionKeysDbContext'; Project = 'src/BuildingBlocks/Basera.Shared' }
    @{ Name = 'OutboxDbContext'; Project = 'src/BuildingBlocks/Basera.Shared' }
    @{ Name = 'IdentityDbContext'; Project = 'src/Modules/Identity/Basera.Modules.Identity' }
    @{ Name = 'PropertiesDbContext'; Project = 'src/Modules/Properties/Basera.Modules.Properties' }
)

if ($contexts.Count -eq 0) {
    Write-Host 'No module migrations yet.'
    return
}

$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet tool restore | Out-Null
foreach ($context in $contexts) {
    Write-Host "Migrating $($context.Name)..."
    dotnet ef database update --project $context.Project --startup-project src/Basera.Web --context $context.Name
    if ($LASTEXITCODE -ne 0) { throw "Migrating $($context.Name) failed." }
}

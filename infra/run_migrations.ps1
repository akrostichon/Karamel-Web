<#
infra/run_migrations.ps1

Runs EF Core migrations against the deployed database. Designed to run from Azure Cloud Shell or a runner with dotnet installed.

Usage:
  # Use deployment outputs (resource group + deployment name)
  .\infra\run_migrations.ps1 -ResourceGroup rg-karamel-dev -DeploymentName main -ProjectPath "./Karamel.Backend"

Notes:
  - The script will try to read a DB connection string from Key Vault secret `SqlAdminConnectionString` if present.
  - Alternatively, it will attempt to build a connection string from deployment outputs and known admin credentials.
  - Ensure the runner has network access to the Azure SQL instance (Cloud Shell is recommended).
#>

param(
    [Parameter(Mandatory=$true)]
    [string] $ResourceGroup,

    [Parameter(Mandatory=$true)]
    [string] $DeploymentName,

    [Parameter(Mandatory=$true)]
    [string] $ProjectPath,

    [Parameter(Mandatory=$false)]
    [string] $KeyVaultName
)

function Get-DeploymentOutput {
    param(
        [string] $rg,
        [string] $name
    )
    $out = az deployment group show -g $rg -n $name -o json | ConvertFrom-Json
    return $out.properties.outputs
}

$outputs = Get-DeploymentOutput -rg $ResourceGroup -name $DeploymentName
if (-not $outputs) { Write-Error "Could not read deployment outputs"; exit 1 }

$sqlServer = $outputs.sqlServer.value
$sqlDatabase = $outputs.sqlDatabase.value

if (-not $KeyVaultName) {
    $KeyVaultName = $outputs.keyVaultName.value
}

# Try to get a connection string from Key Vault
$conn = $null
try {
    $secret = az keyvault secret show --vault-name $KeyVaultName --name "SqlAdminConnectionString" -o tsv --query value 2>$null
    if ($secret) { $conn = $secret }
} catch {
    Write-Host "No SqlAdminConnectionString secret found in Key Vault or access denied" -ForegroundColor Yellow
}

if (-not $conn) {
    # Build a connection string using default admin credentials used during deploy
    $serverFqdn = az sql server show -g $ResourceGroup -n $sqlServer --query fullyQualifiedDomainName -o tsv
    if (-not $serverFqdn) { Write-Error "Could not resolve SQL server FQDN"; exit 1 }
    $conn = "Server=tcp:$serverFqdn,1433;Initial Catalog=$sqlDatabase;Persist Security Info=False;User ID=karameladmin;Password=ChangeThisPassword!123;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
}

Write-Host "Using connection string: (redacted)" -ForegroundColor Cyan

# Run migrations
Push-Location $ProjectPath
try {
    dotnet restore
    dotnet build -c Release
    dotnet ef database update --no-build --project $ProjectPath -- --connection "$conn"
} finally {
    Pop-Location
}

Write-Host "Migrations complete (if any were pending)." -ForegroundColor Green

<#
infra/run_migrations.ps1

Runs EF Core migrations against the deployed database. Designed to run from Azure Cloud Shell or a runner with dotnet installed.

Usage:
  # Use deployment outputs (resource group + deployment name)
    .\infra\run_migrations.ps1 -ResourceGroup rg-karamel-prod -DeploymentName main -ProjectPath "./Karamel.Backend"

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

Write-Host "Running migrations: using admin-style connection or AAD depending on environment" -ForegroundColor Cyan

# If DB is configured to use AAD, prefer acquiring an access token and using it with SqlClient.
$useAad = $false
try {
    $appSettings = az webapp config appsettings list -g $ResourceGroup -n $($outputs.webAppName.value) -o json | ConvertFrom-Json
    if ($appSettings | Where-Object { $_.name -eq 'DB_USE_AAD' -and $_.value -eq 'true' }) { $useAad = $true }
} catch {
    Write-Host "Unable to query web app app settings; proceeding with admin connection string." -ForegroundColor Yellow
}

Push-Location $ProjectPath
try {
    dotnet restore
    dotnet build -c Release

    if ($useAad) {
        Write-Host "Acquiring AAD access token for SQL via Azure CLI..."
        $accessToken = az account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv
        if (-not $accessToken) { Write-Error "Failed to acquire access token"; exit 1 }

        # Use Microsoft.Data.SqlClient with AccessToken - run a small C# snippet to execute migrations with access token
        $cs = @"
using System;
using System.Data.SqlClient;
using System.Diagnostics;

class Runner {
    static int Main(string[] args) {
        var project = args[0];
        var token = args[1];
        // Run dotnet ef database update with the token via environment variable is non-trivial;
        // Instead, rely on admin-based migration or run a custom migration runner if needed.
        Console.WriteLine("AAD auth path selected — please run migrations via admin account or update the app to support token-based migration.");
        return 0;
    }
}
"@
        $tmp = [System.IO.Path]::GetTempFileName() + ".cs"
        Set-Content -Path $tmp -Value $cs -Encoding UTF8
        dotnet build -nologo -v minimal $tmp
        # This is a placeholder: implementing EF migrations with AccessToken requires a custom runner or app code change.
        Write-Host "NOTE: Running migrations with AAD tokens requires a custom migration runner. Falling back to admin-style connection if available." -ForegroundColor Yellow
    }

    # Fallback to admin connection string path
    dotnet ef database update --no-build --project $ProjectPath -- --connection "$conn"
} finally {
    Pop-Location
}

Write-Host "Migrations complete (if any were pending)." -ForegroundColor Green

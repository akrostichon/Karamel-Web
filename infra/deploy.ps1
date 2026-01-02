<#
infra/deploy.ps1

Usage:
    .\infra\deploy.ps1 -ResourceGroup rg-karamel-prod -DeploymentName main [-TokenSecret <value>] [-RunMigrations]

This script reads outputs from the ARM deployment, sets the KARAMEL-TOKEN-SECRET in Key Vault,
assigns a system-assigned identity to the web app, grants it access to Key Vault secrets, and
sets app settings and a SQL connection string on the web app.

NOTE: This script assumes you are logged in with `az login` and have contributor access to the target resource group.
#>

param(
    [Parameter(Mandatory=$true)]
    [string] $ResourceGroup,

    [Parameter(Mandatory=$true)]
    [string] $DeploymentName,

    [Parameter(Mandatory=$false)]
    [string] $TokenSecret,

    [Parameter(Mandatory=$false)]
    [switch] $RunMigrations
)

function Get-DeploymentOutput {
    param(
        [string] $rg,
        [string] $name
    )
    $out = az deployment group show -g $rg -n $name -o json | ConvertFrom-Json
    return $out.properties.outputs
}

# Verify Azure CLI login by running 'az account show' and checking the exit code
$null = az account show -o none 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "Please login with 'az login' before running this script." -ForegroundColor Yellow
    exit 1
}

$outputs = Get-DeploymentOutput -rg $ResourceGroup -name $DeploymentName

if (-not $outputs) {
    Write-Error "Could not read deployment outputs. Ensure deployment succeeded and outputs exist."
    exit 1
}

$kvName = $outputs.keyVaultName.value
$webAppName = $outputs.webAppName.value
$sqlServer = $outputs.sqlServer.value
$sqlDatabase = $outputs.sqlDatabase.value

Write-Host "Deployment outputs:" -ForegroundColor Cyan
Write-Host "  KeyVault: $kvName" -ForegroundColor Green
Write-Host "  WebApp:   $webAppName" -ForegroundColor Green
Write-Host "  SQL Server: $sqlServer" -ForegroundColor Green
Write-Host "  SQL DB:     $sqlDatabase" -ForegroundColor Green

if (-not $TokenSecret) {
    $TokenSecret = Read-Host -AsSecureString "Enter KARAMEL-TOKEN-SECRET (min 32 chars)" | ConvertFrom-SecureString
    # ConvertFrom-SecureString produces an encrypted string; to get plain text, read as secure string then convert
    $secure = Read-Host -AsSecureString "Confirm KARAMEL-TOKEN-SECRET (input again to confirm)"
    if ($null -eq $secure) { Write-Error "No secret provided"; exit 1 }
    $TokenSecretPlain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
} else {
    $TokenSecretPlain = $TokenSecret
}

# Assign system-managed identity to the web app
Write-Host "Enabling system-assigned identity on web app $webAppName..."
az webapp identity assign --name $webAppName --resource-group $ResourceGroup | Out-Null

# Retrieve principalId of the web app identity
$principalId = az webapp identity show -n $webAppName -g $ResourceGroup --query principalId -o tsv
if (-not $principalId) { Write-Error "Failed to get web app principalId"; exit 1 }

Write-Host "Granting Key Vault secret get/list to principal: $principalId"
az keyvault set-policy -n $kvName --object-id $principalId --secret-permissions get list | Out-Null

Write-Host "Storing KARAMEL-TOKEN-SECRET in Key Vault: $kvName"
az keyvault secret set --vault-name $kvName --name "KARAMEL-TOKEN-SECRET" --value $TokenSecretPlain | Out-Null

# Compose connection string (admin account used; consider AAD auth for production)
$sqlFqdn = az sql server show -g $ResourceGroup -n $sqlServer --query fullyQualifiedDomainName -o tsv
if (-not $sqlFqdn) { Write-Error "Could not retrieve SQL server FQDN"; exit 1 }
$connectionString = "Server=tcp:$sqlFqdn,1433;Initial Catalog=$sqlDatabase;Persist Security Info=False;User ID=karameladmin;Password=ChangeThisPassword!123;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

Write-Host "Setting app settings/connection string on web app $webAppName"
az webapp config appsettings set -n $webAppName -g $ResourceGroup --settings "KARAMEL-TOKEN-SECRET=@Microsoft.KeyVault(SecretUri=https://$kvName.vault.azure.net/secrets/KARAMEL-TOKEN-SECRET)" | Out-Null
az webapp config connection-string set -n $webAppName -g $ResourceGroup --settings DefaultConnection=$connectionString --connection-string-type SQLAzure | Out-Null

if ($RunMigrations) {
    Write-Host "Running EF Core migrations via Azure Cloud Shell (requires tooling) -- this is a placeholder."
    Write-Host "Please run migrations manually or configure a deployment job that runs 'dotnet ef database update' against the DB." -ForegroundColor Yellow
}

# Create contained database user for the web app managed identity and grant db_owner
Write-Host "Creating contained DB user for web app managed identity (AAD auth)"
try {
    # Acquire an access token for SQL using the web app principal (caller) - not the web app identity
    # Instead, we'll connect using admin credentials and run T-SQL to create the contained user mapped to the web app identity
    $adminConn = $connectionString
    Add-Type -AssemblyName "System.Data"
    $connection = New-Object System.Data.SqlClient.SqlConnection $adminConn
    $connection.Open()
    $createUserSql = "IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = N'\\" + $webAppName + "\\') BEGIN CREATE USER [" + $webAppName + "] FROM EXTERNAL PROVIDER; EXEC sp_addrolemember N'db_owner', N'" + $webAppName + "'; END"
    $cmd = $connection.CreateCommand()
    $cmd.CommandText = $createUserSql
    $cmd.ExecuteNonQuery() | Out-Null
    $connection.Close()
    Write-Host "Contained user created for $webAppName"
} catch {
    Write-Warning "Failed to create contained DB user: $_";
}

# Switch App Service to use AAD-based connection approach
Write-Host "Setting an app setting to indicate AAD SQL auth will be used"
az webapp config appsettings set -n $webAppName -g $ResourceGroup --settings "DB_USE_AAD=true" | Out-Null

Write-Host "Deployment post-setup complete. Web App identity assigned and Key Vault secret configured." -ForegroundColor Green

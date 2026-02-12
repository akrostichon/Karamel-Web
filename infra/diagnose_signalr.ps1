#!/usr/bin/env pwsh
# Diagnose SignalR-related Azure App Service configuration

param(
    [string]$ResourceGroup = "rg-karamel-prod",
    [string]$WebAppName = "rg-karamel-prod-api"
)

Write-Host "🔍 Diagnosing SignalR configuration for $WebAppName..." -ForegroundColor Cyan
Write-Host ""

# Check if logged in to Azure
try {
    $account = az account show 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Not logged in to Azure. Run 'az login' first." -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "❌ Not logged in to Azure. Run 'az login' first." -ForegroundColor Red
    exit 1
}

Write-Host "🌐 WebSocket Configuration:" -ForegroundColor Yellow
$webSocketsEnabled = az webapp config show -g $ResourceGroup -n $WebAppName --query "webSocketsEnabled" -o tsv
Write-Host "  webSocketsEnabled: $webSocketsEnabled"

Write-Host ""
Write-Host "🔄 ARR Affinity (Sticky Sessions):" -ForegroundColor Yellow
$arrAffinity = az webapp show -g $ResourceGroup -n $WebAppName --query "clientAffinityEnabled" -o tsv
Write-Host "  clientAffinityEnabled: $arrAffinity"

Write-Host ""
Write-Host "⚙️  App Settings (SignalR-related):" -ForegroundColor Yellow
$settings = az webapp config appsettings list -g $ResourceGroup -n $WebAppName -o json | ConvertFrom-Json
$wsetting = $settings | Where-Object { $_.name -eq "WEBSITES_ENABLE_WEBSOCKETS" }
if ($wsetting) {
    Write-Host "  WEBSITES_ENABLE_WEBSOCKETS: $($wsetting.value)"
} else {
    Write-Host "  ⚠️  WEBSITES_ENABLE_WEBSOCKETS: NOT SET" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "📦 App Service Plan:" -ForegroundColor Yellow
$plan = az webapp show -g $ResourceGroup -n $WebAppName --query "appServicePlanId" -o tsv
$planDetails = az appservice plan show --ids $plan -o json | ConvertFrom-Json
Write-Host "  SKU: $($planDetails.sku.name) ($($planDetails.sku.tier))"
Write-Host "  Capacity: $($planDetails.sku.capacity) instance(s)"

Write-Host ""
Write-Host "🔧 Recommendations:" -ForegroundColor Green
if ($webSocketsEnabled -ne "true") {
    Write-Host "  ❌ WebSockets NOT enabled - run: az webapp config set -g $ResourceGroup -n $WebAppName --web-sockets-enabled true" -ForegroundColor Red
}
if ($arrAffinity -ne "true") {
    Write-Host "  ❌ ARR Affinity NOT enabled - run: az webapp update -g $ResourceGroup -n $WebAppName --client-affinity-enabled true" -ForegroundColor Red
}
if ($planDetails.sku.tier -eq "Free") {
    Write-Host "  ⚠️  Free tier has limitations (no AlwaysOn, cold starts) - consider upgrading to B1/S1 for production" -ForegroundColor Yellow
}
if ($webSocketsEnabled -eq "true" -and $arrAffinity -eq "true") {
    Write-Host "  ✅ Configuration looks good!" -ForegroundColor Green
}

Write-Host ""

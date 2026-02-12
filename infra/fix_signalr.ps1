#!/usr/bin/env pwsh
# Quick fix for SignalR connection issues in Azure App Service
# This script applies configuration changes without full bicep redeployment

param(
    [string]$ResourceGroup = "rg-karamel-prod",
    [string]$WebAppName = "rg-karamel-prod-api"
)

Write-Host "🔧 Applying SignalR fixes to $WebAppName..." -ForegroundColor Cyan
Write-Host ""

# Check if logged in
try {
    az account show 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Not logged in to Azure. Run 'az login' first." -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "❌ Not logged in to Azure. Run 'az login' first." -ForegroundColor Red
    exit 1
}

Write-Host "1️⃣  Enabling WebSockets..." -ForegroundColor Yellow
az webapp config set -g $ResourceGroup -n $WebAppName --web-sockets-enabled true
if ($LASTEXITCODE -eq 0) {
    Write-Host "   ✅ WebSockets enabled" -ForegroundColor Green
} else {
    Write-Host "   ❌ Failed to enable WebSockets" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "2️⃣  Enabling ARR Affinity (Sticky Sessions)..." -ForegroundColor Yellow
az webapp update -g $ResourceGroup -n $WebAppName --client-affinity-enabled true
if ($LASTEXITCODE -eq 0) {
    Write-Host "   ✅ ARR Affinity enabled" -ForegroundColor Green
} else {
    Write-Host "   ❌ Failed to enable ARR Affinity" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "3️⃣  Setting WEBSITES_ENABLE_WEBSOCKETS app setting..." -ForegroundColor Yellow
az webapp config appsettings set -g $ResourceGroup -n $WebAppName --settings WEBSITES_ENABLE_WEBSOCKETS=true
if ($LASTEXITCODE -eq 0) {
    Write-Host "   ✅ App setting configured" -ForegroundColor Green
} else {
    Write-Host "   ❌ Failed to set app setting" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "✅ Configuration applied successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "⚠️  Note: App Service may need to restart for changes to take full effect." -ForegroundColor Yellow
Write-Host "   Monitor the app for a few minutes. If issues persist, restart manually:" -ForegroundColor Yellow
Write-Host "   az webapp restart -g $ResourceGroup -n $WebAppName" -ForegroundColor Cyan
Write-Host ""

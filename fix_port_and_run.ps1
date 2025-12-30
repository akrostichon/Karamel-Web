<#
fix_port_and_run.ps1

Detects the HTTP port configured in Karamel.Web/Properties/launchSettings.json,
reports any processes listening on that port, and optionally kills them with
`-ForceKill` before running `dotnet run --project Karamel.Web`.

Usage:
  ./fix_port_and_run.ps1            # Show listeners and exit if conflict
  ./fix_port_and_run.ps1 -ForceKill # Kill processes (if possible) and run
#>

param(
    [switch]$ForceKill = $false
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Push-Location -LiteralPath (Split-Path -Path $MyInvocation.MyCommand.Definition -Parent)

function Get-LaunchPort {
    $launchFile = Join-Path -Path (Get-Location) -ChildPath 'Karamel.Web/Properties/launchSettings.json'
    if (-not (Test-Path $launchFile)) {
        return $null
    }

    $json = Get-Content -Raw -Path $launchFile | ConvertFrom-Json
    # Try to find the first applicationUrl that contains http:// and extract the port
    foreach ($profile in $json.profiles.PSObject.Properties) {
        $p = $json.profiles.$($profile.Name)
        if ($p.applicationUrl) {
            $urls = $p.applicationUrl -split ';'
            foreach ($u in $urls) {
                if ($u -match '^http://(?:\[[^\]]+\]|[^:/]+):(?<port>\d+)') {
                    return [int]$Matches['port']
                }
            }
        }
    }
    return $null
}

function Get-ListeningProcesses($port) {
    # Prefer Get-NetTCPConnection (modern and accurate) when available
    if (Get-Command -Name Get-NetTCPConnection -ErrorAction SilentlyContinue) {
        try {
            $conns = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction Stop
            $pids = $conns | Select-Object -ExpandProperty OwningProcess -Unique
            return $pids
        } catch {
            # continue to fallback
        }
    }

    # Fallback to netstat parsing
    try {
        $text = netstat -ano | Select-String ":$port\s"
    } catch {
        $text = netstat -ano | Select-String ":$port"
    }
    if (-not $text) { return @() }
    $pids = @()
    foreach ($m in $text) {
        $line = $m.Line.Trim()
        # Split on whitespace
        $parts = $line -split '\s+' | Where-Object { $_ -ne '' }
        if ($parts.Length -ge 5) {
            $p = $parts[-1]
            if ($p -as [int]) { $pids += [int]$p }
        }
    }
    return $pids | Select-Object -Unique
}

$port = Get-LaunchPort
if (-not $port) {
    Write-Host "Could not determine HTTP port from launchSettings.json; defaulting to 5245" -ForegroundColor Yellow
    $port = 5245
} else {
    Write-Host "Detected launch port: $port" -ForegroundColor Green
}

$pids = @(Get-ListeningProcesses -port $port)
if ($pids.Count -eq 0) {
    Write-Host "No processes found listening on port $port." -ForegroundColor Green
    Write-Host "Starting the app..." -ForegroundColor Cyan
    $procInfo = Start-Process -FilePath 'dotnet' -ArgumentList 'run','--project','Karamel.Web' -NoNewWindow -PassThru -Wait -ErrorAction SilentlyContinue
    if ($procInfo -ne $null) {
        Write-Host "dotnet run exited with code $($procInfo.ExitCode)" -ForegroundColor Green
    } else {
        Write-Host "dotnet run failed to start." -ForegroundColor Red
    }
    Pop-Location
    return
}

Write-Host "Processes listening on port $($port):" -ForegroundColor Yellow
foreach ($p in $pids) {
    try {
        $proc = Get-Process -Id $p -ErrorAction Stop
        Write-Host "  PID=$($p) Name=$($proc.ProcessName) StartTime=$($proc.StartTime)" -ForegroundColor Yellow
    } catch {
        Write-Host "  PID=$($p) (process info not available)" -ForegroundColor Yellow
    }
}

if (-not $ForceKill) {
    Write-Host "\nPort $($port) is in use. To force kill the processes and start the app, re-run with -ForceKill." -ForegroundColor Red
    Pop-Location
    exit 1
}

Write-Host "Force-kill enabled. Attempting to stop processes on port $port..." -ForegroundColor Red
foreach ($p in $pids) {
    try {
        Stop-Process -Id $p -Force -ErrorAction Stop
        Write-Host "Stopped PID $($p)" -ForegroundColor Green
    } catch {
        Write-Host "Failed to stop PID $($p): $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "Starting the app..." -ForegroundColor Cyan
$procInfo = Start-Process -FilePath 'dotnet' -ArgumentList 'run','--project','Karamel.Web' -NoNewWindow -PassThru -Wait -ErrorAction SilentlyContinue
if ($procInfo -ne $null) {
    Write-Host "dotnet run exited with code $($procInfo.ExitCode)" -ForegroundColor Green
} else {
    Write-Host "dotnet run failed to start." -ForegroundColor Red
}

Pop-Location

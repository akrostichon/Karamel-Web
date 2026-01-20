<#
fix_port_and_run.ps1

Detects the HTTP ports configured in both Karamel.Web and Karamel.Backend,
reports any processes listening on those ports, and optionally kills them with
`-ForceKill` before running both backend and frontend.

Usage:
  ./fix_port_and_run.ps1            # Show listeners and exit if conflict
  ./fix_port_and_run.ps1 -ForceKill # Kill processes (if possible) and run both
#>

param(
    [switch]$ForceKill = $false
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Push-Location -LiteralPath (Split-Path -Path $MyInvocation.MyCommand.Definition -Parent)

function Get-LaunchPort {
    param([string]$ProjectPath)
    
    $launchFile = Join-Path -Path (Get-Location) -ChildPath "$ProjectPath/Properties/launchSettings.json"
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

# Check both frontend and backend ports
$frontendPort = Get-LaunchPort -ProjectPath 'Karamel.Web'
if (-not $frontendPort) {
    Write-Host "Could not determine frontend port from launchSettings.json; defaulting to 5245" -ForegroundColor Yellow
    $frontendPort = 5245
} else {
    Write-Host "Detected frontend port: $frontendPort" -ForegroundColor Green
}

$backendPort = Get-LaunchPort -ProjectPath 'Karamel.Backend'
if (-not $backendPort) {
    Write-Host "Could not determine backend port from launchSettings.json; defaulting to 5001" -ForegroundColor Yellow
    $backendPort = 5001
} else {
    Write-Host "Detected backend port: $backendPort" -ForegroundColor Green
}

# Check for conflicts on both ports
$allPids = @()
$frontendPids = @(Get-ListeningProcesses -port $frontendPort)
$backendPids = @(Get-ListeningProcesses -port $backendPort)

if ($frontendPids.Count -gt 0) {
    Write-Host "Processes listening on frontend port $($frontendPort):" -ForegroundColor Yellow
    foreach ($p in $frontendPids) {
        $allPids += $p
        try {
            $proc = Get-Process -Id $p -ErrorAction Stop
            Write-Host "  PID=$($p) Name=$($proc.ProcessName) StartTime=$($proc.StartTime)" -ForegroundColor Yellow
        } catch {
            Write-Host "  PID=$($p) (process info not available)" -ForegroundColor Yellow
        }
    }
}

if ($backendPids.Count -gt 0) {
    Write-Host "Processes listening on backend port $($backendPort):" -ForegroundColor Yellow
    foreach ($p in $backendPids) {
        if ($allPids -notcontains $p) {
            $allPids += $p
        }
        try {
            $proc = Get-Process -Id $p -ErrorAction Stop
            Write-Host "  PID=$($p) Name=$($proc.ProcessName) StartTime=$($proc.StartTime)" -ForegroundColor Yellow
        } catch {
            Write-Host "  PID=$($p) (process info not available)" -ForegroundColor Yellow
        }
    }
}

if ($allPids.Count -eq 0) {
    Write-Host "No port conflicts found." -ForegroundColor Green
} else {
    if (-not $ForceKill) {
        Write-Host "\nPorts are in use. To force kill the processes and start the apps, re-run with -ForceKill." -ForegroundColor Red
        Pop-Location
        exit 1
    }

    Write-Host "Force-kill enabled. Attempting to stop processes..." -ForegroundColor Red
    foreach ($p in $allPids) {
        try {
            Stop-Process -Id $p -Force -ErrorAction Stop
            Write-Host "Stopped PID $($p)" -ForegroundColor Green
        } catch {
            Write-Host "Failed to stop PID $($p): $($_.Exception.Message)" -ForegroundColor Red
        }
    }
}

# Start backend first (in background)
Write-Host "`nStarting backend on port $backendPort..." -ForegroundColor Cyan
$backendProc = Start-Process -FilePath 'dotnet' -ArgumentList 'run','--project','Karamel.Backend' -NoNewWindow -PassThru

# Wait a moment for backend to initialize
Start-Sleep -Seconds 3

# Start frontend (blocking)
Write-Host "Starting frontend on port $frontendPort..." -ForegroundColor Cyan
Write-Host "Frontend will connect to backend at http://localhost:$backendPort" -ForegroundColor Cyan
Write-Host "`nPress Ctrl+C to stop both services." -ForegroundColor Yellow
Write-Host "================================================`n" -ForegroundColor Gray

try {
    $frontendProc = Start-Process -FilePath 'dotnet' -ArgumentList 'run','--project','Karamel.Web' -NoNewWindow -PassThru -Wait
    Write-Host "`nFrontend exited with code $($frontendProc.ExitCode)" -ForegroundColor Green
} finally {
    # Clean up backend when frontend exits
    if ($backendProc -and -not $backendProc.HasExited) {
        Write-Host "Stopping backend..." -ForegroundColor Yellow
        Stop-Process -Id $backendProc.Id -Force -ErrorAction SilentlyContinue
    }
}

Pop-Location

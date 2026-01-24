#!/usr/bin/env pwsh
# Fix the SQLite-generated migration for SQL Server

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Fixing AddLibrarySongsPhase7 Migration" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Delete the incorrect SQLite-based migration
Write-Host "Step 1: Removing incorrect SQLite migration..." -ForegroundColor Yellow
$migrationFiles = @(
    "Karamel.Backend\Migrations\20260112202005_AddLibrarySongsPhase7.cs",
    "Karamel.Backend\Migrations\20260112202005_AddLibrarySongsPhase7.Designer.cs"
)

foreach ($file in $migrationFiles) {
    if (Test-Path $file) {
        Remove-Item $file -Force
        Write-Host "  [OK] Deleted $file" -ForegroundColor Green
    } else {
        Write-Host "  [WARN] File not found: $file" -ForegroundColor DarkYellow
    }
}

Write-Host ""

# Step 2: Set SQL Server provider environment variable
Write-Host "Step 2: Setting DB_PROVIDER=SqlServer..." -ForegroundColor Yellow
$env:DB_PROVIDER = "SqlServer"
$env:ConnectionStrings__DefaultConnection = "Server=(local);Database=Karamel;Trusted_Connection=True;TrustServerCertificate=True;"
Write-Host "  [OK] Environment configured for SQL Server" -ForegroundColor Green
Write-Host ""

# Step 3: Regenerate migration for SQL Server
Write-Host "Step 3: Regenerating migration for SQL Server..." -ForegroundColor Yellow
Write-Host "  Running: dotnet ef migrations add AddLibrarySongsPhase7 --project Karamel.Backend" -ForegroundColor Gray
Write-Host ""

$output = dotnet ef migrations add AddLibrarySongsPhase7 --project Karamel.Backend 2>&1
$exitCode = $LASTEXITCODE

if ($exitCode -eq 0) {
    Write-Host $output
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "[OK] Migration regenerated successfully!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "1. Review the new migration file: Karamel.Backend\Migrations\20260112202005_AddLibrarySongsPhase7.cs"
    Write-Host "2. Commit the fixed migration"
    Write-Host "3. Push and re-run the GitHub Actions workflow"
    Write-Host ""
} else {
    Write-Host $output -ForegroundColor Red
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Red
    Write-Host "[ERROR] Migration generation failed" -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Red
    Write-Host ""
    Write-Host "The migration might already exist or there may be other issues." -ForegroundColor Yellow
    Write-Host "Check the error message above for details." -ForegroundColor Yellow
    Write-Host ""
    exit 1
}

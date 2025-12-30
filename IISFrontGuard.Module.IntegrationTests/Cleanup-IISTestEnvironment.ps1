# Cleanup script for IISFrontGuard Integration Tests
# This script removes the IIS site and SQL Server database created for testing

param(
    [string]$SiteName = "IISFrontGuard_Test",
    [string]$AppPoolName = "IISFrontGuard_TestPool",
    [string]$PhysicalPath = "C:\inetpub\wwwroot\IISFrontGuard_Test",
    [string]$SqlInstance = ".",
    [string]$DatabaseName = "IISFrontGuard_Test",
    [switch]$KeepDatabase
)

# Requires Administrator privileges
#Requires -RunAsAdministrator

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "IISFrontGuard Integration Test Cleanup" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Import IIS module
Import-Module WebAdministration -ErrorAction Stop

# Step 1: Stop and remove website
Write-Host "[1/4] Removing IIS Website..." -ForegroundColor Yellow
if (Get-Website -Name $SiteName -ErrorAction SilentlyContinue) {
    Stop-WebSite -Name $SiteName -ErrorAction SilentlyContinue
    Remove-WebSite -Name $SiteName
    Write-Host "  Removed website: $SiteName" -ForegroundColor Green
} else {
    Write-Host "  Website '$SiteName' does not exist" -ForegroundColor Gray
}

# Step 2: Stop and remove application pool
Write-Host "[2/4] Removing Application Pool..." -ForegroundColor Yellow
if (Test-Path "IIS:\AppPools\$AppPoolName") {
    Stop-WebAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
    Remove-WebAppPool -Name $AppPoolName
    Write-Host "  Removed application pool: $AppPoolName" -ForegroundColor Green
} else {
    Write-Host "  Application pool '$AppPoolName' does not exist" -ForegroundColor Gray
}

# Step 3: Remove physical directory
Write-Host "[3/4] Removing physical directory..." -ForegroundColor Yellow
if (Test-Path $PhysicalPath) {
    try {
        Remove-Item -Path $PhysicalPath -Recurse -Force -ErrorAction Stop
        Write-Host "  Removed directory: $PhysicalPath" -ForegroundColor Green
    } catch {
        Write-Host "  Warning: Could not remove directory completely" -ForegroundColor Yellow
        Write-Host "  Some files may be locked by IIS. Try running again after a moment." -ForegroundColor Yellow
        Write-Host "  Error: $_" -ForegroundColor Gray
    }
} else {
    Write-Host "  Directory '$PhysicalPath' does not exist" -ForegroundColor Gray
}

# Step 4: Drop SQL Server database
Write-Host "[4/4] Removing SQL Server database..." -ForegroundColor Yellow
if ($KeepDatabase) {
    Write-Host "  Skipping database removal (KeepDatabase flag set)" -ForegroundColor Gray
} else {
    try {
        $dropDbQuery = @"
IF DB_ID(N'$DatabaseName') IS NOT NULL
BEGIN
    ALTER DATABASE [$DatabaseName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [$DatabaseName];
    PRINT 'Database dropped';
END
ELSE
BEGIN
    PRINT 'Database does not exist';
END
"@
        
        $result = Invoke-Sqlcmd -ServerInstance $SqlInstance -Query $dropDbQuery -ErrorAction Stop
        Write-Host "  Removed database: $DatabaseName" -ForegroundColor Green
    } catch {
        Write-Host "  Warning: Could not remove database '$DatabaseName'" -ForegroundColor Yellow
        Write-Host "  Error: $_" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "  You can manually drop it using:" -ForegroundColor Gray
        Write-Host "  sqlcmd -S $SqlInstance -Q `"DROP DATABASE [$DatabaseName]`"" -ForegroundColor Cyan
    }
}

# Summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Cleanup Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Removed:" -ForegroundColor White
Write-Host "  IIS Site:        $SiteName" -ForegroundColor Gray
Write-Host "  App Pool:        $AppPoolName" -ForegroundColor Gray
Write-Host "  Physical Path:   $PhysicalPath" -ForegroundColor Gray
if (-not $KeepDatabase) {
    Write-Host "  Database:        $DatabaseName" -ForegroundColor Gray
} else {
    Write-Host "  Database:        $DatabaseName (kept)" -ForegroundColor Yellow
}
Write-Host ""

param($installPath, $toolsPath, $package, $project)

# This script runs when the package is uninstalled

Write-Host "=================================================================================" -ForegroundColor Cyan
Write-Host "   IISFrontGuard Module Uninstallation" -ForegroundColor Yellow
Write-Host "=================================================================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "NOTICE: To avoid accidental loss of application configuration during package updates, this uninstall script will NOT automatically remove entries from your web.config." -ForegroundColor Yellow
Write-Host "If you previously added IISFrontGuard settings via the package installer, please review and remove them manually if desired. A backup of your web.config will be created if it can be located." -ForegroundColor Yellow
Write-Host ""

try {
    # Attempt to locate the project's web.config file and create a backup only.
    $webConfig = $project.ProjectItems | Where-Object { $_.Name -match "web.config" }

    if ($webConfig) {
        $webConfigPath = $webConfig.Properties | Where-Object { $_.Name -eq "LocalPath" } | Select-Object -ExpandProperty Value -ErrorAction SilentlyContinue

        if ($webConfigPath -and (Test-Path $webConfigPath)) {
            Write-Host "Found web.config at: $webConfigPath" -ForegroundColor Gray

            # Create a timestamped backup and do NOT modify the original file to avoid accidental deletions during package updates
            $backupPath = "$webConfigPath.backup_$(Get-Date -Format 'yyyyMMddHHmmss')"
            Copy-Item $webConfigPath $backupPath -ErrorAction Stop
            Write-Host "  ? Backup created: $backupPath" -ForegroundColor Gray

            Write-Host ""
            Write-Host "Manual cleanup instructions (if you want to remove IISFrontGuard settings):" -ForegroundColor Yellow
            Write-Host "  1. Remove the 'FrontGuardModule' entry from <system.webServer><modules> if present." -ForegroundColor White
            Write-Host "  2. Remove the IISFrontGuard connection string from <connectionStrings> (name=\"IISFrontGuard\")." -ForegroundColor White
            Write-Host "  3. Remove the following appSettings keys if present:" -ForegroundColor White
            Write-Host "     - IISFrontGuard.DefaultConnectionStringName" -ForegroundColor Gray
            Write-Host "     - IISFrontGuardEncryptionKey" -ForegroundColor Gray
            Write-Host "     - IISFrontGuard.RateLimitMaxRequestsPerMinute" -ForegroundColor Gray
            Write-Host "     - IISFrontGuard.RateLimitWindowSeconds" -ForegroundColor Gray
            Write-Host "     - TrustedProxyIPs" -ForegroundColor Gray
            Write-Host "     - IISFrontGuard.Webhook.*" -ForegroundColor Gray
            Write-Host "  4. If you removed elements, keep the original backup created above." -ForegroundColor White
            Write-Host ""
        } else {
            Write-Host "web.config path not found on disk; no backup created." -ForegroundColor Gray
        }
    } else {
        Write-Host "web.config not found in project; no changes made." -ForegroundColor Gray
    }

    Write-Host ""
    Write-Host "CLEANUP SUMMARY:" -ForegroundColor Yellow
    Write-Host "  ? No automated modifications performed to web.config to prevent accidental data loss." -ForegroundColor White
    Write-Host "  ? Backup created if web.config was located." -ForegroundColor White
    Write-Host ""
    Write-Host "MANUAL CLEANUP REQUIRED (optional):" -ForegroundColor Yellow
    Write-Host "  1. Database tables created by init.sql still exist:" -ForegroundColor White
    Write-Host "     - Drop tables: SecurityEvents, WafRules, WafConditions" -ForegroundColor Gray
    Write-Host "  2. GeoLite2-Country.mmdb file may remain in content folder" -ForegroundColor White
    Write-Host "  3. Custom WAF rules in database should be removed manually" -ForegroundColor White
    Write-Host ""
    Write-Host "NOTE: A backup of your web.config has been created if the file was found." -ForegroundColor Cyan
    Write-Host ""

} catch {
    Write-Host ""
    Write-Host "ERROR: Failed to create backup or inspect web.config" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    Write-Host "Please manually remove IISFrontGuard configurations from web.config if required." -ForegroundColor Yellow
    Write-Host ""
}

Write-Host "=================================================================================" -ForegroundColor Cyan
Write-Host "   IISFrontGuard Module Uninstallation Complete" -ForegroundColor Green
Write-Host "=================================================================================" -ForegroundColor Cyan
Write-Host ""

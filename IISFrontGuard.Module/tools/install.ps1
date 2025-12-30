param($installPath, $toolsPath, $package, $project)

# This script runs when the package is installed

Write-Host "=================================================================================" -ForegroundColor Cyan
Write-Host "   IISFrontGuard Module Installation Complete!" -ForegroundColor Green
Write-Host "=================================================================================" -ForegroundColor Cyan
Write-Host ""

# Open the Getting Started guide
$gettingStartedPath = Join-Path $installPath "Content\GETTING_STARTED.txt"
if (Test-Path $gettingStartedPath) {
    Write-Host "Opening Getting Started guide..." -ForegroundColor Yellow
    $dte.ItemOperations.OpenFile($gettingStartedPath)
}

Write-Host ""
Write-Host "SECURITY ENHANCEMENTS APPLIED:" -ForegroundColor Green
Write-Host "  ? X-Powered-By header removal configured" -ForegroundColor White
Write-Host "  ? ASP.NET version header disabled" -ForegroundColor White
Write-Host "  ? Server header removal enabled (IIS 10.0+)" -ForegroundColor White
Write-Host "  ? FrontGuardModule registered" -ForegroundColor White
Write-Host ""
Write-Host "NEXT STEPS:" -ForegroundColor Yellow
Write-Host "  1. Execute the SQL script: Scripts\init.sql" -ForegroundColor White
Write-Host "  2. Update connection string in web.config" -ForegroundColor White
Write-Host "  3. Change IISFrontGuardEncryptionKey to a secure value" -ForegroundColor White
Write-Host "  4. Review web.config settings" -ForegroundColor White
Write-Host ""
Write-Host "SECURITY HEADERS:" -ForegroundColor Yellow
Write-Host "  The module automatically adds security headers including:" -ForegroundColor White
Write-Host "  • X-Content-Type-Options: nosniff" -ForegroundColor Gray
Write-Host "  • X-Frame-Options: SAMEORIGIN" -ForegroundColor Gray
Write-Host "  • X-XSS-Protection: 1; mode=block" -ForegroundColor Gray
Write-Host "  • Referrer-Policy: strict-origin-when-cross-origin" -ForegroundColor Gray
Write-Host "  • Content-Security-Policy (for HTML responses)" -ForegroundColor Gray
Write-Host "  • Strict-Transport-Security (for HTTPS only)" -ForegroundColor Gray
Write-Host ""
Write-Host "Please see GETTING_STARTED.txt for detailed instructions." -ForegroundColor Cyan
Write-Host "=================================================================================" -ForegroundColor Cyan

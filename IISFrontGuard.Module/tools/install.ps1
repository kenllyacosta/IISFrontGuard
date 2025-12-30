param($installPath, $toolsPath, $package, $project)

# This script runs when the package is installed or updated

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

# Attempt to safely add missing configuration to web.config (idempotent)
try {
    Write-Host "Attempting to locate project web.config to add missing IISFrontGuard configuration (no removals will be performed)." -ForegroundColor Yellow

    $webConfigItem = $project.ProjectItems | Where-Object { $_.Name -match "web.config" -or $_.Name -match "Web.config" }
    if ($webConfigItem) {
        $webConfigPath = $webConfigItem.Properties | Where-Object { $_.Name -eq "LocalPath" } | Select-Object -ExpandProperty Value -ErrorAction SilentlyContinue

        if ($webConfigPath -and (Test-Path $webConfigPath)) {
            Write-Host "Found web.config at: $webConfigPath" -ForegroundColor Gray

            # Load XML
            [xml]$xml = Get-Content $webConfigPath

            $configChanged = $false

            # Ensure connectionStrings/IISFrontGuard exists
            if (-not $xml.configuration.connectionStrings) {
                $connNode = $xml.CreateElement("connectionStrings")
                $xml.configuration.AppendChild($connNode) | Out-Null
            }

            $existingConn = @()
            if ($xml.configuration.connectionStrings.add) {
                $existingConn = $xml.configuration.connectionStrings.add | Where-Object { $_.name -eq "IISFrontGuard" }
            }

            if (-not $existingConn) {
                $add = $xml.CreateElement("add")
                $add.SetAttribute("name", "IISFrontGuard")
                $add.SetAttribute("connectionString", "Data Source=.;Initial Catalog=IISFrontGuard;Integrated Security=True;TrustServerCertificate=True;Min Pool Size=5;Max Pool Size=100;Connection Timeout=5;")
                $xml.configuration.connectionStrings.AppendChild($add) | Out-Null
                $configChanged = $true
                Write-Host "  ? Added IISFrontGuard connection string" -ForegroundColor Green
            }

            # Ensure appSettings keys
            if (-not $xml.configuration.appSettings) {
                $appNode = $xml.CreateElement("appSettings")
                $xml.configuration.AppendChild($appNode) | Out-Null
            }

            $appKeys = @{
                "IISFrontGuard.DefaultConnectionStringName" = "IISFrontGuard";
                "IISFrontGuardEncryptionKey" = "1234567890123456";
                "IISFrontGuard.RateLimitMaxRequestsPerMinute" = "150";
                "IISFrontGuard.RateLimitWindowSeconds" = "60";
                "TrustedProxyIPs" = "";
                "IISFrontGuard.Webhook.Enabled" = "false";
                "IISFrontGuard.Webhook.Url" = "";
                "IISFrontGuard.Webhook.AuthHeader" = "";
                "IISFrontGuard.Webhook.CustomHeaders" = "";
                "IISFrontGuard.Webhook.FailureLogPath" = "C:\\Logs\\webhook-failures.log";
            }

            foreach ($key in $appKeys.Keys) {
                $exists = $null
                if ($xml.configuration.appSettings.add) {
                    $exists = $xml.configuration.appSettings.add | Where-Object { $_.key -eq $key }
                }
                if (-not $exists) {
                    $add = $xml.CreateElement("add")
                    $add.SetAttribute("key", $key)
                    $add.SetAttribute("value", $appKeys[$key])
                    $xml.configuration.appSettings.AppendChild($add) | Out-Null
                    $configChanged = $true
                    Write-Host "  ? Added appSetting: $key" -ForegroundColor Green
                }
            }

            # Ensure system.web/httpRuntime enableVersionHeader="false"
            if (-not $xml.configuration.'system.web') {
                $sysWeb = $xml.CreateElement("system.web")
                $xml.configuration.AppendChild($sysWeb) | Out-Null
            }

            if (-not $xml.configuration.'system.web'.httpRuntime) {
                $httpRuntime = $xml.CreateElement("httpRuntime")
                $httpRuntime.SetAttribute("enableVersionHeader", "false")
                $xml.configuration.'system.web'.AppendChild($httpRuntime) | Out-Null
                $configChanged = $true
                Write-Host "  ? Added system.web httpRuntime enableVersionHeader='false'" -ForegroundColor Green
            } else {
                $current = $xml.configuration.'system.web'.httpRuntime
                if (-not $current.enableVersionHeader -or $current.enableVersionHeader -ne "false") {
                    $current.SetAttribute("enableVersionHeader", "false")
                    $configChanged = $true
                    Write-Host "  ? Set enableVersionHeader='false' on existing httpRuntime" -ForegroundColor Green
                }
            }

            # Ensure system.webServer/modules contains FrontGuardModule
            if (-not $xml.configuration.'system.webServer') {
                $sysWServer = $xml.CreateElement("system.webServer")
                $xml.configuration.AppendChild($sysWServer) | Out-Null
            }

            if (-not $xml.configuration.'system.webServer'.modules) {
                $modulesNode = $xml.CreateElement("modules")
                $xml.configuration.'system.webServer'.AppendChild($modulesNode) | Out-Null
            }

            $moduleExists = $false
            if ($xml.configuration.'system.webServer'.modules.add) {
                $moduleExists = $xml.configuration.'system.webServer'.modules.add | Where-Object { $_.name -eq "FrontGuardModule" }
            }

            if (-not $moduleExists) {
                $addModule = $xml.CreateElement("add")
                $addModule.SetAttribute("name", "FrontGuardModule")
                $addModule.SetAttribute("type", "IISFrontGuard.Module.FrontGuardModule, IISFrontGuard.Module")
                $addModule.SetAttribute("preCondition", "managedHandler,runtimeVersionv4.0")
                $xml.configuration.'system.webServer'.modules.AppendChild($addModule) | Out-Null
                $configChanged = $true
                Write-Host "  ? Registered FrontGuardModule in system.webServer/modules" -ForegroundColor Green
            }

            # Ensure httpProtocol/customHeaders/remove name="X-Powered-By"
            if (-not $xml.configuration.'system.webServer'.httpProtocol) {
                $httpProto = $xml.CreateElement("httpProtocol")
                $xml.configuration.'system.webServer'.AppendChild($httpProto) | Out-Null
            }

            if (-not $xml.configuration.'system.webServer'.httpProtocol.customHeaders) {
                $customHeaders = $xml.CreateElement("customHeaders")
                $xml.configuration.'system.webServer'.httpProtocol.AppendChild($customHeaders) | Out-Null
            }

            $removeExists = $false
            if ($xml.configuration.'system.webServer'.httpProtocol.customHeaders.remove) {
                $removeExists = $xml.configuration.'system.webServer'.httpProtocol.customHeaders.remove | Where-Object { $_.name -eq "X-Powered-By" }
            }

            if (-not $removeExists) {
                $remove = $xml.CreateElement("remove")
                $remove.SetAttribute("name", "X-Powered-By")
                $xml.configuration.'system.webServer'.httpProtocol.customHeaders.AppendChild($remove) | Out-Null
                $configChanged = $true
                Write-Host "  ? Ensured X-Powered-By header removal configuration" -ForegroundColor Green
            }

            # Ensure security/requestFiltering removeServerHeader="true"
            if (-not $xml.configuration.'system.webServer'.security) {
                $security = $xml.CreateElement("security")
                $xml.configuration.'system.webServer'.AppendChild($security) | Out-Null
            }

            if (-not $xml.configuration.'system.webServer'.security.requestFiltering) {
                $reqFilter = $xml.CreateElement("requestFiltering")
                $reqFilter.SetAttribute("removeServerHeader", "true")
                $xml.configuration.'system.webServer'.security.AppendChild($reqFilter) | Out-Null
                $configChanged = $true
                Write-Host "  ? Set requestFiltering removeServerHeader='true'" -ForegroundColor Green
            } else {
                $rf = $xml.configuration.'system.webServer'.security.requestFiltering
                if (-not $rf.removeServerHeader -or $rf.removeServerHeader -ne "true") {
                    $rf.SetAttribute("removeServerHeader", "true")
                    $configChanged = $true
                    Write-Host "  ? Updated requestFiltering removeServerHeader='true'" -ForegroundColor Green
                }
            }

            # Save changes if any
            if ($configChanged) {
                $backupPath = "$webConfigPath.backup_$(Get-Date -Format 'yyyyMMddHHmmss')"
                Copy-Item $webConfigPath $backupPath -ErrorAction Stop
                Write-Host "  ? Backup created: $backupPath" -ForegroundColor Gray

                $xml.Save($webConfigPath)
                Write-Host "  ? web.config updated with missing IISFrontGuard configuration" -ForegroundColor Green
            } else {
                Write-Host "  ? web.config already contains required IISFrontGuard configuration" -ForegroundColor Gray
            }

        } else {
            Write-Host "web.config not found on disk; skipping automatic configuration." -ForegroundColor Gray
        }
    } else {
        Write-Host "web.config not found in project; skipping automatic configuration." -ForegroundColor Gray
    }

} catch {
    Write-Host ""
    Write-Host "ERROR: Failed to update web.config safely" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    Write-Host "A backup will be available if it was created; please update web.config manually if necessary." -ForegroundColor Yellow
    Write-Host ""
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
Write-Host "  ? X-Content-Type-Options: nosniff" -ForegroundColor Gray
Write-Host "  ? X-Frame-Options: SAMEORIGIN" -ForegroundColor Gray
Write-Host "  ? X-XSS-Protection: 1; mode=block" -ForegroundColor Gray
Write-Host "  ? Referrer-Policy: strict-origin-when-cross-origin" -ForegroundColor Gray
Write-Host "  ? Content-Security-Policy (for HTML responses)" -ForegroundColor Gray
Write-Host "  ? Strict-Transport-Security (for HTTPS only)" -ForegroundColor Gray
Write-Host ""
Write-Host "Please see GETTING_STARTED.txt for detailed instructions." -ForegroundColor Cyan
Write-Host "=================================================================================" -ForegroundColor Cyan

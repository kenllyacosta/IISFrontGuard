param($installPath, $toolsPath, $package, $project)

# This script runs when the package is uninstalled

Write-Host "=================================================================================" -ForegroundColor Cyan
Write-Host "   IISFrontGuard Module Uninstallation" -ForegroundColor Yellow
Write-Host "=================================================================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Removing IISFrontGuard configurations from web.config..." -ForegroundColor Yellow
Write-Host ""

try {
    # Get the project's web.config file
    $webConfig = $project.ProjectItems | Where-Object { $_.Name -eq "Web.config" }
    
    if ($webConfig) {
        $webConfigPath = $webConfig.Properties | Where-Object { $_.Name -eq "LocalPath" } | Select-Object -ExpandProperty Value
        
        if (Test-Path $webConfigPath) {
            Write-Host "Found web.config at: $webConfigPath" -ForegroundColor Gray
            
            # Load the web.config as XML
            [xml]$xml = Get-Content $webConfigPath
            
            $configChanged = $false
            
            # Remove FrontGuardModule from system.webServer/modules
            Write-Host "  ? Removing FrontGuardModule registration..." -ForegroundColor White
            $modulesNode = $xml.configuration.'system.webServer'.modules
            if ($modulesNode) {
                $moduleToRemove = $modulesNode.add | Where-Object { $_.name -eq "FrontGuardModule" }
                if ($moduleToRemove) {
                    $modulesNode.RemoveChild($moduleToRemove) | Out-Null
                    $configChanged = $true
                    Write-Host "    ? FrontGuardModule removed" -ForegroundColor Green
                }
            }
            
            # Remove X-Powered-By header removal configuration
            Write-Host "  ? Removing X-Powered-By header configuration..." -ForegroundColor White
            $httpProtocolNode = $xml.configuration.'system.webServer'.httpProtocol
            if ($httpProtocolNode) {
                $customHeadersNode = $httpProtocolNode.customHeaders
                if ($customHeadersNode) {
                    $headerToRemove = $customHeadersNode.remove | Where-Object { $_.name -eq "X-Powered-By" }
                    if ($headerToRemove) {
                        $customHeadersNode.RemoveChild($headerToRemove) | Out-Null
                        $configChanged = $true
                        Write-Host "    ? X-Powered-By configuration removed" -ForegroundColor Green
                    }
                    
                    # Remove empty customHeaders node
                    if (-not $customHeadersNode.HasChildNodes) {
                        $httpProtocolNode.RemoveChild($customHeadersNode) | Out-Null
                    }
                }
                
                # Remove empty httpProtocol node
                if (-not $httpProtocolNode.HasChildNodes) {
                    $xml.configuration.'system.webServer'.RemoveChild($httpProtocolNode) | Out-Null
                }
            }
            
            # Remove Server header configuration (requestFiltering)
            Write-Host "  ? Removing Server header configuration..." -ForegroundColor White
            $securityNode = $xml.configuration.'system.webServer'.security
            if ($securityNode) {
                $requestFilteringNode = $securityNode.requestFiltering
                if ($requestFilteringNode -and $requestFilteringNode.removeServerHeader -eq "true") {
                    $requestFilteringNode.RemoveAttribute("removeServerHeader")
                    $configChanged = $true
                    Write-Host "    ? Server header configuration removed" -ForegroundColor Green
                    
                    # Remove empty requestFiltering node
                    if (-not $requestFilteringNode.HasAttributes -and -not $requestFilteringNode.HasChildNodes) {
                        $securityNode.RemoveChild($requestFilteringNode) | Out-Null
                    }
                }
                
                # Remove empty security node
                if (-not $securityNode.HasChildNodes) {
                    $xml.configuration.'system.webServer'.RemoveChild($securityNode) | Out-Null
                }
            }
            
            # Remove enableVersionHeader from system.web/httpRuntime
            Write-Host "  ? Removing ASP.NET version header configuration..." -ForegroundColor White
            $httpRuntimeNode = $xml.configuration.'system.web'.httpRuntime
            if ($httpRuntimeNode -and $httpRuntimeNode.enableVersionHeader -eq "false") {
                $httpRuntimeNode.RemoveAttribute("enableVersionHeader")
                $configChanged = $true
                Write-Host "    ? ASP.NET version header configuration removed" -ForegroundColor Green
            }
            
            # Remove IISFrontGuard connection string
            Write-Host "  ? Removing IISFrontGuard connection string..." -ForegroundColor White
            $connectionStringsNode = $xml.configuration.connectionStrings
            if ($connectionStringsNode) {
                $connStringToRemove = $connectionStringsNode.add | Where-Object { $_.name -eq "IISFrontGuard" }
                if ($connStringToRemove) {
                    $connectionStringsNode.RemoveChild($connStringToRemove) | Out-Null
                    $configChanged = $true
                    Write-Host "    ? IISFrontGuard connection string removed" -ForegroundColor Green
                }
                
                # Remove empty connectionStrings node
                if (-not $connectionStringsNode.HasChildNodes) {
                    $xml.configuration.RemoveChild($connectionStringsNode) | Out-Null
                }
            }
            
            # Remove IISFrontGuard app settings
            Write-Host "  ? Removing IISFrontGuard app settings..." -ForegroundColor White
            $appSettingsNode = $xml.configuration.appSettings
            if ($appSettingsNode) {
                $settingsToRemove = @(
                    "IISFrontGuard.DefaultConnectionStringName",
                    "IISFrontGuardEncryptionKey",
                    "IISFrontGuard.RateLimitMaxRequestsPerMinute",
                    "IISFrontGuard.RateLimitWindowSeconds",
                    "TrustedProxyIPs",
                    "IISFrontGuard.Webhook.Enabled",
                    "IISFrontGuard.Webhook.Url",
                    "IISFrontGuard.Webhook.AuthHeader",
                    "IISFrontGuard.Webhook.CustomHeaders",
                    "IISFrontGuard.Webhook.FailureLogPath"
                )
                
                $removedCount = 0
                foreach ($settingKey in $settingsToRemove) {
                    $settingToRemove = $appSettingsNode.add | Where-Object { $_.key -eq $settingKey }
                    if ($settingToRemove) {
                        $appSettingsNode.RemoveChild($settingToRemove) | Out-Null
                        $removedCount++
                    }
                }
                
                if ($removedCount -gt 0) {
                    $configChanged = $true
                    Write-Host "    ? Removed $removedCount app settings" -ForegroundColor Green
                }
                
                # Remove empty appSettings node
                if (-not $appSettingsNode.HasChildNodes) {
                    $xml.configuration.RemoveChild($appSettingsNode) | Out-Null
                }
            }
            
            # Save the modified web.config if changes were made
            if ($configChanged) {
                # Create a backup before saving
                $backupPath = "$webConfigPath.backup_$(Get-Date -Format 'yyyyMMddHHmmss')"
                Copy-Item $webConfigPath $backupPath
                Write-Host ""
                Write-Host "  ? Backup created: $backupPath" -ForegroundColor Gray
                
                # Save the changes
                $xml.Save($webConfigPath)
                Write-Host "  ? web.config updated successfully" -ForegroundColor Green
            } else {
                Write-Host "  ? No IISFrontGuard configurations found in web.config" -ForegroundColor Gray
            }
        }
    } else {
        Write-Host "  ? web.config not found in project" -ForegroundColor Gray
    }
    
    Write-Host ""
    Write-Host "CLEANUP SUMMARY:" -ForegroundColor Yellow
    Write-Host "  ? Module registration removed" -ForegroundColor White
    Write-Host "  ? Security header configurations removed" -ForegroundColor White
    Write-Host "  ? Connection string removed" -ForegroundColor White
    Write-Host "  ? App settings removed" -ForegroundColor White
    Write-Host ""
    Write-Host "MANUAL CLEANUP REQUIRED:" -ForegroundColor Yellow
    Write-Host "  1. Database tables created by init.sql still exist" -ForegroundColor White
    Write-Host "     ? Drop tables: SecurityEvents, WafRules, WafConditions" -ForegroundColor Gray
    Write-Host "  2. GeoLite2-Country.mmdb file may remain in content folder" -ForegroundColor White
    Write-Host "  3. Custom WAF rules in database should be removed manually" -ForegroundColor White
    Write-Host ""
    Write-Host "NOTE: A backup of your web.config has been created" -ForegroundColor Cyan
    Write-Host ""
    
} catch {
    Write-Host ""
    Write-Host "ERROR: Failed to modify web.config" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    Write-Host "Please manually remove IISFrontGuard configurations from web.config" -ForegroundColor Yellow
    Write-Host ""
}

Write-Host "=================================================================================" -ForegroundColor Cyan
Write-Host "   IISFrontGuard Module Uninstallation Complete" -ForegroundColor Green
Write-Host "=================================================================================" -ForegroundColor Cyan
Write-Host ""

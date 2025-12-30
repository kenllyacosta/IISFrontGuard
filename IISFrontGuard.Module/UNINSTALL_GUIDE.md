# IISFrontGuard Module - Uninstallation Guide

## Overview

This guide covers the complete uninstallation process for the IISFrontGuard Module, including automatic cleanup and manual steps required to fully remove all traces of the module from your application.

## Automatic Uninstallation

### Using NuGet Package Manager

```powershell
Uninstall-Package IISFrontGuard.Module
```

### Using .NET CLI

```bash
dotnet remove package IISFrontGuard.Module
```

### Using Package Manager Console

```powershell
PM> Uninstall-Package IISFrontGuard.Module
```

## What Gets Removed Automatically

When you uninstall the IISFrontGuard.Module package, the `uninstall.ps1` script automatically removes the following configurations from your `web.config`:

### 1. Module Registration
```xml
<!-- REMOVED -->
<system.webServer>
  <modules>
    <add name="FrontGuardModule" ... />
  </modules>
</system.webServer>
```

### 2. Security Header Configurations
```xml
<!-- REMOVED -->
<system.webServer>
  <httpProtocol>
    <customHeaders>
      <remove name="X-Powered-By" />
    </customHeaders>
  </httpProtocol>
  
  <security>
    <requestFiltering removeServerHeader="true" />
  </security>
</system.webServer>
```

### 3. ASP.NET Version Header Configuration
```xml
<!-- REMOVED -->
<system.web>
  <httpRuntime enableVersionHeader="false" />
</system.web>
```

### 4. Connection String
```xml
<!-- REMOVED -->
<connectionStrings>
  <add name="IISFrontGuard" ... />
</connectionStrings>
```

### 5. Application Settings
```xml
<!-- REMOVED -->
<appSettings>
  <add key="IISFrontGuard.DefaultConnectionStringName" ... />
  <add key="IISFrontGuardEncryptionKey" ... />
  <add key="IISFrontGuard.RateLimitMaxRequestsPerMinute" ... />
  <add key="IISFrontGuard.RateLimitWindowSeconds" ... />
  <add key="TrustedProxyIPs" ... />
  <add key="IISFrontGuard.Webhook.Enabled" ... />
  <add key="IISFrontGuard.Webhook.Url" ... />
  <add key="IISFrontGuard.Webhook.AuthHeader" ... />
  <add key="IISFrontGuard.Webhook.CustomHeaders" ... />
  <add key="IISFrontGuard.Webhook.FailureLogPath" ... />
</appSettings>
```

### 6. Web.config Backup
- A backup of your `web.config` is automatically created before modifications
- Backup filename format: `web.config.backup_yyyyMMddHHmmss`
- Backup location: Same directory as your `web.config`

## Manual Cleanup Required

The following items require manual cleanup as they were created outside of the NuGet package installation:

### 1. Database Objects

The SQL database tables and objects created by `init.sql` must be removed manually.

#### Option A: Drop Individual Tables
```sql
USE IISFrontGuard;
GO

-- Drop tables in correct order (respecting foreign keys)
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'WafConditions')
    DROP TABLE WafConditions;
GO

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'WafRules')
    DROP TABLE WafRules;
GO

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'SecurityEvents')
    DROP TABLE SecurityEvents;
GO

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'RequestLogs')
    DROP TABLE RequestLogs;
GO

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'ResponseLogs')
    DROP TABLE ResponseLogs;
GO
```

#### Option B: Drop Entire Database
```sql
USE master;
GO

IF EXISTS (SELECT name FROM sys.databases WHERE name = 'IISFrontGuard')
BEGIN
    ALTER DATABASE IISFrontGuard SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE IISFrontGuard;
END
GO
```

### 2. Content Files

The following files may remain in your project after uninstallation:

#### Files to Remove Manually:
- `GeoLite2-Country.mmdb` - GeoIP database file
- `UpdateGeoDb.bat` - GeoIP update script
- `Scripts/init.sql` - Database initialization script
- `GETTING_STARTED.txt` - Installation guide
- `HEADER_SECURITY.md` - Security header documentation
- `INSTALLATION_CHANGES.md` - Installation changes documentation

#### Remove via Visual Studio:
1. Right-click each file in Solution Explorer
2. Select "Delete"
3. Confirm deletion

#### Remove via File System:
```powershell
# Navigate to your project directory
cd "C:\Path\To\Your\Project"

# Remove files
Remove-Item "GeoLite2-Country.mmdb" -ErrorAction SilentlyContinue
Remove-Item "UpdateGeoDb.bat" -ErrorAction SilentlyContinue
Remove-Item "Scripts\init.sql" -ErrorAction SilentlyContinue
Remove-Item "GETTING_STARTED.txt" -ErrorAction SilentlyContinue
Remove-Item "HEADER_SECURITY.md" -ErrorAction SilentlyContinue
Remove-Item "INSTALLATION_CHANGES.md" -ErrorAction SilentlyContinue
```

### 3. Log Files

Remove any log files created by the module:

```powershell
# Remove webhook failure logs (if configured)
Remove-Item "C:\Logs\webhook-failures.log" -ErrorAction SilentlyContinue

# Remove any custom log files you configured
```

### 4. IIS Cache

If the module is cached by IIS, restart IIS to clear it:

```powershell
# Restart IIS
iisreset

# Or restart specific application pool
Restart-WebAppPool -Name "YourAppPoolName"
```

## Verification Steps

### 1. Verify Web.config

Open your `web.config` and verify that all IISFrontGuard configurations have been removed:

```powershell
# Search for any remaining IISFrontGuard references
Select-String -Path "web.config" -Pattern "IISFrontGuard|FrontGuardModule"
```

Expected output: No matches found

### 2. Verify Project References

Check that the IISFrontGuard.Module assembly is no longer referenced:

1. Right-click project in Solution Explorer
2. Select "Properties"
3. Go to "References" or "Dependencies"
4. Verify `IISFrontGuard.Module` is not listed

### 3. Verify Runtime

Run your application and verify:
- No IISFrontGuard module is loaded
- Security headers are no longer being added/removed by the module
- Application functions normally without the module

```powershell
# Check response headers
$response = Invoke-WebRequest -Uri "https://yoursite.com"
$response.Headers
```

### 4. Verify Database

Connect to SQL Server and verify:
- IISFrontGuard database is dropped (if you chose to drop it)
- No orphaned data remains

## Troubleshooting

### Uninstall Script Fails

If the automatic uninstall script fails:

1. **Check Error Message**: Review the error message displayed in the Package Manager Console
2. **Manual Cleanup**: Follow the manual cleanup steps above
3. **Restore Backup**: If issues occur, restore from the backup: `web.config.backup_*`

```powershell
# Restore from backup
Copy-Item "web.config.backup_20250101120000" "web.config" -Force
```

### Module Still Loaded After Uninstall

If the module is still loaded after uninstallation:

1. **Clean Solution**:
   ```
   Build ? Clean Solution
   ```

2. **Restart IIS**:
   ```powershell
   iisreset
   ```

3. **Check for Multiple web.config Files**: 
   - Parent directories may have web.config files
   - Check IIS configuration files

4. **Restart Visual Studio**: Close and reopen Visual Studio

### Configuration Still Present

If configuration is still present after uninstall:

1. Check the backup file was created (indicates script ran)
2. Manually remove configurations using the XML snippets above
3. Compare with backup to identify what was changed

### Database Connection Errors

If you see database connection errors after uninstall:

1. Verify the connection string was removed from web.config
2. Check for hardcoded connection strings in code
3. Verify database was dropped if you chose to do so

## Rollback/Reinstallation

If you need to rollback the uninstallation:

### Option 1: Restore from Backup
```powershell
Copy-Item "web.config.backup_20250101120000" "web.config" -Force
```

### Option 2: Reinstall Package
```powershell
Install-Package IISFrontGuard.Module -Version 1.0.0
```

## Complete Cleanup Checklist

Use this checklist to ensure complete removal:

- [ ] Package uninstalled via NuGet
- [ ] Web.config backup created
- [ ] Module registration removed from web.config
- [ ] Security header configurations removed from web.config
- [ ] Connection string removed from web.config
- [ ] App settings removed from web.config
- [ ] Database tables dropped
- [ ] Database dropped (optional)
- [ ] Content files removed (GeoLite2, scripts, docs)
- [ ] Log files removed
- [ ] IIS restarted
- [ ] Project rebuilt
- [ ] Application tested and working
- [ ] No IISFrontGuard references in web.config
- [ ] No IISFrontGuard assembly references in project
- [ ] Backups archived or deleted

## Support

If you encounter issues during uninstallation:

1. Check this guide for troubleshooting steps
2. Review the backup file created during uninstall
3. Contact support with:
   - Uninstall error messages
   - Web.config backup file
   - Package version being uninstalled

## Post-Uninstallation

After uninstalling IISFrontGuard:

### Security Considerations

The following security features will no longer be active:
- WAF (Web Application Firewall) protection
- Rate limiting
- Geographic IP filtering
- Automatic security header management
- Security event logging
- Webhook notifications

### Recommended Actions

If you're removing IISFrontGuard, consider:
1. Implementing alternative security measures
2. Enabling IIS security features
3. Using application-level security controls
4. Monitoring access logs for suspicious activity

---

**Note**: Always test the uninstallation in a staging environment before applying to production.

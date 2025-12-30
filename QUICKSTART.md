# IISFrontGuard Module - Quick Start Guide

## Installation

### NuGet Package Manager Console
```powershell
Install-Package IISFrontGuard.Module
```

### .NET CLI
```bash
dotnet add package IISFrontGuard.Module
```

### Package Manager UI
1. Right-click project ? Manage NuGet Packages
2. Search for "IISFrontGuard.Module"
3. Click Install

## Initial Setup (Post-Installation)

### 1. Create Database
```sql
-- Execute the SQL script from the package
-- Location: Content\Scripts\init.sql
```

Or create manually:
```sql
CREATE DATABASE IISFrontGuard;
GO

USE IISFrontGuard;
GO

-- Tables are created automatically on first run
-- Or run the init.sql script
```

### 2. Update Connection String
The web.config is automatically configured, but verify:

```xml
<connectionStrings>
  <add name="IISFrontGuard"
       connectionString="Data Source=YOUR_SERVER;Initial Catalog=IISFrontGuard;Integrated Security=True;TrustServerCertificate=True;" />
</connectionStrings>
```

### 3. Configure Security Settings
Update `web.config` appSettings:

```xml
<appSettings>
  <!-- IMPORTANT: Change this 16-character encryption key -->
  <add key="GlobalRequestEncryptionKey" value="YOUR-16CHAR-KEY!" />
  
  <!-- Adjust rate limits for your needs -->
  <add key="RateLimitMaxRequestsPerMinute" value="150" />
  <add key="RateLimitWindowSeconds" value="60" />
</appSettings>
```

### 4. Verify Module Registration
Should be automatically added to `web.config`:

```xml
<system.webServer>
  <modules>
    <add name="FrontGuardModule"
         type="IISFrontGuard.Module.FrontGuardModule, IISFrontGuard.Module"
         preCondition="managedHandler,runtimeVersionv4.0" />
  </modules>
</system.webServer>
```

## Basic Configuration Examples

### Block SQL Injection Attempts
```sql
INSERT INTO WafRules (Name, Priority, IsEnabled, Action, Conditions)
VALUES ('Block SQL Injection', 100, 1, 'Block', 
  '[{"Field":"QueryString","Operator":"Contains","Value":"UNION SELECT"},
    {"Field":"QueryString","Operator":"Contains","Value":"DROP TABLE"}]');
```

### Block Specific Countries
```sql
INSERT INTO WafRules (Name, Priority, IsEnabled, Action, Conditions)
VALUES ('Block High-Risk Countries', 50, 1, 'Block',
  '[{"Field":"Country","Operator":"Equals","Value":"CN,RU,KP"}]');
```

### Rate Limit Login Endpoint
```sql
INSERT INTO WafRules (Name, Priority, IsEnabled, Action, Conditions)
VALUES ('Rate Limit Login', 75, 1, 'RateLimit',
  '[{"Field":"Path","Operator":"Equals","Value":"/Account/Login"}]');
```

### Challenge Suspicious User Agents
```sql
INSERT INTO WafRules (Name, Priority, IsEnabled, Action, Conditions)
VALUES ('Challenge Bots', 60, 1, 'Challenge',
  '[{"Field":"UserAgent","Operator":"Contains","Value":"bot"}]');
```

## Webhook Integration (Optional)

Enable real-time security notifications:

```xml
<appSettings>
  <add key="Webhook.Enabled" value="true" />
  <add key="Webhook.Url" value="https://your-webhook-endpoint.com/api/security-events" />
  <add key="Webhook.AuthHeader" value="Bearer YOUR_TOKEN_HERE" />
  <add key="Webhook.CustomHeaders" value='{"X-Source":"IISFrontGuard","X-Environment":"Production"}' />
</appSettings>
```

## Monitoring and Logs

### View Security Events
```sql
SELECT TOP 100 
    EventType,
    ClientIP,
    CountryCode,
    RequestPath,
    EventTime,
    Details
FROM SecurityEvents
ORDER BY EventTime DESC;
```

### Check Rate Limit Violations
```sql
SELECT 
    ClientIP,
    COUNT(*) as ViolationCount,
    MAX(EventTime) as LastViolation
FROM SecurityEvents
WHERE EventType = 'RateLimit'
  AND EventTime > DATEADD(hour, -24, GETDATE())
GROUP BY ClientIP
ORDER BY ViolationCount DESC;
```

### Geographic Access Patterns
```sql
SELECT 
    CountryCode,
    COUNT(*) as RequestCount
FROM SecurityEvents
WHERE EventTime > DATEADD(day, -7, GETDATE())
GROUP BY CountryCode
ORDER BY RequestCount DESC;
```

## Troubleshooting

### Module Not Loading
1. Check IIS Application Pool is running .NET 4.x
2. Verify module is registered in web.config
3. Check Windows Event Log for errors

### Database Connection Issues
1. Verify connection string
2. Ensure SQL Server allows remote connections
3. Check firewall rules
4. Test connection with SQL Server Management Studio

### GeoIP Not Working
1. Verify GeoLite2-Country.mmdb file exists in bin directory
2. Update database using UpdateGeoDb.bat
3. Check file permissions

### Rate Limiting Not Working
1. Verify rate limit settings in web.config
2. Check cache provider is functioning
3. Review SecurityEvents table for rate limit entries

## Performance Tuning

### Connection Pooling
```xml
<add name="IISFrontGuard"
     connectionString="...;Min Pool Size=5;Max Pool Size=100;..." />
```

### Rate Limit Cache
Adjust window for better performance:
```xml
<add key="RateLimitWindowSeconds" value="60" />
```

### Disable Logging for Specific Paths
```sql
INSERT INTO WafRules (Name, Priority, IsEnabled, Action, Conditions)
VALUES ('Allow Static Resources', 1, 1, 'Allow',
  '[{"Field":"Path","Operator":"StartsWith","Value":"/Content"}]');
```

## Uninstallation

If you need to remove the module:

1. Uninstall via NuGet:
   ```powershell
   Uninstall-Package IISFrontGuard.Module
   ```

2. Remove from web.config (if not auto-removed):
   ```xml
   <!-- Remove the FrontGuardModule from system.webServer/modules -->
   ```

3. (Optional) Remove database:
   ```sql
   DROP DATABASE IISFrontGuard;
   ```

## Additional Resources

- **Full Documentation**: See README.md in the package
- **Package Info**: See PACKAGE_INFO.md
- **Setup Guide**: See NUGET_SETUP_GUIDE.md
- **Support**: https://dev.azure.com/kacosta/IISFrontGuard

## Version Information

- **Current Version**: 1.0.0
- **Target Framework**: .NET Framework 4.8
- **Minimum IIS Version**: 7.0
- **Minimum SQL Server Version**: 2012

---

**Need Help?** Contact the IISFrontGuard Team or create an issue in Azure DevOps.

[![NuGet](https://img.shields.io/nuget/v/IISFrontGuard.Module.svg)](https://www.nuget.org/packages/IISFrontGuard.Module/)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=DeuxaX5M07WEtzjf4tc72Y3QSk2lqykrX3g123xwb9GKXkG7IYLuJQQJ99BLACAAAAAAAAAAAAASAZDO1MN9&metric=alert_status&token=ad420743823174abe0583d291546b8b5f6205dec)](https://sonarcloud.io/summary/new_code?id=DeuxaX5M07WEtzjf4tc72Y3QSk2lqykrX3g123xwb9GKXkG7IYLuJQQJ99BLACAAAAAAAAAAAAASAZDO1MN9)

 # IISFrontGuard

 [![SonarQube Cloud](https://sonarcloud.io/images/project_badges/sonarcloud-light.svg)](https://sonarcloud.io/summary/new_code?id=DeuxaX5M07WEtzjf4tc72Y3QSk2lqykrX3g123xwb9GKXkG7IYLuJQQJ99BLACAAAAAAAAAAAAASAZDO1MN9)

> **Disclaimer:** IISFrontGuard is a project name, not affiliated with Cloudflare or Microsoft Corp.

[![Quality gate](https://sonarcloud.io/api/project_badges/quality_gate?project=DeuxaX5M07WEtzjf4tc72Y3QSk2lqykrX3g123xwb9GKXkG7IYLuJQQJ99BLACAAAAAAAAAAAAASAZDO1MN9&token=ad420743823174abe0583d291546b8b5f6205dec)](https://sonarcloud.io/summary/new_code?id=DeuxaX5M07WEtzjf4tc72Y3QSk2lqykrX3g123xwb9GKXkG7IYLuJQQJ99BLACAAAAAAAAAAAAASAZDO1MN9)

IISFrontGuard is a Web Application Firewall (WAF) module for Internet Information Services (IIS) built on .NET Framework 4.8. It provides advanced security features including request filtering, rate limiting, managed/interactive challenges, and comprehensive logging with webhook notifications.

## Features

- **Web Application Firewall (WAF)**: Custom rule-based request filtering with pattern matching
- **Rate Limiting**: Protect against DDoS and brute-force attacks with configurable rate limits
- **Geographic IP Filtering**: Block or allow traffic based on country of origin using GeoIP2
- **Security Event Logging**: Comprehensive logging to SQL database for audit and compliance
- **Webhook Notifications**: Real-time security event notifications to external systems
- **Interactive Challenges**: CAPTCHA-like challenges for suspicious requests
- **Request Encryption**: Support for encrypted request validation

## Installation

### Via NuGet Package Manager

```powershell
Install-Package IISFrontGuard.Module
```

### Via .NET CLI

```bash
dotnet add package IISFrontGuard.Module
```

### Via Package Manager Console

```powershell
PM> Install-Package IISFrontGuard.Module
```

**Note:** The package will automatically update your `Web.config` with required settings and open a getting started guide.

## Configuration

### 1. Database Setup

Execute the included SQL script to create the required database tables:

```sql
-- Located in: Content\Scripts\init.sql
```

### 2. Web.config Configuration

**The package automatically configures your Web.config during installation** with the following default settings. Please review and update as needed:

```xml
<configuration>
  <connectionStrings>
    <add name="IISFrontGuard"
         connectionString="Data Source=.;Initial Catalog=IISFrontGuard;Integrated Security=True;TrustServerCertificate=True;" />
  </connectionStrings>
  
  <appSettings>
    <!-- Database Configuration -->
    <add key="IISFrontGuard.DefaultConnectionStringName" value="IISFrontGuard" />
    <add key="IISFrontGuardEncryptionKey" value="YOUR-16-CHAR-KEY" />

    <!-- Rate Limiting Configuration -->
    <add key="IISFrontGuard.RateLimitMaxRequestsPerMinute" value="150" />
    <add key="IISFrontGuard.RateLimitWindowSeconds" value="60" />

    <!-- Trusted Proxy IPs (for X-Forwarded-For header validation) -->
    <add key="TrustedProxyIPs" value="" />

    <!-- Webhook Configuration (Optional) -->
    <add key="IISFrontGuard.Webhook.Enabled" value="false" />
    <add key="IISFrontGuard.Webhook.Url" value="" />
    <add key="IISFrontGuard.Webhook.AuthHeader" value="" />
    <add key="IISFrontGuard.Webhook.CustomHeaders" value="" />
    <add key="IISFrontGuard.Webhook.FailureLogPath" value="C:\Logs\webhook-failures.log" />
  </appSettings>
  
  <system.webServer>
    <modules>
      <add name="FrontGuardModule"
           type="IISFrontGuard.Module.FrontGuardModule, IISFrontGuard.Module"
           preCondition="managedHandler,runtimeVersionv4.0" />
    </modules>
    
    <!-- Remove unnecessary server headers for enhanced security -->
    <httpProtocol>
      <customHeaders>
        <remove name="X-Powered-By" />
      </customHeaders>
    </httpProtocol>
  </system.webServer>
  
  <system.web>
    <!-- Remove ASP.NET version header -->
    <httpRuntime enableVersionHeader="false" />
  </system.web>
</configuration>
```

### 3. GeoIP Database

The package includes a GeoLite2-Country database. To keep it updated:

1. Register for a free MaxMind account at https://www.maxmind.com/
2. Run the included `UpdateGeoDb.bat` script with your license key

## Usage

### Interactive Challenges for localhost

Configure WAF rules for localhost:

```sql
-- Create an AppEntity for localhost testing
INSERT [dbo].[AppEntity] ([Id], [AppName], [AppDescription], [Host], [CreationDate], [TokenExpirationDurationHr]) VALUES (NEWID(), N'Localhost App', N'Test application for localhost', N'localhost', GETDATE(), 12)
GO

-- Retrieve the Id of the newly created AppEntity
DECLARE @LocalAppId UNIQUEIDENTIFIER
SELECT TOP 1 @LocalAppId = [Id] FROM [dbo].[AppEntity] WHERE [Host] = N'localhost'

-- Insert a rule for Interactive Challenge as an example on localhost using the newly created AppEntity
INSERT [dbo].[WafRuleEntity] ([Nombre], [ActionId], [AppId], [Prioridad], [Habilitado], [CreationDate]) 
VALUES (N'Interactive Challenge', 4, @LocalAppId, 0, 1, GETDATE())
```

### Creating WAF Rules

Add custom WAF rules to the database:

```sql
INSERT INTO WafRules (Name, Priority, IsEnabled, Action, Conditions)
VALUES ('Block SQL Injection', 100, 1, 'Block', 
  '[{"Field":"QueryString","Operator":"Contains","Value":"UNION SELECT"}]');
```

### Rate Limiting

Configure rate limits in Web.config:

```xml
<add key="RateLimitMaxRequestsPerMinute" value="150" />
<add key="RateLimitWindowSeconds" value="60" />
```

### Geographic Filtering

Configure country blocking/allowing via database WAF rules:

```sql
INSERT INTO WafRules (Name, Priority, IsEnabled, Action, Conditions)
VALUES ('Block Specific Countries', 50, 1, 'Block',
  '[{"Field":"Country","Operator":"Equals","Value":"CN,RU,KP"}]');
```

## Requirements

- .NET Framework 4.8
- IIS 7.0 or later
- SQL Server 2012 or later

## Uninstallation

To remove IISFrontGuard from your application:

```powershell
Uninstall-Package IISFrontGuard.Module
```

The uninstall process will automatically:
- Remove module registration from web.config
- Remove security header configurations
- Remove connection strings and app settings
- Create a backup of your web.config

**Manual cleanup required:**
- Database tables (see UNINSTALL_GUIDE.md)
- Content files (GeoIP database, scripts, documentation)
- Log files

For complete uninstallation instructions, see `UNINSTALL_GUIDE.md` included in the package.

## Support

For issues, questions, or contributions, please visit:
- Project Repository: https://github.com/kenllyacosta/IISFrontGuard

## License

This project is licensed under the MIT License.

## Author

IISFrontGuard Team

## Changelog

### Version 1.0.0
- Initial release
- WAF functionality with custom rules
- Rate limiting support
- GeoIP filtering
- Security event logging
- Webhook notifications
- Automatic security header management
- Complete uninstallation support

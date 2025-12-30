# IISFrontGuard Module - NuGet Package Configuration

## Package Metadata

### Package Information
- **Package ID**: IISFrontGuard.Module
- **Title**: IISFrontGuard Module
- **Current Version**: 1.0.0
- **License**: MIT
- **Authors**: IISFrontGuard Team
- **Target Framework**: .NET Framework 4.8

### Package Description
IISFrontGuard is an IIS HTTP Module providing comprehensive web application security including WAF functionality, rate limiting, geographic IP filtering, security event logging, and webhook notifications for ASP.NET applications running on .NET Framework 4.8.

## Files Included in Package

### Binary Files
- `lib/net48/IISFrontGuard.Module.dll` - Main assembly
- `lib/net48/IISFrontGuard.Module.pdb` - Debug symbols
- `lib/net48/IISFrontGuard.Module.xml` - XML documentation (if generated)

### Content Files
- `content/GeoLite2-Country.mmdb` - MaxMind GeoIP2 database
- `content/UpdateGeoDb.bat` - Script to update GeoIP database
- `content/Scripts/init.sql` - SQL database initialization script
- `content/web.config.transform` - Web.config transformation for auto-configuration

### Documentation
- `README.md` - Package documentation and usage guide

## NuGet Dependencies

The package declares the following dependencies for .NET Framework 4.8:

| Package | Version |
|---------|---------|
| IPNetwork | 1.3.2.0 |
| MaxMind.Db | 4.3.4 |
| MaxMind.GeoIP2 | 5.4.1 |
| Microsoft.Bcl.AsyncInterfaces | 10.0.1 |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.1 |
| Microsoft.Extensions.Options | 10.0.1 |
| Microsoft.Extensions.Primitives | 10.0.1 |
| System.Buffers | 4.6.1 |
| System.IO.Pipelines | 10.0.1 |
| System.Memory | 4.6.3 |
| System.Numerics.Vectors | 4.6.1 |
| System.Runtime.CompilerServices.Unsafe | 6.1.2 |
| System.Text.Encodings.Web | 10.0.1 |
| System.Text.Json | 10.0.1 |
| System.Threading.Tasks.Extensions | 4.6.3 |
| System.ValueTuple | 4.6.1 |

## Installation Transformations

When the package is installed via NuGet, the `web.config.transform` file automatically:

1. Adds the IISFrontGuard connection string
2. Adds required appSettings configuration keys
3. Registers the FrontGuardModule in system.webServer/modules

## Version History

### 1.0.0 (Initial Release)
- Core WAF functionality with custom rule engine
- Rate limiting per client IP
- GeoIP filtering using MaxMind GeoIP2
- SQL database logging for security events
- Webhook notification support
- Interactive challenge mechanism
- Request encryption validation

## Future Enhancements

Planned for upcoming versions:
- Dashboard UI for rule management
- Enhanced analytics and reporting
- Machine learning-based threat detection
- Support for .NET Core/6/7/8
- Redis cache provider option
- Multi-tenancy support

## Updating Package Dependencies

When updating NuGet dependencies in the project:

1. Update `packages.config` in the project
2. Update dependency versions in `IISFrontGuard.Module.nuspec`
3. Ensure both files stay synchronized
4. Test the package thoroughly after changes

## Building Package Locally

Use the included PowerShell script:

```powershell
.\Build-NuGetPackage.ps1 -Version "1.0.0-beta" -Configuration Release
```

Parameters:
- `-Version`: Package version (default: 1.0.0-local)
- `-Configuration`: Build configuration (default: Release)
- `-SkipBuild`: Skip building the solution
- `-SkipTests`: Skip running unit tests
- `-OutputPath`: Output directory for .nupkg file

## Publishing to Feeds

### Azure Artifacts (Internal)
```powershell
nuget push IISFrontGuard.Module.1.0.0.nupkg -Source https://pkgs.dev.azure.com/{org}/_packaging/{feed}/nuget/v3/index.json -ApiKey az
```

### NuGet.org (Public)
```powershell
nuget push IISFrontGuard.Module.1.0.0.nupkg -Source https://api.nuget.org/v3/index.json -ApiKey {your-api-key}
```

## Package Validation

Before publishing, validate the package:

```powershell
# List package contents
nuget list IISFrontGuard.Module -Source ./NuGetPackages

# Install to test project
Install-Package IISFrontGuard.Module -Source ./NuGetPackages -Version 1.0.0-local

# Verify files are installed correctly
# Check that web.config is transformed
# Test module functionality
```

## Support and Contributions

- **Repository**: https://dev.azure.com/kacosta/IISFrontGuard/_git/IISFrontGuard
- **Issues**: Report via Azure DevOps work items
- **Contact**: IISFrontGuard Team

---

**Maintained by**: IISFrontGuard Team  
**Last Updated**: January 2025

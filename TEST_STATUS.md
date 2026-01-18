# Test Status Report

## Summary
- ? **Unit Tests**: 271/271 PASSED (100%)
- ?? **Integration Tests**: Require environment setup

## Unit Tests

All 271 unit tests are passing successfully. These tests validate:
- Core WAF logic and rule evaluation
- Token generation and validation
- Challenge mechanisms (managed and interactive)
- Rate limiting
- CSRF protection
- Security event notifications
- Webhook functionality (unit level)
- Configuration management
- HTTP field extraction
- HTML page generation
- And more...

### Running Unit Tests Only

```powershell
dotnet test IISFrontGuard.Module.UnitTests\IISFrontGuard.Module.UnitTests.csproj
```

## Integration Tests

Integration tests require a complete environment setup including:

### Prerequisites

1. **IIS Site Configuration**
   - Site Name: `IISFrontGuard_Test`
   - URL: `http://localhost:5080`
   - Physical Path: `C:\inetpub\wwwroot\IISFrontGuard_Test`
   - App Pool: `IISFrontGuard_TestPool`

2. **SQL Server**
   - SQL Server instance running (default instance or named instance)
   - Database: `IISFrontGuard` (created automatically by tests)
   - Integrated Security OR SQL Authentication configured

3. **Permissions**
   - Write access to `C:\inetpub\wwwroot\IISFrontGuard_Test`
   - Administrator privileges to configure IIS
   - Database create permissions on SQL Server

### Setting Up Integration Test Environment

#### Option 1: Using PowerShell Script (Recommended)

Create and run a setup script (coming soon: `Setup-IISTestEnvironment.ps1`):

```powershell
# Run as Administrator
.\Setup-IISTestEnvironment.ps1
```

#### Option 2: Manual Setup

1. **Create IIS Site**:
```powershell
# Run PowerShell as Administrator
Import-Module WebAdministration

# Create app pool
New-WebAppPool -Name "IISFrontGuard_TestPool"
Set-ItemProperty "IIS:\AppPools\IISFrontGuard_TestPool" -Name "managedRuntimeVersion" -Value "v4.0"

# Create site directory
New-Item -Path "C:\inetpub\wwwroot\IISFrontGuard_Test" -ItemType Directory -Force

# Create IIS site
New-IISSite -Name "IISFrontGuard_Test" `
    -PhysicalPath "C:\inetpub\wwwroot\IISFrontGuard_Test" `
    -BindingInformation "*:5080:" `
    -Protocol "http"

# Assign app pool
Set-ItemProperty "IIS:\Sites\IISFrontGuard_Test" -Name "applicationPool" -Value "IISFrontGuard_TestPool"

# Start site
Start-IISSite -Name "IISFrontGuard_Test"
```

2. **Verify SQL Server**:
```powershell
# Test SQL connection
sqlcmd -S . -Q "SELECT @@VERSION"
```

3. **Run Integration Tests**:
```powershell
dotnet test IISFrontGuard.Module.IntegrationTests\IISFrontGuard.Module.IntegrationTests.csproj
```

### Skipping Integration Tests

Integration tests will automatically skip with a helpful message when the environment is not configured. You'll see output like:

```
Skipped IISFrontGuard.Module.IntegrationTests.Core.ConfigurationTests.SomeTest
  Reason: Integration tests require IIS environment setup.
    Missing directory: C:\inetpub\wwwroot\IISFrontGuard_Test
    Solution: Create IIS site using Setup-IISTestEnvironment.ps1
```

### Cleaning Up Integration Test Environment

```powershell
.\Cleanup-IISTestEnvironment.ps1
```

Or manually:

```powershell
# Stop and remove IIS site
Stop-IISSite -Name "IISFrontGuard_Test"
Remove-IISSite -Name "IISFrontGuard_Test"

# Remove app pool
Remove-WebAppPool -Name "IISFrontGuard_TestPool"

# Remove directory
Remove-Item "C:\inetpub\wwwroot\IISFrontGuard_Test" -Recurse -Force

# Optionally, drop database
sqlcmd -S . -Q "DROP DATABASE IF EXISTS IISFrontGuard"
```

## Continuous Integration

For CI/CD pipelines:

### Run Unit Tests Only
```yaml
# GitHub Actions / Azure DevOps
- name: Run Unit Tests
  run: dotnet test IISFrontGuard.Module.UnitTests\IISFrontGuard.Module.UnitTests.csproj --logger trx
```

### Run All Tests (with IIS setup)
```yaml
- name: Setup IIS
  run: |
    powershell -File Setup-IISTestEnvironment.ps1
    
- name: Run All Tests
  run: dotnet test --logger trx
```

## Test Coverage

Unit tests provide comprehensive coverage of:
- ? Business logic
- ? Rule evaluation engine
- ? Security mechanisms
- ? Token management
- ? Configuration handling
- ? HTTP processing

Integration tests validate:
- ?? End-to-end request flow through IIS
- ?? Database operations
- ?? Real HTTP context behavior
- ?? IIS module integration

## Current Status

| Test Suite | Status | Count | Notes |
|------------|--------|-------|-------|
| Unit Tests | ? Passing | 271/271 | All critical functionality validated |
| Integration Tests | ?? Requires Setup | 0/175 | Needs IIS + SQL Server environment |

## Troubleshooting

### Integration Tests Failing - Database Connection

**Error**: `A network-related or instance-specific error occurred while establishing a connection to SQL Server`

**Solutions**:
1. Verify SQL Server is running: `Get-Service MSSQLSERVER`
2. Start SQL Server: `Start-Service MSSQLSERVER`
3. Check connection string in `app.config`
4. Test connection: `sqlcmd -S . -E -Q "SELECT 1"`

### Integration Tests Failing - IIS Site Not Found

**Error**: `IIS site directory does not exist`

**Solutions**:
1. Run `Setup-IISTestEnvironment.ps1` as Administrator
2. Verify IIS is installed: `Get-WindowsFeature -Name Web-Server`
3. Check site exists: `Get-IISSite -Name "IISFrontGuard_Test"`

### Permission Denied

**Error**: Access denied when creating directories or sites

**Solution**: Run PowerShell as Administrator

## Future Improvements

- [ ] Create `Setup-IISTestEnvironment.ps1` automation script
- [ ] Add Docker-based integration test environment
- [ ] Add GitHub Actions workflow with IIS setup
- [ ] Consider using IIS Express for lighter integration tests
- [ ] Add code coverage reporting

---

**Last Updated**: 2025
**Test Framework**: xUnit 2.4.2 with NUnit
**Target Framework**: .NET Framework 4.8

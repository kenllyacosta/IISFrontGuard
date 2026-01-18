<#
.SYNOPSIS
    Sets up the IIS integration test environment for IISFrontGuard.

.DESCRIPTION
    This script creates and configures the IIS site and SQL Server database required
    for running integration tests. It must be run with Administrator privileges.

.PARAMETER SiteName
    The name of the IIS site to create. Default: IISFrontGuard_Test

.PARAMETER Port
    The HTTP port for the test site. Default: 5080

.PARAMETER PhysicalPath
    The physical path for the IIS site. Default: C:\inetpub\wwwroot\IISFrontGuard_Test

.PARAMETER AppPoolName
    The application pool name. Default: IISFrontGuard_TestPool

.PARAMETER DatabaseName
    The SQL Server database name. Default: IISFrontGuard

.PARAMETER SqlServer
    The SQL Server instance name. Default: . (local default instance)

.EXAMPLE
    .\Setup-IISTestEnvironment.ps1
    
    Sets up the test environment with default settings.

.EXAMPLE
    .\Setup-IISTestEnvironment.ps1 -SqlServer ".\SQLEXPRESS"
    
    Sets up the test environment using SQL Server Express.

.NOTES
    Requires Administrator privileges.
    Requires IIS and SQL Server to be installed.
#>

[CmdletBinding()]
param(
    [string]$SiteName = "IISFrontGuard_Test",
    [int]$Port = 5080,
    [string]$PhysicalPath = "C:\inetpub\wwwroot\IISFrontGuard_Test",
    [string]$AppPoolName = "IISFrontGuard_TestPool",
    [string]$DatabaseName = "IISFrontGuard",
    [string]$SqlServer = "."
)

$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host "`n==> $Message" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "    ? $Message" -ForegroundColor Green
}

function Write-Warning {
    param([string]$Message)
    Write-Host "    ? $Message" -ForegroundColor Yellow
}

function Write-ErrorMessage {
    param([string]$Message)
    Write-Host "    ? $Message" -ForegroundColor Red
}

# Check if running as Administrator
Write-Step "Checking privileges..."
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
$isAdmin = $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-ErrorMessage "This script must be run as Administrator!"
    Write-Host "`nPlease right-click PowerShell and select 'Run as Administrator', then run this script again.`n" -ForegroundColor Yellow
    exit 1
}
Write-Success "Running with Administrator privileges"

# Check if IIS is installed
Write-Step "Checking IIS installation..."
$iisFeature = Get-WindowsFeature -Name Web-Server -ErrorAction SilentlyContinue
if (-not $iisFeature) {
    # Try alternative method for client OS
    try {
        Import-Module WebAdministration -ErrorAction Stop
        Write-Success "IIS is installed"
    }
    catch {
        Write-ErrorMessage "IIS is not installed!"
        Write-Host "`nPlease install IIS using:" -ForegroundColor Yellow
        Write-Host "  Enable-WindowsOptionalFeature -Online -FeatureName IIS-WebServerRole`n"
        exit 1
    }
}
elseif (-not $iisFeature.Installed) {
    Write-ErrorMessage "IIS is not installed!"
    Write-Host "`nPlease install IIS using:" -ForegroundColor Yellow
    Write-Host "  Install-WindowsFeature -Name Web-Server -IncludeManagementTools`n"
    exit 1
}
else {
    Write-Success "IIS is installed"
}

# Import IIS module
try {
    Import-Module WebAdministration -ErrorAction Stop
    Write-Success "IIS module loaded"
}
catch {
    Write-ErrorMessage "Failed to load IIS module: $_"
    exit 1
}

# Create physical directory
Write-Step "Creating site directory..."
if (-not (Test-Path $PhysicalPath)) {
    New-Item -Path $PhysicalPath -ItemType Directory -Force | Out-Null
    Write-Success "Created directory: $PhysicalPath"
}
else {
    Write-Warning "Directory already exists: $PhysicalPath"
}

# Create bin subdirectory
$binPath = Join-Path $PhysicalPath "bin"
if (-not (Test-Path $binPath)) {
    New-Item -Path $binPath -ItemType Directory -Force | Out-Null
    Write-Success "Created bin directory"
}

# Create Application Pool
Write-Step "Configuring application pool..."
$appPool = Get-WebAppPoolState -Name $AppPoolName -ErrorAction SilentlyContinue
if (-not $appPool) {
    New-WebAppPool -Name $AppPoolName | Out-Null
    Write-Success "Created app pool: $AppPoolName"
}
else {
    Write-Warning "App pool already exists: $AppPoolName"
}

# Configure app pool settings
Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name "managedRuntimeVersion" -Value "v4.0"
Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name "enable32BitAppOnWin64" -Value $false
Write-Success "Configured app pool for .NET 4.x"

# Create IIS Site
Write-Step "Creating IIS site..."
$site = Get-IISSite -Name $SiteName -ErrorAction SilentlyContinue
if (-not $site) {
    try {
        New-IISSite -Name $SiteName `
            -PhysicalPath $PhysicalPath `
            -BindingInformation "*:${Port}:" `
            -Protocol "http" | Out-Null
        Write-Success "Created IIS site: $SiteName"
    }
    catch {
        Write-ErrorMessage "Failed to create IIS site: $_"
        exit 1
    }
}
else {
    Write-Warning "IIS site already exists: $SiteName"
}

# Assign app pool to site
Set-ItemProperty "IIS:\Sites\$SiteName" -Name "applicationPool" -Value $AppPoolName
Write-Success "Assigned app pool to site"

# Start the site
Write-Step "Starting IIS site..."
try {
    Start-IISSite -Name $SiteName -ErrorAction Stop
    Write-Success "Site started successfully"
}
catch {
    Write-Warning "Site may already be running"
}

# Create minimal web.config
Write-Step "Creating web.config..."
$webConfigPath = Join-Path $PhysicalPath "web.config"
$connectionString = "Data Source=$SqlServer;Initial Catalog=$DatabaseName;Integrated Security=True;TrustServerCertificate=True;"

$webConfigContent = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <appSettings>
    <add key="GlobalLogger.Host.localhost" value="$connectionString" />
    <add key="IISFrontGuard.Webhook.Enabled" value="false" />
    <add key="IISFrontGuardEncryptionKey" value="TestKey1234567890" />
  </appSettings>
  <connectionStrings>
    <add name="IISFrontGuardConnection" 
         connectionString="$connectionString" 
         providerName="System.Data.SqlClient" />
  </connectionStrings>
  <system.web>
    <compilation debug="true" targetFramework="4.8" />
    <httpModules>
      <add name="FrontGuardModule" type="IISFrontGuard.Module.FrontGuardModule, IISFrontGuard.Module" />
    </httpModules>
  </system.web>
  <system.webServer>
    <modules>
      <add name="FrontGuardModule" type="IISFrontGuard.Module.FrontGuardModule, IISFrontGuard.Module" />
    </modules>
  </system.webServer>
</configuration>
"@

$webConfigContent | Out-File -FilePath $webConfigPath -Encoding UTF8 -Force
Write-Success "Created web.config with connection string"

# Test SQL Server connection
Write-Step "Testing SQL Server connection..."
try {
    $sqlCmd = "SELECT @@VERSION"
    $result = Invoke-Sqlcmd -ServerInstance $SqlServer -Query $sqlCmd -ErrorAction Stop
    Write-Success "SQL Server is accessible: $SqlServer"
}
catch {
    Write-ErrorMessage "Cannot connect to SQL Server: $SqlServer"
    Write-Host "`nPlease ensure SQL Server is installed and running." -ForegroundColor Yellow
    Write-Host "To install SQL Server Express:" -ForegroundColor Yellow
    Write-Host "  Download from: https://www.microsoft.com/en-us/sql-server/sql-server-downloads`n"
    Write-Warning "Tests may fail without a working SQL Server connection"
}

# Create database (will be created by tests, but we can create it now)
Write-Step "Creating database..."
try {
    $checkDbSql = "IF DB_ID('$DatabaseName') IS NULL CREATE DATABASE [$DatabaseName]"
    Invoke-Sqlcmd -ServerInstance $SqlServer -Query $checkDbSql -ErrorAction Stop
    Write-Success "Database ready: $DatabaseName"
}
catch {
    Write-Warning "Could not create database (will be created by tests)"
}

# Test site accessibility
Write-Step "Testing site accessibility..."
try {
    Start-Sleep -Seconds 2  # Give IIS a moment to start
    $response = Invoke-WebRequest -Uri "http://localhost:$Port/" -UseBasicParsing -ErrorAction Stop
    Write-Success "Site is accessible at http://localhost:$Port/"
}
catch {
    if ($_.Exception.Response.StatusCode.value__ -eq 404 -or 
        $_.Exception.Response.StatusCode.value__ -eq 403) {
        Write-Success "Site is accessible at http://localhost:$Port/ (HTTP error is expected without content)"
    }
    else {
        Write-Warning "Could not verify site accessibility: $_"
        Write-Host "    This may be normal if no content is deployed yet" -ForegroundColor Gray
    }
}

# Summary
Write-Host "`n" + ("=" * 60) -ForegroundColor Green
Write-Host "Integration Test Environment Setup Complete!" -ForegroundColor Green
Write-Host ("=" * 60) -ForegroundColor Green
Write-Host "`nConfiguration:" -ForegroundColor Cyan
Write-Host "  Site Name:        $SiteName"
Write-Host "  URL:              http://localhost:$Port/"
Write-Host "  Physical Path:    $PhysicalPath"
Write-Host "  App Pool:         $AppPoolName"
Write-Host "  Database:         $DatabaseName"
Write-Host "  SQL Server:       $SqlServer"

Write-Host "`nNext Steps:" -ForegroundColor Cyan
Write-Host "  1. Run integration tests:"
Write-Host "     dotnet test IISFrontGuard.Module.IntegrationTests`n"
Write-Host "  2. To clean up when done:"
Write-Host "     .\Cleanup-IISTestEnvironment.ps1`n"

Write-Host "Note: Integration tests will automatically deploy binaries to the site's bin folder`n" -ForegroundColor Gray

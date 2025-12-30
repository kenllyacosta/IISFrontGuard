# NuGet Package Setup Guide for IISFrontGuard Module

This guide explains how to configure Azure DevOps to automatically build and publish the IISFrontGuard NuGet package.

## Prerequisites

- Azure DevOps account with access to the IISFrontGuard project
- Permissions to create/edit pipelines and feeds
- (Optional) NuGet.org API key for public package distribution

## Step 1: Create Azure Artifacts Feed (Recommended)

If you want to host packages internally within Azure DevOps:

1. Go to Azure DevOps ? **Artifacts**
2. Click **+ Create Feed**
3. Configure:
   - **Name**: `IISFrontGuard-Packages`
   - **Visibility**: Choose based on your needs (Organization/Private)
   - **Upstream sources**: Enable if you want to proxy public packages
4. Click **Create**

## Step 2: Configure the Build Pipeline

The `azure-pipelines.yml` file has been configured to:

1. Build the solution
2. Run unit tests with code coverage
3. Run SonarCloud and WhiteSource scans
4. Create the NuGet package
5. Publish to Azure Artifacts (for master/main branches)

### Pipeline Configuration

The pipeline is already set up in the repository. To use it:

1. Go to Azure DevOps ? **Pipelines**
2. If not already configured, click **New Pipeline**
3. Select **Azure Repos Git**
4. Choose the **IISFrontGuard** repository
5. Select **Existing Azure Pipelines YAML file**
6. Choose `/azure-pipelines.yml`
7. Click **Run**

## Step 3: (Optional) Configure NuGet.org Publishing

To publish to NuGet.org:

### 3.1 Get NuGet.org API Key

1. Go to https://www.nuget.org
2. Sign in (or create an account)
3. Go to **API Keys** in your account settings
4. Click **Create**
5. Configure:
   - **Key Name**: `IISFrontGuard-AzureDevOps`
   - **Package Owner**: Select yourself/organization
   - **Scopes**: Push new packages and package versions
   - **Glob Pattern**: `IISFrontGuard.Module`
6. Copy the generated API key

### 3.2 Create Service Connection in Azure DevOps

1. Go to Azure DevOps ? **Project Settings** ? **Service connections**
2. Click **New service connection**
3. Select **NuGet**
4. Configure:
   - **Feed URL**: `https://api.nuget.org/v3/index.json`
   - **ApiKey**: Paste your NuGet.org API key
   - **Service connection name**: `NuGet.org`
5. Click **Save**

### 3.3 Enable NuGet.org Publishing in Pipeline

Uncomment the NuGet.org publishing task in `azure-pipelines.yml`:

```yaml
- task: NuGetCommand@2
  displayName: 'Push to NuGet.org'
  inputs:
    command: 'push'
    packagesToPush: '$(Build.ArtifactStagingDirectory)/NuGet/*.nupkg'
    nuGetFeedType: 'external'
    publishFeedCredentials: 'NuGet.org'
  condition: and(succeeded(), startsWith(variables['Build.SourceBranch'], 'refs/tags/v'))
```

## Step 4: Version Management

### Automatic Versioning

The pipeline uses the build number format: `YYYY.M.D.r` (e.g., `2025.1.15.1`)

To use semantic versioning (e.g., `1.0.0`):

1. Edit `azure-pipelines.yml`
2. Update the variables section:

```yaml
variables:
  majorVersion: '1'
  minorVersion: '0'
  patchVersion: '0'
  NuGetVersion: '$(majorVersion).$(minorVersion).$(patchVersion)'
```

### Tag-Based Versioning

For release builds using git tags:

1. Create a git tag: `git tag v1.0.0`
2. Push the tag: `git push origin v1.0.0`
3. Update pipeline to extract version from tag:

```yaml
- task: PowerShell@2
  displayName: 'Set Version from Tag'
  inputs:
    targetType: 'inline'
    script: |
      $tag = "$(Build.SourceBranch)" -replace 'refs/tags/v', ''
      if ($tag -match '^\d+\.\d+\.\d+') {
        Write-Host "##vso[task.setvariable variable=NuGetVersion]$tag"
      }
```

## Step 5: Install the Package

### From Azure Artifacts

1. In Visual Studio, go to **Tools** ? **Options** ? **NuGet Package Manager** ? **Package Sources**
2. Click **+** to add a new source
3. Configure:
   - **Name**: `IISFrontGuard`
   - **Source**: `https://pkgs.dev.azure.com/{organization}/_packaging/IISFrontGuard-Packages/nuget/v3/index.json`
4. Click **Update** ? **OK**

In Package Manager Console:
```powershell
Install-Package IISFrontGuard.Module -Source IISFrontGuard
```

### From NuGet.org

Once published to NuGet.org:
```powershell
Install-Package IISFrontGuard.Module
```

## Step 6: Configure Automatic Publishing

### For Every Commit to Master/Main

The current configuration publishes to Azure Artifacts on every successful build of master/main branches.

### For Tagged Releases Only

To publish only on version tags:

1. Update the condition in the NuGet push task:

```yaml
condition: and(succeeded(), startsWith(variables['Build.SourceBranch'], 'refs/tags/v'))
```

## Package Contents

The NuGet package includes:

- **IISFrontGuard.Module.dll**: Main assembly
- **IISFrontGuard.Module.pdb**: Debug symbols
- **GeoLite2-Country.mmdb**: GeoIP database
- **UpdateGeoDb.bat**: Database update script
- **init.sql**: Database initialization script
- **web.config.transform**: Automatic Web.config configuration
- **README.md**: Documentation

## Troubleshooting

### Package Build Fails

Check that all required files exist:
- `IISFrontGuard.Module/IISFrontGuard.Module.nuspec`
- `IISFrontGuard.Module/README.md`
- `IISFrontGuard.Module/Content/web.config.install.xdt`

### Publishing Fails

1. Verify service connection credentials
2. Check feed permissions in Azure Artifacts
3. Ensure package version doesn't already exist

### Missing Dependencies

The .nuspec file lists all dependencies. Verify they match the packages.config versions.

## Best Practices

1. **Semantic Versioning**: Use MAJOR.MINOR.PATCH format
2. **Pre-release Packages**: Use suffixes like `-beta`, `-alpha` for testing
3. **Symbol Packages**: Always include .pdb files for debugging
4. **Release Notes**: Update for each version in the .nuspec file
5. **Testing**: Test package installation in a clean project before releasing

## Support

For issues or questions:
- Create an issue in Azure DevOps
- Contact the IISFrontGuard team

---

**Last Updated**: January 2025

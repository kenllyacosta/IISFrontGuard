# Build and Pack NuGet Package Locally
# This script builds the solution and creates a NuGet package for local testing

param(
    [string]$Version = "1.0.0-local",
    [string]$Configuration = "Release",
    [switch]$SkipBuild = $false,
    [switch]$SkipTests = $false,
    [string]$OutputPath = ".\NuGetPackages"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "IISFrontGuard NuGet Package Builder" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Paths
$solutionPath = "IISFrontGuard.Module.sln"
$nuspecPath = "IISFrontGuard.Module\IISFrontGuard.Module.nuspec"
$projectPath = "IISFrontGuard.Module\IISFrontGuard.Module.csproj"

# Check if solution exists
if (-not (Test-Path $solutionPath)) {
    Write-Error "Solution file not found: $solutionPath"
    exit 1
}

# Step 1: Restore NuGet packages
Write-Host "[1/5] Restoring NuGet packages..." -ForegroundColor Yellow
& nuget restore $solutionPath
if ($LASTEXITCODE -ne 0) {
    Write-Error "NuGet restore failed"
    exit 1
}
Write-Host "? Packages restored successfully" -ForegroundColor Green
Write-Host ""

# Step 2: Build solution
if (-not $SkipBuild) {
    Write-Host "[2/5] Building solution ($Configuration)..." -ForegroundColor Yellow
    & msbuild $solutionPath /p:Configuration=$Configuration /p:Platform="Any CPU" /v:minimal
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed"
        exit 1
    }
    Write-Host "? Build completed successfully" -ForegroundColor Green
    Write-Host ""
} else {
    Write-Host "[2/5] Skipping build (SkipBuild flag set)" -ForegroundColor Gray
    Write-Host ""
}

# Step 3: Run tests
if (-not $SkipTests) {
    Write-Host "[3/5] Running unit tests..." -ForegroundColor Yellow
    $testDlls = Get-ChildItem -Path "IISFrontGuard.Module.UnitTests\bin\$Configuration" -Filter "*Tests.dll" -Recurse
    
    if ($testDlls.Count -gt 0) {
        foreach ($testDll in $testDlls) {
            Write-Host "  Running: $($testDll.Name)"
            & vstest.console.exe $testDll.FullName /Logger:console
            if ($LASTEXITCODE -ne 0) {
                Write-Warning "Some tests failed, but continuing..."
            }
        }
        Write-Host "? Tests completed" -ForegroundColor Green
    } else {
        Write-Host "  No test assemblies found" -ForegroundColor Gray
    }
    Write-Host ""
} else {
    Write-Host "[3/5] Skipping tests (SkipTests flag set)" -ForegroundColor Gray
    Write-Host ""
}

# Step 4: Update version in nuspec
Write-Host "[4/5] Updating NuSpec version to $Version..." -ForegroundColor Yellow
$nuspecContent = Get-Content $nuspecPath -Raw
$nuspecContent = $nuspecContent -replace '<version>.*?</version>', "<version>$Version</version>"
Set-Content -Path $nuspecPath -Value $nuspecContent -NoNewline
Write-Host "? Version updated" -ForegroundColor Green
Write-Host ""

# Step 5: Create NuGet package
Write-Host "[5/5] Creating NuGet package..." -ForegroundColor Yellow

# Ensure output directory exists
if (-not (Test-Path $OutputPath)) {
    New-Item -Path $OutputPath -ItemType Directory | Out-Null
}

& nuget pack $nuspecPath -OutputDirectory $OutputPath -Properties Configuration=$Configuration
if ($LASTEXITCODE -ne 0) {
    Write-Error "NuGet pack failed"
    exit 1
}

Write-Host "? Package created successfully" -ForegroundColor Green
Write-Host ""

# Display results
$packageFile = Get-ChildItem -Path $OutputPath -Filter "IISFrontGuard.Module.$Version.nupkg" | Select-Object -First 1
if ($packageFile) {
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Package Details:" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  File: $($packageFile.Name)" -ForegroundColor White
    Write-Host "  Path: $($packageFile.FullName)" -ForegroundColor White
    Write-Host "  Size: $([math]::Round($packageFile.Length / 1MB, 2)) MB" -ForegroundColor White
    Write-Host ""
    Write-Host "To install locally:" -ForegroundColor Yellow
    Write-Host "  Install-Package IISFrontGuard.Module -Source `"$($packageFile.DirectoryName)`" -Version $Version" -ForegroundColor Gray
    Write-Host ""
    Write-Host "To push to a feed:" -ForegroundColor Yellow
    Write-Host "  nuget push `"$($packageFile.FullName)`" -Source <feed-url> -ApiKey <api-key>" -ForegroundColor Gray
    Write-Host ""
} else {
    Write-Warning "Package file not found in output directory"
}

Write-Host "Done!" -ForegroundColor Green

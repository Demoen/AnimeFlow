# ============================================================================
# Package Dependencies for GitHub Release
# ============================================================================
# This script packages the Dependencies folder into a zip file that can be
# uploaded as a GitHub release asset and downloaded by CI workflows.

param(
    [string]$Version = "latest"
)

$ErrorActionPreference = "Stop"

$ScriptDir = $PSScriptRoot
$Level3Dir = Split-Path -Parent $ScriptDir
$DepsDir = Join-Path $Level3Dir "Dependencies"
$OutputDir = Join-Path $Level3Dir "release-artifacts"

Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host "Package Dependencies for Release" -ForegroundColor Cyan
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host ""

# Check if Dependencies folder exists
if (-not (Test-Path $DepsDir)) {
    Write-Host "[ERROR] Dependencies folder not found!" -ForegroundColor Red
    Write-Host "Run Download-Dependencies.ps1 first" -ForegroundColor Yellow
    exit 1
}

# Create output directory
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

# Package name
$packageName = "Dependencies-Windows-x64-$Version.zip"
$packagePath = Join-Path $OutputDir $packageName

Write-Host "[INFO] Packaging Dependencies folder..." -ForegroundColor Cyan
Write-Host "  Source: $DepsDir" -ForegroundColor Gray
Write-Host "  Output: $packagePath" -ForegroundColor Gray
Write-Host ""

# Remove existing package
if (Test-Path $packagePath) {
    Remove-Item $packagePath -Force
}

# Create package
try {
    Write-Host "[PACKAGE] Creating zip archive..." -ForegroundColor Cyan
    Compress-Archive -Path "$DepsDir\*" -DestinationPath $packagePath -CompressionLevel Optimal -Force
    
    $size = (Get-Item $packagePath).Length / 1MB
    Write-Host "[OK] Package created successfully" -ForegroundColor Green
    Write-Host "  Size: $([Math]::Round($size, 2)) MB" -ForegroundColor Gray
    Write-Host ""
    
    # Verify package contents
    Write-Host "[VERIFY] Checking package contents..." -ForegroundColor Cyan
    $archive = [System.IO.Compression.ZipFile]::OpenRead($packagePath)
    $fileCount = $archive.Entries.Count
    $archive.Dispose()
    
    Write-Host "  Files in archive: $fileCount" -ForegroundColor Gray
    Write-Host "[OK] Package verification passed" -ForegroundColor Green
    Write-Host ""
    
    # Summary
    Write-Host "============================================================================" -ForegroundColor Green
    Write-Host "SUCCESS! Dependencies packaged" -ForegroundColor Green
    Write-Host "============================================================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Package location: $packagePath" -ForegroundColor Cyan
    Write-Host "Package name: $packageName" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "1. Create a GitHub release (e.g., v1.0.0)" -ForegroundColor White
    Write-Host "2. Upload this zip file as a release asset" -ForegroundColor White
    Write-Host "3. Update workflows to download this asset" -ForegroundColor White
    Write-Host ""
    Write-Host "Note: You only need to upload this once per dependencies version" -ForegroundColor Gray
    Write-Host "CI workflows will download and extract it automatically" -ForegroundColor Gray
    Write-Host ""
}
catch {
    Write-Host "[ERROR] Failed to create package: $_" -ForegroundColor Red
    exit 1
}


# ============================================================================
# AnimeFlow Build Script
# ============================================================================
# This script builds the AnimeFlow application
# ============================================================================

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    
    [switch]$SkipRestore,
    [switch]$Clean
)

$ErrorActionPreference = "Stop"

$ScriptDir = $PSScriptRoot
$Level3Dir = Split-Path -Parent $ScriptDir
$ProjectDir = Join-Path $Level3Dir "AnimeFlow"
$ProjectFile = Join-Path $ProjectDir "AnimeFlow.csproj"

Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host "AnimeFlow Build Script" -ForegroundColor Cyan
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host ""

# Check if .NET 8 SDK is installed
Write-Host "[INFO] Checking for .NET 8 SDK..." -ForegroundColor Yellow

try {
    $dotnetVersion = dotnet --version
    Write-Host "[OK] .NET SDK version: $dotnetVersion" -ForegroundColor Green
}
catch {
    Write-Host "[ERROR] .NET SDK not found!" -ForegroundColor Red
    Write-Host "Please install .NET 8 SDK from: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Clean if requested
if ($Clean) {
    Write-Host "[INFO] Cleaning previous build..." -ForegroundColor Yellow
    dotnet clean "$ProjectFile" --configuration $Configuration
    Write-Host "[OK] Clean complete" -ForegroundColor Green
    Write-Host ""
}

# Restore packages
if (-not $SkipRestore) {
    Write-Host "[INFO] Restoring NuGet packages..." -ForegroundColor Yellow
    dotnet restore "$ProjectFile"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[ERROR] Package restore failed" -ForegroundColor Red
        exit 1
    }
    Write-Host "[OK] Packages restored" -ForegroundColor Green
    Write-Host ""
}

# Build
Write-Host "[INFO] Building AnimeFlow ($Configuration)..." -ForegroundColor Yellow
dotnet build "$ProjectFile" --configuration $Configuration --no-restore

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "============================================================================" -ForegroundColor Green
    Write-Host "BUILD SUCCESS!" -ForegroundColor Green
    Write-Host "============================================================================" -ForegroundColor Green
    Write-Host ""
    
    $outputDir = Join-Path $ProjectDir "bin\$Configuration\net8.0-windows"
    Write-Host "Output directory: $outputDir" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "To run the application:" -ForegroundColor Yellow
    Write-Host "  cd `"$outputDir`"" -ForegroundColor White
    Write-Host "  .\AnimeFlow.exe" -ForegroundColor White
    Write-Host ""
}
else {
    Write-Host ""
    Write-Host "============================================================================" -ForegroundColor Red
    Write-Host "BUILD FAILED!" -ForegroundColor Red
    Write-Host "============================================================================" -ForegroundColor Red
    Write-Host ""
    exit 1
}

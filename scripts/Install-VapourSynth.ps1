# Install VapourSynth System-Wide for mpv Integration
# This script helps install VapourSynth so mpv can use RIFE interpolation

Write-Host "╔═══════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║   VapourSynth Installation for RIFE Support      ║" -ForegroundColor Cyan
Write-Host "╚═══════════════════════════════════════════════════╝" -ForegroundColor Cyan

$ErrorActionPreference = "Stop"

# Step 1: Download VapourSynth Installer
Write-Host "`nStep 1: Downloading VapourSynth..." -ForegroundColor Yellow
$vsUrl = "https://github.com/vapoursynth/vapoursynth/releases/download/R70/VapourSynth64-R70.exe"
$vsInstaller = "$env:TEMP\VapourSynth-R70.exe"

try {
    Invoke-WebRequest -Uri $vsUrl -OutFile $vsInstaller -UseBasicParsing
    Write-Host "[OK] Downloaded VapourSynth installer" -ForegroundColor Green
}
catch {
    Write-Host "[ERROR] Download failed. Please download manually from:" -ForegroundColor Red
    Write-Host "  https://github.com/vapoursynth/vapoursynth/releases" -ForegroundColor White
    Write-Host "`nAfter downloading, run:" -ForegroundColor Yellow
    Write-Host "  1. Install VapourSynth (default location)" -ForegroundColor Gray
    Write-Host "  2. Run this script again with -SkipDownload" -ForegroundColor Gray
    exit 1
}

# Step 2: Install VapourSynth
Write-Host "`nStep 2: Installing VapourSynth..." -ForegroundColor Yellow
Write-Host "Please follow the installer prompts..." -ForegroundColor Gray

Start-Process -FilePath $vsInstaller -Wait -NoNewWindow

# Step 3: Verify Installation
Write-Host "`nStep 3: Verifying installation..." -ForegroundColor Yellow

$vsSystemPath = "C:\Program Files\VapourSynth"
if (Test-Path $vsSystemPath) {
    Write-Host "[OK] VapourSynth installed at: $vsSystemPath" -ForegroundColor Green
}
else {
    Write-Host "[WARNING] VapourSynth not found in default location" -ForegroundColor Yellow
    $vsSystemPath = Read-Host "Enter VapourSynth installation path"
}

# Step 4: Copy DLLs to mpv
Write-Host "`nStep 4: Copying VapourSynth DLLs to mpv..." -ForegroundColor Yellow

$projectRoot = Split-Path -Parent $PSScriptRoot
$mpvDir = Join-Path $projectRoot "Dependencies\mpv"

$dllsToCopy = @(
    "VapourSynth.dll",
    "VSScript.dll"
)

foreach ($dll in $dllsToCopy) {
    $sourceDll = Join-Path $vsSystemPath $dll
    if (Test-Path $sourceDll) {
        Copy-Item $sourceDll -Destination $mpvDir -Force
        Write-Host "  [OK] Copied $dll" -ForegroundColor Green
    }
    else {
        Write-Host "  [WARNING] $dll not found" -ForegroundColor Yellow
    }
}

# Step 5: Test Integration
Write-Host "`nStep 5: Testing VapourSynth integration..." -ForegroundColor Yellow

$pythonExe = "C:\Users\$env:USERNAME\AppData\Local\Programs\Python\Python38\python.exe"

if (Test-Path $pythonExe) {
    $testResult = & $pythonExe -c "import vapoursynth as vs; import vsrife; print('[OK] All components ready')" 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host $testResult -ForegroundColor Green
    }
    else {
        Write-Host "[ERROR] Python test failed" -ForegroundColor Red
    }
}

Write-Host "`n╔═══════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║   Installation Complete!                         ║" -ForegroundColor Green
Write-Host "╚═══════════════════════════════════════════════════╝" -ForegroundColor Green

Write-Host "`nNext steps:" -ForegroundColor Cyan
Write-Host "1. Restart AnimeFlow" -ForegroundColor White
Write-Host "2. Load a video" -ForegroundColor White
Write-Host "3. Enable interpolation" -ForegroundColor White
Write-Host "4. RIFE AI interpolation should now work!" -ForegroundColor White


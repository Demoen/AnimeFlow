# Package Dependencies for AnimeFlow v2
# Includes VapourSynth R73 DLLs and RIFE AI setup instructions

param(
    [string]$Version = "v2",
    [string]$OutputDir = "release-artifacts"
)

$ErrorActionPreference = "Stop"

Write-Host "`n╔════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║   Packaging AnimeFlow Dependencies v2             ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════╝" -ForegroundColor Cyan

# Create output directory
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

# Create temporary packaging directory
$tempDir = "temp_package"
if (Test-Path $tempDir) {
    Remove-Item $tempDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $tempDir | Out-Null

Write-Host "`n📦 Copying Dependencies..." -ForegroundColor Yellow

# Copy main dependencies
$sourceDeps = "..\Dependencies"
$destDeps = "$tempDir\Dependencies"

if (-not (Test-Path $sourceDeps)) {
    Write-Host "[ERROR] Dependencies folder not found!" -ForegroundColor Red
    exit 1
}

# Copy entire Dependencies folder
Write-Host "  • Copying mpv..." -ForegroundColor White
Copy-Item -Path "$sourceDeps\mpv" -Destination "$destDeps\mpv" -Recurse -Force

Write-Host "  • Copying tools (yt-dlp)..." -ForegroundColor White
Copy-Item -Path "$sourceDeps\tools" -Destination "$destDeps\tools" -Recurse -Force

Write-Host "  • Copying RIFE models..." -ForegroundColor White
Copy-Item -Path "$sourceDeps\rife" -Destination "$destDeps\rife" -Recurse -Force

Write-Host "  • Copying VapourSynth..." -ForegroundColor White
Copy-Item -Path "$sourceDeps\vapoursynth" -Destination "$destDeps\vapoursynth" -Recurse -Force

# Copy VapourSynth R73 DLLs to mpv directory (required for RIFE)
Write-Host "`n🔧 Configuring VapourSynth R73 DLLs for mpv..." -ForegroundColor Yellow
$mpvDir = "$destDeps\mpv"

# These DLLs need to be in mpv directory for RIFE to work
$vsDlls = @(
    "VSScript.dll",
    "VSScriptPython38.dll",
    "vapoursynth.dll",
    "python38.dll",
    "python3.dll"
)

foreach ($dll in $vsDlls) {
    $sourceDll = "$sourceDeps\mpv\$dll"
    if (Test-Path $sourceDll) {
        Copy-Item $sourceDll -Destination $mpvDir -Force
        Write-Host "  ✓ $dll" -ForegroundColor Green
    } else {
        Write-Host "  ⚠ $dll not found (may need manual setup)" -ForegroundColor Yellow
    }
}

# Create setup instructions
Write-Host "`n📝 Creating setup instructions..." -ForegroundColor Yellow

$setupInstructions = @'
# AnimeFlow Dependencies v2 - Setup Instructions

## What's Included
- mpv player with VapourSynth support
- VapourSynth R73 DLLs (pre-configured)
- RIFE models (anime-optimized)
- yt-dlp for YouTube streaming
- All required runtime DLLs

## IMPORTANT: Additional Setup Required for RIFE AI

### Prerequisites for RIFE AI Interpolation:
1. Python 3.8 - Download from: https://www.python.org/downloads/release/python-3810/
   During installation: Check "Add Python to PATH"
   
2. VapourSynth R73 - Download installer from:
   https://github.com/vapoursynth/vapoursynth/releases/download/R73/VapourSynth-x64-R73.exe
   Install with Python 3.8 support

3. Install Python Packages (after Python 3.8 + VapourSynth installed):

   # Install GPU PyTorch with CUDA 11.8
   python -m pip install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu118
   
   # Install VapourSynth Python package
   python -m pip install vapoursynth
   
   # Install vsrife (RIFE plugin)
   python -m pip install vsrife --no-deps
   python -m pip install numpy requests tqdm

### Quick Start (No RIFE Setup)
If you don't want RIFE AI and just want basic interpolation:
1. Extract all files
2. Run AnimeFlow.exe
3. Uses mpv's built-in GPU interpolation (still excellent quality!)

### With RIFE AI Setup
After completing the setup above:
1. Extract all files
2. Run AnimeFlow.exe
3. Load a video and enable interpolation
4. Enjoy AI-powered 60fps with RIFE!

## File Structure
Dependencies/
  mpv/                    # Video player + VapourSynth DLLs
  tools/                  # yt-dlp for YouTube support
  rife/                   # RIFE AI models
  vapoursynth/           # VapourSynth framework

## Troubleshooting

"Could not initialize VapourSynth scripting"
- Install VapourSynth R73 system-wide (see link above)
- Ensure Python 3.8 is installed and in PATH
- Run: python -m pip install vapoursynth vsrife

Python Import Errors
- Verify Python 3.8 is installed (not 3.9+)
- Install GPU PyTorch with CUDA support (see commands above)

Performance Issues
- Ensure GPU drivers are up to date
- Try lower quality presets in AnimeFlow
- 1080p content is automatically downscaled for real-time performance

## System Requirements
- Windows 10/11 (64-bit)
- .NET 8.0 Runtime
- NVIDIA GTX 1660 or better (RTX series recommended for RIFE)
- Python 3.8 (for RIFE AI)
- 8GB+ RAM
- 10GB free disk space

## Support
- Issues: https://github.com/Demoen/animeflow/issues
- README: https://github.com/Demoen/animeflow
'@

$setupInstructions += "`n`n---`nCreated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')`nVersion: $Version`n"
$setupInstructions | Out-File -FilePath "$tempDir\SETUP_INSTRUCTIONS.txt" -Encoding UTF8

# Create package
Write-Host "`n📦 Creating ZIP package..." -ForegroundColor Yellow
$zipName = "Dependencies-Windows-x64-$Version.zip"
$zipPath = Join-Path $OutputDir $zipName

Compress-Archive -Path "$tempDir\*" -DestinationPath $zipPath -Force

# Cleanup
Remove-Item $tempDir -Recurse -Force

$zipSize = (Get-Item $zipPath).Length / 1MB
Write-Host "`n✅ Package created successfully!" -ForegroundColor Green
Write-Host "   File: $zipPath" -ForegroundColor White
Write-Host "   Size: $([math]::Round($zipSize, 2)) MB" -ForegroundColor White

Write-Host "`n📋 Next Steps:" -ForegroundColor Cyan
Write-Host "1. Upload to GitHub Release with tag 'deps-$Version'" -ForegroundColor White
Write-Host "2. Update workflows to use: deps-$Version" -ForegroundColor White
Write-Host "3. Commit changes and push" -ForegroundColor White

Write-Host "`n✨ Done!" -ForegroundColor Green


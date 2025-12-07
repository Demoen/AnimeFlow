# ============================================================================
# AnimeFlow Dependency Downloader - Final Version
# ============================================================================

param(
    [switch]$SkipModels,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$ScriptDir = $PSScriptRoot
$Level3Dir = Split-Path -Parent $ScriptDir
$DepsDir = Join-Path $Level3Dir "Dependencies"

# ============================================================================
# URLS (Verified December 2025)
# ============================================================================

# mpv (dev) - Contains libmpv-2.dll required for C# wrapper
$MPV_DEV_URL = "https://github.com/zhongfly/mpv-winbuild/releases/download/2025-12-07-dbd7a90/mpv-dev-x86_64-20251207-git-dbd7a90.7z"

# mpv - Using zhongfly's Windows builds (release 2025-12-07)
$MPV_URL = "https://github.com/zhongfly/mpv-winbuild/releases/download/2025-12-07-dbd7a90/mpv-x86_64-20251207-git-dbd7a90.7z"

# RIFE - Using nihui's standalone RIFE (contains models)
$RIFE_STANDALONE_URL = "https://github.com/nihui/rife-ncnn-vulkan/releases/download/20221029/rife-ncnn-vulkan-20221029-windows.zip"

# RIFE VapourSynth Plugin - Required for real-time interpolation
$RIFE_VS_PLUGIN_URL = "https://github.com/HomeOfVapourSynthEvolution/VapourSynth-RIFE-ncnn-Vulkan/releases/download/r6/VapourSynth-RIFE-ncnn-Vulkan-r6-windows.7z"

# VapourSynth - Portable version for video processing
# Note: Using R65 as it's the last known stable portable build
# Check https://github.com/vapoursynth/vapoursynth/releases for updates
$VAPOURSYNTH_URL = "https://github.com/vapoursynth/vapoursynth/releases/download/R65/VapourSynth64-Portable-R65.7z"

# yt-dlp - YouTube downloader for URL playback
$YTDLP_URL = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe"

# VIVTC - For 60fps to 24fps decimation
$VIVTC_URL = "https://github.com/vapoursynth/vivtc/releases/download/R1/vivtc-r1.7z"

$MpvDir = Join-Path $DepsDir "mpv"
$VsDir = Join-Path $DepsDir "vapoursynth"
$ModelsDir = Join-Path $DepsDir "models"
$ToolsDir = Join-Path $DepsDir "tools"
$RifeDir = Join-Path $DepsDir "rife"
$PluginsDir = Join-Path $VsDir "vapoursynth64\plugins"
$TempDir = Join-Path $DepsDir "temp"

# Add type for HttpClient
Add-Type -AssemblyName System.Net.Http

Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host "AnimeFlow Dependency Downloader" -ForegroundColor Cyan
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host ""

# Find 7-Zip
$7zipExe = $null
$7zipPaths = @(
    "C:\Program Files\7-Zip\7z.exe",
    "C:\Program Files (x86)\7-Zip\7z.exe"
)

foreach ($path in $7zipPaths) {
    if (Test-Path $path) {
        $7zipExe = $path
        break
    }
}

if (-not $7zipExe) {
    try {
        $7zipCmd = Get-Command 7z -ErrorAction SilentlyContinue
        if ($7zipCmd) {
            $7zipExe = $7zipCmd.Source
        }
    }
    catch { }
}

if (-not $7zipExe) {
    Write-Host "[ERROR] 7-Zip not found!" -ForegroundColor Red
    Write-Host "Install from: https://www.7-zip.org/" -ForegroundColor Red
    exit 1
}

Write-Host "[OK] Found 7-Zip: $7zipExe" -ForegroundColor Green
Write-Host ""

# Create directories
$directories = @($DepsDir, $MpvDir, $VsDir, $ModelsDir, $ToolsDir, $RifeDir, $PluginsDir, $TempDir)
foreach ($dir in $directories) {
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
}

function Download-FileWithLogProgress {
    param(
        [string]$Url,
        [string]$OutputPath,
        [string]$Description
    )

    Write-Host "[DOWNLOAD] $Description..." -ForegroundColor Cyan
    Write-Host "  URL: $Url" -ForegroundColor Gray

    try {
        $client = New-Object System.Net.Http.HttpClient
        $client.Timeout = [TimeSpan]::FromMinutes(10)
        
        # Start download
        $responseTask = $client.GetAsync($Url, [System.Net.Http.HttpCompletionOption]::ResponseHeadersRead)
        $responseTask.Wait()
        $response = $responseTask.Result
        
        if (-not $response.IsSuccessStatusCode) {
            throw "HTTP Status: $($response.StatusCode)"
        }

        $totalBytes = $response.Content.Headers.ContentLength
        $contentStreamTask = $response.Content.ReadAsStreamAsync()
        $contentStreamTask.Wait()
        $contentStream = $contentStreamTask.Result
        
        $fileStream = [System.IO.File]::Create($OutputPath)
        
        $buffer = New-Object byte[] 8192
        $bytesRead = 0
        $totalRead = 0
        $lastReportPercent = -1

        while (($bytesRead = $contentStream.Read($buffer, 0, $buffer.Length)) -gt 0) {
            $fileStream.Write($buffer, 0, $bytesRead)
            $totalRead += $bytesRead
            
            if ($totalBytes -gt 0) {
                $percent = [Math]::Floor(($totalRead / $totalBytes) * 100)
                # Report every 10%
                if ($percent -ne $lastReportPercent -and $percent % 10 -eq 0) {
                    $mbRead = "{0:N1}" -f ($totalRead / 1MB)
                    $mbTotal = "{0:N1}" -f ($totalBytes / 1MB)
                    Write-Host "  $percent% ($mbRead MB / $mbTotal MB)" -ForegroundColor Gray
                    $lastReportPercent = $percent
                }
            }
        }

        $fileStream.Close()
        $contentStream.Close()
        $client.Dispose()
        
        Write-Host "  100% Download Complete" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Host "[ERROR] Download failed: $($_.Exception.Message)" -ForegroundColor Red
        if ($fileStream) { $fileStream.Close() }
        if (Test-Path $OutputPath) { Remove-Item $OutputPath }
        return $false
    }
}

function Extract-Archive {
    param(
        [string]$ArchivePath,
        [string]$DestinationPath,
        [string]$Description
    )

    Write-Host "[EXTRACT] $Description..." -ForegroundColor Cyan
    
    try {
        & $7zipExe x "$ArchivePath" -o"$DestinationPath" -y | Out-Null
        if ($LASTEXITCODE -eq 0) {
            Write-Host "[OK] Extracted successfully" -ForegroundColor Green
            return $true
        }
        else {
            Write-Host "[ERROR] Extraction failed" -ForegroundColor Red
            return $false
        }
    }
    catch {
        Write-Host "[ERROR] Extraction failed: $_" -ForegroundColor Red
        return $false
    }
}

# ============================================================================
# DOWNLOAD VAPOURSYNTH (Portable)
# ============================================================================

if (-not (Test-Path (Join-Path $VsDir "VapourSynth.dll")) -or $Force) {
    Write-Host "============================================================================" -ForegroundColor Cyan
    Write-Host "0/5 - Downloading VapourSynth Portable" -ForegroundColor Cyan
    Write-Host "============================================================================" -ForegroundColor Cyan

    $vsArchive = Join-Path $TempDir "vapoursynth.7z"
    
    if (Download-FileWithLogProgress -Url $VAPOURSYNTH_URL -OutputPath $vsArchive -Description "VapourSynth portable") {
        if (Extract-Archive -ArchivePath $vsArchive -DestinationPath $VsDir -Description "VapourSynth portable") {
            Write-Host "[OK] VapourSynth installed" -ForegroundColor Green
        }
    }
    Write-Host ""
}
else {
    Write-Host "[SKIP] VapourSynth already exists" -ForegroundColor Yellow
    Write-Host ""
}

# ============================================================================
# DOWNLOAD MPV
# ============================================================================

if (-not (Test-Path (Join-Path $MpvDir "mpv.exe")) -or $Force) {
    Write-Host "============================================================================" -ForegroundColor Cyan
    Write-Host "1/5 - Downloading mpv" -ForegroundColor Cyan
    Write-Host "============================================================================" -ForegroundColor Cyan

    $mpvArchive = Join-Path $TempDir "mpv.7z"
    
    if (Download-FileWithLogProgress -Url $MPV_URL -OutputPath $mpvArchive -Description "mpv player") {
        if (Extract-Archive -ArchivePath $mpvArchive -DestinationPath $MpvDir -Description "mpv player") {
            # Move contents from subfolder if needed
            $mpvSubfolder = Get-ChildItem $MpvDir -Directory | Select-Object -First 1
            if ($mpvSubfolder) {
                Get-ChildItem $mpvSubfolder.FullName | Move-Item -Destination $MpvDir -Force -ErrorAction SilentlyContinue
                Remove-Item $mpvSubfolder.FullName -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }
    Write-Host ""
}
else {
    Write-Host "[SKIP] mpv already exists" -ForegroundColor Yellow
    Write-Host ""
}

# ============================================================================
# DOWNLOAD MPV DEV (shared library)
# ============================================================================

if (-not (Test-Path (Join-Path $MpvDir "libmpv-2.dll")) -and -not (Test-Path (Join-Path $MpvDir "mpv-2.dll")) -or $Force) {
    Write-Host "============================================================================" -ForegroundColor Cyan
    Write-Host "2/5 - Downloading mpv shared library" -ForegroundColor Cyan
    Write-Host "============================================================================" -ForegroundColor Cyan

    $mpvDevArchive = Join-Path $TempDir "mpv-dev.7z"
    $mpvDevExtract = Join-Path $TempDir "mpv-dev"
    
    if (Download-FileWithLogProgress -Url $MPV_DEV_URL -OutputPath $mpvDevArchive -Description "mpv dev package") {
        if (Extract-Archive -ArchivePath $mpvDevArchive -DestinationPath $mpvDevExtract -Description "mpv dev package") {
            # Copy DLLs to mpv directory
            Get-ChildItem -Path $mpvDevExtract -Filter "*.dll" -Recurse | Copy-Item -Destination $MpvDir -Force
            # Also copy include/lib folders if needed for dev, but for runtime just DLL is enough
            Write-Host "[OK] Shared libraries copied to mpv directory" -ForegroundColor Green
        }
    }
    Write-Host ""
}

# ============================================================================
# DOWNLOAD YT-DLP
# ============================================================================

if (-not (Test-Path (Join-Path $ToolsDir "yt-dlp.exe")) -or $Force) {
    Write-Host "============================================================================" -ForegroundColor Cyan
    Write-Host "3/5 - Downloading yt-dlp" -ForegroundColor Cyan
    Write-Host "============================================================================" -ForegroundColor Cyan

    $ytdlpExe = Join-Path $ToolsDir "yt-dlp.exe"
    
    if (Download-FileWithLogProgress -Url $YTDLP_URL -OutputPath $ytdlpExe -Description "yt-dlp") {
        Write-Host "[OK] yt-dlp installed" -ForegroundColor Green
    }
    Write-Host ""
}
else {
    Write-Host "[SKIP] yt-dlp already exists" -ForegroundColor Yellow
    Write-Host ""
}

# ============================================================================
# DOWNLOAD VIVTC (for 60fps ivtc)
# ============================================================================

$vivtcUrl = "https://github.com/vapoursynth/vivtc/releases/download/R1/vivtc-r1.7z"
$vivtcArchive = Join-Path $TempDir "vivtc.7z"

if (-not (Test-Path (Join-Path $PluginsDir "vivtc.dll")) -or $Force) {
    Write-Host "============================================================================" -ForegroundColor Cyan
    Write-Host "4/5 - Downloading VIVTC Plugin" -ForegroundColor Cyan
    Write-Host "============================================================================" -ForegroundColor Cyan

    if (Download-FileWithLogProgress -Url $vivtcUrl -OutputPath $vivtcArchive -Description "VIVTC plugin") {
        if (-not (Test-Path $PluginsDir)) {
             New-Item -ItemType Directory -Path $PluginsDir -Force | Out-Null
        }
        
        Write-Host "[EXTRACT] VIVTC to plugins..." -ForegroundColor Cyan
        # Flatten structure: extract all dlls from archive to plugins dir
        & $7zipExe e "$vivtcArchive" -o"$PluginsDir" *.dll -r -y | Out-Null
        
        if (Test-Path (Join-Path $PluginsDir "vivtc.dll")) {
            Write-Host "[OK] VIVTC installed" -ForegroundColor Green
        } else {
            Write-Host "[ERROR] VIVTC extraction failed" -ForegroundColor Red
        }
    }
    Write-Host ""
}

# ============================================================================
# CHECK RIFE / COPY MODELS
# ============================================================================

$targetModelDir = Join-Path $ModelsDir "rife-v4.6"
$rifeStandaloneModels = Join-Path $RifeDir "rife-v4.6"
$rifeExe = Join-Path $RifeDir "rife-ncnn-vulkan.exe"

# If standalone exists, just extract models
if ((Test-Path $rifeExe) -and (-not $Force)) {
    Write-Host "[SKIP] RIFE standalone already exists" -ForegroundColor Yellow
}
else {
    Write-Host "============================================================================" -ForegroundColor Cyan
    Write-Host "5/5 - Downloading RIFE Standalone" -ForegroundColor Cyan
    Write-Host "============================================================================" -ForegroundColor Cyan

    $rifeArchive = Join-Path $TempDir "rife.zip"
    
    if (Download-FileWithLogProgress -Url $RIFE_STANDALONE_URL -OutputPath $rifeArchive -Description "RIFE standalone") {
        if (Extract-Archive -ArchivePath $rifeArchive -DestinationPath $RifeDir -Description "RIFE standalone") {
            # Move contents from subfolder if needed
            $rifeSubfolder = Get-ChildItem $RifeDir -Directory | Where-Object { $_.Name -match "rife-ncnn-vulkan" } | Select-Object -First 1
            if ($rifeSubfolder) {
                Write-Host "[INFO] Moving RIFE files from subfolder..." -ForegroundColor Gray
                Get-ChildItem $rifeSubfolder.FullName | Move-Item -Destination $RifeDir -Force -ErrorAction SilentlyContinue
                Remove-Item $rifeSubfolder.FullName -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }
    Write-Host ""
}

# Copy models from standalone if they exist
if (Test-Path $rifeStandaloneModels) {
    Write-Host "============================================================================" -ForegroundColor Cyan
    Write-Host "3/4 - Setting up RIFE Models" -ForegroundColor Cyan
    Write-Host "============================================================================" -ForegroundColor Cyan
    
    if (-not (Test-Path $targetModelDir) -or $Force) {
        Write-Host "[COPY] Extracting models from RIFE package..." -ForegroundColor Cyan
        Copy-Item -Path $rifeStandaloneModels -Destination $ModelsDir -Recurse -Force
        Write-Host "[OK] Models set up successfully" -ForegroundColor Green
    }
    else {
        Write-Host "[SKIP] Models already set up" -ForegroundColor Yellow
    }
    Write-Host ""
}

# ============================================================================
# DOWNLOAD RIFE VAPOURSYNTH PLUGIN (CRITICAL FOR INTERPOLATION!)
# ============================================================================

# Check if vsrife Python package is installed
$rifePluginExists = $false
try {
    $pythonCheck = python -c "import sys; import vsrife; print('OK')" 2>&1
    if ($pythonCheck -match "OK") {
        $rifePluginExists = $true
    }
} catch {
    $rifePluginExists = $false
}

if (-not $rifePluginExists -or $Force) {
    Write-Host "============================================================================" -ForegroundColor Cyan
    Write-Host "6/6 - Installing RIFE VapourSynth Plugin (REQUIRED!)" -ForegroundColor Cyan
    Write-Host "============================================================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  This plugin is CRITICAL for real-time interpolation!" -ForegroundColor Yellow
    Write-Host ""

    try {
        Write-Host "[INSTALL] Installing vsrife via pip..." -ForegroundColor Cyan
        
        # Install vsrife using pip (works reliably across all Python environments)
        python -m pip install --upgrade pip 2>&1 | Out-Null
        $installOutput = python -m pip install vsrife 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "[OK] vsrife installed successfully" -ForegroundColor Green
            
            # Verify installation
            $verifyOutput = python -c "import vsrife; print('vsrife version:', vsrife.__version__ if hasattr(vsrife, '__version__') else 'installed')" 2>&1
            if ($verifyOutput -match "vsrife") {
                Write-Host "[OK] Verified: $verifyOutput" -ForegroundColor Green
            }
            else {
                Write-Host "[WARNING] Installation succeeded but verification failed" -ForegroundColor Yellow
            }
        }
        else {
            Write-Host "[ERROR] pip install failed" -ForegroundColor Red
            Write-Host $installOutput -ForegroundColor Gray
        }
    }
    catch {
        Write-Host "[ERROR] Installation failed: $_" -ForegroundColor Red
    }
    Write-Host ""
}
else {
    Write-Host "[SKIP] RIFE VapourSynth plugin already installed" -ForegroundColor Yellow
    Write-Host ""
}

# ============================================================================
# CLEANUP
# ============================================================================

Write-Host "[INFO] Cleaning up..." -ForegroundColor Yellow
if (Test-Path $TempDir) {
    Remove-Item $TempDir -Recurse -Force -ErrorAction SilentlyContinue
}
Write-Host ""

# ============================================================================
# SUMMARY
# ============================================================================

Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host "Download Summary" -ForegroundColor Cyan
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host ""

$components = @(
    @{ Name = "mpv"; Path = Join-Path $MpvDir "mpv.exe" },
    @{ Name = "libmpv"; Path = Join-Path $MpvDir "mpv-2.dll" }, # Or libmpv-2.dll
    @{ Name = "RIFE"; Path = Join-Path $RifeDir "rife-ncnn-vulkan.exe" },
    @{ Name = "RIFE Models"; Path = Join-Path $ModelsDir "rife-v4.6" },
    @{ Name = "RIFE VS Plugin"; Check = "PythonImport"; Import = "vsrife" },
    @{ Name = "VIVTC Plugin"; Path = Join-Path $PluginsDir "VIVTC.dll" },
    @{ Name = "yt-dlp"; Path = Join-Path $ToolsDir "yt-dlp.exe" }
)

$allOk = $true
foreach ($component in $components) {
    $exists = $false
    
    if ($component.Check -eq "PythonImport") {
        # Check if Python package can be imported
        try {
            $importCheck = python -c "import $($component.Import); print('OK')" 2>&1
            $exists = ($importCheck -match "OK")
        } catch {
            $exists = $false
        }
    }
    elseif ($component.Name -eq "libmpv") {
        # Check for either dll name
        $exists = (Test-Path $component.Path) -or (Test-Path (Join-Path $MpvDir "libmpv-2.dll"))
    }
    else {
        $exists = Test-Path $component.Path
    }
    
    if ($exists) {
        Write-Host "[OK] $($component.Name)" -ForegroundColor Green
    }
    else {
        Write-Host "[MISSING] $($component.Name)" -ForegroundColor Red
        $allOk = $false
    }
}

Write-Host ""
Write-Host "Dependencies location: $DepsDir" -ForegroundColor Cyan
Write-Host ""

if ($allOk) {
    Write-Host "============================================================================" -ForegroundColor Green
    Write-Host "SUCCESS! All dependencies downloaded" -ForegroundColor Green
    Write-Host "============================================================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "✅ RIFE VapourSynth plugin is installed!" -ForegroundColor Green
    Write-Host "   Interpolation will work!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Now rebuild the application:" -ForegroundColor Yellow
    Write-Host "  cd ..\AnimeFlow" -ForegroundColor White
    Write-Host "  dotnet build --configuration Release" -ForegroundColor White
    Write-Host ""
    Write-Host "Then run:" -ForegroundColor Yellow
    Write-Host "  .\bin\Release\net8.0-windows\AnimeFlow.exe" -ForegroundColor White
    Write-Host ""
}
else {
    Write-Host "============================================================================" -ForegroundColor Yellow
    Write-Host "Some dependencies are missing" -ForegroundColor Yellow
    Write-Host "============================================================================" -ForegroundColor Yellow
    Write-Host ""
    
    # Check specifically for RIFE plugin
    $rifePluginExists = $false
    try {
        $pythonCheck = python -c "import vsrife; print('OK')" 2>&1
        $rifePluginExists = ($pythonCheck -match "OK")
    } catch {
        $rifePluginExists = $false
    }
    
    if (-not $rifePluginExists) {
        Write-Host "⚠️  CRITICAL: RIFE VapourSynth plugin is missing!" -ForegroundColor Red
        Write-Host "   Interpolation will NOT work without it!" -ForegroundColor Red
        Write-Host ""
        Write-Host "   Manual fix:" -ForegroundColor Yellow
        Write-Host "   python -m pip install vsrife" -ForegroundColor White
        Write-Host ""
    }
}

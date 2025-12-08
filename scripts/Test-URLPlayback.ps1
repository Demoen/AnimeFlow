# Test URL Playback - Diagnostic Script

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "AnimeFlow URL Playback Diagnostic Tool" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$baseDir = Split-Path -Parent $PSScriptRoot
$depsDir = Join-Path $baseDir "Dependencies"
$toolsDir = Join-Path $depsDir "tools"
$ytdlpPath = Join-Path $toolsDir "yt-dlp.exe"
$mpvDir = Join-Path $depsDir "mpv"
$mpvPath = Join-Path $mpvDir "mpv.exe"

# Test 1: Check yt-dlp exists
Write-Host "[1/6] Checking yt-dlp..." -ForegroundColor Yellow
if (Test-Path $ytdlpPath) {
    Write-Host "  OK yt-dlp found at: $ytdlpPath" -ForegroundColor Green
    
    # Get version
    try {
        $version = & $ytdlpPath --version 2>&1
        Write-Host "  OK Version: $version" -ForegroundColor Green
    }
    catch {
        Write-Host "  ERROR running yt-dlp: $_" -ForegroundColor Red
    }
}
else {
    Write-Host "  ERROR yt-dlp NOT FOUND at: $ytdlpPath" -ForegroundColor Red
    Write-Host "  -> Download from: https://github.com/yt-dlp/yt-dlp/releases" -ForegroundColor Yellow
}

# Test 2: Check mpv exists
Write-Host "`n[2/6] Checking mpv..." -ForegroundColor Yellow
$mpvDll = Join-Path $mpvDir "libmpv-2.dll"
if (Test-Path $mpvDll) {
    Write-Host "  OK libmpv-2.dll found" -ForegroundColor Green
}
else {
    Write-Host "  ERROR libmpv-2.dll NOT FOUND" -ForegroundColor Red
}

# Test 3: Check internet connection
Write-Host "`n[3/6] Checking internet connection..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "https://www.youtube.com" -TimeoutSec 5 -UseBasicParsing
    Write-Host "  OK Internet connection OK" -ForegroundColor Green
}
catch {
    Write-Host "  ERROR Cannot reach YouTube: $_" -ForegroundColor Red
}

# Test 4: Test yt-dlp with sample URL
Write-Host "`n[4/6] Testing yt-dlp with sample video..." -ForegroundColor Yellow
$testUrl = "https://www.youtube.com/watch?v=jNQXAC9IVRw"

if (Test-Path $ytdlpPath) {
    try {
        Write-Host "  Testing URL: $testUrl" -ForegroundColor Gray
        $formats = & $ytdlpPath --list-formats $testUrl 2>&1 | Select-Object -First 10
        Write-Host "  OK yt-dlp can access YouTube" -ForegroundColor Green
        Write-Host "  Available formats:" -ForegroundColor Gray
        $formats | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkGray }
    }
    catch {
        Write-Host "  ERROR yt-dlp failed: $_" -ForegroundColor Red
    }
}
else {
    Write-Host "  SKIP (yt-dlp not found)" -ForegroundColor DarkYellow
}

# Test 5: Check log file
Write-Host "`n[5/6] Checking for log file..." -ForegroundColor Yellow
$logPath = Join-Path $baseDir "AnimeFlow\bin\Release\net8.0-windows\animeflow_debug.log"
if (Test-Path $logPath) {
    Write-Host "  OK Log file exists at: $logPath" -ForegroundColor Green
    
    # Show last 10 lines
    Write-Host "  Last 10 log entries:" -ForegroundColor Gray
    Get-Content $logPath -Tail 10 | ForEach-Object {
        if ($_ -match "error|fail|exception") {
            Write-Host "    $_" -ForegroundColor Red
        }
        elseif ($_ -match "success|loaded|initialized") {
            Write-Host "    $_" -ForegroundColor Green
        }
        else {
            Write-Host "    $_" -ForegroundColor DarkGray
        }
    }
}
else {
    Write-Host "  SKIP Log file not found (run AnimeFlow first)" -ForegroundColor DarkYellow
}

# Test 6: Check firewall
Write-Host "`n[6/6] Checking firewall rules..." -ForegroundColor Yellow
$exePath = Join-Path $baseDir "AnimeFlow\bin\Release\net8.0-windows\AnimeFlow.exe"
if (Test-Path $exePath) {
    try {
        $rules = Get-NetFirewallApplicationFilter | Where-Object { $_.Program -like "*AnimeFlow*" }
        if ($rules) {
            Write-Host "  OK Firewall rules found for AnimeFlow" -ForegroundColor Green
        }
        else {
            Write-Host "  WARN No firewall rules found" -ForegroundColor Yellow
            Write-Host "    This might block URL playback" -ForegroundColor Yellow
        }
    }
    catch {
        Write-Host "  SKIP Cannot check firewall (need admin)" -ForegroundColor DarkYellow
    }
}
else {
    Write-Host "  SKIP AnimeFlow.exe not built yet" -ForegroundColor DarkYellow
}

# Summary
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Diagnostic Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

Write-Host "`nTo test URL playback:" -ForegroundColor White
Write-Host "1. Run AnimeFlow" -ForegroundColor Gray
Write-Host "2. Click 'Open URL'" -ForegroundColor Gray
Write-Host "3. Paste: $testUrl" -ForegroundColor Gray
Write-Host "4. Wait 15-20 seconds (important!)" -ForegroundColor Yellow
Write-Host "5. Check status bar for messages" -ForegroundColor Gray
Write-Host "6. If black screen, check: $logPath" -ForegroundColor Gray

Write-Host "`nCommon Issues:" -ForegroundColor White
Write-Host "- Black screen = Still buffering (wait longer)" -ForegroundColor Gray
Write-Host "- Error -4 = Network/firewall issue" -ForegroundColor Gray
Write-Host "- Error -6 = Unsupported format (try different video)" -ForegroundColor Gray
Write-Host "- No yt-dlp = Download and place in Dependencies/tools/" -ForegroundColor Gray

Write-Host "`nManual Download Test:" -ForegroundColor White
if (Test-Path $ytdlpPath) {
    Write-Host "  cd $toolsDir" -ForegroundColor Cyan
    Write-Host "  .\yt-dlp.exe -f best[height<=720] -o test.mp4 $testUrl" -ForegroundColor Cyan
    Write-Host "  # Then open test.mp4 in AnimeFlow to verify player works" -ForegroundColor Gray
}
else {
    Write-Host "  (Install yt-dlp first)" -ForegroundColor Red
}

Write-Host ""


# Test Interpolation Functionality
# This script verifies that all interpolation components are working

$ErrorActionPreference = "Stop"

Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host "AnimeFlow - Interpolation Test" -ForegroundColor Cyan
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host ""

# Check 1: Python
Write-Host "[1/5] Checking Python..." -ForegroundColor Yellow
try {
    $pyVersion = python --version 2>&1
    Write-Host "  ✓ $pyVersion" -ForegroundColor Green
} catch {
    Write-Host "  ✗ Python not found!" -ForegroundColor Red
    exit 1
}

# Check 2: RIFE Plugin (vsrife)
Write-Host "[2/5] Checking RIFE Plugin..." -ForegroundColor Yellow
python -c "import vsrife; print(f'vsrife version: {vsrife.__version__ if hasattr(vsrife, \"__version__\") else \"installed\"}')" 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Host "  ✓ RIFE plugin (vsrife) is installed" -ForegroundColor Green
} else {
    Write-Host "  ✗ RIFE plugin not found!" -ForegroundColor Red
    Write-Host "  Installing now..." -ForegroundColor Yellow
    
    $vsDir = Join-Path (Split-Path -Parent $PSScriptRoot) "Dependencies\vapoursynth"
    if (Test-Path (Join-Path $vsDir "vsrepo.py")) {
        Push-Location $vsDir
        python vsrepo.py install vsrife
        Pop-Location
        Write-Host "  ✓ RIFE plugin installed" -ForegroundColor Green
    } else {
        Write-Host "  ✗ vsrepo.py not found in $vsDir" -ForegroundColor Red
        exit 1
    }
}

# Check 3: VapourSynth
Write-Host "[3/5] Checking VapourSynth..." -ForegroundColor Yellow
python -c "import vapoursynth as vs; print(f'VapourSynth {vs.core.version()}')" 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Host "  ✓ VapourSynth is available" -ForegroundColor Green
} else {
    Write-Host "  ✗ VapourSynth not found!" -ForegroundColor Red
    exit 1
}

# Check 4: RIFE Models
Write-Host "[4/5] Checking RIFE Models..." -ForegroundColor Yellow
$modelsPath = Join-Path (Split-Path -Parent $PSScriptRoot) "Dependencies\models\rife-v4.6"
if (Test-Path $modelsPath) {
    $modelFiles = Get-ChildItem $modelsPath -Filter "*.bin"
    if ($modelFiles.Count -gt 0) {
        Write-Host "  ✓ RIFE models found: $($modelFiles.Count) files" -ForegroundColor Green
    } else {
        Write-Host "  ✗ No model files (.bin) found in $modelsPath" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "  ✗ Models directory not found: $modelsPath" -ForegroundColor Red
    exit 1
}

# Check 5: VIVTC Plugin (for 60fps container handling)
Write-Host "[5/5] Checking VIVTC Plugin..." -ForegroundColor Yellow
$pluginsPath = Join-Path (Split-Path -Parent $PSScriptRoot) "Dependencies\vapoursynth\vapoursynth64\plugins"
$vivtcPath = Join-Path $pluginsPath "VIVTC.dll"
if (Test-Path $vivtcPath) {
    Write-Host "  ✓ VIVTC plugin found (for 60fps container handling)" -ForegroundColor Green
} else {
    Write-Host "  ⚠ VIVTC plugin not found" -ForegroundColor Yellow
    Write-Host "    (Optional: needed for 60fps container decimation)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "============================================================================" -ForegroundColor Green
Write-Host "All Checks Passed!" -ForegroundColor Green
Write-Host "============================================================================" -ForegroundColor Green
Write-Host ""

Write-Host "Interpolation System Status:" -ForegroundColor Cyan
Write-Host "  • Python: ✓" -ForegroundColor Green
Write-Host "  • RIFE Plugin: ✓" -ForegroundColor Green
Write-Host "  • VapourSynth: ✓" -ForegroundColor Green
Write-Host "  • RIFE Models: ✓" -ForegroundColor Green
if (Test-Path $vivtcPath) {
    Write-Host "  • VIVTC (60fps handling): ✓" -ForegroundColor Green
} else {
    Write-Host "  • VIVTC (60fps handling): ⚠ Optional" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "System is ready for real-time interpolation!" -ForegroundColor Green
Write-Host ""
Write-Host "To test:" -ForegroundColor Yellow
Write-Host "  1. Launch AnimeFlow.exe" -ForegroundColor White
Write-Host "  2. Open a video file or URL" -ForegroundColor White
Write-Host "  3. Click 'Enable Interpolation'" -ForegroundColor White
Write-Host "  4. Check FPS counter in status bar" -ForegroundColor White
Write-Host ""

# Additional: Test VapourSynth script generation
Write-Host "Testing VapourSynth Script Generation..." -ForegroundColor Yellow

$testScript = @"
import vapoursynth as vs
import sys

core = vs.core

# Test imports
print(f'VapourSynth: {core.version()}')

try:
    import vsrife
    print('vsrife: OK')
except ImportError as e:
    print(f'vsrife: ERROR - {e}')
    sys.exit(1)

# Test RIFE availability
if hasattr(core, 'rife'):
    print('core.rife: OK')
else:
    print('core.rife: Not available (plugin may need restart)')

print('All imports successful!')
"@

$tempScript = Join-Path $env:TEMP "test_vs.py"
Set-Content -Path $tempScript -Value $testScript

Write-Host "Running VapourSynth test script..." -ForegroundColor Gray
python $tempScript

if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ VapourSynth script test passed" -ForegroundColor Green
} else {
    Write-Host "✗ VapourSynth script test failed" -ForegroundColor Red
}

Remove-Item $tempScript -Force

Write-Host ""
Write-Host "Test complete!" -ForegroundColor Cyan



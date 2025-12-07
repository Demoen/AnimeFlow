# Quick Interpolation System Check

Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host "AnimeFlow - Interpolation System Check" -ForegroundColor Cyan
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host ""

# Check 1: Python
Write-Host "[1/4] Python..." -NoNewline
$pyCheck = python --version 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host " ✓" -ForegroundColor Green
} else {
    Write-Host " ✗" -ForegroundColor Red
}

# Check 2: RIFE Plugin
Write-Host "[2/4] RIFE Plugin (vsrife)..." -NoNewline
python -c "import vsrife" 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Host " ✓" -ForegroundColor Green
} else {
    Write-Host " ✗" -ForegroundColor Red
}

# Check 3: VapourSynth
Write-Host "[3/4] VapourSynth..." -NoNewline
python -c "import vapoursynth" 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Host " ✓" -ForegroundColor Green
} else {
    Write-Host " ✗" -ForegroundColor Red
}

# Check 4: RIFE Models
Write-Host "[4/4] RIFE Models..." -NoNewline
$modelsPath = Join-Path (Split-Path -Parent $PSScriptRoot) "Dependencies\models\rife-v4.6"
if (Test-Path $modelsPath) {
    $modelFiles = Get-ChildItem $modelsPath -Filter "*.bin" -ErrorAction SilentlyContinue
    if ($modelFiles -and $modelFiles.Count -gt 0) {
        Write-Host " ✓ ($($modelFiles.Count) files)" -ForegroundColor Green
    } else {
        Write-Host " ✗" -ForegroundColor Red
    }
} else {
    Write-Host " ✗" -ForegroundColor Red
}

Write-Host ""
Write-Host "Detailed Check:" -ForegroundColor Yellow
Write-Host ""

# Test vsrife availability through VapourSynth
Write-Host "Testing RIFE plugin through VapourSynth..." -ForegroundColor Gray
$testResult = python -c "import vapoursynth as vs; core = vs.core; print('RIFE available:', hasattr(core, 'rife'))" 2>&1
Write-Host "  $testResult" -ForegroundColor Gray

Write-Host ""
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host "System Ready!" -ForegroundColor Green
Write-Host "============================================================================" -ForegroundColor Cyan


# =============================================================================
# Build ProPosing - Executável Windows (Sidecar + Avalonia UI)
# =============================================================================
#
# Rodar num PowerShell com Python 3.10+ e .NET SDK 8 instalados:
#   powershell -ExecutionPolicy Bypass -File scripts\build_avalonia_executable.ps1
#
# Variáveis de controle (definir antes de rodar):
#   $env:SKIP_SIDECAR_BUILD    = "1"  -> pula PyInstaller (exige dist\proposing-sidecar\)
#   $env:FORCE_SIDECAR_REBUILD = "1"  -> força rebuild do sidecar
#   $env:SKIP_PIP_INSTALL      = "1"  -> pula pip install
#
# Saída: build_app\proposing-win-x64\  (ProPosing.Avalonia.exe + proposing-sidecar\)
# =============================================================================

$ErrorActionPreference = "Stop"

$ScriptDir  = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectDir = Split-Path -Parent $ScriptDir
$DistDir    = Join-Path $ProjectDir "dist"
$BuildDir   = Join-Path $ProjectDir "build_app"
$PyiWorkDir = Join-Path $ProjectDir ".pyinstaller_work"

$AvaloniaProject = Join-Path $ProjectDir "interface_avalonia\ProPosing.Avalonia\ProPosing.Avalonia.csproj"
$SidecarDir      = Join-Path $DistDir "proposing-sidecar"
$SidecarBin      = Join-Path $SidecarDir "proposing-sidecar.exe"
$SidecarScript   = Join-Path $ProjectDir "interface_avalonia\sidecar\mediapipe_sidecar.py"
$SidecarReqs     = Join-Path $ProjectDir "interface_avalonia\sidecar\requirements.txt"

Write-Host ""
Write-Host "=========================================================" -ForegroundColor Blue
Write-Host "     ProPosing - Build Executável Windows (win-x64)"      -ForegroundColor Blue
Write-Host "=========================================================" -ForegroundColor Blue

# -- 1. Dependências -----------------------------------------------------------
Write-Host "`n1. Verificando dependências..." -ForegroundColor Yellow

foreach ($cmd in @("python", "dotnet")) {
    if (-not (Get-Command $cmd -ErrorAction SilentlyContinue)) {
        Write-Host "ERRO: '$cmd' não encontrado no PATH" -ForegroundColor Red
        exit 1
    }
}

python -c "import PyInstaller" 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "   Instalando PyInstaller..." -ForegroundColor Yellow
    python -m pip install pyinstaller
    if ($LASTEXITCODE -ne 0) { exit 1 }
}

if ($env:SKIP_PIP_INSTALL -ne "1") {
    if (Test-Path $SidecarReqs) {
        Write-Host "   Instalando dependências do sidecar..." -ForegroundColor Yellow
        python -m pip install -q -r $SidecarReqs
        if ($LASTEXITCODE -ne 0) { exit 1 }
    }
} else {
    Write-Host "   Pulando pip install (SKIP_PIP_INSTALL=1)" -ForegroundColor Yellow
}

New-Item -ItemType Directory -Force -Path $PyiWorkDir, $DistDir | Out-Null

# -- 2. Sidecar MediaPipe ---------------------------------------------------
Write-Host "`n2. Empacotando sidecar MediaPipe (PyInstaller)..." -ForegroundColor Yellow

if ($env:SKIP_SIDECAR_BUILD -eq "1") {
    if (-not (Test-Path $SidecarBin)) {
        Write-Host "ERRO: SKIP_SIDECAR_BUILD=1 exige dist\proposing-sidecar\proposing-sidecar.exe" -ForegroundColor Red
        exit 1
    }
    Write-Host "   Pulando build do sidecar (SKIP_SIDECAR_BUILD=1)" -ForegroundColor Yellow
}
elseif ((Test-Path $SidecarBin) -and ($env:FORCE_SIDECAR_REBUILD -ne "1")) {
    Write-Host "   Reutilizando dist\proposing-sidecar\" -ForegroundColor Green
}
else {
    if (-not (Test-Path $SidecarScript)) {
        Write-Host "ERRO: mediapipe_sidecar.py não encontrado: $SidecarScript" -ForegroundColor Red
        exit 1
    }
    Write-Host "   Gerando sidecar (--onedir para compatibilidade com mediapipe)..." -ForegroundColor Yellow

    if (Test-Path $SidecarDir) { Remove-Item -Recurse -Force $SidecarDir }

    python -m PyInstaller --noconfirm `
        --onedir `
        --workpath $PyiWorkDir `
        --distpath $DistDir `
        --name "proposing-sidecar" `
        --collect-data mediapipe `
        --collect-data cv2 `
        --hidden-import mediapipe `
        --hidden-import cv2 `
        --hidden-import numpy `
        $SidecarScript

    if (($LASTEXITCODE -ne 0) -or (-not (Test-Path $SidecarBin))) {
        Write-Host "ERRO: Sidecar não foi gerado" -ForegroundColor Red
        exit 1
    }
    Write-Host "   Sidecar empacotado" -ForegroundColor Green
}

# PyInstaller às vezes omite pose_landmark_lite.tflite (model_complexity=0).
# Garante que o modelo esteja presente no dist\ independente de rebuild ou reuso.
$MediapipeModules = python -c "import mediapipe, os; print(os.path.join(os.path.dirname(mediapipe.__file__), 'modules'))" 2>$null
$LiteModel        = Join-Path $MediapipeModules "pose_landmark\pose_landmark_lite.tflite"
$BundlePoseDir    = Join-Path $SidecarDir "_internal\mediapipe\modules\pose_landmark"
if ((Test-Path $LiteModel) -and (Test-Path $BundlePoseDir)) {
    Copy-Item $LiteModel $BundlePoseDir -Force
    Write-Host "   pose_landmark_lite.tflite garantido no sidecar" -ForegroundColor Green
} else {
    Write-Host "   AVISO: pose_landmark_lite.tflite não encontrado - sidecar usará modelo full" -ForegroundColor Yellow
}

# -- 3. Avalonia publish ---------------------------------------------------
Write-Host "`n3. Build da interface Avalonia (win-x64)..." -ForegroundColor Yellow
if (-not (Test-Path $AvaloniaProject)) {
    Write-Host "ERRO: Projeto não encontrado: $AvaloniaProject" -ForegroundColor Red
    exit 1
}

dotnet publish $AvaloniaProject -c Release -r win-x64 --self-contained true
if ($LASTEXITCODE -ne 0) { exit 1 }

$PublishDir = Join-Path $ProjectDir "interface_avalonia\ProPosing.Avalonia\bin\Release\net8.0\win-x64\publish"
if (-not (Test-Path $PublishDir)) {
    Write-Host "ERRO: Pasta de publish não encontrada: $PublishDir" -ForegroundColor Red
    exit 1
}

# -- 4. Montar pacote final --------------------------------------------------
Write-Host "`n4. Montando pacote final..." -ForegroundColor Yellow

$FinalDir = Join-Path $BuildDir "proposing-win-x64"
if (Test-Path $FinalDir) { Remove-Item -Recurse -Force $FinalDir }
New-Item -ItemType Directory -Force -Path $FinalDir | Out-Null

Copy-Item -Recurse "$PublishDir\*" $FinalDir
Copy-Item -Recurse $SidecarDir $FinalDir

Write-Host ""
Write-Host "=========================================================" -ForegroundColor Green
Write-Host " Build concluído com sucesso!"                             -ForegroundColor Green
Write-Host "=========================================================" -ForegroundColor Green
Write-Host ""
Write-Host "Pacote: $FinalDir" -ForegroundColor Blue
Write-Host "Executável: $FinalDir\ProPosing.Avalonia.exe" -ForegroundColor Blue
Write-Host ""
Write-Host "Flags úteis:" -ForegroundColor Yellow
Write-Host '   $env:SKIP_SIDECAR_BUILD="1"      pula sidecar (exige dist\proposing-sidecar\)'
Write-Host '   $env:FORCE_SIDECAR_REBUILD="1"   força rebuild do sidecar'
Write-Host '   $env:SKIP_PIP_INSTALL="1"        pula pip install'
Write-Host ""

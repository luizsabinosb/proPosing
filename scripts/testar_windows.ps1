# =============================================================================
# ProPosing - Teste rápido no Windows
# =============================================================================
# Roda o app direto do código-fonte, sem precisar empacotar (PyInstaller).
# Ideal para validar antes do build final.
#
# Pré-requisitos (instalar uma vez):
#   - Python 3.10+  https://www.python.org/downloads/  (marcar "Add python.exe to PATH")
#   - .NET SDK 8    https://dotnet.microsoft.com/download/dotnet/8.0
#   - Webcam conectada
#
# Como rodar:
#   powershell -ExecutionPolicy Bypass -File scripts\testar_windows.ps1
# =============================================================================

$ErrorActionPreference = "Stop"

$ScriptDir   = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectDir  = Split-Path -Parent $ScriptDir
$SidecarDir  = Join-Path $ProjectDir "interface_avalonia\sidecar"
$Reqs        = Join-Path $SidecarDir "requirements.txt"
$AppProject  = Join-Path $ProjectDir "interface_avalonia\ProPosing.Avalonia\ProPosing.Avalonia.csproj"

Write-Host ""
Write-Host "=============================================" -ForegroundColor Blue
Write-Host "   ProPosing - Teste Windows"                -ForegroundColor Blue
Write-Host "=============================================" -ForegroundColor Blue

# -- 1. Verificar pré-requisitos -----------------------------------------------
Write-Host "`n[1/3] Verificando pré-requisitos..." -ForegroundColor Yellow

$missing = @()
if (-not (Get-Command "python"  -ErrorAction SilentlyContinue)) { $missing += "Python 3.10+  -> https://www.python.org/downloads/" }
if (-not (Get-Command "dotnet"  -ErrorAction SilentlyContinue)) { $missing += ".NET SDK 8    -> https://dotnet.microsoft.com/download/dotnet/8.0" }

if ($missing.Count -gt 0) {
    Write-Host "`nERRO: faltam pré-requisitos:" -ForegroundColor Red
    $missing | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    Write-Host "`nInstale o que falta e rode o script novamente." -ForegroundColor Yellow
    exit 1
}

$pyVer = python -c "import sys; print(sys.version_info.major, sys.version_info.minor)"
Write-Host "   Python ok  ($pyVer)" -ForegroundColor Green

$dotnetVer = dotnet --version
Write-Host "   .NET ok    ($dotnetVer)" -ForegroundColor Green

# -- 2. Instalar dependências Python -------------------------------------------
Write-Host "`n[2/3] Instalando dependências Python..." -ForegroundColor Yellow

if (-not (Test-Path $Reqs)) {
    Write-Host "ERRO: requirements.txt não encontrado: $Reqs" -ForegroundColor Red
    exit 1
}

python -m pip install -q -r $Reqs
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERRO: falha no pip install" -ForegroundColor Red
    exit 1
}
Write-Host "   Dependências ok" -ForegroundColor Green

# -- 3. Iniciar o app em modo kiosk (sem tela de login) -----------------------
Write-Host "`n[3/3] Iniciando ProPosing..." -ForegroundColor Yellow
Write-Host ""
Write-Host "  Modo kiosk ativo — sem tela de login." -ForegroundColor Cyan
Write-Host "  Teclas: numeros 0-8 trocam a pose | Esc sai do fullscreen" -ForegroundColor Cyan
Write-Host ""
Write-Host "  (feche esta janela ou pressione Ctrl+C para encerrar)" -ForegroundColor DarkGray
Write-Host ""

$env:PROPOSING_KIOSK = "1"

dotnet run --project $AppProject

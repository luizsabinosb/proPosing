# =============================================================================
# ProPosing - Instalação completa (Windows)
# =============================================================================
# Instala pré-requisitos, faz o build completo e cria atalho no Desktop.
# Rodar uma única vez por máquina — depois é só usar o atalho.
#
# Como rodar (clicar com botão direito no arquivo e "Executar com PowerShell",
# ou abrir PowerShell na pasta do projeto e rodar):
#   powershell -ExecutionPolicy Bypass -File scripts\setup_windows.ps1
# =============================================================================

$ErrorActionPreference = "Stop"

$ScriptDir  = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectDir = Split-Path -Parent $ScriptDir
$Reqs       = Join-Path $ProjectDir "interface_avalonia\sidecar\requirements.txt"
$BuildScript= Join-Path $ScriptDir "build_avalonia_executable.ps1"
$AppDir     = Join-Path $ProjectDir "build_app\proposing-win-x64"
$AppExe     = Join-Path $AppDir "ProPosing.Avalonia.exe"
$Desktop    = [Environment]::GetFolderPath("Desktop")

function Refresh-Path {
    $env:Path = [System.Environment]::GetEnvironmentVariable("Path", "Machine") + ";" +
                [System.Environment]::GetEnvironmentVariable("Path", "User")
}

function Install-Via-Winget($PackageId, $Name) {
    Write-Host "   Instalando $Name via winget..." -ForegroundColor Yellow
    winget install --id $PackageId --silent --accept-package-agreements --accept-source-agreements
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERRO: falha ao instalar $Name automaticamente." -ForegroundColor Red
        Write-Host "Instale manualmente e rode o script novamente." -ForegroundColor Yellow
        exit 1
    }
    Refresh-Path
    Write-Host "   $Name instalado" -ForegroundColor Green
}

Write-Host ""
Write-Host "=============================================" -ForegroundColor Blue
Write-Host "   ProPosing - Instalação Windows"           -ForegroundColor Blue
Write-Host "=============================================" -ForegroundColor Blue

# -- 1. Pré-requisitos ---------------------------------------------------------
Write-Host "`n[1/4] Verificando pré-requisitos..." -ForegroundColor Yellow

# winget (vem com Windows 10 1709+ e Windows 11)
if (-not (Get-Command "winget" -ErrorAction SilentlyContinue)) {
    Write-Host ""
    Write-Host "ERRO: winget não encontrado." -ForegroundColor Red
    Write-Host "Instale manualmente e rode o script novamente:" -ForegroundColor Yellow
    Write-Host "  Python 3.10+  -> https://www.python.org/downloads/" -ForegroundColor Cyan
    Write-Host "                   (marcar 'Add python.exe to PATH')" -ForegroundColor Cyan
    Write-Host "  .NET SDK 8    -> https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Cyan
    exit 1
}

# Python
if (-not (Get-Command "python" -ErrorAction SilentlyContinue)) {
    Install-Via-Winget "Python.Python.3.10" "Python 3.10"
} else {
    Write-Host "   Python ok" -ForegroundColor Green
}

# .NET SDK 8
$dotnetOk = dotnet --list-sdks 2>$null | Where-Object { $_ -match "^8\." }
if (-not $dotnetOk) {
    Install-Via-Winget "Microsoft.DotNet.SDK.8" ".NET SDK 8"
} else {
    Write-Host "   .NET SDK 8 ok" -ForegroundColor Green
}

# -- 2. Dependências Python ----------------------------------------------------
Write-Host "`n[2/4] Instalando dependências Python (mediapipe, opencv...)..." -ForegroundColor Yellow
Write-Host "   (pode levar alguns minutos na primeira vez)" -ForegroundColor DarkGray

python -m pip install -q -r $Reqs
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERRO: falha no pip install" -ForegroundColor Red
    exit 1
}
python -m pip install -q pyinstaller
Write-Host "   Dependências Python ok" -ForegroundColor Green

# -- 3. Build completo ---------------------------------------------------------
Write-Host "`n[3/4] Gerando executável..." -ForegroundColor Yellow
Write-Host "   (primeira vez leva 10-15 min — vá tomar um café)" -ForegroundColor DarkGray
Write-Host ""

& $BuildScript
if ($LASTEXITCODE -ne 0) {
    Write-Host "`nERRO: build falhou. Veja o erro acima." -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $AppExe)) {
    Write-Host "ERRO: executável não encontrado após build: $AppExe" -ForegroundColor Red
    exit 1
}

# -- 4. Configurar e criar atalho no Desktop -----------------------------------
Write-Host "`n[4/4] Criando atalho no Desktop..." -ForegroundColor Yellow

# Ativa modo kiosk (fullscreen, sem login)
Set-Content -Path (Join-Path $AppDir "kiosk.mode") -Value "kiosk" -Encoding ASCII

# Atalho no Desktop
$Shortcut = Join-Path $Desktop "ProPosing.lnk"
$Shell = New-Object -ComObject WScript.Shell
$Link  = $Shell.CreateShortcut($Shortcut)
$Link.TargetPath       = $AppExe
$Link.WorkingDirectory = $AppDir
$Link.Description      = "ProPosing - Sala de Poses"
$Link.Save()

Write-Host "   Atalho criado: $Shortcut" -ForegroundColor Green

# Resumo final
Write-Host ""
Write-Host "=============================================" -ForegroundColor Green
Write-Host "   Instalação concluída!"                    -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Atalho 'ProPosing' criado no Desktop." -ForegroundColor Cyan
Write-Host "  Abra-o para iniciar (câmera + poses em fullscreen)." -ForegroundColor Cyan
Write-Host ""
Write-Host "  Teclas:" -ForegroundColor DarkGray
Write-Host "    0-8  trocar pose" -ForegroundColor DarkGray
Write-Host "    Esc  sair do fullscreen" -ForegroundColor DarkGray
Write-Host ""

$abrir = Read-Host "Deseja abrir o ProPosing agora? (S/N)"
if ($abrir -match "^[Ss]") {
    Start-Process $AppExe -WorkingDirectory $AppDir
}

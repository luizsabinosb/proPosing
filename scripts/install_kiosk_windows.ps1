# =============================================================================
# ProPosing - Instalação de kiosk (Windows)
# =============================================================================
# Configura a máquina da academia para rodar o ProPosing sem operador:
#   1. Cria o marcador kiosk.mode ao lado do .exe (ativa fullscreen + sem login)
#   2. Cria atalho na pasta Inicializar (app abre sozinho após reboot/login)
#   3. Desativa suspensão da máquina e do monitor (sala de poses fica sempre on)
#
# Rodar APÓS o build (build_avalonia_executable.ps1), apontando para a pasta
# do app se ela não estiver no local padrão:
#   powershell -ExecutionPolicy Bypass -File scripts\install_kiosk_windows.ps1
#   powershell ... -AppDir "C:\ProPosing"
#
# Para desfazer: apague kiosk.mode, o atalho em shell:startup e restaure as
# configurações de energia (powercfg /change standby-timeout-ac 30).
# =============================================================================

param(
    [string]$AppDir = ""
)

$ErrorActionPreference = "Stop"

if (-not $AppDir) {
    $ScriptDir  = Split-Path -Parent $MyInvocation.MyCommand.Path
    $ProjectDir = Split-Path -Parent $ScriptDir
    $AppDir     = Join-Path $ProjectDir "build_app\proposing-win-x64"
}

$Exe = Join-Path $AppDir "ProPosing.Avalonia.exe"
if (-not (Test-Path $Exe)) {
    Write-Host "ERRO: executável não encontrado: $Exe" -ForegroundColor Red
    Write-Host "Rode o build primeiro ou passe -AppDir com o caminho do app." -ForegroundColor Yellow
    exit 1
}

# 1. Marcador de kiosk — o app detecta este arquivo e abre fullscreen sem login.
Set-Content -Path (Join-Path $AppDir "kiosk.mode") -Value "kiosk" -Encoding ASCII
Write-Host "OK: kiosk.mode criado em $AppDir" -ForegroundColor Green

# 2. Atalho na pasta Inicializar do usuário atual.
$Startup  = [Environment]::GetFolderPath("Startup")
$Shortcut = Join-Path $Startup "ProPosing.lnk"
$Shell    = New-Object -ComObject WScript.Shell
$Link     = $Shell.CreateShortcut($Shortcut)
$Link.TargetPath       = $Exe
$Link.WorkingDirectory = $AppDir
$Link.Description      = "ProPosing - sala de poses"
$Link.Save()
Write-Host "OK: atalho de inicialização criado: $Shortcut" -ForegroundColor Green

# 3. Máquina e monitor sempre ligados (energia AC).
powercfg /change standby-timeout-ac 0
powercfg /change monitor-timeout-ac 0
Write-Host "OK: suspensão da máquina e do monitor desativadas" -ForegroundColor Green

Write-Host ""
Write-Host "Kiosk configurado. Reinicie a máquina para validar o ciclo completo:" -ForegroundColor Blue
Write-Host "boot -> login do Windows -> ProPosing abre sozinho em fullscreen." -ForegroundColor Blue
Write-Host "Dica: configure login automático do Windows (netplwiz) para boot 100% sem toque." -ForegroundColor Yellow

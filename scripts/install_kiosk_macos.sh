#!/bin/bash
# =============================================================================
# ProPosing - Instalação de kiosk (macOS) — uso em piloto/demonstração
# =============================================================================
# 1. Cria o marcador kiosk.mode dentro do .app (fullscreen + sem login)
# 2. Adiciona o app aos Itens de Início de Sessão (abre sozinho após login)
#
# Rodar após o build:  ./scripts/install_kiosk_macos.sh
# Desfazer: remova kiosk.mode do bundle e o item em Ajustes → Itens de Início.
# Obs.: desative a suspensão do Mac em Ajustes → Bateria/Energia manualmente.
# =============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
APP="${1:-$PROJECT_DIR/build_app/ProPosing.app}"

if [ ! -d "$APP/Contents/MacOS" ]; then
    echo "ERRO: app não encontrado: $APP"
    echo "Rode o build primeiro ou passe o caminho do .app como argumento."
    exit 1
fi

echo "kiosk" > "$APP/Contents/MacOS/kiosk.mode"
echo "OK: kiosk.mode criado no bundle"

osascript -e "tell application \"System Events\" to make login item at end with properties {path:\"$APP\", hidden:false}" > /dev/null
echo "OK: adicionado aos Itens de Início de Sessão"

echo ""
echo "Kiosk configurado. Faça logout/login (ou reinicie) para validar:"
echo "o ProPosing deve abrir sozinho em fullscreen, sem tela de login."

#!/bin/bash
# Roda ProPosing no macOS (modo desenvolvimento ou app empacotado)

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
AVALONIA_DIR="$PROJECT_DIR/interface_avalonia/ProPosing.Avalonia"
APP_BUNDLE="$PROJECT_DIR/build_app/ProPosing.app"

GREEN='\033[0;32m'
RED='\033[0;31m'
BLUE='\033[0;34m'
NC='\033[0m'

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "               ProPosing - Avalonia UI (macOS)"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

# App empacotado tem prioridade
if [ -d "$APP_BUNDLE" ]; then
    echo -e "${BLUE}Abrindo app empacotado...${NC}"
    open "$APP_BUNDLE"
    echo -e "${GREEN}✅ Aplicativo aberto: $APP_BUNDLE${NC}"
    exit 0
fi

if ! command -v dotnet >/dev/null 2>&1; then
    echo -e "${RED}❌ 'dotnet' não encontrado${NC}"; exit 1
fi

if [ ! -d "$AVALONIA_DIR" ]; then
    echo -e "${RED}❌ Projeto Avalonia não encontrado em $AVALONIA_DIR${NC}"; exit 1
fi

echo -e "${BLUE}Iniciando em modo desenvolvimento...${NC}"
cd "$AVALONIA_DIR"
exec dotnet run

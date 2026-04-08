#!/bin/bash
# =============================================================================
# Build ProPosing - Executável desktop (Backend + Avalonia UI)
# =============================================================================

set -e

GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
DIST_DIR="$PROJECT_DIR/dist"
BUILD_DIR="$PROJECT_DIR/build_app"
PYI_WORK_DIR="$PROJECT_DIR/.pyinstaller_work"
TARGET_RUNTIME="${TARGET_RUNTIME:-osx-arm64}"
FORCE_BACKEND_REBUILD="${FORCE_BACKEND_REBUILD:-0}"
PYINSTALLER_CLEAN="${PYINSTALLER_CLEAN:-0}"
SKIP_BACKEND_PIP_INSTALL="${SKIP_BACKEND_PIP_INSTALL:-0}"
SKIP_BACKEND_BUILD="${SKIP_BACKEND_BUILD:-0}"

AVALONIA_PROJECT="$PROJECT_DIR/interface_avalonia/ProPosing.Avalonia/ProPosing.Avalonia.csproj"

echo -e "${BLUE}"
echo "╔════════════════════════════════════════════════════════╗"
echo "║      ProPosing - Build Executável Avalonia            ║"
echo "╚════════════════════════════════════════════════════════╝"
echo -e "${NC}"

check_command() {
    if ! command -v "$1" >/dev/null 2>&1; then
        echo -e "${RED}❌ Comando '$1' não encontrado${NC}"
        exit 1
    fi
}

echo -e "\n${YELLOW}1. Verificando dependências...${NC}"
check_command "python3"
check_command "pip3"
check_command "dotnet"
check_command "curl"

if ! python3 -c "import PyInstaller" 2>/dev/null; then
    echo -e "${YELLOW}   Instalando PyInstaller...${NC}"
    pip3 install pyinstaller
fi

if [ "$SKIP_BACKEND_PIP_INSTALL" -eq 0 ]; then
    echo -e "${YELLOW}   Instalando dependências backend...${NC}"
    pip3 install -q -r "$PROJECT_DIR/backend/requirements.txt"
else
    echo -e "${YELLOW}   Pulando pip install do backend (SKIP_BACKEND_PIP_INSTALL=1)${NC}"
fi

echo -e "\n${YELLOW}2. Empacotando backend (PyInstaller)...${NC}"
cd "$PROJECT_DIR"
BACKEND_BIN="$DIST_DIR/proposing-backend"

if [ "$SKIP_BACKEND_BUILD" -eq 1 ]; then
    if [ ! -f "$BACKEND_BIN" ]; then
        echo -e "${RED}❌ SKIP_BACKEND_BUILD=1 exige dist/proposing-backend já existente${NC}"
        exit 1
    fi
    echo -e "${YELLOW}   Pulando build do backend (SKIP_BACKEND_BUILD=1)${NC}"
elif [ -f "$BACKEND_BIN" ] && [ "$FORCE_BACKEND_REBUILD" -eq 0 ]; then
    echo -e "${GREEN}   ✅ Reutilizando backend já empacotado em dist/proposing-backend${NC}"
else
    echo -e "${YELLOW}   Gerando backend com PyInstaller (pode demorar na primeira vez)...${NC}"
    mkdir -p "$PYI_WORK_DIR" "$DIST_DIR"

    if [ "$PYINSTALLER_CLEAN" -eq 1 ]; then
        rm -rf "$PYI_WORK_DIR" "$DIST_DIR" 2>/dev/null || true
        mkdir -p "$PYI_WORK_DIR" "$DIST_DIR"
        pyinstaller --clean --noconfirm \
            --workpath "$PYI_WORK_DIR" \
            --distpath "$DIST_DIR" \
            config/proposing_build.spec &
    else
        pyinstaller --noconfirm \
            --workpath "$PYI_WORK_DIR" \
            --distpath "$DIST_DIR" \
            config/proposing_build.spec &
    fi
    PYI_PID=$!
    START_TS=$(date +%s)
    while kill -0 "$PYI_PID" >/dev/null 2>&1; do
        NOW_TS=$(date +%s)
        ELAPSED=$((NOW_TS - START_TS))
        echo -e "${BLUE}   [PyInstaller] em andamento... ${ELAPSED}s${NC}"
        sleep 15
    done
    wait "$PYI_PID"

    if [ ! -f "$BACKEND_BIN" ]; then
        echo -e "${RED}❌ Backend não foi gerado${NC}"
        exit 1
    fi
    echo -e "${GREEN}   ✅ Backend empacotado${NC}"
fi

echo -e "\n${YELLOW}3. Build da interface Avalonia (${TARGET_RUNTIME})...${NC}"
if [ ! -f "$AVALONIA_PROJECT" ]; then
    echo -e "${RED}❌ Projeto Avalonia não encontrado: $AVALONIA_PROJECT${NC}"
    exit 1
fi

dotnet publish "$AVALONIA_PROJECT" \
    -c Release \
    -r "$TARGET_RUNTIME" \
    --self-contained true \
    /p:PublishSingleFile=true \
    /p:IncludeNativeLibrariesForSelfExtract=true

PUBLISH_DIR="$PROJECT_DIR/interface_avalonia/ProPosing.Avalonia/bin/Release/net8.0/$TARGET_RUNTIME/publish"
if [ ! -d "$PUBLISH_DIR" ]; then
    echo -e "${RED}❌ Pasta de publish não encontrada: $PUBLISH_DIR${NC}"
    exit 1
fi

echo -e "\n${YELLOW}4. Montando app final...${NC}"
mkdir -p "$BUILD_DIR"

if [[ "$TARGET_RUNTIME" == osx-* ]]; then
    FINAL_APP="$BUILD_DIR/ProPosing.app"
    rm -rf "$FINAL_APP" 2>/dev/null || true
    mkdir -p "$FINAL_APP/Contents/MacOS" "$FINAL_APP/Contents/Resources"

    cp -R "$PUBLISH_DIR/"* "$FINAL_APP/Contents/MacOS/"
    cp "$DIST_DIR/proposing-backend" "$FINAL_APP/Contents/MacOS/"

    cat > "$FINAL_APP/Contents/MacOS/proposing-launcher" << 'LAUNCHER'
#!/bin/bash
DIR="$(cd "$(dirname "$0")" && pwd)"

"$DIR/proposing-backend" &
BACKEND_PID=$!

for i in {1..40}; do
    if curl -s http://localhost:8000/health > /dev/null 2>&1; then
        break
    fi
    sleep 0.5
done

cleanup() {
    kill "$BACKEND_PID" 2>/dev/null || true
}
trap cleanup EXIT

"$DIR/ProPosing.Avalonia"
LAUNCHER

    cat > "$FINAL_APP/Contents/Info.plist" << 'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key>
  <string>ProPosing</string>
  <key>CFBundleDisplayName</key>
  <string>ProPosing</string>
  <key>CFBundleExecutable</key>
  <string>proposing-launcher</string>
  <key>CFBundleIdentifier</key>
  <string>com.proposing.app</string>
  <key>CFBundleVersion</key>
  <string>1.0.0</string>
  <key>CFBundleShortVersionString</key>
  <string>1.0.0</string>
  <key>LSMinimumSystemVersion</key>
  <string>12.0</string>
  <key>NSCameraUsageDescription</key>
  <string>ProPosing precisa de acesso à câmera para análise de poses.</string>
</dict>
</plist>
PLIST

    chmod +x "$FINAL_APP/Contents/MacOS/proposing-launcher"
    chmod +x "$FINAL_APP/Contents/MacOS/proposing-backend"
    chmod +x "$FINAL_APP/Contents/MacOS/ProPosing.Avalonia"
else
    FINAL_DIR="$BUILD_DIR/proposing-$TARGET_RUNTIME"
    rm -rf "$FINAL_DIR" 2>/dev/null || true
    mkdir -p "$FINAL_DIR"
    cp -R "$PUBLISH_DIR/"* "$FINAL_DIR/"
    cp "$DIST_DIR/proposing-backend" "$FINAL_DIR/"
fi

echo ""
echo -e "${GREEN}════════════════════════════════════════════════════════${NC}"
echo -e "${GREEN}✅ Build concluído com sucesso!${NC}"
echo -e "${GREEN}════════════════════════════════════════════════════════${NC}"
echo ""
if [[ "$TARGET_RUNTIME" == osx-* ]]; then
    echo -e "${BLUE}📦 Aplicativo: $FINAL_APP${NC}"
    echo "   open \"$FINAL_APP\""
else
    echo -e "${BLUE}📦 Pacote: $FINAL_DIR${NC}"
fi
echo -e "${YELLOW}Dicas: FORCE_BACKEND_REBUILD=1 força PyInstaller; PYINSTALLER_CLEAN=1 limpa cache antes do build.${NC}"
echo -e "${YELLOW}      SKIP_BACKEND_BUILD=1 pula PyInstaller (se dist/proposing-backend já existir).${NC}"
echo ""

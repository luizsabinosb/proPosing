#!/bin/bash
# =============================================================================
# Build ProPosing - Executável desktop (Sidecar + Avalonia UI)
# =============================================================================
#
# Variáveis de controle (exportar antes de rodar ou prefixar no comando):
#   TARGET_RUNTIME          osx-arm64 (padrão) | osx-x64 | win-x64
#   SKIP_BACKEND_BUILD      1 = pula PyInstaller do backend (exige dist/proposing-backend)
#   SKIP_SIDECAR_BUILD      1 = pula PyInstaller do sidecar (exige dist/proposing-sidecar/)
#   FORCE_BACKEND_REBUILD   1 = força rebuild do backend mesmo se dist/ existir
#   FORCE_SIDECAR_REBUILD   1 = força rebuild do sidecar mesmo se dist/ existir
#   PYINSTALLER_CLEAN       1 = passa --clean para o PyInstaller
#   SKIP_PIP_INSTALL        1 = pula pip install (quando deps já estão instaladas)
#
# =============================================================================

set -euo pipefail

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
SKIP_BACKEND_BUILD="${SKIP_BACKEND_BUILD:-0}"
SKIP_SIDECAR_BUILD="${SKIP_SIDECAR_BUILD:-0}"
FORCE_BACKEND_REBUILD="${FORCE_BACKEND_REBUILD:-0}"
FORCE_SIDECAR_REBUILD="${FORCE_SIDECAR_REBUILD:-0}"
PYINSTALLER_CLEAN="${PYINSTALLER_CLEAN:-0}"
SKIP_PIP_INSTALL="${SKIP_PIP_INSTALL:-0}"

AVALONIA_PROJECT="$PROJECT_DIR/interface_avalonia/ProPosing.Avalonia/ProPosing.Avalonia.csproj"
BACKEND_BIN="$DIST_DIR/proposing-backend"
SIDECAR_DIR="$DIST_DIR/proposing-sidecar"          # --onedir output
SIDECAR_BIN="$SIDECAR_DIR/proposing-sidecar"       # executável dentro do dir
SIDECAR_SCRIPT="$PROJECT_DIR/interface_avalonia/sidecar/mediapipe_sidecar.py"

echo -e "${BLUE}"
echo "╔════════════════════════════════════════════════════════╗"
echo "║      ProPosing - Build Executável Avalonia            ║"
echo "╚════════════════════════════════════════════════════════╝"
echo -e "${NC}"

# ── Helper: roda PyInstaller em background e exibe progresso ──────────────────
run_pyinstaller() {
    local label="$1"; shift
    "$@" &
    local PID=$!
    local START=$(date +%s)
    while kill -0 "$PID" 2>/dev/null; do
        echo -e "${BLUE}   [PyInstaller $label] em andamento... $(($(date +%s)-START))s${NC}"
        sleep 15
    done
    wait "$PID"
}

# ── 1. Dependências ────────────────────────────────────────────────────────────
echo -e "\n${YELLOW}1. Verificando dependências...${NC}"

for cmd in python3 pip3 dotnet; do
    if ! command -v "$cmd" &>/dev/null; then
        echo -e "${RED}❌ '$cmd' não encontrado${NC}"; exit 1
    fi
done

if ! python3 -c "import PyInstaller" 2>/dev/null; then
    echo -e "${YELLOW}   Instalando PyInstaller...${NC}"
    pip3 install pyinstaller
fi

if [ "$SKIP_PIP_INSTALL" -eq 0 ]; then
    echo -e "${YELLOW}   Instalando dependências backend...${NC}"
    pip3 install -q -r "$PROJECT_DIR/backend/requirements.txt"
else
    echo -e "${YELLOW}   Pulando pip install (SKIP_PIP_INSTALL=1)${NC}"
fi

PYINSTALLER_CLEAN_FLAG=""
[ "$PYINSTALLER_CLEAN" -eq 1 ] && PYINSTALLER_CLEAN_FLAG="--clean"

mkdir -p "$PYI_WORK_DIR" "$DIST_DIR"

# ── 2. Backend (FastAPI HTTP) ──────────────────────────────────────────────────
echo -e "\n${YELLOW}2. Empacotando backend (PyInstaller)...${NC}"

if [ "$SKIP_BACKEND_BUILD" -eq 1 ]; then
    [ ! -f "$BACKEND_BIN" ] && \
        { echo -e "${RED}❌ SKIP_BACKEND_BUILD=1 exige dist/proposing-backend${NC}"; exit 1; }
    echo -e "${YELLOW}   Pulando build do backend (SKIP_BACKEND_BUILD=1)${NC}"
elif [ -f "$BACKEND_BIN" ] && [ "$FORCE_BACKEND_REBUILD" -eq 0 ]; then
    echo -e "${GREEN}   ✅ Reutilizando dist/proposing-backend${NC}"
else
    echo -e "${YELLOW}   Gerando backend...${NC}"
    [ "$PYINSTALLER_CLEAN" -eq 1 ] && { rm -rf "$PYI_WORK_DIR" "$DIST_DIR"; mkdir -p "$PYI_WORK_DIR" "$DIST_DIR"; }
    run_pyinstaller "backend" pyinstaller $PYINSTALLER_CLEAN_FLAG --noconfirm \
        --workpath "$PYI_WORK_DIR" \
        --distpath "$DIST_DIR" \
        "$PROJECT_DIR/config/proposing_build.spec"
    [ ! -f "$BACKEND_BIN" ] && { echo -e "${RED}❌ Backend não foi gerado${NC}"; exit 1; }
    echo -e "${GREEN}   ✅ Backend empacotado${NC}"
fi

# ── 2b. Sidecar MediaPipe ──────────────────────────────────────────────────────
echo -e "\n${YELLOW}2b. Empacotando sidecar MediaPipe (PyInstaller)...${NC}"

if [ "$SKIP_SIDECAR_BUILD" -eq 1 ]; then
    [ ! -f "$SIDECAR_BIN" ] && \
        { echo -e "${RED}❌ SKIP_SIDECAR_BUILD=1 exige dist/proposing-sidecar/proposing-sidecar${NC}"; exit 1; }
    echo -e "${YELLOW}   Pulando build do sidecar (SKIP_SIDECAR_BUILD=1)${NC}"
elif [ -f "$SIDECAR_BIN" ] && [ "$FORCE_SIDECAR_REBUILD" -eq 0 ]; then
    echo -e "${GREEN}   ✅ Reutilizando dist/proposing-sidecar/${NC}"
else
    [ ! -f "$SIDECAR_SCRIPT" ] && \
        { echo -e "${RED}❌ mediapipe_sidecar.py não encontrado: $SIDECAR_SCRIPT${NC}"; exit 1; }
    echo -e "${YELLOW}   Gerando sidecar (--onedir para compatibilidade com mediapipe)...${NC}"

    # Remove sidecar anterior para evitar conflito
    rm -rf "$SIDECAR_DIR"

    run_pyinstaller "sidecar" pyinstaller $PYINSTALLER_CLEAN_FLAG --noconfirm \
        --onedir \
        --workpath "$PYI_WORK_DIR" \
        --distpath "$DIST_DIR" \
        --name "proposing-sidecar" \
        --collect-data mediapipe \
        --collect-data cv2 \
        --hidden-import mediapipe \
        --hidden-import cv2 \
        --hidden-import numpy \
        "$SIDECAR_SCRIPT"

    [ ! -f "$SIDECAR_BIN" ] && { echo -e "${RED}❌ Sidecar não foi gerado${NC}"; exit 1; }
    echo -e "${GREEN}   ✅ Sidecar empacotado${NC}"
fi

# ── 3. Avalonia publish ────────────────────────────────────────────────────────
echo -e "\n${YELLOW}3. Build da interface Avalonia (${TARGET_RUNTIME})...${NC}"
[ ! -f "$AVALONIA_PROJECT" ] && \
    { echo -e "${RED}❌ Projeto não encontrado: $AVALONIA_PROJECT${NC}"; exit 1; }

dotnet publish "$AVALONIA_PROJECT" \
    -c Release \
    -r "$TARGET_RUNTIME" \
    --self-contained true

PUBLISH_DIR="$PROJECT_DIR/interface_avalonia/ProPosing.Avalonia/bin/Release/net8.0/$TARGET_RUNTIME/publish"
[ ! -d "$PUBLISH_DIR" ] && \
    { echo -e "${RED}❌ Pasta de publish não encontrada: $PUBLISH_DIR${NC}"; exit 1; }

# ── 4. Montar .app ─────────────────────────────────────────────────────────────
echo -e "\n${YELLOW}4. Montando app final...${NC}"
mkdir -p "$BUILD_DIR"

if [[ "$TARGET_RUNTIME" == osx-* ]]; then
    FINAL_APP="$BUILD_DIR/ProPosing.app"
    rm -rf "$FINAL_APP"
    mkdir -p "$FINAL_APP/Contents/MacOS" "$FINAL_APP/Contents/Resources"

    # Avalonia binaries
    cp -R "$PUBLISH_DIR/"* "$FINAL_APP/Contents/MacOS/"

    # Backend
    cp "$BACKEND_BIN" "$FINAL_APP/Contents/MacOS/"

    # Sidecar (--onedir: copia o diretório inteiro)
    cp -R "$SIDECAR_DIR" "$FINAL_APP/Contents/MacOS/"

    # Ícone
    ICNS_SRC="$PROJECT_DIR/interface_avalonia/ProPosing.Avalonia/Assets/Logo/proposing.icns"
    if [ -f "$ICNS_SRC" ]; then
        cp "$ICNS_SRC" "$FINAL_APP/Contents/Resources/AppIcon.icns"
        echo -e "${GREEN}   ✅ Ícone copiado${NC}"
    else
        echo -e "${YELLOW}   ⚠️  proposing.icns não encontrado${NC}"
    fi

    # Launcher — inicia backend em background (não bloqueante) e depois o app
    cat > "$FINAL_APP/Contents/MacOS/proposing-launcher" << 'LAUNCHER'
#!/bin/bash
DIR="$(cd "$(dirname "$0")" && pwd)"

# Inicia backend em background; continua mesmo se falhar
"$DIR/proposing-backend" &
BACKEND_PID=$!

# Aguarda até 10s pelo backend; se não subir, prossegue mesmo assim
for i in $(seq 1 20); do
    if curl -s http://localhost:8000/health >/dev/null 2>&1; then
        break
    fi
    # Se o processo já morreu, não adianta esperar mais
    if ! kill -0 "$BACKEND_PID" 2>/dev/null; then
        break
    fi
    sleep 0.5
done

cleanup() {
    kill "$BACKEND_PID" 2>/dev/null || true
}
trap cleanup EXIT

exec "$DIR/ProPosing.Avalonia"
LAUNCHER

    # Info.plist
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
  <key>CFBundleIconFile</key>
  <string>AppIcon</string>
  <key>LSMinimumSystemVersion</key>
  <string>12.0</string>
  <key>NSCameraUsageDescription</key>
  <string>ProPosing precisa de acesso à câmera para análise de poses.</string>
  <key>NSHighResolutionCapable</key>
  <true/>
</dict>
</plist>
PLIST

    chmod +x "$FINAL_APP/Contents/MacOS/proposing-launcher"
    chmod +x "$FINAL_APP/Contents/MacOS/proposing-backend"
    chmod +x "$FINAL_APP/Contents/MacOS/proposing-sidecar/proposing-sidecar"
    chmod +x "$FINAL_APP/Contents/MacOS/ProPosing.Avalonia"

else
    FINAL_DIR="$BUILD_DIR/proposing-$TARGET_RUNTIME"
    rm -rf "$FINAL_DIR"
    mkdir -p "$FINAL_DIR"
    cp -R "$PUBLISH_DIR/"* "$FINAL_DIR/"
    cp "$BACKEND_BIN" "$FINAL_DIR/"
    cp -R "$SIDECAR_DIR" "$FINAL_DIR/"
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
echo ""
echo -e "${YELLOW}Flags úteis:${NC}"
echo "   SKIP_BACKEND_BUILD=1      pula backend  (exige dist/proposing-backend)"
echo "   SKIP_SIDECAR_BUILD=1      pula sidecar  (exige dist/proposing-sidecar/)"
echo "   FORCE_BACKEND_REBUILD=1   força rebuild do backend"
echo "   FORCE_SIDECAR_REBUILD=1   força rebuild do sidecar"
echo "   SKIP_PIP_INSTALL=1        pula pip install"
echo "   PYINSTALLER_CLEAN=1       limpa cache do PyInstaller"
echo ""

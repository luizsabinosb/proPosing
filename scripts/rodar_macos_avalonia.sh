#!/bin/bash

# --- Script para rodar ProPosing com interface Avalonia no macOS ---

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
BACKEND_DIR="$PROJECT_DIR/backend"
AVALONIA_DIR="$PROJECT_DIR/interface_avalonia/ProPosing.Avalonia"
BACKEND_LOG="$PROJECT_DIR/.backend_macos.log"
BACKEND_PID_FILE="$PROJECT_DIR/.backend_macos_pid"
AVALONIA_PID_FILE="$PROJECT_DIR/.avalonia_macos_pid"
APP_BUNDLE="$PROJECT_DIR/build_app/ProPosing.app"

GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
BLUE='\033[0;34m'
NC='\033[0m'

STARTED_BACKEND=0

log_message() { echo -e "${BLUE}[$(date '+%H:%M:%S')]${NC} $1"; }
log_success() { echo -e "${GREEN}✅ $1${NC}"; }
log_warning() { echo -e "${YELLOW}⚠️  $1${NC}"; }
log_error() { echo -e "${RED}❌ $1${NC}"; }

check_command() {
    if ! command -v "$1" >/dev/null 2>&1; then
        log_error "Comando '$1' não encontrado."
        exit 1
    fi
}

check_backend_health() {
    curl -s http://localhost:8000/health >/dev/null 2>&1
}

start_backend() {
    log_message "Iniciando backend..."
    (
        cd "$BACKEND_DIR" || exit 1
        pip3 install -q -r requirements.txt 2>/dev/null || true
        python3 -m uvicorn app.main:app --host 0.0.0.0 --port 8000 > "$BACKEND_LOG" 2>&1 &
        echo $! > "$BACKEND_PID_FILE"
    )
    STARTED_BACKEND=1

    for i in {1..30}; do
        if check_backend_health; then
            log_success "Backend respondeu em ${i}s"
            return 0
        fi
        sleep 1
    done

    log_error "Backend não respondeu após 30 segundos. Verifique $BACKEND_LOG"
    return 1
}

cleanup() {
    if [ -f "$AVALONIA_PID_FILE" ]; then
        AVALONIA_PID=$(cat "$AVALONIA_PID_FILE")
        kill "$AVALONIA_PID" 2>/dev/null || true
        rm -f "$AVALONIA_PID_FILE"
    fi

    if [ "$STARTED_BACKEND" -eq 1 ] && [ -f "$BACKEND_PID_FILE" ]; then
        BACKEND_PID=$(cat "$BACKEND_PID_FILE")
        kill "$BACKEND_PID" 2>/dev/null || true
        rm -f "$BACKEND_PID_FILE"
        log_success "Backend encerrado"
    fi
}

trap cleanup SIGINT SIGTERM

clear
echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "                 🚀 ProPosing - Avalonia UI (macOS)"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

check_command "python3"
check_command "pip3"

if [ -d "$APP_BUNDLE" ]; then
    log_message "Executando app empacotado..."
    open "$APP_BUNDLE"
    log_success "Aplicativo aberto: $APP_BUNDLE"
    exit 0
fi

check_command "dotnet"

if [ ! -d "$AVALONIA_DIR" ]; then
    log_error "Projeto Avalonia não encontrado em $AVALONIA_DIR"
    exit 1
fi

if check_backend_health; then
    log_success "Backend já está rodando"
else
    log_warning "Backend não está rodando. Iniciando..."
    start_backend || exit 1
fi

log_message "Iniciando Avalonia em modo desenvolvimento..."
cd "$AVALONIA_DIR" || exit 1
dotnet run &
AVALONIA_PID=$!
echo "$AVALONIA_PID" > "$AVALONIA_PID_FILE"

sleep 2
if ps -p "$AVALONIA_PID" >/dev/null 2>&1; then
    log_success "Interface Avalonia iniciada (PID: $AVALONIA_PID)"
    echo ""
    echo "🔧 Backend: http://localhost:8000"
    echo "📚 Docs: http://localhost:8000/docs"
    echo "💡 Para parar, pressione Ctrl+C"
    echo ""
    wait "$AVALONIA_PID"
else
    log_error "Falha ao iniciar a interface Avalonia."
fi

cleanup

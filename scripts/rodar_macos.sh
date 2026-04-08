#!/bin/bash

# Compatibilidade: entrada padrão de desktop agora usa Avalonia.
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
exec "$SCRIPT_DIR/rodar_macos_avalonia.sh"

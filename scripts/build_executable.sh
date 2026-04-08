#!/bin/bash

# Compatibilidade: build padrão agora usa Avalonia.
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
exec "$SCRIPT_DIR/build_avalonia_executable.sh"

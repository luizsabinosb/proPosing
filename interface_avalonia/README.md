# ProPosing Avalonia UI

Nova interface desktop (macOS/Windows/Linux) em Avalonia UI.

## Requisitos

- .NET SDK 8+
- Python 3.10+ (backend)

## Rodar em desenvolvimento (macOS)

```bash
./scripts/rodar_macos_avalonia.sh
```

## Build executável

```bash
./scripts/build_avalonia_executable.sh
```

Por padrão gera `build_app/ProPosing.app` (macOS arm64).  
Para outro runtime:

```bash
TARGET_RUNTIME=win-x64 ./scripts/build_avalonia_executable.sh
TARGET_RUNTIME=linux-x64 ./scripts/build_avalonia_executable.sh
```

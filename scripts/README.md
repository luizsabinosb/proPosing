# Scripts de Automacao

Todos os scripts devem ser executados a partir da raiz do projeto.

## Execucao

- `./scripts/rodar_macos.sh`: sobe backend e interface Avalonia no macOS (atalho padrão).
- `./scripts/rodar_macos_avalonia.sh`: sobe backend e interface Avalonia no macOS.
- `./scripts/rodar_web.sh`: sobe backend (se necessario) e interface web.
- `./scripts/parar_projeto.sh`: encerra backend e interface Avalonia.
- `./scripts/parar_projeto_avalonia.sh`: encerra backend e interface Avalonia.
- `./scripts/iniciar_backend.sh`: inicia somente o backend.

## Build e limpeza

- `./scripts/build_executable.sh`: gera app empacotado (atalho para build Avalonia).
- `./scripts/build_avalonia_executable.sh`: gera app empacotado Avalonia + backend.
- `./scripts/limpar_flutter_macos.sh`: utilitário legado para limpeza de artefatos Flutter (web/mobile).

## Boas praticas

- Logs e PIDs ficam fora do versionamento.
- Novos scripts devem manter nomes em `snake_case.sh`.
- Preferir scripts idempotentes e com mensagens claras de erro.

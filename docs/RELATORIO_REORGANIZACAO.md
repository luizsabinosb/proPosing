# Relatório de Reorganização do Projeto

**Data:** Abril 2026
**Auditado por:** Análise automatizada
**Status:** Aguardando aprovação para execução

---

## Resumo Executivo

O projeto ocupa **~1,7 GB** no disco, dos quais **~1,73 GB (99%)** são artefatos de build completamente regeneráveis. O código-fonte real ocupa menos de **200 KB**. Além disso, há arquivos órfãos, logs stale e uma entrada de `__pycache__` na raiz com fontes já deletados.

---

## 1. Limpeza — Arquivos e Pastas a Remover

### 1a. Artefatos de build regeneráveis (~1,73 GB total)

Todos esses diretórios são criados automaticamente ao rodar o build. Não há nenhum risco em apagá-los.

| Caminho | Tamanho | Regenerado por |
|---------|---------|----------------|
| `.pyinstaller_work/` | 436 MB | `pyinstaller config/proposing_build.spec` |
| `dist/` | 398 MB | `pyinstaller config/proposing_build.spec` |
| `build_app/` | 508 MB | `scripts/build_avalonia_executable.sh` |
| `interface_avalonia/ProPosing.Avalonia/bin/` | 380 MB | `dotnet build` |
| `interface_avalonia/ProPosing.Avalonia/obj/` | 8,6 MB | `dotnet build` |

**Liberação total estimada: ~1,73 GB**

### 1b. Caches Python (`__pycache__/`) — Regeneráveis

Gerados automaticamente ao importar qualquer módulo Python. Podem ser deletados a qualquer momento.

```
__pycache__/                      ← raiz (contém .pyc de fontes JÁ DELETADOS — veja 1c)
proposing/__pycache__/
backend/app/__pycache__/
backend/app/core/__pycache__/
backend/app/api/__pycache__/
backend/app/api/v1/__pycache__/
backend/app/models/__pycache__/
treinamento/__pycache__/
```

### 1c. `__pycache__` órfão na raiz — Problema específico

O `__pycache__/` na raiz do projeto contém `.pyc` de módulos cujos **arquivos-fonte foram deletados** em alguma refatoração anterior:

```
BodyVision.cpython-310.pyc        ← fonte: BodyVision.py (DELETADO)
camera_utils.cpython-310.pyc      ← fonte: camera_utils.py (DELETADO)
data_collector.cpython-310.pyc    ← migrado para proposing/ (DUPLICATA OBSOLETA)
export_training_data.cpython-310.pyc ← migrado para treinamento/ (DUPLICATA OBSOLETA)
ml_evaluator.cpython-310.pyc      ← migrado para proposing/ (DUPLICATA OBSOLETA)
pose_evaluator.cpython-310.pyc    ← migrado para proposing/ (DUPLICATA OBSOLETA)
text_renderer.cpython-310.pyc     ← fonte não localizado (DELETADO)
ui_helpers.cpython-310.pyc        ← fonte não localizado (DELETADO)
ui_renderer.cpython-310.pyc       ← fonte não localizado (DELETADO)
```

Esses `.pyc` são resquícios de uma versão anterior onde os módulos viviam na raiz. **São totalmente inofensivos** mas confirmam que a refatoração para `proposing/` já aconteceu.

### 1d. Logs e arquivos temporários

| Arquivo | Tamanho | Motivo |
|---------|---------|--------|
| `.backend.log` | 854 B | Log de execução antigo |
| `.backend_macos.log` | 108 KB | Log de execução antigo |
| `.git/index.lock` | 0 B | Lock file stale do Git (sem operação ativa) |
| `100` | 0 B | Arquivo vazio na raiz, sem histórico no Git |

### 1e. Arquivos `.DS_Store` (macOS metadata)

Presentes em 5 locais dentro do projeto. São gerados automaticamente pelo Finder e não têm utilidade no repositório.

---

## 2. Reorganização de Estrutura

### 2a. Mover `test_api.py` para pasta dedicada

O arquivo `backend/test_api.py` está no mesmo nível do código de produção. A convenção é isolá-lo:

```
ANTES:
  backend/
  ├── test_api.py        ← solto na raiz do backend
  └── app/

DEPOIS:
  backend/
  ├── tests/
  │   └── test_api.py    ← isolado em pasta de testes
  └── app/
```

### 2b. Estrutura proposta final do projeto

Após a limpeza, a árvore essencial do projeto ficaria:

```
proPosing/
├── backend/
│   ├── app/
│   │   ├── __init__.py
│   │   ├── main.py
│   │   ├── api/v1/pose.py
│   │   ├── core/cv_service.py
│   │   └── models/pose.py
│   ├── tests/
│   │   └── test_api.py
│   ├── requirements.txt
│   └── run_standalone.py
├── proposing/              ← biblioteca de lógica de negócio
│   ├── __init__.py
│   ├── data_collector.py
│   ├── ml_evaluator.py
│   ├── pose_evaluator.py
│   └── pose_metrics_loader.py
├── treinamento/            ← utilitários de ML (separado)
│   ├── README.md
│   ├── README_TREINAMENTO_AVANCADO.md
│   ├── consolidate_training_data.py
│   ├── export_training_data.py
│   ├── image_processor.py
│   ├── process_pose_info.py
│   ├── train_model.py
│   └── web_scraper.py
├── ml/
│   ├── data/
│   ├── models/
│   └── pose_info/
├── config/
│   └── proposing_build.spec
├── scripts/
│   ├── build_avalonia_executable.sh
│   ├── iniciar_backend.sh
│   ├── parar_projeto_avalonia.sh
│   └── rodar_macos_avalonia.sh
├── interface_avalonia/     ← C# Avalonia UI
│   ├── ProPosing.Avalonia.sln
│   └── ProPosing.Avalonia/
│       ├── *.cs / *.axaml
│       └── (bin/ e obj/ NÃO commitados)
└── docs/
    ├── REPO_ORGANIZATION.md
    └── RELATORIO_REORGANIZACAO.md
```

---

## 3. Melhorias no `.gitignore`

O `.gitignore` atual cobre pouco. Adicionando as entradas abaixo, os artefatos deixam de ser commitados acidentalmente:

```gitignore
# Build artifacts — Python
dist/
build_app/
.pyinstaller_work/
__pycache__/
*.py[cod]
*.pyo

# Build artifacts — .NET
interface_avalonia/**/bin/
interface_avalonia/**/obj/

# Logs e temporários
*.log
.*.log
*.tmp

# macOS
.DS_Store
**/.DS_Store

# Git
.git/index.lock
```

---

## 4. Resumo das Ações — O que Aprovar

| # | Ação | Reversível? | Espaço liberado |
|---|------|-------------|-----------------|
| 1 | Deletar `.pyinstaller_work/` | Sim (rebuild) | 436 MB |
| 2 | Deletar `dist/` | Sim (rebuild) | 398 MB |
| 3 | Deletar `build_app/` | Sim (rebuild) | 508 MB |
| 4 | Deletar `interface_avalonia/.../bin/` | Sim (rebuild) | 380 MB |
| 5 | Deletar `interface_avalonia/.../obj/` | Sim (rebuild) | 8,6 MB |
| 6 | Deletar todos os `__pycache__/` | Sim (auto-gerado) | < 1 MB |
| 7 | Deletar `.backend.log`, `.backend_macos.log` | Não (log perdido) | 109 KB |
| 8 | Deletar `.git/index.lock` | Sim (git recria) | < 1 KB |
| 9 | Deletar arquivo `100` | Não (mas era vazio) | 0 |
| 10 | Deletar `.DS_Store` (5 arquivos) | Sim (macOS recria) | < 50 KB |
| 11 | Mover `backend/test_api.py` → `backend/tests/` | Sim (git mv) | — |
| 12 | Atualizar `.gitignore` | Sim | — |

**Espaço total liberado: ~1,73 GB**

---

## O que NÃO será alterado

- Nenhum arquivo de código-fonte (`.py`, `.cs`, `.axaml`)
- Nenhum arquivo de configuração (`proposing_build.spec`, `.csproj`, `.sln`)
- Nenhum asset de ML (`ml/pose_info/`, `ml/models/`)
- Nenhum script de build (`.sh`)
- Nenhuma documentação existente

---

*Para executar as ações acima, confirme no chat e as mudanças serão aplicadas.*

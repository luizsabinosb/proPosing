# ProPosing

Sistema de análise de poses de fisiculturismo em tempo real, com avaliação geométrica baseada em landmarks 3D do MediaPipe.

A interface roda 100% nativa em desktop (Avalonia / .NET 8) e a estimativa de pose é feita por um sidecar Python (MediaPipe) embarcado no bundle final — sem servidor HTTP, sem dependências de runtime na máquina do usuário.

---

## Arquitetura

```
┌─────────────────────────────────┐         stdin (frames JPEG)
│   Avalonia UI  (.NET 8 / C#)    │ ──────────────────────────► ┌──────────────────────────┐
│                                 │                             │  MediaPipe Sidecar       │
│  • OpenCvSharp captura câmera   │                             │  (Python / PyInstaller)  │
│  • Pipeline de inferência       │                             │  • mediapipe.solutions.pose
│  • Renderização do esqueleto    │ ◄────────────────────────── │  • emite landmarks JSON  │
│  • Avaliação geométrica das     │         stdout (landmarks)  │                          │
│    poses (PoseEvaluator*)       │                             └──────────────────────────┘
│  • UI: feedback + métricas      │
└─────────────────────────────────┘
```

- **Avalonia UI**: tudo que o usuário vê e interage. Faz captura de câmera, gerencia o ciclo de inferência (uma chamada a cada N frames), renderiza o esqueleto sobreposto e roda a avaliação geométrica em C#.
- **Sidecar MediaPipe**: processo filho lançado pela UI. Recebe frames pelo `stdin`, devolve landmarks normalizados pelo `stdout`. PyInstaller (`--onedir`) congela todas as dependências Python em um bundle dentro do `.app`.

A comunicação é binária + JSON (sem rede). O sidecar é o único componente Python — toda a lógica de avaliação foi reescrita em C#.

---

## Stack

| Camada | Tecnologia |
|---|---|
| UI / desktop | Avalonia 11.1, .NET 8, CommunityToolkit.Mvvm |
| Captura de câmera | OpenCvSharp 4.13 (AVFoundation no macOS) |
| Estimativa de pose | MediaPipe `solutions.pose`, model_complexity=0 (Lite) |
| Empacotamento sidecar | PyInstaller (`--onedir`) |
| Build do app | `dotnet publish` self-contained + bundle `.app` |
| Plataforma alvo | macOS arm64 (primário), Windows x64 e Linux x64 também publicáveis |

---

## Estrutura do repositório

```
proPosing/
├── interface_avalonia/
│   ├── ProPosing.Avalonia/                      # Projeto C# Avalonia
│   │   ├── Views/                               # MainWindow, LoginWindow, controles (skeleton overlay)
│   │   ├── ViewModels/                          # MainWindowViewModel, LoginViewModel
│   │   ├── Models/                              # LandmarkPoint, PoseFeedback, PoseMetrics, PipelineUpdate, PoseOption
│   │   ├── Services/                            # CameraPipelineService, MediaPipeSidecar, AppConfig
│   │   ├── Evaluation/                          # ★ Lógica de avaliação de poses
│   │   │   ├── GeometryHelper.cs                #   Ângulos/distâncias aspect-corrected, body-yaw, tilt, twist
│   │   │   ├── PoseContext.cs                   #   Contexto por frame (escala, orientação, métricas derivadas)
│   │   │   ├── PoseThresholds.cs                #   Thresholds (em ratios torso-normalizados)
│   │   │   ├── PoseEvaluatorRegistry.cs         #   Roteia para o avaliador por pose
│   │   │   ├── PoseFeedbackSmoother.cs          #   Histerese de status + EMA + tightness
│   │   │   ├── SymmetryMetric.cs                #   Score de simetria ponderado por confiança
│   │   │   └── Poses/                           #   Um IPoseEvaluator por pose (10 poses)
│   │   ├── Converters/                          # StatusToBrush, StatusToLabel
│   │   ├── Assets/                              # Styles (design tokens), fontes, imagens de referência
│   │   ├── Evaluation/Poses/                    # 10 avaliadores
│   │   ├── pose_thresholds.json                 # Configuração de thresholds carregada em runtime
│   │   └── ProPosing.Avalonia.csproj
│   ├── sidecar/
│   │   ├── mediapipe_sidecar.py                 # Sidecar Python (stdin/stdout)
│   │   └── requirements.txt
│   └── ProPosing.Avalonia.sln
│
├── scripts/
│   ├── build_avalonia_executable.sh             # Build completo: sidecar + Avalonia → .app
│   ├── rodar_macos_avalonia.sh                  # Atalho para abrir o .app (ou rodar via dotnet)
│   └── run_training_pipeline.sh                 # Pipeline de treinamento ML (futuro)
│
├── ml/                                          # Material de referência por pose
│   ├── pose_info/                               # Texto e imagens de referência
│   ├── data/                                    # Dados coletados para treinamento
│   └── models/                                  # Modelos .pkl (futuro)
│
├── treinamento/                                 # Scripts Python para coleta/processamento ML
├── docs/
├── build_app/                                   # Saída do build: ProPosing.app
└── README.md                                    # Este arquivo
```

---

## Pipeline de avaliação de poses

Para cada frame, o serviço de pipeline:

1. Captura via OpenCV (1280×720), reduz pela metade para inferência.
2. Envia o JPEG ao sidecar; recebe 33 landmarks normalizados (`x, y` em `[0,1]`, `z` em metros vs. mid-hip, `visibility` em `[0,1]`).
3. Constrói um `PoseContext` com:
   - **Aspect ratio** real do frame — sem isso, ângulos calculados em coordenadas normalizadas ficam distorcidos em frames não quadrados.
   - **Escala**: `ShoulderSpan`, `TorsoLength`, `HipSpan` — todas aspect-corrected.
   - **Orientação**: `BodyYawDeg` calculado pela projeção do vetor entre ombros no plano x-z (não depende de visibilidade do nariz).
   - **`BodyView`**: `Front` / `ThreeQuarter` / `Side` / `Back` (classificação automática a partir do yaw).
   - **`LateralTiltDeg`**: ângulo do tronco em relação à vertical.
   - **`TorsoTwistDeg`**: ângulo entre linha dos ombros e linha dos quadris (relevante para tea cup / poses oblíquas).
4. Roteia para o `IPoseEvaluator` correspondente, que retorna `PoseFeedback` (status + dicas).
5. O `PoseFeedbackSmoother` aplica:
   - Histerese de status (3 frames consecutivos para trocar de cor),
   - EMA nas métricas numéricas,
   - Cálculo de **tightness** a partir do desvio-padrão recente do twist e tilt.
6. Métricas adicionais (`PoseMetrics`) são calculadas pelo registry: V-taper, simetria ponderada, yaw, tilt, twist.

### Decisões de projeto importantes

- **Tudo aspect-corrected**: `Angle2D`/`Angle3D` recebem `aspect = width/height`. Sem isso, um cotovelo em flexão de 90° real lê ~85° em câmeras 16:9.
- **Thresholds em ratios torso-normalizados**: tolerâncias de altura/distância são expressas como fração do torso, não como valores absolutos em coordenadas normalizadas. Resultado: o usuário pode se aproximar ou afastar da câmera sem invalidar os thresholds.
- **Seleção de braço/perna por profundidade (Z)**: em poses laterais (Side Chest, Side Triceps, Quarter Turn), a discriminação "braço da frente vs. de trás" usa Z, não X. Em perfil verdadeiro, X dos dois ombros é praticamente igual e flutua com ruído — Z é estável.
- **Sem dependência da visibilidade facial para detectar orientação**: o yaw geométrico substitui a heurística antiga "se o nariz está visível, o usuário está de frente".

---

## Poses suportadas

Teclas `0`–`9` alternam a pose ativa.

| # | Modo | Descrição |
|---|---|---|
| 0 | `enquadramento` | Verifica se o usuário está centralizado e a uma distância adequada |
| 1 | `double_biceps` | Duplo bíceps frontal |
| 2 | `side_chest` | Side chest |
| 3 | `side_triceps` | Side triceps |
| 4 | `most_muscular` | Most muscular |
| 5 | `quarter_turn_side` | Quarter turn (perfil 90°) |
| 6 | `front_lat_spread` | Front lat spread |
| 7 | `back_lat_spread` | Back lat spread |
| 8 | `abs_and_thighs` | Abs and thighs |
| 9 | `teacup` | Tea cup (3/4 oblíquo) |

---

## Pré-requisitos

| Ferramenta | Uso | Necessário para |
|---|---|---|
| .NET SDK 8+ | UI Avalonia | Desenvolver e rodar |
| Python 3.10+ | Sidecar MediaPipe | Build do app empacotado |
| `pip3` | Dependências Python | Build do app empacotado |
| PyInstaller | Empacota sidecar | Build do app empacotado (instalado automaticamente pelo script) |

> O usuário final do `.app` empacotado **não** precisa de Python nem de .NET — tudo está auto-contido no bundle.

---

## Como rodar

### Modo desenvolvimento (mais rápido para iterar)

```bash
dotnet run --project interface_avalonia/ProPosing.Avalonia/ProPosing.Avalonia.csproj
```

Roda direto a UI Avalonia. **Sem o sidecar**: a janela abre normalmente, mas a detecção de pose não funcionará. Use este modo para iterar em UI/avaliação geométrica com landmarks fakes ou sem feedback ao vivo.

### App empacotado (produção)

Build:

```bash
./scripts/build_avalonia_executable.sh
```

O script faz:
1. Empacota o sidecar Python via PyInstaller (`dist/proposing-sidecar/`)
2. Publica a UI Avalonia em modo Release self-contained
3. Monta o bundle final em `build_app/ProPosing.app` (no macOS) ou `build_app/proposing-<runtime>/` em outras plataformas

Abrir:

```bash
open build_app/ProPosing.app                # macOS
./scripts/rodar_macos_avalonia.sh           # atalho que prefere o .app, cai para dotnet run
```

#### Flags úteis

| Variável | Efeito |
|---|---|
| `SKIP_SIDECAR_BUILD=1` | Reutiliza `dist/proposing-sidecar/` existente (rebuild rápido só da UI) |
| `SKIP_PIP_INSTALL=1` | Pula `pip install` se as deps já estão instaladas |
| `FORCE_SIDECAR_REBUILD=1` | Força rebuild do sidecar mesmo com `dist/` presente |
| `PYINSTALLER_CLEAN=1` | Passa `--clean` para o PyInstaller |
| `TARGET_RUNTIME=...` | `osx-arm64` (default), `osx-x64`, `win-x64` |

Iteração típica em desenvolvimento de UI/avaliação após o primeiro build:

```bash
SKIP_SIDECAR_BUILD=1 SKIP_PIP_INSTALL=1 ./scripts/build_avalonia_executable.sh
```

---

## Configuração

### Thresholds de avaliação

Editáveis em `interface_avalonia/ProPosing.Avalonia/pose_thresholds.json`. O arquivo é copiado para o lado do executável no build. Se o arquivo estiver ausente, o app usa os defaults compilados em `PoseThresholds.cs`.

Convenções de naming:

- `*_min_angle` / `*_max_angle` — graus
- `*_ratio` — fração do torso ou do shoulder span (ex.: `elbow_drop_ratio_max: 0.06` = 6% do torso)
- `*_max_deg` — ângulos derivados (ex.: `torso_tilt_max_deg`)

Recarregue o app após editar.

### Configurações de runtime

`AppConfig` (em `Services/AppConfig.cs`) expõe:
- `CameraIndex` — qual câmera usar
- `TargetFps` — alvo do loop de captura
- `InferenceStride` — rodar inferência a cada N frames

---

## Métricas expostas

Cada `PoseFeedback` carrega um objeto `PoseMetrics` com:

| Métrica | Significado |
|---|---|
| `BodyYawDeg` | Yaw do corpo: 0 = de frente, ±90 = perfil, ±180 = de costas |
| `LateralTiltDeg` | Inclinação lateral do tronco em graus (vertical = 0) |
| `TorsoTwistDeg` | Diferença entre yaw dos ombros e dos quadris |
| `VTaper` | `shoulderSpan / hipSpan` (proxy de cintura) |
| `Symmetry` | Score [0,1] de simetria, ponderado pela confiança dos landmarks |
| `Tightness` | Score [0,1] de estabilidade temporal (variância recente do twist/tilt) |

> A versão atual da UI **ainda não exibe** as métricas em uma faixa visível — elas chegam até o ViewModel mas não estão renderizadas. Próximo passo: faixa compacta sob o card de feedback no painel direito.

---

## Troubleshooting

| Problema | Solução |
|---|---|
| Câmera não inicia no macOS | Ajustes do Sistema → Privacidade e Segurança → Câmera → permitir para ProPosing (ou para o Terminal, se rodando via `dotnet run`). Após a primeira concessão, feche e reabra o app — o macOS não ativa o `VideoCapture` existente. |
| Sidecar não carrega no app empacotado | Verifique `Contents/MacOS/proposing-sidecar/proposing-sidecar` no `.app` (deve ser executável). Rebuild com `FORCE_SIDECAR_REBUILD=1`. |
| `pose_landmark_lite.tflite` faltando | O script garante esse modelo após o build. Se mesmo assim faltar, rode com `FORCE_SIDECAR_REBUILD=1 PYINSTALLER_CLEAN=1`. |
| Build .NET falha | Confirme `dotnet --version` ≥ 8.0. Limpe `obj/` e `bin/` em `interface_avalonia/ProPosing.Avalonia` e rode novamente. |
| Pose nunca passa a "correct" | Edite `pose_thresholds.json` ou observe os hints retornados — eles indicam exatamente o que ajustar. |
| Hints piscam entre frames | Já há histerese (3 frames consecutivos). Se ainda piscar, verifique `InferenceStride` (rodar inferência menos frequentemente reduz ruído). |

---

## Documentação adicional

- [`scripts/README.md`](scripts/README.md) — referência rápida dos scripts
- [`interface_avalonia/README.md`](interface_avalonia/README.md) — notas específicas da UI
- [`treinamento/README.md`](treinamento/README.md) — pipeline de treinamento (em evolução)

---

**ProPosing** — Análise de poses de fisiculturismo, em tempo real, no desktop.

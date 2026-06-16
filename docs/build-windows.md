# Roteiro — primeira sessão de build no Windows

Objetivo: sair com o ProPosing rodando num PC Windows (o hardware-alvo das
academias). Reserve ~2h; a maior parte dos riscos conhecidos está no passo 4.

## Pré-requisitos na máquina Windows

1. **Python 3.10+** — instalar marcando "Add python.exe to PATH"
2. **.NET SDK 8** — https://dotnet.microsoft.com/download/dotnet/8.0
3. Clonar/copiar o repositório
4. Webcam conectada

## Passos

```powershell
# 1. Build completo (sidecar PyInstaller + Avalonia publish win-x64)
powershell -ExecutionPolicy Bypass -File scripts\build_avalonia_executable.ps1

# 2. Teste manual: rodar e validar câmera + esqueleto + poses
build_app\proposing-win-x64\ProPosing.Avalonia.exe

# 3. Configurar modo kiosk (fullscreen, sem login, auto-start, sem suspensão)
powershell -ExecutionPolicy Bypass -File scripts\install_kiosk_windows.ps1

# 4. Reiniciar a máquina e validar o ciclo completo sem teclado:
#    boot → ProPosing abre sozinho em fullscreen com câmera ligada
```

## Riscos conhecidos (em ordem de probabilidade)

1. **PyInstaller + mediapipe**: se o sidecar falhar ao iniciar, rodar
   `dist\proposing-sidecar\proposing-sidecar.exe` direto num terminal e ler o
   erro — geralmente falta um data file do mediapipe (resolver com
   `--collect-data`/`--hidden-import` adicionais no .ps1).
2. **Antivírus/SmartScreen**: o .exe do PyInstaller sem assinatura costuma ser
   sinalizado. Para o piloto, adicionar exceção no Defender; a solução real é
   assinatura de código (certificado OV ~US$ 200/ano).
3. **Câmera**: se o índice 0 for uma câmera virtual (OBS etc.), o app sonda
   índices 0–4 sozinho. Forçar com `CAMERA_INDEX=1` se necessário.
4. **Fonte Ethnocentric**: conferir se os títulos renderizam corretamente.

## Validações de aceite

- [ ] Pose detectada com esqueleto alinhado ao vídeo
- [ ] Trocar as 9 poses pelo teclado numérico (0–8)
- [ ] Matar o sidecar pelo Gerenciador de Tarefas → recupera sozinho em ~5 s
- [ ] Desplugar/replugar a webcam → reconecta sozinho
- [ ] Reboot → app volta em fullscreen sem nenhum toque
- [ ] 1h rodando sem travar (olhar memória no Gerenciador de Tarefas)

## Observações

- O marcador `kiosk.mode` dentro da pasta do app ativa o modo kiosk; o
  rebuild apaga a pasta, então rode `install_kiosk_windows.ps1` de novo
  após cada build (ou copie o kiosk.mode de volta).
- Login automático do Windows (netplwiz) deixa o boot 100% sem toque.
- Anotar o modelo exato de mini-PC/webcam que funcionou — isso vira a
  especificação de hardware vendida às academias.

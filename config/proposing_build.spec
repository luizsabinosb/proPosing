# -*- mode: python ; coding: utf-8 -*-
# PyInstaller spec para ProPosing Backend
# Gera executável que inclui backend + proposing + treinamento (sem alterar código core)

import sys
from pathlib import Path
from PyInstaller.utils.hooks import collect_data_files

# SPECPATH = diretório do .spec (config/) → parent = raiz do projeto
PROJECT_ROOT = Path(SPECPATH).parent

# Dados adicionais de ML (usados em runtime)
added_data = [
    (str(PROJECT_ROOT / "ml" / "models"), "ml/models"),
    (str(PROJECT_ROOT / "ml" / "pose_info"), "ml/pose_info"),
    (str(PROJECT_ROOT / "ml" / "data"), "ml/data"),
    # Modelos e dados do MediaPipe (necessários para execução sem internet)
    *collect_data_files("mediapipe"),
]

# Oculta imports necessários para MediaPipe, OpenCV, etc
hiddenimports = [
    "app",
    "app.main",
    "app.api",
    "app.api.v1",
    "app.api.v1.pose",
    "app.core",
    "app.core.cv_service",
    "app.models",
    "app.models.pose",
    "proposing",
    "proposing.pose_evaluator",
    "proposing.ml_evaluator",
    "proposing.pose_metrics_loader",
    "uvicorn.logging",
    "uvicorn.loops",
    "uvicorn.loops.auto",
    "uvicorn.protocols",
    "uvicorn.protocols.http",
    "uvicorn.protocols.http.auto",
    "uvicorn.protocols.websockets",
    "uvicorn.protocols.websockets.auto",
    "uvicorn.lifespan",
    "uvicorn.lifespan.on",
    "multipart",
    "cv2",
    "mediapipe",
    "sklearn",
    "sklearn.ensemble",
    "sklearn.tree",
    "joblib",
]

a = Analysis(
    [str(PROJECT_ROOT / "backend" / "run_standalone.py")],
    pathex=[str(PROJECT_ROOT), str(PROJECT_ROOT / "backend")],
    datas=added_data,
    hiddenimports=hiddenimports,
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=[],
    win_no_prefer_redirects=False,
    win_private_assemblies=False,
    cipher=None,
    noarchive=False,
)

pyz = PYZ(a.pure, a.zipped_data, cipher=None)

exe = EXE(
    pyz,
    a.scripts,
    a.binaries,
    a.zipfiles,
    a.datas,
    [],
    name="proposing-backend",
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=True,
    upx_exclude=[],
    runtime_tmpdir=None,
    console=True,
    disable_windowed_traceback=False,
    argv_emulation=False,
    target_arch=None,
    codesign_identity=None,
    entitlements_file=None,
)

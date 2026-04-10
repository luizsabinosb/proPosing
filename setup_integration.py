#!/usr/bin/env python3
"""
setup_integration.py — ProPosing Integration Script
====================================================
Run this once from your terminal to integrate the new camera and ML modules:

    cd /path/to/proPosing
    python setup_integration.py

What it does:
  1. Patches proposing/__init__.py  → graceful mediapipe import
  2. Patches proposing/ml_evaluator.py  → uses MLModelLoader
  3. Patches backend/app/main.py   → uses startup_validator + better logging
  4. Replaces backend/app/core/cv_service.py with the new cross-platform version
  5. Reports what still needs to be done (train models, etc.)
"""

import os
import re
import sys
import shutil
import textwrap
from pathlib import Path

ROOT = Path(__file__).resolve().parent

# ─────────────────────────────────────────────────────────────────────────────
# Helpers
# ─────────────────────────────────────────────────────────────────────────────

def info(msg):  print(f"  ✓  {msg}")
def warn(msg):  print(f"  ⚠  {msg}")
def err(msg):   print(f"  ✗  {msg}")
def header(msg): print(f"\n{'─'*60}\n  {msg}\n{'─'*60}")


def backup_and_write(path: Path, new_content: str, patch_func=None):
    """
    Read the existing file, optionally transform it with patch_func,
    write it back (with .bak backup).
    If patch_func is None, overwrite completely with new_content.
    """
    bak = path.with_suffix(path.suffix + ".bak")
    if path.exists():
        shutil.copy2(path, bak)
        info(f"Backup: {bak.relative_to(ROOT)}")
        if patch_func:
            original = path.read_text(encoding="utf-8")
            result = patch_func(original)
            if result == original:
                warn(f"No changes needed in {path.relative_to(ROOT)}")
                bak.unlink()   # remove unnecessary backup
                return False
            path.write_text(result, encoding="utf-8")
        else:
            path.write_text(new_content, encoding="utf-8")
        info(f"Updated: {path.relative_to(ROOT)}")
        return True
    else:
        path.write_text(new_content, encoding="utf-8")
        info(f"Created: {path.relative_to(ROOT)}")
        return True


# ─────────────────────────────────────────────────────────────────────────────
# 1. Fix proposing/__init__.py — graceful mediapipe import
# ─────────────────────────────────────────────────────────────────────────────

def patch_proposing_init(src: str) -> str:
    """
    Wrap bare `import mediapipe as mp` with a try/except so the package
    stays importable even when mediapipe is not installed.
    """
    # Already patched?
    if "try:" in src and "mediapipe" in src and "ImportError" in src:
        return src

    # Replace bare import line
    patched = re.sub(
        r"^(import mediapipe as mp\s*)$",
        textwrap.dedent("""\
            try:
                import mediapipe as mp
            except ImportError:
                mp = None  # mediapipe is optional; pose detection will be disabled
        """),
        src,
        flags=re.MULTILINE,
    )

    # Also wrap `from mediapipe ...` lines
    patched = re.sub(
        r"^(from mediapipe\s+import[^\n]+\n)",
        r"try:\n    \1except ImportError:\n    pass\n",
        patched,
        flags=re.MULTILINE,
    )

    return patched


# ─────────────────────────────────────────────────────────────────────────────
# 2. Patch ml_evaluator.py — inject MLModelLoader usage
# ─────────────────────────────────────────────────────────────────────────────

ML_LOADER_IMPORT = """\
# ── NEW: robust model loading ──────────────────────────────────────────────
import sys as _sys
import pathlib as _pathlib
_sys.path.insert(0, str(_pathlib.Path(__file__).resolve().parent.parent))
from proposing.ml_model_loader import MLModelLoader as _MLModelLoader
# ───────────────────────────────────────────────────────────────────────────
"""

def patch_ml_evaluator(src: str) -> str:
    """
    Insert MLModelLoader import and replace the hardcoded model-loading
    block with a robust version.
    """
    if "_MLModelLoader" in src:
        return src  # already patched

    # Inject import after the last existing import block
    import_end = 0
    for m in re.finditer(r"^(import |from )\S", src, re.MULTILINE):
        import_end = m.end()
    insert_pos = src.rfind("\n", 0, import_end) + 1

    patched = src[:insert_pos] + "\n" + ML_LOADER_IMPORT + src[insert_pos:]

    # Replace the silent fallback warning block that produces the messages
    # the user sees in the logs.
    OLD_WARNING_PATTERN = re.compile(
        r"(⚠️\s*Nenhum modelo ML encontrado\..*?Usando apenas regras\..*?)(?=\n\S|\Z)",
        re.DOTALL | re.MULTILINE,
    )
    REPLACEMENT_COMMENT = (
        "# Model loading is now handled by MLModelLoader in proposing/ml_model_loader.py\n"
        "# It will log actionable messages if models are missing."
    )
    patched = OLD_WARNING_PATTERN.sub(REPLACEMENT_COMMENT, patched)

    # Inject loader usage near the top of any load/init method if there's one
    LOADER_SNIPPET = textwrap.dedent("""\

        # ── Robust model loading ──
        _loader = _MLModelLoader()
        _ml_models = _loader.load_all()   # logs clearly if models are missing
        _metrics   = _loader.load_training_metrics()
        # ─────────────────────────
    """)

    # Find a reasonable insertion point (after class definition or at module level)
    class_match = re.search(r"^class \w+.*?:\s*\n", patched, re.MULTILINE)
    if class_match:
        insert_at = class_match.end()
        patched = patched[:insert_at] + LOADER_SNIPPET + patched[insert_at:]

    return patched


# ─────────────────────────────────────────────────────────────────────────────
# 3. Patch backend/app/main.py — add startup validator call
# ─────────────────────────────────────────────────────────────────────────────

STARTUP_VALIDATOR_SNIPPET = textwrap.dedent("""\
    # ── Startup health check (camera + ML models + deps) ──────────────────
    try:
        from app.core.startup_validator import run_startup_checks
        run_startup_checks(strict=False)
    except Exception as _e:
        import logging as _log
        _log.getLogger(__name__).warning("Startup validator error: %s", _e)
    # ──────────────────────────────────────────────────────────────────────
""")

def patch_main(src: str) -> str:
    if "startup_validator" in src:
        return src  # already patched

    # Find the app = FastAPI(...) line and insert after it
    match = re.search(r"^app\s*=\s*FastAPI\(.*?\)\s*\n", src, re.MULTILINE | re.DOTALL)
    if match:
        insert_pos = match.end()
        return src[:insert_pos] + "\n" + STARTUP_VALIDATOR_SNIPPET + src[insert_pos:]

    # Fallback: insert after the last import block
    lines = src.splitlines(keepends=True)
    last_import = 0
    for i, line in enumerate(lines):
        if line.startswith(("import ", "from ")):
            last_import = i
    insert_pos = last_import + 1
    lines.insert(insert_pos, "\n" + STARTUP_VALIDATOR_SNIPPET + "\n")
    return "".join(lines)


# ─────────────────────────────────────────────────────────────────────────────
# 4. Replace cv_service.py with the new cross-platform version
# ─────────────────────────────────────────────────────────────────────────────

def replace_cv_service():
    new_file = ROOT / "backend/app/core/cv_service_new.py"
    target   = ROOT / "backend/app/core/cv_service.py"

    if not new_file.exists():
        err("cv_service_new.py not found — skipping")
        return

    bak = target.with_suffix(".py.bak")
    if target.exists():
        shutil.copy2(target, bak)
        info(f"Backup: {bak.relative_to(ROOT)}")

    shutil.copy2(new_file, target)
    new_file.unlink()
    info(f"cv_service.py replaced with cross-platform version")


# ─────────────────────────────────────────────────────────────────────────────
# 5. Fix training data path issue in pose_metrics_loader (if needed)
# ─────────────────────────────────────────────────────────────────────────────

def patch_metrics_loader(src: str) -> str:
    """Replace hardcoded /data_collected/... path with project-relative path."""
    if "/data_collected/" not in src:
        return src

    patched = re.sub(
        r'["\']\/data_collected\/processed\/pose_info_training_data\.json["\']',
        'str(__import__("pathlib").Path(__file__).resolve().parent.parent / '
        '"ml/data/processed/pose_info_training_data.json")',
        src,
    )
    return patched


# ─────────────────────────────────────────────────────────────────────────────
# Main runner
# ─────────────────────────────────────────────────────────────────────────────

def main():
    print("\n" + "═"*60)
    print("  ProPosing Integration Script")
    print("═"*60)

    errors = []

    # 1. proposing/__init__.py
    header("1. Fix proposing/__init__.py (mediapipe graceful import)")
    p = ROOT / "proposing/__init__.py"
    try:
        backup_and_write(p, "", patch_func=patch_proposing_init)
    except Exception as e:
        err(f"Failed: {e}"); errors.append(str(e))

    # 2. ml_evaluator.py
    header("2. Patch proposing/ml_evaluator.py (MLModelLoader)")
    p = ROOT / "proposing/ml_evaluator.py"
    try:
        backup_and_write(p, "", patch_func=patch_ml_evaluator)
    except Exception as e:
        err(f"Failed: {e}"); errors.append(str(e))

    # 3. backend/app/main.py
    header("3. Patch backend/app/main.py (startup validator)")
    p = ROOT / "backend/app/main.py"
    try:
        backup_and_write(p, "", patch_func=patch_main)
    except Exception as e:
        err(f"Failed: {e}"); errors.append(str(e))

    # 4. Replace cv_service.py
    header("4. Replace cv_service.py (cross-platform camera)")
    try:
        replace_cv_service()
    except Exception as e:
        err(f"Failed: {e}"); errors.append(str(e))

    # 5. pose_metrics_loader.py
    header("5. Fix hardcoded path in pose_metrics_loader.py")
    p = ROOT / "proposing/pose_metrics_loader.py"
    try:
        backup_and_write(p, "", patch_func=patch_metrics_loader)
    except Exception as e:
        err(f"Failed: {e}"); errors.append(str(e))

    # ── Final status ─────────────────────────────────────────────────────────
    print("\n" + "═"*60)
    print("  Integration complete!")
    print("═"*60)

    if errors:
        print(f"\n  ⚠  {len(errors)} error(s) occurred:")
        for e in errors:
            print(f"     • {e}")

    # Check what still needs to be done
    print("\n  Next steps:")

    models_dir = ROOT / "ml/models"
    model_files = list(models_dir.glob("*.pkl")) + list(models_dir.glob("*.joblib")) \
                + list(models_dir.glob("*.h5")) + list(models_dir.glob("*.onnx"))
    if not model_files:
        print("  1. Train the ML models:")
        print("       python treinamento/train_model.py")
    else:
        print(f"  1. ✓ Models found: {[f.name for f in model_files]}")

    td = ROOT / "ml/data/processed/pose_info_training_data.json"
    if not td.exists():
        print("  2. Generate training data:")
        print("       python treinamento/process_pose_info.py")
    else:
        print(f"  2. ✓ Training data: {td}")

    print("  3. Run the startup check:")
    print("       cd backend && python -m app.core.startup_validator")
    print("  4. Start the backend normally:")
    print("       cd backend && python run_standalone.py")
    print()


if __name__ == "__main__":
    main()

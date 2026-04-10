"""
startup_validator.py — App Startup Health Check
================================================
Run this at the top of main.py (or app lifespan) to surface all
environment problems before the app starts serving requests.

Usage in main.py / lifespan:
    from app.core.startup_validator import run_startup_checks

    @asynccontextmanager
    async def lifespan(app: FastAPI):
        run_startup_checks()         # logs clearly, never crashes the app
        yield

Or as a standalone script:
    python -m app.core.startup_validator
"""
import sys as _sys
import pathlib as _pathlib
_project_root = str(_pathlib.Path(__file__).resolve().parent.parent.parent.parent)
if _project_root not in _sys.path:
    _sys.path.insert(0, _project_root)


import sys
import logging
import platform
from pathlib import Path

logger = logging.getLogger(__name__)


def run_startup_checks(strict: bool = False) -> bool:
    """
    Run all environment checks and log a structured report.

    Args:
        strict: If True, raise RuntimeError when critical checks fail.
                If False (default), log errors and continue.

    Returns:
        True if everything is OK, False if there are issues.
    """
    logger.info("=" * 60)
    logger.info("ProPosing — Startup Environment Check")
    logger.info("  Platform : %s %s", platform.system(), platform.release())
    logger.info("  Python   : %s", sys.version.split()[0])
    logger.info("=" * 60)

    results: list[tuple[str, bool, str]] = []  # (name, passed, detail)

    # ---------------------------------------------------------------
    # 1. Python dependencies
    # ---------------------------------------------------------------
    results += _check_dependencies()

    # ---------------------------------------------------------------
    # 2. ML models
    # ---------------------------------------------------------------
    results += _check_ml_models()

    # ---------------------------------------------------------------
    # 3. Camera availability
    # ---------------------------------------------------------------
    results += _check_camera()

    # ---------------------------------------------------------------
    # Summary
    # ---------------------------------------------------------------
    passed = [r for r in results if r[1]]
    failed = [r for r in results if not r[1]]

    logger.info("-" * 60)
    logger.info("Startup Check Summary: %d passed, %d failed", len(passed), len(failed))
    for name, ok, detail in results:
        status = "✓" if ok else "✗"
        level = logging.INFO if ok else logging.WARNING
        logger.log(level, "  %s %-30s  %s", status, name, detail)
    logger.info("=" * 60)

    if failed and strict:
        names = ", ".join(r[0] for r in failed)
        raise RuntimeError(f"Startup checks failed: {names}")

    return len(failed) == 0


# ---------------------------------------------------------------------------
# Individual checks
# ---------------------------------------------------------------------------

def _check_dependencies() -> list[tuple[str, bool, str]]:
    """Verify all required packages are importable."""
    packages = {
        "cv2": "opencv-python",
        "mediapipe": "mediapipe",
        "numpy": "numpy",
        "fastapi": "fastapi",
        "uvicorn": "uvicorn",
    }
    optional = {"joblib", "sklearn", "tensorflow", "onnxruntime"}
    results = []

    for module, pip_name in packages.items():
        try:
            m = __import__(module)
            version = getattr(m, "__version__", "?")
            results.append((f"dep:{module}", True, f"v{version}"))
        except ImportError:
            results.append((
                f"dep:{module}", False,
                f"MISSING — run: pip install {pip_name}"
            ))

    for module in optional:
        try:
            m = __import__(module)
            version = getattr(m, "__version__", "?")
            results.append((f"opt:{module}", True, f"v{version}"))
        except ImportError:
            results.append((f"opt:{module}", True, "not installed (optional)"))

    return results


def _check_ml_models() -> list[tuple[str, bool, str]]:
    """Check ML model files and training data."""
    results = []
    try:
        # Import lazily to avoid circular issues at startup
        from proposing.ml_model_loader import MLModelLoader
        loader = MLModelLoader()
        report = loader.validate_environment()

        if report.has_models:
            results.append((
                "ml:models", True,
                f"{len(report.model_files)} file(s) in {report.models_dir}"
            ))
        else:
            results.append((
                "ml:models", False,
                f"No model files in {report.models_dir} — "
                f"run: python treinamento/train_model.py"
            ))

        if report.training_data_exists:
            results.append(("ml:training_data", True, str(report.training_data_path)))
        else:
            results.append((
                "ml:training_data", False,
                f"Not found: {report.training_data_path} — "
                f"run: python treinamento/process_pose_info.py"
            ))

    except Exception as exc:
        results.append(("ml:loader", False, f"Import error: {exc}"))

    return results


def _check_camera() -> list[tuple[str, bool, str]]:
    """Check if at least one camera is accessible."""
    results = []
    try:
        import cv2
        from app.core.camera_manager import list_available_cameras, _get_platform

        # Quick non-invasive check: probe index 0 only
        cap = cv2.VideoCapture(0)
        if cap.isOpened():
            ret, _ = cap.read()
            cap.release()
            if ret:
                results.append(("camera:index_0", True, "accessible"))
            else:
                results.append((
                    "camera:index_0", False,
                    "opened but frame read failed — check macOS Camera privacy"
                    if _get_platform() == "darwin"
                    else "opened but no frames returned"
                ))
        else:
            cap.release()
            results.append((
                "camera:index_0", False,
                "not accessible — check permissions or if camera is in use"
            ))

    except ImportError:
        results.append(("camera:opencv", False, "OpenCV not installed"))
    except Exception as exc:
        results.append(("camera:probe", False, str(exc)))

    return results


# ---------------------------------------------------------------------------
# Standalone runner
# ---------------------------------------------------------------------------

if __name__ == "__main__":
    logging.basicConfig(
        level=logging.DEBUG,
        format="%(asctime)s %(levelname)-8s %(message)s",
        datefmt="%H:%M:%S",
    )
    ok = run_startup_checks(strict=False)
    sys.exit(0 if ok else 1)

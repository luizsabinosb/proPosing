"""
ml_model_loader.py — Robust ML Model Loading
=============================================
Handles model discovery, validation, and loading with:
- Multi-strategy path resolution (relative, absolute, env var)
- Actionable error messages instead of silent fallbacks
- Startup validation that reports exactly what is missing and why
- Support for sklearn (.pkl / .joblib), Keras/TF (.h5 / .keras), and ONNX

Usage:
    from proposing.ml_model_loader import MLModelLoader, ModelLoadError

    loader = MLModelLoader()
    report = loader.validate_environment()   # check before loading
    models = loader.load_all()               # raises ModelLoadError if critical
"""

import os
import sys
import json
import logging
import platform
from pathlib import Path
from dataclasses import dataclass, field
from typing import Any, Optional

logger = logging.getLogger(__name__)

# ---------------------------------------------------------------------------
# Exceptions
# ---------------------------------------------------------------------------

class ModelLoadError(RuntimeError):
    """Raised when a *required* model cannot be loaded."""


class ModelNotTrainedError(ModelLoadError):
    """
    Raised when the models directory exists but is empty — meaning the user
    has not trained any models yet.
    """


# ---------------------------------------------------------------------------
# Path resolution
# ---------------------------------------------------------------------------

def _project_root() -> Path:
    """
    Walk up the directory tree from this file until we find a known
    project marker (e.g. a 'backend' or 'ml' subdirectory).
    """
    candidate = Path(__file__).resolve()
    for parent in [candidate, *candidate.parents]:
        if (parent / "ml").is_dir() and (parent / "backend").is_dir():
            return parent
    # Fallback: two levels up from this file (proposing/ → project root)
    return Path(__file__).resolve().parent.parent


def _resolve_models_dir() -> Path:
    """
    Determine the models directory using (in order of priority):
    1. PROPOSING_MODELS_DIR environment variable
    2. PROJECT_ROOT/ml/models/
    3. Directory of this file /../ml/models/

    Returns a Path (which may not exist yet — callers should check).
    """
    env_override = os.environ.get("PROPOSING_MODELS_DIR")
    if env_override:
        path = Path(env_override).expanduser().resolve()
        logger.debug("Using PROPOSING_MODELS_DIR env var: %s", path)
        return path

    root = _project_root()
    return root / "ml" / "models"


def _resolve_training_data_path() -> Path:
    """
    Find pose_info_training_data.json using multiple strategies:
    1. PROPOSING_TRAINING_DATA env var
    2. PROJECT_ROOT/ml/data/processed/pose_info_training_data.json
    3. Legacy hardcoded path  /data_collected/processed/... (mapped to project)
    """
    env_override = os.environ.get("PROPOSING_TRAINING_DATA")
    if env_override:
        return Path(env_override).expanduser().resolve()

    root = _project_root()
    return root / "ml" / "data" / "processed" / "pose_info_training_data.json"


# ---------------------------------------------------------------------------
# Data classes
# ---------------------------------------------------------------------------

@dataclass
class ModelInfo:
    name: str                          # e.g. "pose_classifier"
    path: Path
    model_type: str = "unknown"        # "sklearn", "keras", "onnx", "unknown"
    loaded: bool = False
    error: Optional[str] = None
    obj: Any = field(default=None, repr=False)   # The actual loaded model


@dataclass
class ValidationReport:
    models_dir: Path
    models_dir_exists: bool
    model_files: list[Path]
    training_data_path: Path
    training_data_exists: bool
    issues: list[str] = field(default_factory=list)
    warnings: list[str] = field(default_factory=list)

    @property
    def has_models(self) -> bool:
        return bool(self.model_files)

    @property
    def is_ready(self) -> bool:
        return self.has_models and not self.issues


# ---------------------------------------------------------------------------
# Loader
# ---------------------------------------------------------------------------

class MLModelLoader:
    """
    Discovers and loads ML models from the project's ml/models/ directory.

    Supported file types:
        .pkl, .joblib  → scikit-learn / joblib
        .h5, .keras    → Keras / TensorFlow
        .onnx          → ONNX Runtime

    Extend `_load_single` to support additional formats.
    """

    MODEL_EXTENSIONS = {".pkl", ".joblib", ".h5", ".keras", ".onnx"}

    def __init__(
        self,
        models_dir: Optional[Path] = None,
        training_data_path: Optional[Path] = None,
        raise_on_missing: bool = False,
    ):
        """
        Args:
            models_dir: Override the default model directory.
            training_data_path: Override the default training-data JSON path.
            raise_on_missing: If True, raise ModelNotTrainedError when no
                              model files are found. If False, log a warning
                              and return empty dict (rule-based fallback).
        """
        self.models_dir = Path(models_dir) if models_dir else _resolve_models_dir()
        self.training_data_path = (
            Path(training_data_path)
            if training_data_path
            else _resolve_training_data_path()
        )
        self.raise_on_missing = raise_on_missing
        self._loaded_models: dict[str, ModelInfo] = {}

    # ------------------------------------------------------------------
    # Public API
    # ------------------------------------------------------------------

    def validate_environment(self) -> ValidationReport:
        """
        Check the filesystem before loading anything.
        Returns a ValidationReport with a list of issues/warnings.
        Call this at startup to surface problems early.
        """
        dir_exists = self.models_dir.is_dir()
        model_files = []
        if dir_exists:
            model_files = [
                f for f in self.models_dir.iterdir()
                if f.suffix.lower() in self.MODEL_EXTENSIONS
            ]

        training_exists = self.training_data_path.is_file()

        report = ValidationReport(
            models_dir=self.models_dir,
            models_dir_exists=dir_exists,
            model_files=model_files,
            training_data_path=self.training_data_path,
            training_data_exists=training_exists,
        )

        # --- Diagnose issues ---
        if not dir_exists:
            report.issues.append(
                f"Models directory not found: {self.models_dir}\n"
                f"  → Create it with:  mkdir -p \"{self.models_dir}\"\n"
                f"  → Or set env var:  PROPOSING_MODELS_DIR=/path/to/models"
            )
        elif not model_files:
            report.issues.append(
                f"Models directory is EMPTY: {self.models_dir}\n"
                f"  → No .pkl / .joblib / .h5 / .onnx files found.\n"
                f"  → You need to train the models first.\n"
                f"  → Run:  python treinamento/train_model.py"
            )
        else:
            logger.info("Found %d model file(s) in %s:", len(model_files), self.models_dir)
            for f in model_files:
                logger.info("  • %s (%s)", f.name, _human_size(f.stat().st_size))

        if not training_exists:
            report.warnings.append(
                f"Training data not found: {self.training_data_path}\n"
                f"  → Expected location: {self.training_data_path}\n"
                f"  → Or set env var:    PROPOSING_TRAINING_DATA=/path/to/file.json\n"
                f"  → To generate it:    python treinamento/process_pose_info.py"
            )

        # Check write permissions on models dir (if it exists)
        if dir_exists and not os.access(self.models_dir, os.W_OK):
            report.warnings.append(
                f"Models directory is NOT writable: {self.models_dir}\n"
                f"  → Fix with:  chmod u+w \"{self.models_dir}\""
            )

        return report

    def load_all(self) -> dict[str, Any]:
        """
        Load all model files found in the models directory.

        Returns:
            dict mapping model name → loaded model object.
            Empty dict if no models exist (and raise_on_missing=False).

        Raises:
            ModelNotTrainedError: if no model files found and raise_on_missing=True.
            ModelLoadError: if a model file exists but fails to load.
        """
        report = self.validate_environment()
        self._log_report(report)

        if not report.has_models:
            if self.raise_on_missing:
                raise ModelNotTrainedError(
                    f"No ML models found in: {self.models_dir}\n"
                    "Train the models first by running:\n"
                    "  python treinamento/train_model.py\n"
                    "The app cannot start without trained models when "
                    "raise_on_missing=True."
                )
            logger.warning(
                "⚠ No ML models available — falling back to rule-based evaluation."
            )
            return {}

        result: dict[str, Any] = {}
        for model_path in report.model_files:
            info = self._load_single(model_path)
            self._loaded_models[info.name] = info
            if info.loaded:
                result[info.name] = info.obj
            else:
                # A file exists but couldn't be loaded — always raise
                raise ModelLoadError(
                    f"Failed to load model '{info.name}' from {info.path}:\n"
                    f"  {info.error}\n"
                    f"  → Delete the corrupt file and retrain, or check the logs."
                )

        logger.info("✓ Successfully loaded %d model(s): %s",
                    len(result), list(result.keys()))
        return result

    def load_training_metrics(self) -> dict:
        """
        Load the training-data JSON for pose metrics.
        Returns the parsed dict, or an empty dict with a logged warning.
        """
        path = self.training_data_path
        if not path.is_file():
            logger.warning(
                "⚠ Training metrics not found: %s\n"
                "  → Using hardcoded default metrics.\n"
                "  → To fix: run `python treinamento/process_pose_info.py`\n"
                "  → Or set: PROPOSING_TRAINING_DATA=/correct/path.json",
                path,
            )
            return {}

        try:
            with open(path, "r", encoding="utf-8") as fh:
                data = json.load(fh)
            logger.info("✓ Training metrics loaded from: %s", path)
            return data
        except json.JSONDecodeError as exc:
            logger.error(
                "Training metrics file is corrupted: %s\n  JSON error: %s",
                path, exc,
            )
            return {}
        except PermissionError:
            logger.error(
                "Permission denied reading training metrics: %s\n"
                "  → Fix with: chmod u+r \"%s\"",
                path, path,
            )
            return {}

    # ------------------------------------------------------------------
    # Private helpers
    # ------------------------------------------------------------------

    def _load_single(self, path: Path) -> ModelInfo:
        """Load a single model file based on its extension."""
        name = path.stem
        ext = path.suffix.lower()
        info = ModelInfo(name=name, path=path)

        logger.debug("Loading model: %s", path)

        try:
            if ext in (".pkl", ".joblib"):
                info.obj = self._load_sklearn(path)
                info.model_type = "sklearn"
                info.loaded = True

            elif ext in (".h5", ".keras"):
                info.obj = self._load_keras(path)
                info.model_type = "keras"
                info.loaded = True

            elif ext == ".onnx":
                info.obj = self._load_onnx(path)
                info.model_type = "onnx"
                info.loaded = True

            else:
                info.error = f"Unsupported model format: {ext}"

        except ImportError as exc:
            info.error = (
                f"Missing dependency for {ext} models: {exc}\n"
                f"  → Install it:  pip install {_dep_hint(ext)}"
            )
        except Exception as exc:
            info.error = str(exc)

        if not info.loaded:
            logger.error("✗ Failed to load model '%s': %s", name, info.error)

        return info

    @staticmethod
    def _load_sklearn(path: Path) -> Any:
        """Load a scikit-learn model saved with joblib or pickle."""
        try:
            import joblib
            return joblib.load(path)
        except ImportError:
            import pickle
            with open(path, "rb") as fh:
                return pickle.load(fh)

    @staticmethod
    def _load_keras(path: Path) -> Any:
        """Load a Keras/TensorFlow model."""
        import tensorflow as tf  # type: ignore
        return tf.keras.models.load_model(str(path))

    @staticmethod
    def _load_onnx(path: Path) -> Any:
        """Load an ONNX model via onnxruntime."""
        import onnxruntime as ort  # type: ignore
        return ort.InferenceSession(str(path))

    @staticmethod
    def _log_report(report: ValidationReport):
        """Pretty-print the validation report at startup."""
        logger.info("-" * 60)
        logger.info("ML Environment Report")
        logger.info("  Models directory : %s", report.models_dir)
        logger.info("  Directory exists : %s", report.models_dir_exists)
        logger.info("  Model files      : %d", len(report.model_files))
        logger.info("  Training data    : %s", report.training_data_path)
        logger.info("  Training data ok : %s", report.training_data_exists)

        for issue in report.issues:
            logger.error("  ✗ ISSUE: %s", issue)

        for warning in report.warnings:
            logger.warning("  ⚠ WARNING: %s", warning)

        if report.is_ready:
            logger.info("  Status           : ✓ Ready")
        elif report.has_models:
            logger.info("  Status           : ⚠ Models found but warnings present")
        else:
            logger.info("  Status           : ✗ No models — rule-based fallback active")

        logger.info("-" * 60)


# ---------------------------------------------------------------------------
# Utilities
# ---------------------------------------------------------------------------

def _human_size(n_bytes: int) -> str:
    for unit in ("B", "KB", "MB", "GB"):
        if n_bytes < 1024:
            return f"{n_bytes:.1f} {unit}"
        n_bytes /= 1024
    return f"{n_bytes:.1f} TB"


def _dep_hint(ext: str) -> str:
    return {
        ".pkl": "scikit-learn joblib",
        ".joblib": "joblib",
        ".h5": "tensorflow",
        ".keras": "tensorflow",
        ".onnx": "onnxruntime",
    }.get(ext, "the required package")


# ---------------------------------------------------------------------------
# Quick smoke-test (run directly: python ml_model_loader.py)
# ---------------------------------------------------------------------------

if __name__ == "__main__":
    logging.basicConfig(level=logging.DEBUG, format="%(levelname)s %(message)s")
    loader = MLModelLoader()
    report = loader.validate_environment()
    print("\n=== Validation Report ===")
    print(f"Models dir   : {report.models_dir}")
    print(f"Dir exists   : {report.models_dir_exists}")
    print(f"Model files  : {[f.name for f in report.model_files]}")
    print(f"Training data: {report.training_data_path}")
    print(f"TD exists    : {report.training_data_exists}")
    print(f"Issues       : {report.issues}")
    print(f"Warnings     : {report.warnings}")
    print(f"Ready        : {report.is_ready}")

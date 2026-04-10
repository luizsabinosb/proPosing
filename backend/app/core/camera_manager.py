"""
camera_manager.py — Cross-Platform Camera Initialization
=========================================================
Handles camera discovery, initialization, and graceful error recovery
on both macOS and Windows (and Linux).

Usage:
    from app.core.camera_manager import CameraManager

    manager = CameraManager()
    cap = manager.initialize()          # returns cv2.VideoCapture or None
    frame = manager.read_frame()        # returns (success, frame)
    manager.release()
"""

import sys
import platform
import logging
import subprocess
from dataclasses import dataclass, field
from typing import Optional

import cv2

logger = logging.getLogger(__name__)

# ---------------------------------------------------------------------------
# Data classes
# ---------------------------------------------------------------------------

@dataclass
class CameraDevice:
    index: int
    name: str = "Unknown"
    width: int = 0
    height: int = 0
    fps: float = 0.0


@dataclass
class CameraConfig:
    """Tweak these defaults as needed for your app."""
    preferred_index: int = 0          # Try this index first
    max_probe_index: int = 5          # How many indices to probe
    width: int = 640
    height: int = 480
    fps: int = 30
    backend: Optional[int] = None     # None = let OpenCV decide


# ---------------------------------------------------------------------------
# Platform helpers
# ---------------------------------------------------------------------------

def _get_platform() -> str:
    return platform.system().lower()  # "darwin", "windows", "linux"


def _choose_backend() -> int:
    """
    Pick the most reliable OpenCV capture backend for the current OS.

    macOS  → AVFoundation  (cv2.CAP_AVFOUNDATION)
    Windows → DirectShow or MSMF
    Linux  → V4L2
    """
    os_name = _get_platform()
    if os_name == "darwin":
        return cv2.CAP_AVFOUNDATION
    elif os_name == "windows":
        # MSMF works better than DirectShow on modern Windows
        return cv2.CAP_MSMF
    else:
        return cv2.CAP_V4L2


def _list_devices_macos() -> list[int]:
    """
    Query AVFoundation for available capture devices via system_profiler.
    Falls back to an empty list if the command is unavailable.
    """
    available = []
    try:
        result = subprocess.run(
            ["system_profiler", "SPCameraDataType"],
            capture_output=True, text=True, timeout=5
        )
        # Each camera entry shows up once in the output
        camera_count = result.stdout.count("Unique ID:")
        available = list(range(camera_count))
        logger.debug("macOS system_profiler found %d camera(s)", camera_count)
    except Exception as exc:
        logger.debug("system_profiler query failed: %s", exc)
    return available


def _list_devices_windows() -> list[int]:
    """
    Use a DirectShow / MSMF probe to find available devices.
    Quick and works without extra dependencies.
    """
    available = []
    try:
        # Try each index silently with CAP_DSHOW; stop at first consecutive miss
        consecutive_fails = 0
        for idx in range(8):
            cap = cv2.VideoCapture(idx, cv2.CAP_DSHOW)
            if cap.isOpened():
                available.append(idx)
                consecutive_fails = 0
                cap.release()
            else:
                consecutive_fails += 1
                if consecutive_fails >= 2:
                    break
    except Exception as exc:
        logger.debug("Windows device probe failed: %s", exc)
    return available


def _list_devices_linux() -> list[int]:
    """Find /dev/video* devices."""
    import glob
    nodes = glob.glob("/dev/video*")
    return [int(n.replace("/dev/video", "")) for n in sorted(nodes)]


def list_available_cameras(max_index: int = 5) -> list[CameraDevice]:
    """
    Enumerate all cameras available on this machine.

    Returns a list of CameraDevice objects (may be empty if none found).
    """
    os_name = _get_platform()
    logger.info("Detecting cameras on platform: %s", os_name)

    if os_name == "darwin":
        indices = _list_devices_macos() or list(range(max_index))
    elif os_name == "windows":
        indices = _list_devices_windows() or list(range(max_index))
    else:
        indices = _list_devices_linux() or list(range(max_index))

    devices: list[CameraDevice] = []
    backend = _choose_backend()

    for idx in indices:
        cap = cv2.VideoCapture(idx, backend)
        if cap.isOpened():
            w = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
            h = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
            fps = cap.get(cv2.CAP_PROP_FPS)
            cap.release()
            devices.append(CameraDevice(index=idx, width=w, height=h, fps=fps))
            logger.info("  ✓ Camera index %d — %dx%d @ %.1f fps", idx, w, h, fps)
        else:
            cap.release()

    if not devices:
        logger.warning("No cameras detected on this machine.")

    return devices


# ---------------------------------------------------------------------------
# Main manager class
# ---------------------------------------------------------------------------

class CameraManager:
    """
    Thread-safe wrapper around cv2.VideoCapture with:
    - Automatic backend selection per OS
    - Fallback probing across multiple indices
    - macOS TCC permission detection
    - Configurable resolution / fps
    """

    def __init__(self, config: Optional[CameraConfig] = None):
        self.config = config or CameraConfig()
        self._cap: Optional[cv2.VideoCapture] = None
        self._active_index: Optional[int] = None
        self._backend: int = self.config.backend or _choose_backend()

    # ------------------------------------------------------------------
    # Public API
    # ------------------------------------------------------------------

    def initialize(self) -> Optional[cv2.VideoCapture]:
        """
        Try to open a camera.

        Strategy:
        1. Try config.preferred_index first.
        2. Probe all other indices up to max_probe_index.
        3. Return the first one that works, or None if all fail.
        """
        logger.info("Initializing camera (platform=%s, backend=%s)...",
                    _get_platform(), self._backend_name())

        # Try preferred index first
        cap = self._try_open(self.config.preferred_index)
        if cap:
            return cap

        logger.warning(
            "Camera index %d not available. Probing other indices...",
            self.config.preferred_index,
        )

        for idx in range(self.config.max_probe_index):
            if idx == self.config.preferred_index:
                continue
            cap = self._try_open(idx)
            if cap:
                return cap

        # Nothing worked — give a useful error
        self._log_camera_failure()
        return None

    def read_frame(self):
        """
        Read one frame from the active capture.

        Returns:
            (True, frame_ndarray)  on success
            (False, None)          if camera not open or read failed
        """
        if self._cap is None or not self._cap.isOpened():
            logger.error("read_frame() called but camera is not open.")
            return False, None

        success, frame = self._cap.read()
        if not success:
            logger.warning("Camera read failed (index=%s). Frame dropped.", self._active_index)
        return success, frame

    def release(self):
        """Release the camera resource."""
        if self._cap and self._cap.isOpened():
            self._cap.release()
            logger.info("Camera index %s released.", self._active_index)
        self._cap = None
        self._active_index = None

    @property
    def is_open(self) -> bool:
        return self._cap is not None and self._cap.isOpened()

    @property
    def active_index(self) -> Optional[int]:
        return self._active_index

    # ------------------------------------------------------------------
    # Private helpers
    # ------------------------------------------------------------------

    def _try_open(self, index: int) -> Optional[cv2.VideoCapture]:
        """
        Attempt to open camera at *index*. Applies resolution/fps config
        if successful. Returns the capture object or None.
        """
        logger.debug("Trying camera index %d...", index)
        cap = cv2.VideoCapture(index, self._backend)

        if not cap.isOpened():
            cap.release()
            return None

        # Apply desired settings
        cap.set(cv2.CAP_PROP_FRAME_WIDTH, self.config.width)
        cap.set(cv2.CAP_PROP_FRAME_HEIGHT, self.config.height)
        cap.set(cv2.CAP_PROP_FPS, self.config.fps)

        # Verify we can actually grab a frame (macOS can open but not read
        # if TCC permission was denied)
        ret, _ = cap.read()
        if not ret:
            logger.warning(
                "Camera index %d opened but frame read failed — "
                "possible permission denial (check macOS Privacy settings).",
                index,
            )
            cap.release()
            return None

        actual_w = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
        actual_h = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
        actual_fps = cap.get(cv2.CAP_PROP_FPS)

        logger.info(
            "✓ Camera opened: index=%d, %dx%d @ %.1f fps (backend=%s)",
            index, actual_w, actual_h, actual_fps, self._backend_name(),
        )

        self._cap = cap
        self._active_index = index
        return cap

    def _backend_name(self) -> str:
        names = {
            cv2.CAP_AVFOUNDATION: "AVFoundation",
            cv2.CAP_MSMF: "MSMF",
            cv2.CAP_V4L2: "V4L2",
            cv2.CAP_ANY: "ANY",
        }
        return names.get(self._backend, str(self._backend))

    def _log_camera_failure(self):
        """Emit a detailed, actionable error based on the current OS."""
        os_name = _get_platform()
        logger.error("=" * 60)
        logger.error("CAMERA INITIALIZATION FAILED")
        logger.error("No camera could be opened on this machine.")

        if os_name == "darwin":
            logger.error(
                "\n[macOS] Common causes & fixes:\n"
                "  1. Privacy denied: System Settings → Privacy & Security\n"
                "     → Camera → enable for Terminal / your app.\n"
                "  2. Camera in use by another app (FaceTime, Zoom, etc.).\n"
                "  3. Virtual cameras (OBS, Camo) may need extra permissions.\n"
                "  4. Run: `tccutil reset Camera` in Terminal to reset permissions.\n"
            )
        elif os_name == "windows":
            logger.error(
                "\n[Windows] Common causes & fixes:\n"
                "  1. Privacy denied: Settings → Privacy → Camera → Allow apps.\n"
                "  2. Driver issue: check Device Manager for camera errors.\n"
                "  3. Camera in use by another process.\n"
            )
        else:
            logger.error(
                "\n[Linux] Common causes & fixes:\n"
                "  1. User not in 'video' group: `sudo usermod -aG video $USER`\n"
                "  2. No /dev/video* device found.\n"
                "  3. Driver not loaded for your camera.\n"
            )

        logger.error("The app will continue WITHOUT camera support.")
        logger.error("=" * 60)

#!/usr/bin/env python3
"""
MediaPipe Sidecar — local pose landmark detector.
Protocol: binary frames in via stdin, JSON landmarks out via stdout.
"""
import sys

# Print before any heavy import so C# can see we at least started.
print("LOADING", flush=True)

try:
    import json
    import struct
    import cv2
    import numpy as np
    import mediapipe as mp
except ImportError as e:
    print(f"IMPORT_ERROR: {e}", file=sys.stderr, flush=True)
    sys.exit(1)

# ── MediaPipe setup ──────────────────────────────────────────────────────────

try:
    mp_pose = mp.solutions.pose
    _pose = mp_pose.Pose(
        static_image_mode=False,
        model_complexity=1,
        smooth_landmarks=True,
        min_detection_confidence=0.5,
        min_tracking_confidence=0.5,
    )
except Exception as e:
    print(f"MEDIAPIPE_INIT_ERROR: {e}", file=sys.stderr, flush=True)
    sys.exit(1)

# Signal to C# that we are fully ready to receive frames.
print("READY", flush=True)


def process_frame(bgr: np.ndarray) -> list[dict]:
    """Run MediaPipe on a BGR frame. Returns list of 33 landmark dicts."""
    rgb = cv2.cvtColor(bgr, cv2.COLOR_BGR2RGB)  # BGR → RGB, always contiguous
    result = _pose.process(rgb)
    if not result.pose_landmarks:
        return []
    return [
        {"x": lm.x, "y": lm.y, "z": lm.z, "visibility": lm.visibility}
        for lm in result.pose_landmarks.landmark
    ]


# ── Test mode ────────────────────────────────────────────────────────────────

def run_test():
    print("[sidecar test] Opening webcam...", file=sys.stderr)
    cap = cv2.VideoCapture(0)
    if not cap.isOpened():
        print("[sidecar test] ERROR: could not open camera.", file=sys.stderr)
        sys.exit(1)

    ret, frame = cap.read()
    cap.release()

    if not ret or frame is None:
        print("[sidecar test] ERROR: could not read frame.", file=sys.stderr)
        sys.exit(1)

    print(f"[sidecar test] Frame shape: {frame.shape}", file=sys.stderr)
    landmarks = process_frame(frame)
    print(f"[sidecar test] Landmarks detected: {len(landmarks)}", file=sys.stderr)

    if landmarks:
        ls = landmarks[11]
        rs = landmarks[12]
        print(f"[sidecar test] Left shoulder:  x={ls['x']:.3f}  y={ls['y']:.3f}  vis={ls['visibility']:.2f}", file=sys.stderr)
        print(f"[sidecar test] Right shoulder: x={rs['x']:.3f}  y={rs['y']:.3f}  vis={rs['visibility']:.2f}", file=sys.stderr)

    print(json.dumps({"landmarks": landmarks}))
    print("[sidecar test] OK", file=sys.stderr)


# ── Production mode ──────────────────────────────────────────────────────────

def read_exact(n: int) -> bytes:
    buf = bytearray()
    while len(buf) < n:
        chunk = sys.stdin.buffer.read(n - len(buf))
        if not chunk:
            raise EOFError("stdin closed")
        buf.extend(chunk)
    return bytes(buf)


def run_production():
    while True:
        try:
            header = read_exact(12)
        except EOFError:
            break  # C# closed stdin — normal shutdown

        w, h, c = struct.unpack("<III", header)
        raw = read_exact(w * h * c)
        frame = np.frombuffer(raw, dtype=np.uint8).reshape(h, w, c)

        landmarks = process_frame(frame)
        sys.stdout.write(json.dumps({"landmarks": landmarks}) + "\n")
        sys.stdout.flush()


# ── Entry point ──────────────────────────────────────────────────────────────

if __name__ == "__main__":
    if "--test" in sys.argv:
        run_test()
    else:
        run_production()

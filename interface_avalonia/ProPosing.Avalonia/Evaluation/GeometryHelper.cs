using ProPosing.Avalonia.Models;

namespace ProPosing.Avalonia.Evaluation;

/// <summary>
/// All geometry here operates on MediaPipe normalized coordinates
/// (x = px / imageWidth, y = px / imageHeight, z ≈ meters vs mid-hip).
/// Because x and y share the [0,1] range but map to different physical scales
/// on a non-square frame, every 2D computation must be aspect-corrected by
/// multiplying y by (imageWidth / imageHeight). All public helpers take an
/// <c>aspect</c> argument so callers cannot forget.
/// </summary>
public static class GeometryHelper
{
    // MediaPipe Pose landmark indices (0–32)
    public static class Idx
    {
        public const int Nose          = 0;
        public const int LeftEar       = 7;
        public const int RightEar      = 8;
        public const int LeftShoulder  = 11;
        public const int RightShoulder = 12;
        public const int LeftElbow     = 13;
        public const int RightElbow    = 14;
        public const int LeftWrist     = 15;
        public const int RightWrist    = 16;
        public const int LeftHip       = 23;
        public const int RightHip      = 24;
        public const int LeftKnee      = 25;
        public const int RightKnee     = 26;
        public const int LeftAnkle     = 27;
        public const int RightAnkle    = 28;
    }

    private const double Eps = 1e-6;

    // ── Angles ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Angle at B formed by A–B–C, in degrees, using aspect-corrected 2D coords.
    /// Returns <see cref="double.NaN"/> if either vector is degenerate.
    /// </summary>
    public static double Angle2D(LandmarkPoint a, LandmarkPoint b, LandmarkPoint c, double aspect)
    {
        double ax = a.X - b.X, ay = (a.Y - b.Y) * aspect;
        double cx = c.X - b.X, cy = (c.Y - b.Y) * aspect;
        return AngleFromVectors(ax, ay, 0, cx, cy, 0);
    }

    /// <summary>
    /// 3D angle at B formed by A–B–C using (x, aspect·y, z). Use when depth matters
    /// (any side/oblique pose) — otherwise 2D joint angles collapse in profile views.
    /// </summary>
    public static double Angle3D(LandmarkPoint a, LandmarkPoint b, LandmarkPoint c, double aspect)
    {
        double ax = a.X - b.X, ay = (a.Y - b.Y) * aspect, az = a.Z - b.Z;
        double cx = c.X - b.X, cy = (c.Y - b.Y) * aspect, cz = c.Z - b.Z;
        return AngleFromVectors(ax, ay, az, cx, cy, cz);
    }

    private static double AngleFromVectors(
        double ax, double ay, double az, double cx, double cy, double cz)
    {
        double magA = Math.Sqrt(ax * ax + ay * ay + az * az);
        double magC = Math.Sqrt(cx * cx + cy * cy + cz * cz);
        if (magA < Eps || magC < Eps) return double.NaN;
        double cos = Math.Clamp((ax * cx + ay * cy + az * cz) / (magA * magC), -1.0, 1.0);
        return Math.Acos(cos) * (180.0 / Math.PI);
    }

    // ── Distances ─────────────────────────────────────────────────────────────

    public static double Distance2D(LandmarkPoint a, LandmarkPoint b, double aspect)
    {
        double dx = a.X - b.X, dy = (a.Y - b.Y) * aspect;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public static double Distance3D(LandmarkPoint a, LandmarkPoint b, double aspect)
    {
        double dx = a.X - b.X, dy = (a.Y - b.Y) * aspect, dz = a.Z - b.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    public static double VerticalGap(LandmarkPoint top, LandmarkPoint bottom, double aspect)
        => (bottom.Y - top.Y) * aspect;   // positive when bottom is lower in the image

    // ── Scale references ──────────────────────────────────────────────────────

    public static double ShoulderSpan(IReadOnlyList<LandmarkPoint> lm, double aspect)
        => Distance2D(lm[Idx.LeftShoulder], lm[Idx.RightShoulder], aspect);

    public static double HipSpan(IReadOnlyList<LandmarkPoint> lm, double aspect)
        => Distance2D(lm[Idx.LeftHip], lm[Idx.RightHip], aspect);

    /// <summary>Torso length: mid-shoulder → mid-hip in aspect-corrected space.</summary>
    public static double TorsoLength(IReadOnlyList<LandmarkPoint> lm, double aspect)
    {
        var ls = lm[Idx.LeftShoulder]; var rs = lm[Idx.RightShoulder];
        var lh = lm[Idx.LeftHip];      var rh = lm[Idx.RightHip];
        double sx = (ls.X + rs.X) / 2.0;
        double sy = (ls.Y + rs.Y) / 2.0;
        double hx = (lh.X + rh.X) / 2.0;
        double hy = (lh.Y + rh.Y) / 2.0;
        double dx = sx - hx, dy = (sy - hy) * aspect;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    // ── Body orientation ──────────────────────────────────────────────────────

    /// <summary>
    /// Signed body yaw in degrees, derived from the shoulder-to-shoulder vector
    /// projected onto the camera's x-z plane. 0° ≈ facing camera, ±90° ≈ side,
    /// ±180° ≈ back. Independent of facial-landmark visibility.
    /// </summary>
    public static double BodyYawDegrees(LandmarkPoint ls, LandmarkPoint rs)
    {
        double dx = ls.X - rs.X;
        double dz = ls.Z - rs.Z;
        if (Math.Abs(dx) < Eps && Math.Abs(dz) < Eps) return 0.0;
        return Math.Atan2(dz, dx) * (180.0 / Math.PI);
    }

    /// <summary>Lateral tilt of the torso midline from vertical, in degrees.</summary>
    public static double LateralTiltDegrees(IReadOnlyList<LandmarkPoint> lm, double aspect)
    {
        var ls = lm[Idx.LeftShoulder]; var rs = lm[Idx.RightShoulder];
        var lh = lm[Idx.LeftHip];      var rh = lm[Idx.RightHip];
        double dx = (ls.X + rs.X) / 2.0 - (lh.X + rh.X) / 2.0;
        double dy = ((ls.Y + rs.Y) / 2.0 - (lh.Y + rh.Y) / 2.0) * aspect;
        if (Math.Abs(dx) < Eps && Math.Abs(dy) < Eps) return 0.0;
        // atan2(|dx|, |dy|) → 0 when perfectly vertical, grows with tilt.
        return Math.Atan2(Math.Abs(dx), Math.Abs(dy)) * (180.0 / Math.PI);
    }

    /// <summary>
    /// Torso twist in degrees: absolute yaw difference between shoulder line and
    /// hip line (measured in the x-z plane). High in "tea cup" / oblique poses.
    /// </summary>
    public static double TorsoTwistDegrees(IReadOnlyList<LandmarkPoint> lm)
    {
        double shoulderYaw = BodyYawDegrees(lm[Idx.LeftShoulder], lm[Idx.RightShoulder]);
        double hipYaw      = BodyYawDegrees(lm[Idx.LeftHip],      lm[Idx.RightHip]);
        double diff = Math.Abs(shoulderYaw - hipYaw);
        if (diff > 180) diff = 360 - diff;
        return diff;
    }

    /// <summary>
    /// Body view classification derived from yaw magnitude. The thresholds match
    /// typical bodybuilding pose categories (front, 3/4, side, back).
    /// </summary>
    public enum BodyView { Front, ThreeQuarter, Side, Back }

    public static BodyView Classify(double yawDeg)
    {
        double a = Math.Abs(yawDeg);
        if (a < 20)  return BodyView.Front;
        if (a < 60)  return BodyView.ThreeQuarter;
        if (a < 110) return BodyView.Side;
        return BodyView.Back;
    }

    // ── Visibility / confidence ───────────────────────────────────────────────

    /// <summary>
    /// Strict reliability: requires a populated confidence ≥ threshold.
    /// Prefer this over <see cref="IsVisible"/>; unknown confidence is NOT reliable.
    /// </summary>
    public static bool IsReliable(LandmarkPoint lm, double threshold = 0.5)
        => lm.Visibility.HasValue && lm.Visibility.Value >= threshold;

    /// <summary>
    /// Legacy permissive check: treats null visibility as visible.
    /// Kept only for cases where the sidecar omits visibility entirely.
    /// </summary>
    public static bool IsVisible(LandmarkPoint lm, double threshold = 0.5)
        => lm.Visibility is null || lm.Visibility >= threshold;

    public static double Weight(LandmarkPoint lm)
        => Math.Clamp(lm.Visibility ?? 0.0, 0.0, 1.0);

    public static bool AllReliable(params LandmarkPoint[] lms)
    {
        foreach (var p in lms) if (!IsReliable(p)) return false;
        return true;
    }

    // ── Limb metrics ──────────────────────────────────────────────────────────

    /// <summary>
    /// Collinearity ∈ [0,1] of a 3-point joint (A-B-C), where 1 ≈ straight (180°),
    /// 0 ≈ folded (0°). Returns NaN on degenerate vectors.
    /// </summary>
    public static double Collinearity(LandmarkPoint a, LandmarkPoint b, LandmarkPoint c, double aspect)
    {
        double angle = Angle3D(a, b, c, aspect);
        if (double.IsNaN(angle)) return double.NaN;
        return 1.0 - Math.Abs(180.0 - angle) / 180.0;
    }
}

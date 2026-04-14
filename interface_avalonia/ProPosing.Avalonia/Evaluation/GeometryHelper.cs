using ProPosing.Avalonia.Models;

namespace ProPosing.Avalonia.Evaluation;

public static class GeometryHelper
{
    // MediaPipe Pose landmark indices (0-32)
    public static class Idx
    {
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

    /// <summary>
    /// Angle in degrees at point B, formed by A–B–C.
    /// Works in normalized [0,1] landmark space.
    /// </summary>
    public static double Angle(LandmarkPoint a, LandmarkPoint b, LandmarkPoint c)
    {
        double ax = a.X - b.X, ay = a.Y - b.Y;
        double cx = c.X - b.X, cy = c.Y - b.Y;
        double dot  = ax * cx + ay * cy;
        double magA = Math.Sqrt(ax * ax + ay * ay);
        double magC = Math.Sqrt(cx * cx + cy * cy);
        if (magA < 1e-6 || magC < 1e-6) return 0;
        return Math.Acos(Math.Clamp(dot / (magA * magC), -1.0, 1.0)) * (180.0 / Math.PI);
    }

    public static bool IsVisible(LandmarkPoint lm, double threshold = 0.5)
        => lm.Visibility is null || lm.Visibility >= threshold;
}

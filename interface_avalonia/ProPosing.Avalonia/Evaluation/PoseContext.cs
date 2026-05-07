using ProPosing.Avalonia.Models;
using static ProPosing.Avalonia.Evaluation.GeometryHelper;

namespace ProPosing.Avalonia.Evaluation;

/// <summary>
/// Per-frame evaluation context shared by all pose evaluators.
/// Bundles landmarks with derived geometric facts (aspect-correct scale references,
/// body orientation, lateral tilt, torso twist) so individual evaluators no longer
/// reimplement or miscompute these primitives.
/// </summary>
public readonly record struct PoseContext(
    IReadOnlyList<LandmarkPoint> Landmarks,
    double Aspect,               // imageWidth / imageHeight
    double ShoulderSpan,         // aspect-corrected
    double TorsoLength,          // aspect-corrected
    double HipSpan,              // aspect-corrected
    double BodyYawDeg,           // signed; 0 = facing camera, ±90 = side
    BodyView View,
    double LateralTiltDeg,       // torso midline vs vertical
    double TorsoTwistDeg)        // |shoulder yaw − hip yaw|
{
    /// <summary>Preferred scale reference for normalizing distances.</summary>
    public double ScaleRef
    {
        get
        {
            if (double.IsFinite(ShoulderSpan) && ShoulderSpan > 1e-6) return ShoulderSpan;
            if (double.IsFinite(TorsoLength)  && TorsoLength  > 1e-6) return TorsoLength;
            return 1.0;
        }
    }

    public static PoseContext FromLandmarks(IReadOnlyList<LandmarkPoint> lms, double aspect)
    {
        var ls = lms[Idx.LeftShoulder]; var rs = lms[Idx.RightShoulder];
        double shoulder = GeometryHelper.ShoulderSpan(lms, aspect);
        double torso    = GeometryHelper.TorsoLength(lms, aspect);
        double hip      = GeometryHelper.HipSpan(lms, aspect);
        double yaw      = GeometryHelper.BodyYawDegrees(ls, rs);
        double tilt     = GeometryHelper.LateralTiltDegrees(lms, aspect);
        double twist    = GeometryHelper.TorsoTwistDegrees(lms);
        return new(lms, aspect, shoulder, torso, hip, yaw, Classify(yaw), tilt, twist);
    }
}

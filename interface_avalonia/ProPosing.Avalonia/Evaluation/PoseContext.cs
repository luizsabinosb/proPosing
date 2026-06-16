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
    double TorsoLength,          // aspect-corrected (shoulder-span estimate when hips occluded)
    double HipSpan,              // aspect-corrected; NaN when hips not reliable
    double BodyYawDeg,           // signed; 0 = facing camera, ±90 = side
    BodyView View,
    double LateralTiltDeg,       // torso midline vs vertical; NaN when hips not reliable
    double TorsoTwistDeg,        // |shoulder yaw − hip yaw|; NaN when hips not reliable
    bool HipsReliable,           // both hips visible — hip-derived metrics trustworthy
    bool LowerBodyVisible)       // hips + at least one knee visible — legs can be judged
{
    /// <summary>
    /// When the hips are out of frame (waist-up framing), MediaPipe still reports
    /// extrapolated hip landmarks, so a true mid-shoulder→mid-hip torso length is
    /// unavailable. We estimate scale from shoulder span instead; ~1.35× shoulder
    /// span approximates adult torso length closely enough for ratio thresholds.
    /// </summary>
    private const double TorsoToShoulderRatio = 1.35;

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
        var lh = lms[Idx.LeftHip];      var rh = lms[Idx.RightHip];

        double shoulder = GeometryHelper.ShoulderSpan(lms, aspect);
        double yaw      = GeometryHelper.BodyYawDegrees(ls, rs);   // shoulders only — valid waist-up

        // Hip-derived quantities are only trustworthy when both hips are reliably seen.
        // Otherwise treat them as unavailable (NaN) and estimate scale from the shoulders,
        // so a waist-up pose is judged on what is visible instead of on garbage hip data.
        bool hipsReliable     = IsReliable(lh) && IsReliable(rh);
        bool kneesVisible     = IsReliable(lms[Idx.LeftKnee]) || IsReliable(lms[Idx.RightKnee]);
        bool lowerBodyVisible = hipsReliable && kneesVisible;

        double torso = hipsReliable
            ? GeometryHelper.TorsoLength(lms, aspect)
            : (shoulder > 1e-6 ? shoulder * TorsoToShoulderRatio : double.NaN);
        double hip   = hipsReliable ? GeometryHelper.HipSpan(lms, aspect)         : double.NaN;
        double tilt  = hipsReliable ? GeometryHelper.LateralTiltDegrees(lms, aspect) : double.NaN;
        double twist = hipsReliable ? GeometryHelper.TorsoTwistDegrees(lms)       : double.NaN;

        return new(lms, aspect, shoulder, torso, hip, yaw, Classify(yaw),
                   tilt, twist, hipsReliable, lowerBodyVisible);
    }
}

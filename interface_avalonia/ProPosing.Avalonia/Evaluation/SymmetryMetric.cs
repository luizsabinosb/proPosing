using static ProPosing.Avalonia.Evaluation.GeometryHelper;

namespace ProPosing.Avalonia.Evaluation;

/// <summary>
/// Unified symmetry score. Combines elbow-height, wrist-height, knee-height and
/// shoulder-height asymmetries into a single [0,1] measure, torso-normalized so
/// distance to camera does not distort it. Missing landmarks are skipped via
/// visibility weights rather than discarded entirely, so partial occlusion
/// degrades the score gracefully instead of zeroing it.
/// </summary>
public static class SymmetryMetric
{
    /// <summary>Returns a symmetry score in [0,1]. Higher = more symmetric. NaN if nothing usable.</summary>
    public static double Compute(PoseContext ctx)
    {
        var lms = ctx.Landmarks;
        double scale = ctx.ScaleRef;
        if (scale <= 1e-6) return double.NaN;

        double sumWeighted = 0, sumWeights = 0;

        void AddPair(int leftIdx, int rightIdx, double weight)
        {
            var l = lms[leftIdx]; var r = lms[rightIdx];
            double w = Math.Min(Weight(l), Weight(r));
            if (w < 1e-3) return;
            double asym = Math.Abs(l.Y - r.Y) * ctx.Aspect / scale;
            sumWeighted += w * weight * asym;
            sumWeights  += w * weight;
        }

        AddPair(Idx.LeftShoulder, Idx.RightShoulder, 1.0);
        AddPair(Idx.LeftElbow,    Idx.RightElbow,    1.0);
        AddPair(Idx.LeftWrist,    Idx.RightWrist,    0.7);
        AddPair(Idx.LeftKnee,     Idx.RightKnee,     0.8);
        AddPair(Idx.LeftHip,      Idx.RightHip,      0.9);

        if (sumWeights < 1e-3) return double.NaN;
        double meanAsym = sumWeighted / sumWeights;
        // An asymmetry of 0 → score 1. An asymmetry of ½ torso length → score ≈ 0.
        return Math.Clamp(1.0 - meanAsym * 2.0, 0.0, 1.0);
    }
}

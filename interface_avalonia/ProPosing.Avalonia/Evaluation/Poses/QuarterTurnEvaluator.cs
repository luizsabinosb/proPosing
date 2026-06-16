using ProPosing.Avalonia.Models;
using static ProPosing.Avalonia.Evaluation.GeometryHelper;

namespace ProPosing.Avalonia.Evaluation.Poses;

/// <summary>
/// Quarter Turn: true 90° side profile. Front arm bent ~90° at waist level,
/// back arm more extended, body completely sideways to camera.
/// </summary>
public sealed class QuarterTurnEvaluator : IPoseEvaluator
{
    private readonly QuarterTurnThresholds _t;

    public QuarterTurnEvaluator(QuarterTurnThresholds? thresholds = null)
        => _t = thresholds ?? new QuarterTurnThresholds();

    public string PoseMode => "quarter_turn_side";

    public PoseFeedback Evaluate(PoseContext ctx)
    {
        if (ctx.View != BodyView.Side)
            return new PoseFeedback("incorrect", "Vire completamente de lado — perfil de 90°", []);

        var lms = ctx.Landmarks;
        var ls = lms[Idx.LeftShoulder];  var rs = lms[Idx.RightShoulder];
        var le = lms[Idx.LeftElbow];     var re = lms[Idx.RightElbow];
        var lw = lms[Idx.LeftWrist];     var rw = lms[Idx.RightWrist];
        var lh = lms[Idx.LeftHip];       var rh = lms[Idx.RightHip];
        var lk = lms[Idx.LeftKnee];      var rk = lms[Idx.RightKnee];
        var la = lms[Idx.LeftAnkle];     var ra = lms[Idx.RightAnkle];

        bool leftOk  = AllReliable(ls, le, lw);
        bool rightOk = AllReliable(rs, re, rw);

        // Arms are defining for this pose: with neither detected, every check below
        // would be skipped and the pose would pass trivially.
        if (!leftOk && !rightOk)
            return new PoseFeedback("incorrect", "Braços não detectados — verifique o enquadramento", []);

        // Front arm = nearer shoulder (lower z). Stable in true profile where X collapses.
        bool leftIsFront = IsReliable(ls) && IsReliable(rs) ? ls.Z <= rs.Z : leftOk;
        var (fs, fe, fw) = leftIsFront ? (ls, le, lw) : (rs, re, rw);
        var (bs, be, bw) = leftIsFront ? (rs, re, rw) : (ls, le, lw);
        bool frontOk = leftIsFront ? leftOk : rightOk;
        bool backOk  = leftIsFront ? rightOk : leftOk;

        var errors = new List<string>();

        if (frontOk)
        {
            double angle = Angle3D(fs, fe, fw, ctx.Aspect);
            if (!double.IsNaN(angle))
            {
                if (angle < _t.FrontElbowMinAngle)
                    errors.Add($"Braço frontal muito fechado — {_t.FrontElbowMinAngle}–{_t.FrontElbowMaxAngle}° (atual: {angle:F0}°)");
                else if (angle > _t.FrontElbowMaxAngle)
                    errors.Add($"Braço frontal muito aberto — {_t.FrontElbowMinAngle}–{_t.FrontElbowMaxAngle}° (atual: {angle:F0}°)");
            }
        }

        if (backOk)
        {
            double angle = Angle3D(bs, be, bw, ctx.Aspect);
            if (!double.IsNaN(angle) && angle < _t.BackArmMinAngle)
                errors.Add($"Estenda mais o braço traseiro (atual: {angle:F0}°)");
        }

        // Front leg is the leg whose hip z is smaller (near hip). Check whichever is reliable.
        bool leftLegIsFront = IsReliable(lh) && IsReliable(rh) ? lh.Z <= rh.Z : IsReliable(lh);
        var (h, k, a) = leftLegIsFront ? (lh, lk, la) : (rh, rk, ra);
        if (AllReliable(h, k, a))
        {
            double angle = Angle3D(h, k, a, ctx.Aspect);
            if (!double.IsNaN(angle) && angle < _t.KneeMinAngle)
                errors.Add($"Estenda mais a perna ({angle:F0}°)");
        }

        return PoseFeedback.FromErrors(errors, "Excelente quarter turn! Perfil e postura impecáveis.");
    }
}

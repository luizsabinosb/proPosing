using ProPosing.Avalonia.Models;
using static ProPosing.Avalonia.Evaluation.GeometryHelper;

namespace ProPosing.Avalonia.Evaluation.Poses;

/// <summary>
/// Side Triceps: ~85–90° side profile, posterior arm fully extended
/// with wrist at hip/thigh level to showcase the triceps.
/// </summary>
public sealed class SideTricepsEvaluator : IPoseEvaluator
{
    private readonly SideTricepsThresholds _t;

    public SideTricepsEvaluator(SideTricepsThresholds? thresholds = null)
        => _t = thresholds ?? new SideTricepsThresholds();

    public string PoseMode => "side_triceps";

    public PoseFeedback Evaluate(PoseContext ctx)
    {
        if (ctx.View != BodyView.Side)
            return new PoseFeedback("incorrect", "Vire completamente de lado para a câmera", []);

        var lms = ctx.Landmarks;
        var ls = lms[Idx.LeftShoulder];  var rs = lms[Idx.RightShoulder];
        var le = lms[Idx.LeftElbow];     var re = lms[Idx.RightElbow];
        var lw = lms[Idx.LeftWrist];     var rw = lms[Idx.RightWrist];
        var lh = lms[Idx.LeftHip];       var rh = lms[Idx.RightHip];
        var lk = lms[Idx.LeftKnee];      var rk = lms[Idx.RightKnee];
        var la = lms[Idx.LeftAnkle];     var ra = lms[Idx.RightAnkle];

        bool leftOk  = AllReliable(ls, le, lw);
        bool rightOk = AllReliable(rs, re, rw);
        if (!leftOk && !rightOk)
            return new PoseFeedback("incorrect", "Braços não detectados — verifique o enquadramento", []);

        // Posterior arm = the one further from camera (larger z) AND the more extended.
        // Use z as primary signal; fall back to extension if z is ambiguous.
        bool useLeft = PickBackByZ(ls, rs, leftOk, rightOk);
        var (ps, pe, pw) = useLeft ? (ls, le, lw) : (rs, re, rw);
        if (useLeft ? !leftOk : !rightOk)
            return new PoseFeedback("incorrect", "Braço posterior não detectado", []);

        double torso = Math.Max(ctx.TorsoLength, 1e-6);
        var errors = new List<string>();

        // 1. Arm extension (close to straight). Use 3D to avoid profile collapse.
        double armAngle = Angle3D(ps, pe, pw, ctx.Aspect);
        if (!double.IsNaN(armAngle) && armAngle < _t.ArmMinAngle)
            errors.Add($"Estenda mais o braço — tríceps deve estar quase reto (atual: {armAngle:F0}°)");

        // 2. Elbow must be clearly below shoulder — ratio to torso.
        double elbowBelow = VerticalGap(ps, pe, ctx.Aspect) / torso;
        if (elbowBelow < _t.ElbowBelowShoulderRatioMin)
            errors.Add("Abaixe o braço — cotovelo deve estar abaixo do ombro, apontando para baixo");

        // 3. Wrist near the near hip (Z-selected), not an arbitrary visible one.
        var nearHip = lh.Z <= rh.Z ? lh : rh;
        if (IsReliable(nearHip))
        {
            double wristOverHip = -VerticalGap(nearHip, pw, ctx.Aspect) / torso; // positive if wrist above hip
            if (wristOverHip > _t.WristHipRatioTolerance)
                errors.Add("Puxe o braço para baixo — pulso deve estar na altura do quadril");
        }

        // 4. Front leg extended. Near leg = leg whose hip has smaller z.
        bool leftLegIsFront = lh.Z <= rh.Z;
        var (h, k, a) = leftLegIsFront ? (lh, lk, la) : (rh, rk, ra);
        if (AllReliable(h, k, a))
        {
            double kneeAngle = Angle3D(h, k, a, ctx.Aspect);
            if (!double.IsNaN(kneeAngle) && kneeAngle < _t.KneeMinAngle)
                errors.Add($"Estenda a perna frontal (~180°) (atual: {kneeAngle:F0}°)");
        }

        return PoseFeedback.FromErrors(errors, "Excelente side triceps! Tríceps bem estendido e destacado.");
    }

    private static bool PickBackByZ(LandmarkPoint ls, LandmarkPoint rs, bool leftOk, bool rightOk)
    {
        if (leftOk && !rightOk) return true;
        if (rightOk && !leftOk) return false;
        return ls.Z >= rs.Z;
    }
}

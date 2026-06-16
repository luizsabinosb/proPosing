using ProPosing.Avalonia.Models;
using static ProPosing.Avalonia.Evaluation.GeometryHelper;

namespace ProPosing.Avalonia.Evaluation.Poses;

/// <summary>
/// Tea Cup: ~3/4 oblique turn, one arm fully raised and flexed overhead,
/// other arm relaxed at the hip, torso rotated to show obliques.
/// </summary>
public sealed class TeaCupEvaluator : IPoseEvaluator
{
    private readonly TeaCupThresholds _t;

    public TeaCupEvaluator(TeaCupThresholds? thresholds = null)
        => _t = thresholds ?? new TeaCupThresholds();

    public string PoseMode => "teacup";

    public PoseFeedback Evaluate(PoseContext ctx)
    {
        var lms = ctx.Landmarks;
        var ls = lms[Idx.LeftShoulder];  var rs = lms[Idx.RightShoulder];
        var le = lms[Idx.LeftElbow];     var re = lms[Idx.RightElbow];
        var lw = lms[Idx.LeftWrist];     var rw = lms[Idx.RightWrist];
        var lh = lms[Idx.LeftHip];       var rh = lms[Idx.RightHip];
        var lk = lms[Idx.LeftKnee];      var rk = lms[Idx.RightKnee];
        var la = lms[Idx.LeftAnkle];     var ra = lms[Idx.RightAnkle];

        double torso = Math.Max(ctx.TorsoLength, 1e-6);
        var errors = new List<string>();

        // 1. Torso twist — the real quantity. Replaces "one shoulder higher" hack.
        //    Defining feature: without reliable hips, TorsoTwistDeg is NaN and the
        //    comparison would silently pass — ask for the hips instead.
        if (!ctx.HipsReliable)
            errors.Add("Inclua o quadril no enquadramento — a rotação do tronco define o tea cup");
        else if (ctx.TorsoTwistDeg < _t.TorsoTwistMinDeg)
            errors.Add("Gire o torso em 3/4 — ombros e quadris devem estar desalinhados");

        // 2. Identify the raised arm. Require wrist well above shoulder, not just higher.
        double leftRaise  = IsReliable(lw) && IsReliable(ls)
            ? -VerticalGap(lw, ls, ctx.Aspect) / torso : double.NegativeInfinity;
        double rightRaise = IsReliable(rw) && IsReliable(rs)
            ? -VerticalGap(rw, rs, ctx.Aspect) / torso : double.NegativeInfinity;

        bool leftRaised  = leftRaise  > _t.RaisedWristAboveShoulderRatioMin;
        bool rightRaised = rightRaise > _t.RaisedWristAboveShoulderRatioMin;

        if (!leftRaised && !rightRaised)
        {
            // Provide a more actionable hint when *neither* arm is far enough up.
            double best = Math.Max(leftRaise, rightRaise);
            if (double.IsFinite(best))
                errors.Add("Levante mais o braço — punho deve estar claramente acima do ombro");
            else
                errors.Add("Levante um braço com o cotovelo flexionado — punho acima do ombro");
        }
        else
        {
            // Prefer the clearly-higher arm; only defer to leftRaised if tied.
            bool useLeft = leftRaise >= rightRaise;
            var (rs1, re1, rw1) = useLeft ? (ls, le, lw) : (rs, re, rw);
            var (lowHip, lowWrist) = useLeft ? (rh, rw) : (lh, lw);

            // 3. Raised arm flex angle.
            if (AllReliable(rs1, re1, rw1))
            {
                double angle = Angle3D(rs1, re1, rw1, ctx.Aspect);
                if (!double.IsNaN(angle))
                {
                    if (angle < _t.RaisedElbowMinAngle)
                        errors.Add($"Braço levantado muito fechado — {_t.RaisedElbowMinAngle}–{_t.RaisedElbowMaxAngle}° (atual: {angle:F0}°)");
                    else if (angle > _t.RaisedElbowMaxAngle)
                        errors.Add($"Braço levantado muito aberto — {_t.RaisedElbowMinAngle}–{_t.RaisedElbowMaxAngle}° (atual: {angle:F0}°)");
                }
            }

            // 4. Low arm relaxed near the hip.
            if (AllReliable(lowWrist, lowHip))
            {
                double deviation = Math.Abs(lowWrist.Y - lowHip.Y) * ctx.Aspect / torso;
                if (deviation > _t.LowWristHipRatioMax)
                    errors.Add("Braço baixo deve estar relaxado ao nível do quadril");
            }
        }

        // 5. Knees reasonably extended.
        if (AllReliable(lh, lk, la))
        {
            double angle = Angle3D(lh, lk, la, ctx.Aspect);
            if (!double.IsNaN(angle) && angle < _t.KneeMinAngle)
                errors.Add($"Estenda mais a perna esquerda ({angle:F0}°)");
        }
        if (AllReliable(rh, rk, ra))
        {
            double angle = Angle3D(rh, rk, ra, ctx.Aspect);
            if (!double.IsNaN(angle) && angle < _t.KneeMinAngle)
                errors.Add($"Estenda mais a perna direita ({angle:F0}°)");
        }

        return PoseFeedback.FromErrors(errors, "Excelente tea cup! Oblíquos e postura em destaque.");
    }
}

using ProPosing.Avalonia.Models;
using static ProPosing.Avalonia.Evaluation.GeometryHelper;

namespace ProPosing.Avalonia.Evaluation.Poses;

/// <summary>
/// Most Muscular: front-facing, both hands brought close at lower chest/abdomen,
/// elbows flared outward and downward, body slightly leaned forward.
/// </summary>
public sealed class MostMuscularEvaluator : IPoseEvaluator
{
    private readonly MostMuscularThresholds _t;

    public MostMuscularEvaluator(MostMuscularThresholds? thresholds = null)
        => _t = thresholds ?? new MostMuscularThresholds();

    public string PoseMode => "most_muscular";

    public PoseFeedback Evaluate(PoseContext ctx)
    {
        if (ctx.View == BodyView.Back)
            return new PoseFeedback("incorrect", "Vire de frente para a câmera", []);

        var lms = ctx.Landmarks;
        var ls = lms[Idx.LeftShoulder];  var rs = lms[Idx.RightShoulder];
        var le = lms[Idx.LeftElbow];     var re = lms[Idx.RightElbow];
        var lw = lms[Idx.LeftWrist];     var rw = lms[Idx.RightWrist];
        var lh = lms[Idx.LeftHip];       var rh = lms[Idx.RightHip];
        var lk = lms[Idx.LeftKnee];      var rk = lms[Idx.RightKnee];
        var la = lms[Idx.LeftAnkle];     var ra = lms[Idx.RightAnkle];

        // Upper-body framing is allowed: evaluate the arms/torso from what is visible.
        // Lower-body feedback is gated on visibility so we never invent leg errors.
        if (!AllReliable(ls, rs))
            return new PoseFeedback("incorrect", "Ombros não visíveis — ajuste o enquadramento", []);

        double torso = Math.Max(ctx.TorsoLength, 1e-6);
        var errors = new List<string>();

        // 1. Elbows below shoulders (torso-normalized).
        if (AllReliable(ls, le))
        {
            double below = VerticalGap(ls, le, ctx.Aspect) / torso;
            if (below < _t.ElbowBelowShoulderRatioMin)
                errors.Add("Cotovelo esquerdo deve estar abaixo do ombro — puxe para baixo");
        }
        if (AllReliable(rs, re))
        {
            double below = VerticalGap(rs, re, ctx.Aspect) / torso;
            if (below < _t.ElbowBelowShoulderRatioMin)
                errors.Add("Cotovelo direito deve estar abaixo do ombro — puxe para baixo");
        }

        // 2. Hands brought close together at the abdomen — the DEFINING feature of the
        //    pose. Not skippable: if the wrists aren't detected (arms down / out of frame)
        //    the athlete isn't presenting the pose, so we flag it instead of passing.
        if (!AllReliable(lw, rw))
        {
            errors.Add("Traga as duas mãos à frente, próximas no centro do abdômen");
        }
        else
        {
            if (ctx.ShoulderSpan > 1e-6)
            {
                double wristDist = Distance2D(lw, rw, ctx.Aspect);
                if (wristDist / ctx.ShoulderSpan > _t.WristProximityRatio)
                    errors.Add("Aproxime mais as mãos — punhos devem estar juntos no centro do corpo");
            }

            // Wrists at abdomen level — not raised above the shoulders.
            double shoulderMidY = (ls.Y + rs.Y) / 2.0;
            double wristMidY    = (lw.Y + rw.Y) / 2.0;
            double above = (shoulderMidY - wristMidY) * ctx.Aspect / torso;
            if (above > _t.WristAboveShoulderRatioMax)
                errors.Add("Abaixe as mãos — punhos devem estar na altura do abdômen");
        }

        // 4. Lateral torso tilt — only when hips are visible (tilt needs the hip line).
        if (ctx.HipsReliable && ctx.LateralTiltDeg > _t.TorsoTiltMaxDeg)
            errors.Add("Mantenha o torso alinhado — não incline para um lado");

        // 5. Knees reasonably extended — skipped entirely on waist-up framing.
        if (ctx.LowerBodyVisible)
        {
            CheckKnee(errors, "esquerda", ctx, lh, lk, la);
            CheckKnee(errors, "direita",  ctx, rh, rk, ra);
        }

        return PoseFeedback.FromErrors(errors, "Excelente most muscular! Musculatura máxima em destaque.");
    }

    private void CheckKnee(List<string> errors, string side, PoseContext ctx,
        LandmarkPoint h, LandmarkPoint k, LandmarkPoint a)
    {
        if (!AllReliable(h, k, a)) return;
        double angle = Angle3D(h, k, a, ctx.Aspect);
        if (!double.IsNaN(angle) && angle < _t.KneeMinAngle)
            errors.Add($"Estenda mais a perna {side} (atual: {angle:F0}°)");
    }
}

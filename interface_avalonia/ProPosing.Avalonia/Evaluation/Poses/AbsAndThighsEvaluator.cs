using ProPosing.Avalonia.Models;
using static ProPosing.Avalonia.Evaluation.GeometryHelper;

namespace ProPosing.Avalonia.Evaluation.Poses;

/// <summary>
/// Abs and Thighs: front-facing, both arms raised with elbows wide,
/// one leg stepped forward with knee bent to contract the quad.
/// </summary>
public sealed class AbsAndThighsEvaluator : IPoseEvaluator
{
    private readonly AbsAndThighsThresholds _t;

    public AbsAndThighsEvaluator(AbsAndThighsThresholds? thresholds = null)
        => _t = thresholds ?? new AbsAndThighsThresholds();

    public string PoseMode => "abs_and_thighs";

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

        // Hips required; knees allowed to be partially occluded (rear leg often is).
        if (!AllReliable(lh, rh))
            return new PoseFeedback("incorrect",
                "Recue da câmera para mostrar o corpo inteiro", []);

        double torso = Math.Max(ctx.TorsoLength, 1e-6);
        var errors = new List<string>();

        // 1. Wrists above shoulders — torso-normalized, per-side.
        if (AllReliable(ls, lw))
        {
            double below = VerticalGap(ls, lw, ctx.Aspect) / torso; // negative when wrist above
            if (below > _t.WristAboveShoulderRatioMax)
                errors.Add("Levante o braço esquerdo — punho deve estar acima do ombro");
        }
        if (AllReliable(rs, rw))
        {
            double below = VerticalGap(rs, rw, ctx.Aspect) / torso;
            if (below > _t.WristAboveShoulderRatioMax)
                errors.Add("Levante o braço direito — punho deve estar acima do ombro");
        }

        // 2. Elbows spread — ratio to shoulder span, aspect-correct.
        if (AllReliable(le, re) && ctx.ShoulderSpan > 1e-6)
        {
            double elbowSpan = Distance2D(le, re, ctx.Aspect);
            if (elbowSpan / ctx.ShoulderSpan < _t.ElbowSpreadMinRatio)
                errors.Add("Abra mais os cotovelos — mantenha-os bem afastados para expor o abdômen");
        }

        // 3. Leg step-forward: asymmetric knee heights (torso-normalized).
        //    Uses knees only if at least one side is reliable.
        bool lkOk = IsReliable(lk), rkOk = IsReliable(rk);
        if (lkOk && rkOk)
        {
            double kneeYDiff = Math.Abs(lk.Y - rk.Y) * ctx.Aspect / torso;
            if (kneeYDiff < _t.KneeAsymmetryRatioMin)
                errors.Add("Avance uma perna à frente e contraia o quadríceps");
        }
        else if (!lkOk && !rkOk)
        {
            errors.Add("Ajuste a câmera para enquadrar as pernas");
        }

        // 4. Lateral torso tilt — use the geometric angle.
        if (ctx.LateralTiltDeg > _t.TorsoTiltMaxDeg)
            errors.Add("Mantenha o torso alinhado — não incline o corpo para o lado");

        return PoseFeedback.FromErrors(errors, "Excelente abs and thighs! Abdômen e quadríceps bem marcados.");
    }
}

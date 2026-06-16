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
        var lk = lms[Idx.LeftKnee];      var rk = lms[Idx.RightKnee];

        // Upper-body framing is allowed: judge abs/arms from what is visible. The
        // thigh (leg) checks below are gated on lower-body visibility, so a waist-up
        // shot is evaluated on the abs without inventing leg errors.
        if (!AllReliable(ls, rs))
            return new PoseFeedback("incorrect", "Ombros não visíveis — ajuste o enquadramento", []);

        double torso = Math.Max(ctx.TorsoLength, 1e-6);
        var errors = new List<string>();

        // 1. Hands up behind the head (wrists above shoulders) — the DEFINING feature.
        //    Not skippable: an undetected OR low wrist both mean "not raised", so we
        //    never let missing arms silently pass as a correct pose.
        CheckRaised(errors, "esquerdo", ls, lw, torso, ctx);
        CheckRaised(errors, "direito",  rs, rw, torso, ctx);

        // 2. Elbows spread — ratio to shoulder span, aspect-correct.
        if (AllReliable(le, re) && ctx.ShoulderSpan > 1e-6)
        {
            double elbowSpan = Distance2D(le, re, ctx.Aspect);
            if (elbowSpan / ctx.ShoulderSpan < _t.ElbowSpreadMinRatio)
                errors.Add("Abra mais os cotovelos — mantenha-os bem afastados para expor o abdômen");
        }

        // 3. Leg step-forward: asymmetric knee heights (torso-normalized).
        //    Only evaluated when the legs are actually in frame — a waist-up shot
        //    skips this silently instead of asking the athlete to show the legs.
        if (ctx.LowerBodyVisible && IsReliable(lk) && IsReliable(rk))
        {
            double kneeYDiff = Math.Abs(lk.Y - rk.Y) * ctx.Aspect / torso;
            if (kneeYDiff < _t.KneeAsymmetryRatioMin)
                errors.Add("Avance uma perna à frente e contraia o quadríceps");
        }

        // 4. Lateral torso tilt — only when hips are visible (tilt needs the hip line).
        if (ctx.HipsReliable && ctx.LateralTiltDeg > _t.TorsoTiltMaxDeg)
            errors.Add("Mantenha o torso alinhado — não incline o corpo para o lado");

        return PoseFeedback.FromErrors(errors, "Excelente abs and thighs! Abdômen e quadríceps bem marcados.");
    }

    private void CheckRaised(List<string> errors, string side,
        LandmarkPoint shoulder, LandmarkPoint wrist, double torso, PoseContext ctx)
    {
        // VerticalGap(top,bottom) > 0 when the wrist sits below the shoulder. An
        // unreliable wrist is treated as +∞ ("definitely not raised") so a missing
        // hand triggers the same "raise your arm" guidance rather than being skipped.
        double below = AllReliable(shoulder, wrist)
            ? VerticalGap(shoulder, wrist, ctx.Aspect) / torso
            : double.PositiveInfinity;
        if (below > _t.WristAboveShoulderRatioMax)
            errors.Add($"Levante o braço {side} — mão atrás da cabeça, acima do ombro");
    }
}

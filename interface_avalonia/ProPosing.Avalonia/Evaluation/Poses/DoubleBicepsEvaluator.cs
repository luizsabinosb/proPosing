using ProPosing.Avalonia.Models;
using static ProPosing.Avalonia.Evaluation.GeometryHelper;

namespace ProPosing.Avalonia.Evaluation.Poses;

/// <summary>
/// Double Biceps: front-on, both arms raised with elbows at shoulder height,
/// flexed ~30–80°, elbows spread beyond shoulder width.
/// </summary>
public sealed class DoubleBicepsEvaluator : IPoseEvaluator
{
    private readonly DoubleBicepsThresholds _t;

    public DoubleBicepsEvaluator(DoubleBicepsThresholds? thresholds = null)
        => _t = thresholds ?? new DoubleBicepsThresholds();

    public string PoseMode => "double_biceps";

    public PoseFeedback Evaluate(PoseContext ctx)
    {
        var lms = ctx.Landmarks;
        var ls = lms[Idx.LeftShoulder];  var rs = lms[Idx.RightShoulder];
        var le = lms[Idx.LeftElbow];     var re = lms[Idx.RightElbow];
        var lw = lms[Idx.LeftWrist];     var rw = lms[Idx.RightWrist];

        // Orientation-gated: reject back views entirely, nudge 3/4 → front.
        if (ctx.View == BodyView.Back)
            return new PoseFeedback("incorrect", "Vire de frente para a câmera", []);

        bool leftOk  = AllReliable(ls, le, lw);
        bool rightOk = AllReliable(rs, re, rw);
        if (!leftOk && !rightOk)
            return new PoseFeedback("incorrect", "Braços não detectados — verifique o enquadramento", []);

        double torso = ctx.TorsoLength;
        if (torso < 1e-6) torso = ctx.ShoulderSpan > 1e-6 ? ctx.ShoulderSpan : 1.0;

        var errors = new List<string>();

        // 1. Elbows at shoulder height — drop normalized by torso.
        if (leftOk && VerticalGap(ls, le, ctx.Aspect) / torso > _t.ElbowDropRatioMax)
            errors.Add("Eleve o cotovelo esquerdo até a altura do ombro");
        if (rightOk && VerticalGap(rs, re, ctx.Aspect) / torso > _t.ElbowDropRatioMax)
            errors.Add("Eleve o cotovelo direito até a altura do ombro");

        // 2. Flex angles — 2D (image plane). This is a FRONT pose: the arm moves in the
        //    frontal plane, so the XY angle is the true bend. Using 3D here let MediaPipe's
        //    noisy z (elbow pushed forward, shoulder+fist behind it → opposite-sign z on the
        //    two vectors) inflate a clearly-flexed arm past 100° ("muito aberto" false error).
        if (leftOk) CheckFlex(errors, "esquerdo", Angle2D(ls, le, lw, ctx.Aspect));
        if (rightOk) CheckFlex(errors, "direito", Angle2D(rs, re, rw, ctx.Aspect));

        // 3. Elbow spread vs shoulder span — ratio, aspect-correct.
        if (leftOk && rightOk && ctx.ShoulderSpan > 1e-6)
        {
            double elbowSpan = Distance2D(le, re, ctx.Aspect);
            if (elbowSpan / ctx.ShoulderSpan < _t.ElbowSpreadMinRatio)
                errors.Add("Abra mais os cotovelos para os lados — braços bem abertos");
        }

        // 4. Elbow-height symmetry — torso-normalized.
        if (leftOk && rightOk)
        {
            double asym = Math.Abs(le.Y - re.Y) * ctx.Aspect / torso;
            if (asym > _t.ElbowAsymmetryRatio)
                errors.Add("Alinhe os cotovelos — um lado está mais baixo que o outro");
        }

        return PoseFeedback.FromErrors(errors, "Excelente duplo bíceps! Bíceps bem definidos e simétricos.");
    }

    private void CheckFlex(List<string> errors, string side, double angle)
    {
        if (double.IsNaN(angle)) return;
        if (angle < _t.ElbowMinAngle)
            errors.Add($"Braço {side} muito fechado — abra para {_t.ElbowMinAngle}–{_t.ElbowMaxAngle}° (atual: {angle:F0}°)");
        else if (angle > _t.ElbowMaxAngle)
            errors.Add($"Braço {side} muito aberto — feche para {_t.ElbowMinAngle}–{_t.ElbowMaxAngle}° (atual: {angle:F0}°)");
    }
}

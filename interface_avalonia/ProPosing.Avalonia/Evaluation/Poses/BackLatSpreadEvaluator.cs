using ProPosing.Avalonia.Models;
using static ProPosing.Avalonia.Evaluation.GeometryHelper;

namespace ProPosing.Avalonia.Evaluation.Poses;

/// <summary>
/// Back Lat Spread: back to camera, elbows flared wide with hands on hips/lower back,
/// shoulders level, lats maximally spread.
/// </summary>
public sealed class BackLatSpreadEvaluator : IPoseEvaluator
{
    private readonly BackLatSpreadThresholds _t;

    public BackLatSpreadEvaluator(BackLatSpreadThresholds? thresholds = null)
        => _t = thresholds ?? new BackLatSpreadThresholds();

    public string PoseMode => "back_lat_spread";

    public PoseFeedback Evaluate(PoseContext ctx)
    {
        if (ctx.View != BodyView.Back)
            return new PoseFeedback("incorrect", "Vire de costas para a câmera", []);

        var lms = ctx.Landmarks;
        var ls = lms[Idx.LeftShoulder];  var rs = lms[Idx.RightShoulder];
        var le = lms[Idx.LeftElbow];     var re = lms[Idx.RightElbow];
        var lw = lms[Idx.LeftWrist];     var rw = lms[Idx.RightWrist];
        var lh = lms[Idx.LeftHip];       var rh = lms[Idx.RightHip];

        if (!AllReliable(ls, rs))
            return new PoseFeedback("incorrect", "Ombros não visíveis — vire completamente de costas", []);

        double torso = Math.Max(ctx.TorsoLength, 1e-6);
        var errors = new List<string>();

        // 1. Elbows spread wider than shoulders.
        if (AllReliable(le, re) && ctx.ShoulderSpan > 1e-6)
        {
            double elbowSpan = Distance2D(le, re, ctx.Aspect);
            if (elbowSpan / ctx.ShoulderSpan < _t.ElbowSpreadMinRatio)
                errors.Add("Abra mais os cotovelos para expandir os lats");
        }

        // 2. Wrists near hip level — per-side, torso-normalized, fixed naming.
        if (AllReliable(lh, lw))
        {
            double diff = Math.Abs(lw.Y - lh.Y) * ctx.Aspect / torso;
            if (diff > _t.WristHipRatioTolerance)
                errors.Add("Mão esquerda: posicione no quadril");
        }
        if (AllReliable(rh, rw))
        {
            double diff = Math.Abs(rw.Y - rh.Y) * ctx.Aspect / torso;
            if (diff > _t.WristHipRatioTolerance)
                errors.Add("Mão direita: posicione no quadril");
        }

        // 3. Torso squarely toward camera — hipSpan should be comparable to shoulderSpan.
        if (AllReliable(lh, rh) && ctx.ShoulderSpan > 1e-6)
        {
            if (ctx.HipSpan / ctx.ShoulderSpan < _t.HipSpanRatioMin)
                errors.Add("Vire completamente de costas para mostrar os quadris");
        }

        // 4. Shoulder asymmetry (torso-normalized).
        double sAsym = Math.Abs(ls.Y - rs.Y) * ctx.Aspect / torso;
        if (sAsym > _t.ShoulderAsymmetryRatio)
            errors.Add("Mantenha os ombros nivelados para simetria");

        return PoseFeedback.FromErrors(errors, "Excelente back lat spread! Costas bem expandidas.");
    }
}

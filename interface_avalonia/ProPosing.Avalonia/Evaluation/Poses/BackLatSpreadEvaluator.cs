using ProPosing.Avalonia.Models;
using static ProPosing.Avalonia.Evaluation.GeometryHelper;

namespace ProPosing.Avalonia.Evaluation.Poses;

public sealed class BackLatSpreadEvaluator : IPoseEvaluator
{
    private readonly BackLatSpreadThresholds _t;

    public BackLatSpreadEvaluator(BackLatSpreadThresholds? thresholds = null)
        => _t = thresholds ?? new BackLatSpreadThresholds();

    public string PoseMode => "back_lat_spread";

    public PoseFeedback Evaluate(IReadOnlyList<LandmarkPoint> lms)
    {
        var nose = lms[Idx.Nose];
        var ls   = lms[Idx.LeftShoulder];  var rs = lms[Idx.RightShoulder];
        var le   = lms[Idx.LeftElbow];     var re = lms[Idx.RightElbow];
        var lw   = lms[Idx.LeftWrist];     var rw = lms[Idx.RightWrist];
        var lh   = lms[Idx.LeftHip];       var rh = lms[Idx.RightHip];

        // If nose is clearly visible the user is facing the camera, not the back
        if (IsVisible(nose, 0.5))
            return new PoseFeedback("incorrect", "Vire de costas para a câmera", []);

        if (!IsVisible(ls) || !IsVisible(rs))
            return new PoseFeedback("incorrect", "Ombros não visíveis — vire completamente de costas", []);

        var errors = new List<string>();

        double shoulderSpan = Math.Abs(ls.X - rs.X);

        // Elbows spread wider than shoulders (lats spread)
        if (IsVisible(le) && IsVisible(re) && shoulderSpan > 0)
        {
            double elbowSpan = Math.Abs(le.X - re.X);
            if (elbowSpan < shoulderSpan * _t.ElbowSpreadMinRatio)
                errors.Add("Abra mais os cotovelos para expandir os lats");
        }

        // Wrists should be near hip level (hands on hips/lower back)
        if (IsVisible(lh) && IsVisible(lw))
        {
            double diff = Math.Abs(lw.Y - lh.Y);
            if (diff > _t.ElbowHipHeightTolerance)
                errors.Add("Mão esquerda: posicione no quadril");
        }
        if (IsVisible(rh) && IsVisible(rw))
        {
            double diff = Math.Abs(rw.Y - rh.Y);
            if (diff > _t.ElbowHipHeightTolerance)
                errors.Add("Mão direita: posicione no quadril");
        }

        // Hip width visible (body not twisted)
        if (IsVisible(lh) && IsVisible(rh))
        {
            double hipWidth = Math.Abs(lh.X - rh.X);
            if (hipWidth < _t.HipWidthMin)
                errors.Add("Vire completamente de costas para mostrar os quadris");
        }

        // Shoulder symmetry
        if (IsVisible(ls) && IsVisible(rs))
        {
            double asymmetry = Math.Abs(ls.Y - rs.Y);
            if (asymmetry > _t.ShoulderAsymmetryMax)
                errors.Add("Mantenha os ombros nivelados para simetria");
        }

        return PoseFeedback.FromErrors(errors, "Excelente back lat spread! Costas bem expandidas.");
    }
}

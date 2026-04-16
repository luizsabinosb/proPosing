using ProPosing.Avalonia.Models;
using static ProPosing.Avalonia.Evaluation.GeometryHelper;

namespace ProPosing.Avalonia.Evaluation.Poses;

public sealed class EnquadramentoEvaluator : IPoseEvaluator
{
    private readonly EnquadramentoThresholds _t;

    public EnquadramentoEvaluator(EnquadramentoThresholds? thresholds = null)
        => _t = thresholds ?? new EnquadramentoThresholds();

    public string PoseMode => "enquadramento";

    public PoseFeedback Evaluate(IReadOnlyList<LandmarkPoint> lms)
    {
        var ls = lms[Idx.LeftShoulder];
        var rs = lms[Idx.RightShoulder];

        if (!IsVisible(ls) || !IsVisible(rs))
            return new PoseFeedback("incorrect", "Ombros não visíveis — recue um pouco", []);

        var errors = new List<string>();

        // Horizontal centering: midpoint of shoulders should be near 0.5
        // Python equivalent: offset < width * 0.1  →  abs(midX - 0.5) < 0.1
        double midX = (ls.X + rs.X) / 2.0;
        if (midX < _t.CenterMinX)
            errors.Add("Mova para a direita para centralizar");
        else if (midX > _t.CenterMaxX)
            errors.Add("Mova para a esquerda para centralizar");

        // Distância da câmera via span dos ombros
        double span = Math.Abs(ls.X - rs.X);
        if (span < _t.ShoulderSpanMin)
            errors.Add("Muito longe — aproxime-se da câmera");
        else if (span > _t.ShoulderSpanMax)
            errors.Add("Muito perto — afaste-se da câmera");

        // Posição vertical: ombros devem estar na parte superior do quadro
        double shoulderMidY = (ls.Y + rs.Y) / 2.0;
        if (shoulderMidY > _t.ShoulderMidYMax)
            errors.Add("Muito baixo no quadro — recue ou ajuste a câmera");

        return PoseFeedback.FromErrors(errors, "Enquadramento perfeito!");
    }
}

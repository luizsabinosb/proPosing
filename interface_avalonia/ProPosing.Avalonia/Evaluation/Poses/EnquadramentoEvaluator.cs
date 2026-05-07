using ProPosing.Avalonia.Models;
using static ProPosing.Avalonia.Evaluation.GeometryHelper;

namespace ProPosing.Avalonia.Evaluation.Poses;

public sealed class EnquadramentoEvaluator : IPoseEvaluator
{
    private readonly EnquadramentoThresholds _t;

    public EnquadramentoEvaluator(EnquadramentoThresholds? thresholds = null)
        => _t = thresholds ?? new EnquadramentoThresholds();

    public string PoseMode => "enquadramento";

    public PoseFeedback Evaluate(PoseContext ctx)
    {
        var lms = ctx.Landmarks;
        var ls = lms[Idx.LeftShoulder];
        var rs = lms[Idx.RightShoulder];

        if (!IsReliable(ls) || !IsReliable(rs))
            return new PoseFeedback("incorrect", "Ombros não visíveis — recue um pouco", []);

        var errors = new List<string>();

        // Horizontal centering: midpoint of shoulders near 0.5.
        double midX = (ls.X + rs.X) / 2.0;
        if (midX < _t.CenterMinX)
            errors.Add("Mova para a direita para centralizar");
        else if (midX > _t.CenterMaxX)
            errors.Add("Mova para a esquerda para centralizar");

        // Distance via raw x-only shoulder span (this rule is inherently in frame-space).
        double spanX = Math.Abs(ls.X - rs.X);
        if (spanX < _t.ShoulderSpanMin)
            errors.Add("Muito longe — aproxime-se da câmera");
        else if (spanX > _t.ShoulderSpanMax)
            errors.Add("Muito perto — afaste-se da câmera");

        // Vertical position check — both directions, not just "too low".
        double midY = (ls.Y + rs.Y) / 2.0;
        if (midY < _t.ShoulderMidYMin)
            errors.Add("Muito alto no quadro — ajuste a câmera para baixo");
        else if (midY > _t.ShoulderMidYMax)
            errors.Add("Muito baixo no quadro — ajuste a câmera para cima");

        return PoseFeedback.FromErrors(errors, "Enquadramento perfeito!");
    }
}

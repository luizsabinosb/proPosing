using ProPosing.Avalonia.Models;
using static ProPosing.Avalonia.Evaluation.GeometryHelper;

namespace ProPosing.Avalonia.Evaluation.Poses;

/// <summary>
/// Double Biceps: facing camera front-on, both arms raised with elbows at shoulder height,
/// spread wide, flexed at 60–80°, wrists curled inward/upward.
/// </summary>
public sealed class DoubleBicepsEvaluator : IPoseEvaluator
{
    private readonly DoubleBicepsThresholds _t;

    public DoubleBicepsEvaluator(DoubleBicepsThresholds? thresholds = null)
        => _t = thresholds ?? new DoubleBicepsThresholds();

    public string PoseMode => "double_biceps";

    public PoseFeedback Evaluate(IReadOnlyList<LandmarkPoint> lms)
    {
        var ls = lms[Idx.LeftShoulder];  var rs = lms[Idx.RightShoulder];
        var le = lms[Idx.LeftElbow];     var re = lms[Idx.RightElbow];
        var lw = lms[Idx.LeftWrist];     var rw = lms[Idx.RightWrist];

        bool leftOk  = IsVisible(ls) && IsVisible(le) && IsVisible(lw);
        bool rightOk = IsVisible(rs) && IsVisible(re) && IsVisible(rw);

        if (!leftOk && !rightOk)
            return new PoseFeedback("incorrect", "Braços não detectados — verifique o enquadramento", []);

        var errors = new List<string>();

        // ── 1. Cotovelos elevados (na altura dos ombros) ──────────────
        if (leftOk && le.Y > ls.Y + _t.ElbowAboveShoulderTolerance)
            errors.Add("Eleve o cotovelo esquerdo até a altura do ombro");
        if (rightOk && re.Y > rs.Y + _t.ElbowAboveShoulderTolerance)
            errors.Add("Eleve o cotovelo direito até a altura do ombro");

        // ── 2. Ângulo de flexão dos braços ────────────────────────────
        if (leftOk)
        {
            double angle = Angle(ls, le, lw);
            if (angle < _t.ElbowMinAngle)
                errors.Add($"Braço esquerdo muito fechado — abra para {_t.ElbowMinAngle}–{_t.ElbowMaxAngle}° (atual: {angle:F0}°)");
            else if (angle > _t.ElbowMaxAngle)
                errors.Add($"Braço esquerdo muito aberto — feche para {_t.ElbowMinAngle}–{_t.ElbowMaxAngle}° (atual: {angle:F0}°)");
        }
        if (rightOk)
        {
            double angle = Angle(rs, re, rw);
            if (angle < _t.ElbowMinAngle)
                errors.Add($"Braço direito muito fechado — abra para {_t.ElbowMinAngle}–{_t.ElbowMaxAngle}° (atual: {angle:F0}°)");
            else if (angle > _t.ElbowMaxAngle)
                errors.Add($"Braço direito muito aberto — feche para {_t.ElbowMinAngle}–{_t.ElbowMaxAngle}° (atual: {angle:F0}°)");
        }

        // ── 3. Cotovelos abertos (não colados ao tronco) ──────────────
        if (leftOk && rightOk && IsVisible(ls) && IsVisible(rs))
        {
            double shoulderSpan = Math.Abs(ls.X - rs.X);
            double elbowSpan    = Math.Abs(le.X - re.X);
            if (shoulderSpan > 0 && elbowSpan < shoulderSpan * _t.ElbowSpreadMinRatio)
                errors.Add("Abra mais os cotovelos para os lados — braços devem estar bem abertos");
        }

        // ── 4. Simetria dos cotovelos ─────────────────────────────────
        if (leftOk && rightOk)
        {
            double elbowAsymmetry = Math.Abs(le.Y - re.Y);
            if (elbowAsymmetry > _t.ElbowAsymmetryMax)
                errors.Add("Alinhe os cotovelos — um lado está mais baixo que o outro");
        }

        return PoseFeedback.FromErrors(errors, "Excelente duplo bíceps! Bíceps bem definidos e simétricos.");
    }
}

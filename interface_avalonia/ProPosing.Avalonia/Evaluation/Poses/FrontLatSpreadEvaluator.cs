using ProPosing.Avalonia.Models;
using static ProPosing.Avalonia.Evaluation.GeometryHelper;

namespace ProPosing.Avalonia.Evaluation.Poses;

/// <summary>
/// Front Lat Spread: front-facing, elbows bent and pulled wide/down below shoulder level,
/// hands near waist/hip, lats maximally expanded.
/// </summary>
public sealed class FrontLatSpreadEvaluator : IPoseEvaluator
{
    private readonly FrontLatSpreadThresholds _t;

    public FrontLatSpreadEvaluator(FrontLatSpreadThresholds? thresholds = null)
        => _t = thresholds ?? new FrontLatSpreadThresholds();

    public string PoseMode => "front_lat_spread";

    public PoseFeedback Evaluate(IReadOnlyList<LandmarkPoint> lms)
    {
        var ls = lms[Idx.LeftShoulder];  var rs = lms[Idx.RightShoulder];
        var le = lms[Idx.LeftElbow];     var re = lms[Idx.RightElbow];
        var lw = lms[Idx.LeftWrist];     var rw = lms[Idx.RightWrist];
        var lh = lms[Idx.LeftHip];       var rh = lms[Idx.RightHip];

        if (!IsVisible(ls) || !IsVisible(rs))
            return new PoseFeedback("incorrect", "Ombros não visíveis — ajuste o enquadramento", []);

        var errors = new List<string>();

        double shoulderSpan = Math.Abs(ls.X - rs.X);

        // ── 1. Cotovelos abertos além dos ombros (lats expandidos) ────
        if (IsVisible(le) && IsVisible(re) && shoulderSpan > 0)
        {
            double elbowSpan = Math.Abs(le.X - re.X);
            if (elbowSpan < shoulderSpan * _t.ElbowSpreadMinRatio)
                errors.Add("Abra mais os cotovelos — expanda os lats ao máximo para os lados");
        }

        // ── 2. Cotovelos abaixo (ou na altura) dos ombros ─────────────
        // Na front lat spread, cotovelos ficam na altura do ombro ou levemente abaixo
        if (IsVisible(ls) && IsVisible(le))
        {
            if (le.Y < ls.Y - _t.ElbowAboveShoulderMax)
                errors.Add("Cotovelo esquerdo acima do ombro — abaixe para o nível do ombro");
        }
        if (IsVisible(rs) && IsVisible(re))
        {
            if (re.Y < rs.Y - _t.ElbowAboveShoulderMax)
                errors.Add("Cotovelo direito acima do ombro — abaixe para o nível do ombro");
        }

        // ── 3. Ângulo de flexão dos cotovelos ─────────────────────────
        if (IsVisible(ls) && IsVisible(le) && IsVisible(lw))
        {
            double angle = Angle(ls, le, lw);
            if (angle < _t.ElbowMinAngle)
                errors.Add($"Cotovelo esquerdo muito fechado — abra para {_t.ElbowMinAngle}–{_t.ElbowMaxAngle}° (atual: {angle:F0}°)");
            else if (angle > _t.ElbowMaxAngle)
                errors.Add($"Cotovelo esquerdo muito aberto — feche para {_t.ElbowMinAngle}–{_t.ElbowMaxAngle}° (atual: {angle:F0}°)");
        }
        if (IsVisible(rs) && IsVisible(re) && IsVisible(rw))
        {
            double angle = Angle(rs, re, rw);
            if (angle < _t.ElbowMinAngle)
                errors.Add($"Cotovelo direito muito fechado — abra para {_t.ElbowMinAngle}–{_t.ElbowMaxAngle}° (atual: {angle:F0}°)");
            else if (angle > _t.ElbowMaxAngle)
                errors.Add($"Cotovelo direito muito aberto — feche para {_t.ElbowMinAngle}–{_t.ElbowMaxAngle}° (atual: {angle:F0}°)");
        }

        // ── 4. Simetria dos cotovelos ─────────────────────────────────
        if (IsVisible(le) && IsVisible(re))
        {
            double asymmetry = Math.Abs(le.Y - re.Y);
            if (asymmetry > _t.ElbowAsymmetryMax)
                errors.Add("Mantenha os cotovelos na mesma altura — um lado está desequilibrado");
        }

        // ── 5. Punhos na zona da cintura/quadril ──────────────────────
        if (IsVisible(lh) && IsVisible(rh) && IsVisible(lw) && IsVisible(rw))
        {
            double hipY = (lh.Y + rh.Y) / 2.0;
            double wristMidY = (lw.Y + rw.Y) / 2.0;
            if (wristMidY < (ls.Y + rs.Y) / 2.0 - 0.05)
                errors.Add("Traga os punhos para a região da cintura — não levante os braços");
        }

        return PoseFeedback.FromErrors(errors, "Excelente front lat spread! Lats bem expandidos.");
    }
}

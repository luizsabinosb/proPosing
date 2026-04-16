using ProPosing.Avalonia.Models;
using static ProPosing.Avalonia.Evaluation.GeometryHelper;

namespace ProPosing.Avalonia.Evaluation.Poses;

/// <summary>
/// Most Muscular: front-facing, both hands brought close together at lower chest/abdomen,
/// elbows pointing outward and downward, body slightly leaned forward.
/// </summary>
public sealed class MostMuscularEvaluator : IPoseEvaluator
{
    private readonly MostMuscularThresholds _t;

    public MostMuscularEvaluator(MostMuscularThresholds? thresholds = null)
        => _t = thresholds ?? new MostMuscularThresholds();

    public string PoseMode => "most_muscular";

    public PoseFeedback Evaluate(IReadOnlyList<LandmarkPoint> lms)
    {
        var ls = lms[Idx.LeftShoulder];  var rs = lms[Idx.RightShoulder];
        var le = lms[Idx.LeftElbow];     var re = lms[Idx.RightElbow];
        var lw = lms[Idx.LeftWrist];     var rw = lms[Idx.RightWrist];
        var lk = lms[Idx.LeftKnee];      var rk = lms[Idx.RightKnee];
        var lh = lms[Idx.LeftHip];       var rh = lms[Idx.RightHip];
        var la = lms[Idx.LeftAnkle];     var ra = lms[Idx.RightAnkle];

        // ── 0. Corpo completo deve estar no enquadramento ─────────────
        if (!IsVisible(lh) || !IsVisible(rh))
            return new PoseFeedback("incorrect",
                "Recue da câmera para mostrar o corpo inteiro na tela", []);

        var errors = new List<string>();

        // ── 1. Cotovelos abaixo dos ombros (braços puxados para baixo) ─
        if (IsVisible(ls) && IsVisible(le) && le.Y <= ls.Y + _t.ElbowBelowShoulderTolerance)
            errors.Add("Cotovelo esquerdo deve estar abaixo do ombro — puxe para baixo");
        if (IsVisible(rs) && IsVisible(re) && re.Y <= rs.Y + _t.ElbowBelowShoulderTolerance)
            errors.Add("Cotovelo direito deve estar abaixo do ombro — puxe para baixo");

        // ── 2. Punhos próximos — mãos quase se tocando ────────────────
        // Usa distância 2D para tolerar mãos entrelaçadas onde X e Y divergem levemente
        if (IsVisible(lw) && IsVisible(rw) && IsVisible(ls) && IsVisible(rs))
        {
            double dx           = lw.X - rw.X;
            double dy           = lw.Y - rw.Y;
            double wristDist    = Math.Sqrt(dx * dx + dy * dy);
            double shoulderSpan = Math.Abs(ls.X - rs.X);
            if (shoulderSpan > 0 && wristDist > shoulderSpan * _t.WristProximityRatio)
                errors.Add("Aproxime mais as mãos — punhos devem estar juntos no centro do corpo");
        }

        // ── 3. Punhos na altura do tronco (não acima dos ombros) ──────
        if (IsVisible(ls) && IsVisible(rs) && IsVisible(lw) && IsVisible(rw))
        {
            double shoulderMidY = (ls.Y + rs.Y) / 2.0;
            double wristMidY    = (lw.Y + rw.Y) / 2.0;
            if (wristMidY < shoulderMidY - 0.05)
                errors.Add("Abaixe as mãos — punhos devem estar na altura do abdômen, não acima dos ombros");
        }

        // ── 4. Simetria do torso ───────────────────────────────────────
        if (IsVisible(ls) && IsVisible(lh) && IsVisible(rs) && IsVisible(rh))
        {
            double leftTorso  = Math.Abs(ls.Y - lh.Y);
            double rightTorso = Math.Abs(rs.Y - rh.Y);
            if (Math.Abs(leftTorso - rightTorso) > _t.TorsoAsymmetryMax)
                errors.Add("Mantenha o torso alinhado — não incline para um lado");
        }

        // ── 5. Joelhos estendidos ──────────────────────────────────────
        if (IsVisible(lh) && IsVisible(lk) && IsVisible(la))
        {
            double angle = Angle(lh, lk, la);
            if (angle < _t.KneeMinAngle)
                errors.Add($"Estenda mais a perna esquerda (atual: {angle:F0}°)");
        }
        if (IsVisible(rh) && IsVisible(rk) && IsVisible(ra))
        {
            double angle = Angle(rh, rk, ra);
            if (angle < _t.KneeMinAngle)
                errors.Add($"Estenda mais a perna direita (atual: {angle:F0}°)");
        }

        return PoseFeedback.FromErrors(errors, "Excelente most muscular! Musculatura máxima em destaque.");
    }
}

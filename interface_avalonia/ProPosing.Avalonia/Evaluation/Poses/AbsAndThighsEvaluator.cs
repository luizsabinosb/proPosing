using ProPosing.Avalonia.Models;
using static ProPosing.Avalonia.Evaluation.GeometryHelper;

namespace ProPosing.Avalonia.Evaluation.Poses;

/// <summary>
/// Abs and Thighs: front-facing, both arms raised with elbows spread wide,
/// one leg stepped forward with knee bent to contract the quad.
/// </summary>
public sealed class AbsAndThighsEvaluator : IPoseEvaluator
{
    private readonly AbsAndThighsThresholds _t;

    public AbsAndThighsEvaluator(AbsAndThighsThresholds? thresholds = null)
        => _t = thresholds ?? new AbsAndThighsThresholds();

    public string PoseMode => "abs_and_thighs";

    public PoseFeedback Evaluate(IReadOnlyList<LandmarkPoint> lms)
    {
        var ls = lms[Idx.LeftShoulder];  var rs = lms[Idx.RightShoulder];
        var le = lms[Idx.LeftElbow];     var re = lms[Idx.RightElbow];
        var lw = lms[Idx.LeftWrist];     var rw = lms[Idx.RightWrist];
        var lh = lms[Idx.LeftHip];       var rh = lms[Idx.RightHip];
        var lk = lms[Idx.LeftKnee];      var rk = lms[Idx.RightKnee];

        // ── 0. Corpo completo deve estar no enquadramento ─────────────
        if (!IsVisible(lh) || !IsVisible(rh) || !IsVisible(lk) || !IsVisible(rk))
            return new PoseFeedback("incorrect",
                "Recue da câmera para mostrar o corpo inteiro — pernas devem estar visíveis", []);

        var errors = new List<string>();

        // ── 1. Braços levantados: punhos acima dos ombros ─────────────
        if (IsVisible(ls) && IsVisible(lw))
        {
            if (lw.Y > ls.Y + _t.WristAboveShoulderTolerance)
                errors.Add("Levante o braço esquerdo — punho deve estar acima do ombro");
        }
        if (IsVisible(rs) && IsVisible(rw))
        {
            if (rw.Y > rs.Y + _t.WristAboveShoulderTolerance)
                errors.Add("Levante o braço direito — punho deve estar acima do ombro");
        }

        // ── 2. Cotovelos abertos (lats expandidos e abdômen exposto) ──
        if (IsVisible(le) && IsVisible(re))
        {
            double elbowSpan = Math.Abs(le.X - re.X);
            if (elbowSpan < _t.ElbowSpreadMin)
                errors.Add("Abra mais os cotovelos — mantenha-os bem afastados para expor o abdômen");
        }

        // ── 3. Uma perna avançada (joelhos com altura diferente) ──────
        if (IsVisible(lk) && IsVisible(rk))
        {
            double kneeYDiff = Math.Abs(lk.Y - rk.Y);
            if (kneeYDiff < _t.KneeYAsymmetryMin)
                errors.Add("Avance uma perna à frente e contraia o quadríceps");
        }

        // ── 4. Simetria do torso (sem inclinação lateral) ─────────────
        if (IsVisible(ls) && IsVisible(lh) && IsVisible(rs) && IsVisible(rh))
        {
            double leftTorso  = Math.Abs(ls.Y - lh.Y);
            double rightTorso = Math.Abs(rs.Y - rh.Y);
            if (Math.Abs(leftTorso - rightTorso) > _t.TorsoAsymmetryMax)
                errors.Add("Mantenha o torso alinhado — não incline o corpo para o lado");
        }

        return PoseFeedback.FromErrors(errors, "Excelente abs and thighs! Abdômen e quadríceps bem marcados.");
    }
}

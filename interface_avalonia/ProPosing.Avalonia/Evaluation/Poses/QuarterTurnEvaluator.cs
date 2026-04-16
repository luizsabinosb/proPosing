using ProPosing.Avalonia.Models;
using static ProPosing.Avalonia.Evaluation.GeometryHelper;

namespace ProPosing.Avalonia.Evaluation.Poses;

/// <summary>
/// Quarter Turn: true 90° side profile. Front arm bent ~90° at waist level,
/// back arm more extended, body completely sideways to camera.
/// </summary>
public sealed class QuarterTurnEvaluator : IPoseEvaluator
{
    private readonly QuarterTurnThresholds _t;

    public QuarterTurnEvaluator(QuarterTurnThresholds? thresholds = null)
        => _t = thresholds ?? new QuarterTurnThresholds();

    public string PoseMode => "quarter_turn_side";

    public PoseFeedback Evaluate(IReadOnlyList<LandmarkPoint> lms)
    {
        var nose = lms[Idx.Nose];
        var ls   = lms[Idx.LeftShoulder];  var rs = lms[Idx.RightShoulder];
        var le   = lms[Idx.LeftElbow];     var re = lms[Idx.RightElbow];
        var lw   = lms[Idx.LeftWrist];     var rw = lms[Idx.RightWrist];
        var lh   = lms[Idx.LeftHip];       var rh = lms[Idx.RightHip];
        var lk   = lms[Idx.LeftKnee];      var rk = lms[Idx.RightKnee];
        var la   = lms[Idx.LeftAnkle];     var ra = lms[Idx.RightAnkle];

        // ── 1. Perfil lateral obrigatório (90°) ───────────────────────
        if (IsVisible(nose, _t.NoseMaxVisibility))
            return new PoseFeedback("incorrect", "Vire completamente de lado — quarter turn é 90° de perfil", []);

        if (IsVisible(ls) && IsVisible(rs))
        {
            double span = Math.Abs(ls.X - rs.X);
            if (span > _t.ShoulderSpanMax)
                return new PoseFeedback("incorrect", "Gire o corpo de lado — posição de perfil completo", []);
        }

        // ── 2. Determinar braço frontal (maior X = mais perto da câmera) ─
        bool leftOk  = IsVisible(ls) && IsVisible(le) && IsVisible(lw);
        bool rightOk = IsVisible(rs) && IsVisible(re) && IsVisible(rw);

        bool leftIsFront = IsVisible(ls) && IsVisible(rs) && ls.X > rs.X;
        var (frontShoulder, frontElbow, frontWrist) = leftIsFront ? (ls, le, lw) : (rs, re, rw);
        var (backShoulder,  backElbow,  backWrist)  = leftIsFront ? (rs, re, rw) : (ls, le, lw);
        bool frontOk = leftIsFront ? leftOk  : rightOk;
        bool backOk  = leftIsFront ? rightOk : leftOk;

        var errors = new List<string>();

        // ── 3. Braço frontal: dobrado ~90° na cintura ─────────────────
        if (frontOk)
        {
            double angle = Angle(frontShoulder, frontElbow, frontWrist);
            if (angle < _t.FrontElbowMinAngle)
                errors.Add($"Braço frontal muito fechado — abra para {_t.FrontElbowMinAngle}–{_t.FrontElbowMaxAngle}° (atual: {angle:F0}°)");
            else if (angle > _t.FrontElbowMaxAngle)
                errors.Add($"Braço frontal muito aberto — feche para {_t.FrontElbowMinAngle}–{_t.FrontElbowMaxAngle}° (atual: {angle:F0}°)");
        }

        // ── 4. Braço traseiro mais estendido ──────────────────────────
        if (backOk)
        {
            double angle = Angle(backShoulder, backElbow, backWrist);
            if (angle < _t.BackArmMinAngle)
                errors.Add($"Estenda mais o braço traseiro (atual: {angle:F0}°)");
        }

        // ── 5. Joelhos razoavelmente estendidos ───────────────────────
        if (IsVisible(lh) && IsVisible(lk) && IsVisible(la))
        {
            double angle = Angle(lh, lk, la);
            if (angle < _t.KneeMinAngle)
                errors.Add($"Estenda mais a perna ({angle:F0}°)");
        }
        else if (IsVisible(rh) && IsVisible(rk) && IsVisible(ra))
        {
            double angle = Angle(rh, rk, ra);
            if (angle < _t.KneeMinAngle)
                errors.Add($"Estenda mais a perna ({angle:F0}°)");
        }

        return PoseFeedback.FromErrors(errors, "Excelente quarter turn! Perfil e postura impecáveis.");
    }
}

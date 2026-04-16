using ProPosing.Avalonia.Models;
using static ProPosing.Avalonia.Evaluation.GeometryHelper;

namespace ProPosing.Avalonia.Evaluation.Poses;

/// <summary>
/// Side Chest: ~80° side profile, front arm bent 70–130° "hugging" the torso,
/// wrist at chest/waist level, chest projected forward.
/// </summary>
public sealed class SideChestEvaluator : IPoseEvaluator
{
    private readonly SideChestThresholds _t;

    public SideChestEvaluator(SideChestThresholds? thresholds = null)
        => _t = thresholds ?? new SideChestThresholds();

    public string PoseMode => "side_chest";

    public PoseFeedback Evaluate(IReadOnlyList<LandmarkPoint> lms)
    {
        var nose = lms[Idx.Nose];
        var ls   = lms[Idx.LeftShoulder];  var rs = lms[Idx.RightShoulder];
        var le   = lms[Idx.LeftElbow];     var re = lms[Idx.RightElbow];
        var lw   = lms[Idx.LeftWrist];     var rw = lms[Idx.RightWrist];
        var lk   = lms[Idx.LeftKnee];      var rk = lms[Idx.RightKnee];
        var lh   = lms[Idx.LeftHip];       var rh = lms[Idx.RightHip];
        var la   = lms[Idx.LeftAnkle];     var ra = lms[Idx.RightAnkle];

        // ── 1. Perfil lateral obrigatório ──────────────────────────────
        if (IsVisible(nose, _t.NoseMaxVisibility))
            return new PoseFeedback("incorrect", "Vire mais de lado para a câmera (~80°)", []);

        if (IsVisible(lh) && IsVisible(rh))
        {
            double hipWidth = Math.Abs(lh.X - rh.X);
            if (hipWidth > _t.HipWidthMax)
                return new PoseFeedback("incorrect", "Gire o tronco de lado (~80–85°) para mostrar o peito", []);
        }

        bool leftOk  = IsVisible(ls) && IsVisible(le) && IsVisible(lw);
        bool rightOk = IsVisible(rs) && IsVisible(re) && IsVisible(rw);

        if (!leftOk && !rightOk)
            return new PoseFeedback("incorrect", "Braços não detectados — verifique o enquadramento", []);

        // ── 2. Braço frontal = mais contraído (menor ângulo) ──────────
        double leftAngle  = leftOk  ? Angle(ls, le, lw) : double.MaxValue;
        double rightAngle = rightOk ? Angle(rs, re, rw) : double.MaxValue;

        bool useLeft = leftAngle <= rightAngle && leftOk;
        var (frontShoulder, frontElbow, frontWrist) = useLeft
            ? (ls, le, lw) : (rs, re, rw);
        double frontAngle = useLeft ? leftAngle : rightAngle;
        double oppositeAngle = useLeft ? (rightOk ? rightAngle : 0) : (leftOk ? leftAngle : 0);

        var errors = new List<string>();

        // ── 3. Ângulo do braço frontal ────────────────────────────────
        if (frontAngle < _t.ArmMinAngle)
            errors.Add($"Braço frontal muito fechado — abra para {_t.ArmMinAngle}–{_t.ArmMaxAngle}° (atual: {frontAngle:F0}°)");
        else if (frontAngle > _t.ArmMaxAngle)
            errors.Add($"Braço frontal muito aberto — contraia para {_t.ArmMinAngle}–{_t.ArmMaxAngle}° (atual: {frontAngle:F0}°)");

        // ── 4. Cotovelo não acima do ombro ────────────────────────────
        if (frontElbow.Y < frontShoulder.Y - _t.ElbowAboveShoulderMax)
            errors.Add("Cotovelo muito alto — abaixe para o nível do ombro");

        // ── 5. Pulso na zona do tronco (entre ombro e quadril) ────────
        if (IsVisible(frontShoulder) && IsVisible(lh) && IsVisible(rh))
        {
            double hipY = (lh.Y + rh.Y) / 2.0;
            // Wrist should be between shoulder level and hip level
            if (frontWrist.Y < frontShoulder.Y - 0.05)
                errors.Add("Braço levantado demais — traga o punho para a altura do peito/cintura");
            else if (frontWrist.Y > hipY + 0.05)
                errors.Add("Braço muito baixo — mantenha o punho na altura do peito ou cintura");
        }

        // ── 6. Braço posterior flexionado ─────────────────────────────
        if (oppositeAngle > 0 && oppositeAngle > _t.OppositeArmExtendedMin)
            errors.Add("Flexione o braço posterior para comprimir o peitoral");

        // ── 7. Joelho levemente flexionado ────────────────────────────
        bool kneeLeftOk  = IsVisible(lk) && IsVisible(lh) && IsVisible(la);
        bool kneeRightOk = IsVisible(rk) && IsVisible(rh) && IsVisible(ra);
        if (kneeLeftOk || kneeRightOk)
        {
            double kneeAngle = kneeLeftOk ? Angle(lh, lk, la) : Angle(rh, rk, ra);
            if (kneeAngle < _t.KneeMinAngle)
                errors.Add($"Joelho muito flexionado — estenda ligeiramente para {_t.KneeMinAngle}–{_t.KneeMaxAngle}° (atual: {kneeAngle:F0}°)");
            else if (kneeAngle > _t.KneeMaxAngle)
                errors.Add($"Flexione levemente o joelho frontal para {_t.KneeMinAngle}–{_t.KneeMaxAngle}° (atual: {kneeAngle:F0}°)");
        }

        return PoseFeedback.FromErrors(errors, "Excelente side chest! Peito projetado e peitoral comprimido.");
    }
}

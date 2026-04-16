using ProPosing.Avalonia.Models;
using static ProPosing.Avalonia.Evaluation.GeometryHelper;

namespace ProPosing.Avalonia.Evaluation.Poses;

/// <summary>
/// Side Triceps: body in ~85–90° side profile, both arms pulled DOWN beside the torso,
/// posterior arm fully extended with wrist at hip/thigh level to showcase the triceps.
/// </summary>
public sealed class SideTricepsEvaluator : IPoseEvaluator
{
    private readonly SideTricepsThresholds _t;

    public SideTricepsEvaluator(SideTricepsThresholds? thresholds = null)
        => _t = thresholds ?? new SideTricepsThresholds();

    public string PoseMode => "side_triceps";

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
        // Nariz claramente visível → usuário está de frente para a câmera
        if (IsVisible(nose, _t.NoseMaxVisibility))
            return new PoseFeedback("incorrect", "Vire completamente de lado para a câmera", []);

        // Quadris visíveis e largos → tronco ainda de frente
        if (IsVisible(lh) && IsVisible(rh))
        {
            double hipWidth = Math.Abs(lh.X - rh.X);
            if (hipWidth > _t.HipWidthMax)
                return new PoseFeedback("incorrect", "Gire o tronco completamente de lado (~85–90°)", []);
        }

        bool leftOk  = IsVisible(ls) && IsVisible(le) && IsVisible(lw);
        bool rightOk = IsVisible(rs) && IsVisible(re) && IsVisible(rw);

        if (!leftOk && !rightOk)
            return new PoseFeedback("incorrect", "Braços não detectados — verifique o enquadramento", []);

        // ── 2. Braço posterior (mais estendido = mostrando tríceps) ───
        double leftAngle  = leftOk  ? Angle(ls, le, lw) : double.MinValue;
        double rightAngle = rightOk ? Angle(rs, re, rw) : double.MinValue;

        bool useLeft = leftAngle >= rightAngle && leftOk;
        var (postShoulder, postElbow, postWrist) = useLeft
            ? (ls, le, lw) : (rs, re, rw);
        double postAngle = useLeft ? leftAngle : rightAngle;

        var errors = new List<string>();

        // ── 3. Braço deve estar bem estendido (tríceps alongado) ──────
        if (postAngle < _t.ArmMinAngle)
            errors.Add($"Estenda mais o braço — tríceps deve estar quase reto (atual: {postAngle:F0}°)");

        // ── 4. Braço apontando para BAIXO — cotovelo abaixo do ombro ──
        // Y maior = mais baixo na imagem; cotovelo deve estar abaixo do ombro
        if (postElbow.Y < postShoulder.Y + _t.ElbowBelowShoulderMin)
            errors.Add("Abaixe o braço — cotovelo deve estar abaixo do ombro, apontando para baixo");

        // ── 5. Pulso na altura do quadril (braço puxado para baixo) ───
        if (IsVisible(lh) || IsVisible(rh))
        {
            double hipY = IsVisible(lh) ? lh.Y : rh.Y;
            if (postWrist.Y < hipY - _t.WristHipYTolerance)
                errors.Add("Puxe o braço para baixo — pulso deve estar na altura do quadril");
        }

        // ── 6. Cotovelo não deve estar acima do ombro ─────────────────
        if (postElbow.Y < postShoulder.Y - _t.ElbowAboveShoulderMax)
            errors.Add("Cotovelo muito acima do ombro — abaixe o braço completamente");

        // ── 7. Joelho da perna frontal estendido ──────────────────────
        bool kneeLeftOk  = IsVisible(lk) && IsVisible(lh) && IsVisible(la);
        bool kneeRightOk = IsVisible(rk) && IsVisible(rh) && IsVisible(ra);
        if (kneeLeftOk || kneeRightOk)
        {
            double kneeAngle = kneeLeftOk ? Angle(lh, lk, la) : Angle(rh, rk, ra);
            if (kneeAngle < _t.KneeMinAngle)
                errors.Add($"Estenda a perna frontal (~180°) (atual: {kneeAngle:F0}°)");
        }

        return PoseFeedback.FromErrors(errors, "Excelente side triceps! Tríceps bem estendido e destacado.");
    }
}

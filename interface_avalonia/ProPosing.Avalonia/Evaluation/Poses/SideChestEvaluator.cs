using ProPosing.Avalonia.Models;
using static ProPosing.Avalonia.Evaluation.GeometryHelper;

namespace ProPosing.Avalonia.Evaluation.Poses;

public sealed class SideChestEvaluator : IPoseEvaluator
{
    // Thresholds from pose_evaluator.py
    private const double MinAngle = 70;
    private const double MaxAngle = 130;

    public string PoseMode => "side_chest";

    public PoseFeedback Evaluate(IReadOnlyList<LandmarkPoint> lms)
    {
        var ls = lms[Idx.LeftShoulder];  var rs = lms[Idx.RightShoulder];
        var le = lms[Idx.LeftElbow];     var re = lms[Idx.RightElbow];
        var lw = lms[Idx.LeftWrist];     var rw = lms[Idx.RightWrist];
        var lk = lms[Idx.LeftKnee];      var rk = lms[Idx.RightKnee];
        var lh = lms[Idx.LeftHip];       var rh = lms[Idx.RightHip];
        var la = lms[Idx.LeftAnkle];     var ra = lms[Idx.RightAnkle];

        bool leftOk  = IsVisible(ls) && IsVisible(le) && IsVisible(lw);
        bool rightOk = IsVisible(rs) && IsVisible(re) && IsVisible(rw);

        if (!leftOk && !rightOk)
            return new PoseFeedback("incorrect", "Braços não detectados — verifique o enquadramento", []);

        // Front arm = the more contracted (smaller angle) visible arm
        double leftAngle  = leftOk  ? Angle(ls, le, lw) : double.MaxValue;
        double rightAngle = rightOk ? Angle(rs, re, rw) : double.MaxValue;

        double frontAngle;
        LandmarkPoint frontElbow, frontShoulder;
        double oppositeAngle;

        if (leftAngle <= rightAngle && leftOk)
        {
            frontAngle = leftAngle; frontElbow = le; frontShoulder = ls;
            oppositeAngle = rightOk ? rightAngle : 0;
        }
        else
        {
            frontAngle = rightAngle; frontElbow = re; frontShoulder = rs;
            oppositeAngle = leftOk ? leftAngle : 0;
        }

        var errors = new List<string>();

        // Primary metric: front arm angle 70–130°
        if (frontAngle < MinAngle)
            errors.Add($"Braço frontal muito fechado — abra para {MinAngle}–{MaxAngle}° (atual: {frontAngle:F0}°)");
        else if (frontAngle > MaxAngle)
            errors.Add($"Braço frontal deve estar contraído entre {MinAngle}–{MaxAngle}° (atual: {frontAngle:F0}°)");

        // Elbow must not be excessively above shoulder
        if (frontElbow.Y < frontShoulder.Y - 0.08)
            errors.Add("Cotovelo muito acima do ombro — abaixe para mostrar o peito");

        // Hip rotation: hips should appear narrow (side view) — X gap < 0.15
        if (IsVisible(lh) && IsVisible(rh))
        {
            double hipWidth = Math.Abs(lh.X - rh.X);
            if (hipWidth > 0.20)
                errors.Add("Gire o tronco para o lado (~80-85°) para melhor visualização do peito");
        }

        // Opposite (posterior) arm should be bent, not extended
        if (oppositeAngle > 0 && oppositeAngle > 160)
            errors.Add("Mantenha o braço posterior flexionado para comprimir o peitoral");

        // Front knee: slightly bent 160–175° — only when visible
        bool kneeLeftOk  = IsVisible(lk) && IsVisible(lh) && IsVisible(la);
        bool kneeRightOk = IsVisible(rk) && IsVisible(rh) && IsVisible(ra);
        if (kneeLeftOk || kneeRightOk)
        {
            double kneeAngle = kneeLeftOk
                ? Angle(lh, lk, la)
                : Angle(rh, rk, ra);
            if (kneeAngle < 160)
                errors.Add($"Joelho muito flexionado — estenda ligeiramente para 165–170° (atual: {kneeAngle:F0}°)");
            else if (kneeAngle > 175)
                errors.Add($"Joelho muito estendido — flexione ligeiramente para 165–170° (atual: {kneeAngle:F0}°)");
        }

        return PoseFeedback.FromErrors(errors,
            "Excelente side chest! Peito bem projetado e compressão ativa do peitoral.");
    }
}

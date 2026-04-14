using ProPosing.Avalonia.Models;
using static ProPosing.Avalonia.Evaluation.GeometryHelper;

namespace ProPosing.Avalonia.Evaluation.Poses;

public sealed class SideTricepsEvaluator : IPoseEvaluator
{
    // Thresholds from pose_evaluator.py
    private const double MinAngle = 120;
    private const double MaxAngle = 180;

    public string PoseMode => "side_triceps";

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

        // Posterior arm = the more extended (larger angle) visible arm
        double leftAngle  = leftOk  ? Angle(ls, le, lw) : double.MinValue;
        double rightAngle = rightOk ? Angle(rs, re, rw) : double.MinValue;

        double postAngle;
        LandmarkPoint postElbow, postShoulder;

        if (leftAngle >= rightAngle && leftOk)
        { postAngle = leftAngle; postElbow = le; postShoulder = ls; }
        else
        { postAngle = rightAngle; postElbow = re; postShoulder = rs; }

        var errors = new List<string>();

        // Primary: posterior arm must be extended 120–180°
        if (postAngle < MinAngle)
            errors.Add($"Braço posterior deve estar estendido ({MinAngle}–{MaxAngle}°) (atual: {postAngle:F0}°)");

        // Elbow must not be excessively above shoulder
        if (postElbow.Y < postShoulder.Y - 0.08)
            errors.Add("Cotovelo posterior muito acima do ombro — abaixe para mostrar o tríceps corretamente");

        // Hip rotation: side view → hips should appear narrow
        if (IsVisible(lh) && IsVisible(rh))
        {
            double hipWidth = Math.Abs(lh.X - rh.X);
            if (hipWidth > 0.20)
                errors.Add("Gire o tronco para o lado (~85–90°) para melhor visualização do tríceps");
        }

        // Front knee: should be extended ~170–180°
        bool kneeLeftOk  = IsVisible(lk) && IsVisible(lh) && IsVisible(la);
        bool kneeRightOk = IsVisible(rk) && IsVisible(rh) && IsVisible(ra);
        if (kneeLeftOk || kneeRightOk)
        {
            double kneeAngle = kneeLeftOk
                ? Angle(lh, lk, la)
                : Angle(rh, rk, ra);
            if (kneeAngle < 170)
                errors.Add($"Joelho da perna frontal deve estar estendido (~180°) (atual: {kneeAngle:F0}°)");
        }

        return PoseFeedback.FromErrors(errors,
            "Excelente side triceps! Tríceps bem estendido e destacado.");
    }
}

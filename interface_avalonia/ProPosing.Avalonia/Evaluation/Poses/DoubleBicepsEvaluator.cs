using ProPosing.Avalonia.Models;
using static ProPosing.Avalonia.Evaluation.GeometryHelper;

namespace ProPosing.Avalonia.Evaluation.Poses;

public sealed class DoubleBicepsEvaluator : IPoseEvaluator
{
    // Thresholds from pose_evaluator.py
    private const double MinAngle = 30;
    private const double MaxAngle = 80;

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

        if (leftOk)
        {
            double angle = Angle(ls, le, lw);
            // In normalized Y: smaller Y = higher in image.
            // Elbow must be at or ABOVE shoulder → le.Y <= ls.Y
            if (le.Y > ls.Y + 0.02)
                errors.Add("Cotovelo esquerdo muito baixo — eleve até a altura do ombro");
            if (angle < MinAngle)
                errors.Add($"Braço esquerdo muito fechado — abra para {MinAngle}–{MaxAngle}° (atual: {angle:F0}°)");
            else if (angle > MaxAngle)
                errors.Add($"Braço esquerdo muito aberto — feche para {MinAngle}–{MaxAngle}° (atual: {angle:F0}°)");
        }

        if (rightOk)
        {
            double angle = Angle(rs, re, rw);
            if (re.Y > rs.Y + 0.02)
                errors.Add("Cotovelo direito muito baixo — eleve até a altura do ombro");
            if (angle < MinAngle)
                errors.Add($"Braço direito muito fechado — abra para {MinAngle}–{MaxAngle}° (atual: {angle:F0}°)");
            else if (angle > MaxAngle)
                errors.Add($"Braço direito muito aberto — feche para {MinAngle}–{MaxAngle}° (atual: {angle:F0}°)");
        }

        // Symmetry check when both arms are visible
        if (leftOk && rightOk)
        {
            double elbowAsymmetry = Math.Abs(le.Y - re.Y);
            if (elbowAsymmetry > 0.06)
                errors.Add("Assimetria: alinhe os cotovelos");
        }

        return PoseFeedback.FromErrors(errors,
            "Excelente duplo bíceps! Bíceps bem definidos e simétricos.");
    }
}

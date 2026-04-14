using ProPosing.Avalonia.Models;
using static ProPosing.Avalonia.Evaluation.GeometryHelper;

namespace ProPosing.Avalonia.Evaluation.Poses;

public sealed class MostMuscularEvaluator : IPoseEvaluator
{
    public string PoseMode => "most_muscular";

    public PoseFeedback Evaluate(IReadOnlyList<LandmarkPoint> lms)
    {
        var ls = lms[Idx.LeftShoulder];  var rs = lms[Idx.RightShoulder];
        var le = lms[Idx.LeftElbow];     var re = lms[Idx.RightElbow];
        var lw = lms[Idx.LeftWrist];     var rw = lms[Idx.RightWrist];
        var lk = lms[Idx.LeftKnee];      var rk = lms[Idx.RightKnee];
        var lh = lms[Idx.LeftHip];       var rh = lms[Idx.RightHip];
        var la = lms[Idx.LeftAnkle];     var ra = lms[Idx.RightAnkle];

        var errors = new List<string>();

        // Primary: elbows must be BELOW shoulders (larger Y = lower in frame)
        // Python: if left_elbow_height <= left_shoulder_height + 10px → error
        // Normalized equivalent: le.Y <= ls.Y + 0.02 → error (elbow not sufficiently below shoulder)
        if (IsVisible(ls) && IsVisible(le) && le.Y <= ls.Y + 0.02)
            errors.Add("Cotovelo esquerdo deve estar abaixo do ombro");
        if (IsVisible(rs) && IsVisible(re) && re.Y <= rs.Y + 0.02)
            errors.Add("Cotovelo direito deve estar abaixo do ombro");

        // Wrists must be close together (< 50% of shoulder width)
        if (IsVisible(lw) && IsVisible(rw) && IsVisible(ls) && IsVisible(rs))
        {
            double wristDist    = Math.Abs(lw.X - rw.X);
            double shoulderSpan = Math.Abs(ls.X - rs.X);
            if (shoulderSpan > 0 && wristDist > shoulderSpan * 0.5)
                errors.Add("Aproxime as mãos — braços devem estar contraídos um contra o outro");
        }

        // Torso alignment: left and right torso height should be symmetric
        if (IsVisible(ls) && IsVisible(lh) && IsVisible(rs) && IsVisible(rh))
        {
            double leftTorso  = Math.Abs(ls.Y - lh.Y);
            double rightTorso = Math.Abs(rs.Y - rh.Y);
            if (Math.Abs(leftTorso - rightTorso) > 0.05)
                errors.Add("Mantenha o torso alinhado para mostrar simetria");
        }

        // Knees extended > 160°
        if (IsVisible(lh) && IsVisible(lk) && IsVisible(la))
        {
            double angle = Angle(lh, lk, la);
            if (angle < 160)
                errors.Add($"Estenda mais a perna esquerda (atual: {angle:F0}°)");
        }
        if (IsVisible(rh) && IsVisible(rk) && IsVisible(ra))
        {
            double angle = Angle(rh, rk, ra);
            if (angle < 160)
                errors.Add($"Estenda mais a perna direita (atual: {angle:F0}°)");
        }

        return PoseFeedback.FromErrors(errors,
            "Excelente most muscular! Toda a musculatura bem destacada.");
    }
}

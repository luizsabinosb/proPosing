using ProPosing.Avalonia.Models;
using static ProPosing.Avalonia.Evaluation.GeometryHelper;

namespace ProPosing.Avalonia.Evaluation.Poses;

/// <summary>
/// Front Lat Spread: front-facing, elbows pulled wide and slightly down,
/// hands near waist/hip, lats maximally expanded.
/// </summary>
public sealed class FrontLatSpreadEvaluator : IPoseEvaluator
{
    private readonly FrontLatSpreadThresholds _t;

    public FrontLatSpreadEvaluator(FrontLatSpreadThresholds? thresholds = null)
        => _t = thresholds ?? new FrontLatSpreadThresholds();

    public string PoseMode => "front_lat_spread";

    public PoseFeedback Evaluate(PoseContext ctx)
    {
        if (ctx.View == BodyView.Back)
            return new PoseFeedback("incorrect", "Vire de frente para a câmera", []);

        var lms = ctx.Landmarks;
        var ls = lms[Idx.LeftShoulder];  var rs = lms[Idx.RightShoulder];
        var le = lms[Idx.LeftElbow];     var re = lms[Idx.RightElbow];
        var lw = lms[Idx.LeftWrist];     var rw = lms[Idx.RightWrist];
        var lh = lms[Idx.LeftHip];       var rh = lms[Idx.RightHip];

        if (!AllReliable(ls, rs))
            return new PoseFeedback("incorrect", "Ombros não visíveis — ajuste o enquadramento", []);

        double torso = Math.Max(ctx.TorsoLength, 1e-6);
        var errors = new List<string>();

        // 1. Elbows spread wider than shoulders — the DEFINING lat-spread shape. Not
        //    skippable: undetected elbows (arms down / out of frame) mean the pose isn't
        //    being presented, so we flag it instead of passing trivially.
        if (!AllReliable(le, re))
        {
            errors.Add("Leve as mãos à cintura e abra bem os cotovelos para os lados");
        }
        else if (ctx.ShoulderSpan > 1e-6)
        {
            double elbowSpan = Distance2D(le, re, ctx.Aspect);
            if (elbowSpan / ctx.ShoulderSpan < _t.ElbowSpreadMinRatio)
                errors.Add("Abra mais os cotovelos — expanda os lats ao máximo para os lados");
        }

        // 2. Elbows not above shoulders.
        if (AllReliable(ls, le))
        {
            double rise = -VerticalGap(le, ls, ctx.Aspect) / torso; // positive if elbow above shoulder
            if (rise > _t.ElbowRiseRatioMax)
                errors.Add("Cotovelo esquerdo acima do ombro — abaixe para o nível do ombro");
        }
        if (AllReliable(rs, re))
        {
            double rise = -VerticalGap(re, rs, ctx.Aspect) / torso;
            if (rise > _t.ElbowRiseRatioMax)
                errors.Add("Cotovelo direito acima do ombro — abaixe para o nível do ombro");
        }

        // 3. Elbow flex angle.
        if (AllReliable(ls, le, lw)) CheckFlex(errors, "esquerdo", Angle3D(ls, le, lw, ctx.Aspect));
        if (AllReliable(rs, re, rw)) CheckFlex(errors, "direito",  Angle3D(rs, re, rw, ctx.Aspect));

        // 4. Elbow symmetry (torso-normalized).
        if (AllReliable(le, re))
        {
            double asym = Math.Abs(le.Y - re.Y) * ctx.Aspect / torso;
            if (asym > _t.ElbowAsymmetryRatio)
                errors.Add("Mantenha os cotovelos na mesma altura — um lado está desequilibrado");
        }

        // 5. Wrists in the upper-torso band [mid-torso ... hipline].
        //    Lats start mid-torso, so the hand should rest where the lat begins,
        //    not at the hip itself. Symmetric tolerance on each side of the band.
        if (AllReliable(ls, rs, lh, rh, lw, rw))
        {
            double midShoulderY = (ls.Y + rs.Y) / 2.0;
            double midHipY      = (lh.Y + rh.Y) / 2.0;
            double midTorsoY    = (midShoulderY + midHipY) / 2.0;
            double wristY       = (lw.Y + rw.Y) / 2.0;

            double aboveMidTorso = (midTorsoY - wristY) * ctx.Aspect / torso;
            double belowHip      = (wristY - midHipY)   * ctx.Aspect / torso;

            if (aboveMidTorso > _t.WristHipRatioTolerance)
                errors.Add("Mãos muito altas — desça para a região entre a cintura e o meio do tronco");
            else if (belowHip > _t.WristHipRatioTolerance)
                errors.Add("Mãos muito baixas — eleve até a altura da cintura");
        }

        return PoseFeedback.FromErrors(errors, "Excelente front lat spread! Lats bem expandidos.");
    }

    private void CheckFlex(List<string> errors, string side, double angle)
    {
        if (double.IsNaN(angle)) return;
        if (angle < _t.ElbowMinAngle)
            errors.Add($"Cotovelo {side} muito fechado — {_t.ElbowMinAngle}–{_t.ElbowMaxAngle}° (atual: {angle:F0}°)");
        else if (angle > _t.ElbowMaxAngle)
            errors.Add($"Cotovelo {side} muito aberto — {_t.ElbowMinAngle}–{_t.ElbowMaxAngle}° (atual: {angle:F0}°)");
    }
}

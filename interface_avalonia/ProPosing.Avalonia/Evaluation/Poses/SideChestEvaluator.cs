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

    public PoseFeedback Evaluate(PoseContext ctx)
    {
        // Side-profile gate: geometric yaw, not facial visibility.
        if (ctx.View is not (BodyView.Side or BodyView.ThreeQuarter))
            return new PoseFeedback("incorrect", "Vire mais de lado para a câmera (~80°)", []);

        var lms = ctx.Landmarks;
        var ls = lms[Idx.LeftShoulder];  var rs = lms[Idx.RightShoulder];
        var le = lms[Idx.LeftElbow];     var re = lms[Idx.RightElbow];
        var lw = lms[Idx.LeftWrist];     var rw = lms[Idx.RightWrist];
        var lh = lms[Idx.LeftHip];       var rh = lms[Idx.RightHip];
        var lk = lms[Idx.LeftKnee];      var rk = lms[Idx.RightKnee];
        var la = lms[Idx.LeftAnkle];     var ra = lms[Idx.RightAnkle];

        bool leftOk  = AllReliable(ls, le, lw);
        bool rightOk = AllReliable(rs, re, rw);
        if (!leftOk && !rightOk)
            return new PoseFeedback("incorrect", "Braços não detectados — verifique o enquadramento", []);

        // Front arm = the one nearer the camera (lower z). Robust in profile.
        bool useLeft = PickFrontByZ(ls, rs, leftOk, rightOk);
        var (fs, fe, fw) = useLeft ? (ls, le, lw) : (rs, re, rw);
        var (bs, be, bw) = useLeft ? (rs, re, rw) : (ls, le, lw);
        bool frontOk = useLeft ? leftOk : rightOk;
        bool backOk  = useLeft ? rightOk : leftOk;
        if (!frontOk)
            return new PoseFeedback("incorrect", "Braço frontal não detectado", []);

        double torso = Math.Max(ctx.TorsoLength, 1e-6);
        var errors = new List<string>();

        // 1. Front-arm flex angle (3D — side view would flatten in 2D).
        double frontAngle = Angle3D(fs, fe, fw, ctx.Aspect);
        if (!double.IsNaN(frontAngle))
        {
            if (frontAngle < _t.ArmMinAngle)
                errors.Add($"Braço frontal muito fechado — {_t.ArmMinAngle}–{_t.ArmMaxAngle}° (atual: {frontAngle:F0}°)");
            else if (frontAngle > _t.ArmMaxAngle)
                errors.Add($"Braço frontal muito aberto — {_t.ArmMinAngle}–{_t.ArmMaxAngle}° (atual: {frontAngle:F0}°)");
        }

        // 2. Elbow must not be well above shoulder — ratio-normalized.
        double elbowRise = VerticalGap(fe, fs, ctx.Aspect) / torso; // shoulder above elbow → negative
        if (-elbowRise > _t.ElbowRiseRatioMax)
            errors.Add("Cotovelo muito alto — abaixe para o nível do ombro");

        // 3. Front wrist in torso band. Use the near hip only — far hip is occluded.
        var nearHip = PickNearer(lh, rh);
        if (IsReliable(nearHip))
        {
            double wristAboveShoulder = -VerticalGap(fw, fs, ctx.Aspect) / torso;
            double wristBelowHip      =  VerticalGap(nearHip, fw, ctx.Aspect) / torso;
            if (wristAboveShoulder > _t.WristAboveShoulderRatioMax)
                errors.Add("Braço levantado demais — traga o punho para o peito/cintura");
            else if (wristBelowHip > _t.WristBelowHipRatioMax)
                errors.Add("Braço muito baixo — mantenha o punho na altura do peito ou cintura");
        }

        // 4. Back arm must be flexed (it grips the front wrist; otherwise pec isn't
        //    compressed). In profile the back arm is partially occluded so MediaPipe's
        //    extrapolated z is noisy — only judge it when each landmark is highly
        //    visible (≥0.7), and only fire on clear extension via OppositeArmExtendedMin.
        if (backOk && IsReliable(bs, 0.7) && IsReliable(be, 0.7) && IsReliable(bw, 0.7))
        {
            double backAngle = Angle3D(bs, be, bw, ctx.Aspect);
            if (!double.IsNaN(backAngle) && backAngle > _t.OppositeArmExtendedMin)
                errors.Add("Flexione o braço posterior para comprimir o peitoral");
        }

        // 5. Front-leg knee angle — prefer the near knee.
        CheckFrontKneeAngle(ctx, errors, useLeft, lk, rk, lh, rh, la, ra);

        return PoseFeedback.FromErrors(errors, "Excelente side chest! Peito projetado e peitoral comprimido.");
    }

    /// <summary>Pick the arm whose shoulder has the smaller Z (closer to camera).</summary>
    private static bool PickFrontByZ(LandmarkPoint ls, LandmarkPoint rs, bool leftOk, bool rightOk)
    {
        if (leftOk && !rightOk) return true;
        if (rightOk && !leftOk) return false;
        return ls.Z <= rs.Z;
    }

    private static LandmarkPoint PickNearer(LandmarkPoint a, LandmarkPoint b)
    {
        bool aOk = IsReliable(a), bOk = IsReliable(b);
        if (aOk && !bOk) return a;
        if (bOk && !aOk) return b;
        return a.Z <= b.Z ? a : b;
    }

    private void CheckFrontKneeAngle(
        PoseContext ctx, List<string> errors, bool frontIsLeft,
        LandmarkPoint lk, LandmarkPoint rk, LandmarkPoint lh, LandmarkPoint rh,
        LandmarkPoint la, LandmarkPoint ra)
    {
        var (k, h, a) = frontIsLeft ? (lk, lh, la) : (rk, rh, ra);
        if (!AllReliable(h, k, a)) return;
        double angle = Angle3D(h, k, a, ctx.Aspect);
        if (double.IsNaN(angle)) return;
        if (angle < _t.KneeMinAngle)
            errors.Add($"Joelho frontal muito flexionado — {_t.KneeMinAngle}–{_t.KneeMaxAngle}° (atual: {angle:F0}°)");
        else if (angle > _t.KneeMaxAngle)
            errors.Add($"Flexione levemente o joelho frontal — {_t.KneeMinAngle}–{_t.KneeMaxAngle}° (atual: {angle:F0}°)");
    }
}

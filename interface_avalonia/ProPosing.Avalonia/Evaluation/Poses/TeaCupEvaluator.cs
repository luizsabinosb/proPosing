using ProPosing.Avalonia.Models;
using static ProPosing.Avalonia.Evaluation.GeometryHelper;

namespace ProPosing.Avalonia.Evaluation.Poses;

/// <summary>
/// Tea Cup: ~3/4 oblique turn, one arm fully raised and flexed overhead,
/// other arm relaxed at the hip, torso rotated to show obliques.
/// </summary>
public sealed class TeaCupEvaluator : IPoseEvaluator
{
    private readonly TeaCupThresholds _t;

    public TeaCupEvaluator(TeaCupThresholds? thresholds = null)
        => _t = thresholds ?? new TeaCupThresholds();

    public string PoseMode => "teacup";

    public PoseFeedback Evaluate(IReadOnlyList<LandmarkPoint> lms)
    {
        var ls = lms[Idx.LeftShoulder];  var rs = lms[Idx.RightShoulder];
        var le = lms[Idx.LeftElbow];     var re = lms[Idx.RightElbow];
        var lw = lms[Idx.LeftWrist];     var rw = lms[Idx.RightWrist];
        var lh = lms[Idx.LeftHip];       var rh = lms[Idx.RightHip];
        var lk = lms[Idx.LeftKnee];      var rk = lms[Idx.RightKnee];
        var la = lms[Idx.LeftAnkle];     var ra = lms[Idx.RightAnkle];

        var errors = new List<string>();

        // ── 1. Rotação do torso: um ombro notavelmente mais alto ──────
        if (IsVisible(ls) && IsVisible(rs))
        {
            double shoulderYDiff = Math.Abs(ls.Y - rs.Y);
            if (shoulderYDiff < _t.ShoulderYDiffMin)
                errors.Add("Gire o torso em 3/4 — um ombro deve estar mais alto que o outro");
        }

        // ── 2. Detectar braço levantado (punho acima do ombro) ────────
        bool leftRaised  = IsVisible(lw) && IsVisible(ls) && lw.Y < ls.Y;
        bool rightRaised = IsVisible(rw) && IsVisible(rs) && rw.Y < rs.Y;

        if (!leftRaised && !rightRaised)
        {
            errors.Add("Levante um braço com o cotovelo flexionado — punho acima do ombro");
        }
        else
        {
            var raisedShoulder = leftRaised ? ls : rs;
            var raisedElbow    = leftRaised ? le : re;
            var raisedWrist    = leftRaised ? lw : rw;
            var lowHip         = leftRaised ? rh : lh;
            var lowWrist       = leftRaised ? rw : lw;

            // ── 3. Punho levantado bem acima do ombro ─────────────────
            if (IsVisible(raisedShoulder) && IsVisible(raisedWrist))
            {
                if (raisedWrist.Y > raisedShoulder.Y - _t.RaisedWristAboveShoulderTolerance)
                    errors.Add("Levante mais o braço — punho deve estar claramente acima do ombro");
            }

            // ── 4. Cotovelo do braço levantado flexionado ─────────────
            if (IsVisible(raisedShoulder) && IsVisible(raisedElbow) && IsVisible(raisedWrist))
            {
                double angle = Angle(raisedShoulder, raisedElbow, raisedWrist);
                if (angle < _t.RaisedElbowMinAngle)
                    errors.Add($"Braço levantado muito fechado — flexione para {_t.RaisedElbowMinAngle}–{_t.RaisedElbowMaxAngle}° (atual: {angle:F0}°)");
                else if (angle > _t.RaisedElbowMaxAngle)
                    errors.Add($"Braço levantado muito aberto — contraia para {_t.RaisedElbowMinAngle}–{_t.RaisedElbowMaxAngle}° (atual: {angle:F0}°)");
            }

            // ── 5. Braço baixo: punho perto do quadril ────────────────
            if (IsVisible(lowWrist) && IsVisible(lowHip))
            {
                double diff = Math.Abs(lowWrist.Y - lowHip.Y);
                if (diff > _t.LowWristHipYMax)
                    errors.Add("Braço baixo deve estar relaxado ao nível do quadril");
            }
        }

        // ── 6. Joelhos razoavelmente estendidos ───────────────────────
        if (IsVisible(lh) && IsVisible(lk) && IsVisible(la))
        {
            double angle = Angle(lh, lk, la);
            if (angle < _t.KneeMinAngle)
                errors.Add($"Estenda mais a perna esquerda ({angle:F0}°)");
        }
        if (IsVisible(rh) && IsVisible(rk) && IsVisible(ra))
        {
            double angle = Angle(rh, rk, ra);
            if (angle < _t.KneeMinAngle)
                errors.Add($"Estenda mais a perna direita ({angle:F0}°)");
        }

        return PoseFeedback.FromErrors(errors, "Excelente tea cup! Oblíquos e postura em destaque.");
    }
}

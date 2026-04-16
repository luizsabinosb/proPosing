using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProPosing.Avalonia.Evaluation;

/// <summary>
/// Thresholds carregados de pose_thresholds.json.
/// Se o arquivo não existir, usa os defaults hardcoded (mesmos valores de proposing/pose_evaluator.py).
/// </summary>
public sealed class PoseThresholds
{
    [JsonPropertyName("double_biceps")]
    public DoubleBicepsThresholds DoubleBiceps { get; set; } = new();

    [JsonPropertyName("side_chest")]
    public SideChestThresholds SideChest { get; set; } = new();

    [JsonPropertyName("side_triceps")]
    public SideTricepsThresholds SideTriceps { get; set; } = new();

    [JsonPropertyName("most_muscular")]
    public MostMuscularThresholds MostMuscular { get; set; } = new();

    [JsonPropertyName("enquadramento")]
    public EnquadramentoThresholds Enquadramento { get; set; } = new();

    [JsonPropertyName("quarter_turn_side")]
    public QuarterTurnThresholds QuarterTurn { get; set; } = new();

    [JsonPropertyName("front_lat_spread")]
    public FrontLatSpreadThresholds FrontLatSpread { get; set; } = new();

    [JsonPropertyName("back_lat_spread")]
    public BackLatSpreadThresholds BackLatSpread { get; set; } = new();

    [JsonPropertyName("abs_and_thighs")]
    public AbsAndThighsThresholds AbsAndThighs { get; set; } = new();

    [JsonPropertyName("teacup")]
    public TeaCupThresholds TeaCup { get; set; } = new();

    public static PoseThresholds Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "pose_thresholds.json");

        if (!File.Exists(path))
        {
            Console.WriteLine("[PoseThresholds] pose_thresholds.json não encontrado — usando defaults.");
            return new PoseThresholds();
        }

        try
        {
            var json = File.ReadAllText(path);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var loaded = JsonSerializer.Deserialize<PoseThresholds>(json, options);
            Console.WriteLine("[PoseThresholds] Carregado de pose_thresholds.json.");
            return loaded ?? new PoseThresholds();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PoseThresholds] Erro ao ler config: {ex.Message} — usando defaults.");
            return new PoseThresholds();
        }
    }
}

// ── Double Bíceps ────────────────────────────────────────────────────────────
public sealed class DoubleBicepsThresholds
{
    [JsonPropertyName("elbow_min_angle")]
    public double ElbowMinAngle { get; set; } = 30;

    [JsonPropertyName("elbow_max_angle")]
    public double ElbowMaxAngle { get; set; } = 80;

    /// <summary>Tolerância máx. para cotovelo abaixo do ombro (Y maior = mais baixo). Default: 0.02</summary>
    [JsonPropertyName("elbow_above_shoulder_tolerance")]
    public double ElbowAboveShoulderTolerance { get; set; } = 0.02;

    [JsonPropertyName("elbow_asymmetry_max")]
    public double ElbowAsymmetryMax { get; set; } = 0.06;

    /// <summary>Razão mínima: span dos cotovelos / span dos ombros. Default: 0.85</summary>
    [JsonPropertyName("elbow_spread_min_ratio")]
    public double ElbowSpreadMinRatio { get; set; } = 0.85;
}

// ── Side Chest ───────────────────────────────────────────────────────────────
public sealed class SideChestThresholds
{
    [JsonPropertyName("arm_min_angle")]
    public double ArmMinAngle { get; set; } = 70;

    [JsonPropertyName("arm_max_angle")]
    public double ArmMaxAngle { get; set; } = 130;

    [JsonPropertyName("elbow_above_shoulder_max")]
    public double ElbowAboveShoulderMax { get; set; } = 0.08;

    /// <summary>Largura máxima dos quadris em vista lateral. Tightened: 0.18</summary>
    [JsonPropertyName("hip_width_max")]
    public double HipWidthMax { get; set; } = 0.18;

    [JsonPropertyName("opposite_arm_extended_min")]
    public double OppositeArmExtendedMin { get; set; } = 160;

    [JsonPropertyName("knee_min_angle")]
    public double KneeMinAngle { get; set; } = 160;

    [JsonPropertyName("knee_max_angle")]
    public double KneeMaxAngle { get; set; } = 175;

    /// <summary>Visibilidade máxima do nariz para confirmar vista lateral. Default: 0.65</summary>
    [JsonPropertyName("nose_max_visibility")]
    public double NoseMaxVisibility { get; set; } = 0.65;
}

// ── Side Tríceps ─────────────────────────────────────────────────────────────
public sealed class SideTricepsThresholds
{
    /// <summary>Ângulo mínimo do braço posterior estendido. Tightened: 150°</summary>
    [JsonPropertyName("arm_min_angle")]
    public double ArmMinAngle { get; set; } = 150;

    [JsonPropertyName("arm_max_angle")]
    public double ArmMaxAngle { get; set; } = 180;

    [JsonPropertyName("elbow_above_shoulder_max")]
    public double ElbowAboveShoulderMax { get; set; } = 0.05;

    /// <summary>Largura máxima dos quadris em vista lateral. Tightened: 0.18</summary>
    [JsonPropertyName("hip_width_max")]
    public double HipWidthMax { get; set; } = 0.18;

    [JsonPropertyName("knee_min_angle")]
    public double KneeMinAngle { get; set; } = 170;

    /// <summary>Visibilidade máxima do nariz para confirmar perfil lateral. Default: 0.55</summary>
    [JsonPropertyName("nose_max_visibility")]
    public double NoseMaxVisibility { get; set; } = 0.55;

    /// <summary>Cotovelo deve estar pelo menos este valor ABAIXO do ombro (Y maior = mais baixo). Default: 0.05</summary>
    [JsonPropertyName("elbow_below_shoulder_min")]
    public double ElbowBelowShoulderMin { get; set; } = 0.05;

    /// <summary>Pulso pode estar no máximo este valor ACIMA do quadril. Default: 0.08</summary>
    [JsonPropertyName("wrist_hip_y_tolerance")]
    public double WristHipYTolerance { get; set; } = 0.08;
}

// ── Most Muscular ─────────────────────────────────────────────────────────────
public sealed class MostMuscularThresholds
{
    [JsonPropertyName("elbow_below_shoulder_tolerance")]
    public double ElbowBelowShoulderTolerance { get; set; } = 0.02;

    /// <summary>Distância 2D máxima dos punhos como fração da largura dos ombros. Default: 0.45</summary>
    [JsonPropertyName("wrist_proximity_ratio")]
    public double WristProximityRatio { get; set; } = 0.45;

    [JsonPropertyName("torso_asymmetry_max")]
    public double TorsoAsymmetryMax { get; set; } = 0.05;

    [JsonPropertyName("knee_min_angle")]
    public double KneeMinAngle { get; set; } = 160;
}

// ── Quarter Turn Side ─────────────────────────────────────────────────────────
public sealed class QuarterTurnThresholds
{
    [JsonPropertyName("shoulder_span_max")]
    public double ShoulderSpanMax { get; set; } = 0.25;

    [JsonPropertyName("front_elbow_min_angle")]
    public double FrontElbowMinAngle { get; set; } = 65;

    [JsonPropertyName("front_elbow_max_angle")]
    public double FrontElbowMaxAngle { get; set; } = 120;

    [JsonPropertyName("back_arm_min_angle")]
    public double BackArmMinAngle { get; set; } = 130;

    [JsonPropertyName("knee_min_angle")]
    public double KneeMinAngle { get; set; } = 150;

    /// <summary>Visibilidade máxima do nariz para confirmar perfil lateral 90°. Default: 0.50</summary>
    [JsonPropertyName("nose_max_visibility")]
    public double NoseMaxVisibility { get; set; } = 0.50;
}

// ── Front Lat Spread ──────────────────────────────────────────────────────────
public sealed class FrontLatSpreadThresholds
{
    /// <summary>Razão mínima: distância entre cotovelos / span dos ombros. Default: 1.15</summary>
    [JsonPropertyName("elbow_spread_min_ratio")]
    public double ElbowSpreadMinRatio { get; set; } = 1.15;

    /// <summary>Ângulo mínimo do cotovelo (braços curvados). Default: 65°</summary>
    [JsonPropertyName("elbow_min_angle")]
    public double ElbowMinAngle { get; set; } = 65;

    /// <summary>Ângulo máximo do cotovelo. Default: 125°</summary>
    [JsonPropertyName("elbow_max_angle")]
    public double ElbowMaxAngle { get; set; } = 125;

    /// <summary>Tolerância máxima para cotovelo acima do ombro (coords normalizadas). Default: 0.10</summary>
    [JsonPropertyName("elbow_above_shoulder_max")]
    public double ElbowAboveShoulderMax { get; set; } = 0.10;

    /// <summary>Assimetria máxima entre cotovelos (altura). Default: 0.07</summary>
    [JsonPropertyName("elbow_asymmetry_max")]
    public double ElbowAsymmetryMax { get; set; } = 0.07;
}

// ── Back Lat Spread ───────────────────────────────────────────────────────────
public sealed class BackLatSpreadThresholds
{
    /// <summary>Razão mínima: distância entre cotovelos / span dos ombros. Default: 1.10</summary>
    [JsonPropertyName("elbow_spread_min_ratio")]
    public double ElbowSpreadMinRatio { get; set; } = 1.10;

    /// <summary>Cotovelo deve estar no nível dos quadris (±tolerância). Default: 0.14</summary>
    [JsonPropertyName("elbow_hip_height_tolerance")]
    public double ElbowHipHeightTolerance { get; set; } = 0.14;

    /// <summary>Largura mínima dos quadris visíveis. Default: 0.12</summary>
    [JsonPropertyName("hip_width_min")]
    public double HipWidthMin { get; set; } = 0.12;

    /// <summary>Assimetria máxima de ombros (coords normalizadas). Default: 0.06</summary>
    [JsonPropertyName("shoulder_asymmetry_max")]
    public double ShoulderAsymmetryMax { get; set; } = 0.06;
}

// ── Abs And Thighs ────────────────────────────────────────────────────────────
public sealed class AbsAndThighsThresholds
{
    /// <summary>Punhos devem estar acima dos ombros (Y menor). Tolerância máx. abaixo do ombro. Default: 0.05</summary>
    [JsonPropertyName("wrist_above_shoulder_tolerance")]
    public double WristAboveShoulderTolerance { get; set; } = 0.05;

    /// <summary>Distância mínima entre cotovelos (lats expandidos). Default: 0.28</summary>
    [JsonPropertyName("elbow_spread_min")]
    public double ElbowSpreadMin { get; set; } = 0.28;

    /// <summary>Assimetria mínima nos joelhos (perna avançada). Default: 0.07</summary>
    [JsonPropertyName("knee_y_asymmetry_min")]
    public double KneeYAsymmetryMin { get; set; } = 0.07;

    /// <summary>Assimetria máxima do torso (simetria lateral). Default: 0.06</summary>
    [JsonPropertyName("torso_asymmetry_max")]
    public double TorsoAsymmetryMax { get; set; } = 0.06;
}

// ── Tea Cup ───────────────────────────────────────────────────────────────────
public sealed class TeaCupThresholds
{
    /// <summary>Diferença mínima de Y entre ombros (torso girado). Tightened: 0.07</summary>
    [JsonPropertyName("shoulder_y_diff_min")]
    public double ShoulderYDiffMin { get; set; } = 0.07;

    /// <summary>Braço levantado: punho deve estar acima do ombro ipsilateral (tolerância máx. abaixo). Default: 0.04</summary>
    [JsonPropertyName("raised_wrist_above_shoulder_tolerance")]
    public double RaisedWristAboveShoulderTolerance { get; set; } = 0.04;

    /// <summary>Ângulo mínimo do cotovelo do braço levantado (curva). Default: 65°</summary>
    [JsonPropertyName("raised_elbow_min_angle")]
    public double RaisedElbowMinAngle { get; set; } = 65;

    /// <summary>Ângulo máximo do cotovelo do braço levantado. Default: 120°</summary>
    [JsonPropertyName("raised_elbow_max_angle")]
    public double RaisedElbowMaxAngle { get; set; } = 120;

    /// <summary>Braço baixo: punho deve estar próximo ao quadril (máx. distância Y). Default: 0.12</summary>
    [JsonPropertyName("low_wrist_hip_y_max")]
    public double LowWristHipYMax { get; set; } = 0.12;

    /// <summary>Ângulo mínimo dos joelhos (leve flexão). Default: 150°</summary>
    [JsonPropertyName("knee_min_angle")]
    public double KneeMinAngle { get; set; } = 150;
}

// ── Enquadramento ─────────────────────────────────────────────────────────────
// Ref: proposing/pose_evaluator.py → evaluate_centered()
public sealed class EnquadramentoThresholds
{
    /// <summary>X mínimo do centro dos ombros para ser "centralizado". Default: 0.40</summary>
    [JsonPropertyName("center_min_x")]
    public double CenterMinX { get; set; } = 0.40;

    /// <summary>X máximo do centro dos ombros para ser "centralizado". Default: 0.60</summary>
    [JsonPropertyName("center_max_x")]
    public double CenterMaxX { get; set; } = 0.60;

    /// <summary>Span mínimo entre ombros (não muito longe da câmera). Default: 0.18</summary>
    [JsonPropertyName("shoulder_span_min")]
    public double ShoulderSpanMin { get; set; } = 0.18;

    /// <summary>Span máximo entre ombros (não muito perto da câmera). Default: 0.55</summary>
    [JsonPropertyName("shoulder_span_max")]
    public double ShoulderSpanMax { get; set; } = 0.55;

    /// <summary>Y máximo do centro dos ombros (não muito baixo no quadro). Default: 0.65</summary>
    [JsonPropertyName("shoulder_mid_y_max")]
    public double ShoulderMidYMax { get; set; } = 0.65;
}

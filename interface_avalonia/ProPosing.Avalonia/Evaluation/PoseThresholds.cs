using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProPosing.Avalonia.Evaluation;

/// <summary>
/// Thresholds loaded from pose_thresholds.json. Scale-sensitive tolerances are now
/// expressed as torso-normalized <b>ratios</b> (suffix <c>_ratio</c>) so they stay
/// valid as the subject moves toward or away from the camera. Angular thresholds
/// stay in degrees. Orientation is no longer driven by nose-visibility — each
/// evaluator consumes <see cref="PoseContext.View"/> instead.
/// </summary>
public sealed class PoseThresholds
{
    [JsonPropertyName("double_biceps")]     public DoubleBicepsThresholds DoubleBiceps   { get; set; } = new();
    [JsonPropertyName("side_chest")]        public SideChestThresholds    SideChest      { get; set; } = new();
    [JsonPropertyName("side_triceps")]      public SideTricepsThresholds  SideTriceps    { get; set; } = new();
    [JsonPropertyName("most_muscular")]     public MostMuscularThresholds MostMuscular   { get; set; } = new();
    [JsonPropertyName("enquadramento")]     public EnquadramentoThresholds Enquadramento { get; set; } = new();
    [JsonPropertyName("quarter_turn_side")] public QuarterTurnThresholds  QuarterTurn    { get; set; } = new();
    [JsonPropertyName("front_lat_spread")]  public FrontLatSpreadThresholds FrontLatSpread { get; set; } = new();
    [JsonPropertyName("back_lat_spread")]   public BackLatSpreadThresholds  BackLatSpread  { get; set; } = new();
    [JsonPropertyName("abs_and_thighs")]    public AbsAndThighsThresholds   AbsAndThighs   { get; set; } = new();
    [JsonPropertyName("teacup")]            public TeaCupThresholds         TeaCup         { get; set; } = new();

    public static PoseThresholds Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "pose_thresholds.json");
        if (!File.Exists(path))
        {
            Console.WriteLine("[PoseThresholds] pose_thresholds.json not found — using defaults.");
            return new PoseThresholds();
        }
        try
        {
            var json = File.ReadAllText(path);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var loaded = JsonSerializer.Deserialize<PoseThresholds>(json, options);
            Console.WriteLine("[PoseThresholds] Loaded from pose_thresholds.json.");
            return loaded ?? new PoseThresholds();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PoseThresholds] Read error: {ex.Message} — using defaults.");
            return new PoseThresholds();
        }
    }
}

// ── Double Biceps ───────────────────────────────────────────────────────────
public sealed class DoubleBicepsThresholds
{
    [JsonPropertyName("elbow_min_angle")]          public double ElbowMinAngle         { get; set; } = 30;
    [JsonPropertyName("elbow_max_angle")]          public double ElbowMaxAngle         { get; set; } = 80;
    /// <summary>Max allowed (elbow.y − shoulder.y)·aspect / torso before reporting "elbow too low".</summary>
    [JsonPropertyName("elbow_drop_ratio_max")]     public double ElbowDropRatioMax     { get; set; } = 0.06;
    /// <summary>Max elbow-height asymmetry / torso.</summary>
    [JsonPropertyName("elbow_asymmetry_ratio")]    public double ElbowAsymmetryRatio   { get; set; } = 0.15;
    /// <summary>Min elbowSpan / shoulderSpan for "arms open wide".</summary>
    [JsonPropertyName("elbow_spread_min_ratio")]   public double ElbowSpreadMinRatio   { get; set; } = 0.85;
}

// ── Side Chest ──────────────────────────────────────────────────────────────
public sealed class SideChestThresholds
{
    [JsonPropertyName("arm_min_angle")]                public double ArmMinAngle             { get; set; } = 70;
    [JsonPropertyName("arm_max_angle")]                public double ArmMaxAngle             { get; set; } = 130;
    /// <summary>Max (shoulder.y − elbow.y)·aspect / torso before elbow is "too high".</summary>
    [JsonPropertyName("elbow_rise_ratio_max")]         public double ElbowRiseRatioMax       { get; set; } = 0.20;
    [JsonPropertyName("opposite_arm_extended_min")]    public double OppositeArmExtendedMin  { get; set; } = 160;
    [JsonPropertyName("knee_min_angle")]               public double KneeMinAngle            { get; set; } = 160;
    [JsonPropertyName("knee_max_angle")]               public double KneeMaxAngle            { get; set; } = 175;
    /// <summary>Wrist-to-shoulder tolerance (torso-normalized) above which wrist is "too high".</summary>
    [JsonPropertyName("wrist_above_shoulder_ratio_max")] public double WristAboveShoulderRatioMax { get; set; } = 0.12;
    /// <summary>Wrist-to-hip tolerance below which wrist is "too low".</summary>
    [JsonPropertyName("wrist_below_hip_ratio_max")]    public double WristBelowHipRatioMax   { get; set; } = 0.12;
}

// ── Side Triceps ────────────────────────────────────────────────────────────
public sealed class SideTricepsThresholds
{
    [JsonPropertyName("arm_min_angle")]                public double ArmMinAngle             { get; set; } = 150;
    [JsonPropertyName("arm_max_angle")]                public double ArmMaxAngle             { get; set; } = 180;
    /// <summary>Min (elbow.y − shoulder.y)·aspect / torso: elbow must be at least this far below shoulder.</summary>
    [JsonPropertyName("elbow_below_shoulder_ratio_min")] public double ElbowBelowShoulderRatioMin { get; set; } = 0.12;
    [JsonPropertyName("knee_min_angle")]               public double KneeMinAngle            { get; set; } = 170;
    /// <summary>Wrist-hip Y tolerance / torso.</summary>
    [JsonPropertyName("wrist_hip_ratio_tolerance")]    public double WristHipRatioTolerance  { get; set; } = 0.20;
}

// ── Most Muscular ───────────────────────────────────────────────────────────
public sealed class MostMuscularThresholds
{
    [JsonPropertyName("elbow_below_shoulder_ratio_min")] public double ElbowBelowShoulderRatioMin { get; set; } = 0.05;
    /// <summary>Max wrist-distance / shoulderSpan to count as "hands close together".</summary>
    [JsonPropertyName("wrist_proximity_ratio")]        public double WristProximityRatio     { get; set; } = 0.45;
    /// <summary>Max lateral torso tilt in degrees.</summary>
    [JsonPropertyName("torso_tilt_max_deg")]           public double TorsoTiltMaxDeg         { get; set; } = 5.0;
    [JsonPropertyName("knee_min_angle")]               public double KneeMinAngle            { get; set; } = 160;
    /// <summary>Max (shoulder.y − wrist.y)·aspect / torso — hands not above shoulders.</summary>
    [JsonPropertyName("wrist_above_shoulder_ratio_max")] public double WristAboveShoulderRatioMax { get; set; } = 0.12;
}

// ── Quarter Turn Side ───────────────────────────────────────────────────────
public sealed class QuarterTurnThresholds
{
    [JsonPropertyName("front_elbow_min_angle")]   public double FrontElbowMinAngle { get; set; } = 65;
    [JsonPropertyName("front_elbow_max_angle")]   public double FrontElbowMaxAngle { get; set; } = 120;
    [JsonPropertyName("back_arm_min_angle")]      public double BackArmMinAngle    { get; set; } = 130;
    [JsonPropertyName("knee_min_angle")]          public double KneeMinAngle       { get; set; } = 150;
}

// ── Front Lat Spread ────────────────────────────────────────────────────────
public sealed class FrontLatSpreadThresholds
{
    [JsonPropertyName("elbow_spread_min_ratio")]  public double ElbowSpreadMinRatio { get; set; } = 1.15;
    [JsonPropertyName("elbow_min_angle")]         public double ElbowMinAngle       { get; set; } = 65;
    [JsonPropertyName("elbow_max_angle")]         public double ElbowMaxAngle       { get; set; } = 125;
    [JsonPropertyName("elbow_rise_ratio_max")]    public double ElbowRiseRatioMax   { get; set; } = 0.25;
    [JsonPropertyName("elbow_asymmetry_ratio")]   public double ElbowAsymmetryRatio { get; set; } = 0.17;
    /// <summary>Max |wrist.y − hip.y|·aspect / torso — wrists should be near hips.</summary>
    [JsonPropertyName("wrist_hip_ratio_tolerance")] public double WristHipRatioTolerance { get; set; } = 0.30;
}

// ── Back Lat Spread ─────────────────────────────────────────────────────────
public sealed class BackLatSpreadThresholds
{
    [JsonPropertyName("elbow_spread_min_ratio")]   public double ElbowSpreadMinRatio { get; set; } = 1.10;
    /// <summary>Max |wrist.y − hip.y|·aspect / torso — wrist at hip level.</summary>
    [JsonPropertyName("wrist_hip_ratio_tolerance")] public double WristHipRatioTolerance { get; set; } = 0.30;
    /// <summary>Min hipSpan / shoulderSpan — body turned enough to see both hips.</summary>
    [JsonPropertyName("hip_span_ratio_min")]       public double HipSpanRatioMin     { get; set; } = 0.55;
    [JsonPropertyName("shoulder_asymmetry_ratio")] public double ShoulderAsymmetryRatio { get; set; } = 0.15;
}

// ── Abs And Thighs ──────────────────────────────────────────────────────────
public sealed class AbsAndThighsThresholds
{
    /// <summary>Max (wrist.y − shoulder.y)·aspect / torso — wrist must be above shoulder.</summary>
    [JsonPropertyName("wrist_above_shoulder_ratio_max")] public double WristAboveShoulderRatioMax { get; set; } = 0.12;
    /// <summary>Min elbowSpan / shoulderSpan.</summary>
    [JsonPropertyName("elbow_spread_min_ratio")]   public double ElbowSpreadMinRatio  { get; set; } = 1.20;
    /// <summary>Min |leftKnee.y − rightKnee.y|·aspect / torso — one leg stepped forward.</summary>
    [JsonPropertyName("knee_asymmetry_ratio_min")] public double KneeAsymmetryRatioMin { get; set; } = 0.18;
    /// <summary>Max torso lateral tilt in degrees.</summary>
    [JsonPropertyName("torso_tilt_max_deg")]       public double TorsoTiltMaxDeg      { get; set; } = 6.0;
}

// ── Tea Cup ─────────────────────────────────────────────────────────────────
public sealed class TeaCupThresholds
{
    /// <summary>Min torso twist (shoulders vs hips), in degrees — proper 3/4 rotation.</summary>
    [JsonPropertyName("torso_twist_min_deg")]      public double TorsoTwistMinDeg    { get; set; } = 22.0;
    /// <summary>Min (shoulder.y − wrist.y)·aspect / torso — raised wrist clearly above shoulder.</summary>
    [JsonPropertyName("raised_wrist_above_shoulder_ratio_min")] public double RaisedWristAboveShoulderRatioMin { get; set; } = 0.10;
    [JsonPropertyName("raised_elbow_min_angle")]   public double RaisedElbowMinAngle { get; set; } = 65;
    [JsonPropertyName("raised_elbow_max_angle")]   public double RaisedElbowMaxAngle { get; set; } = 120;
    /// <summary>Max |wrist.y − hip.y|·aspect / torso for the low arm.</summary>
    [JsonPropertyName("low_wrist_hip_ratio_max")]  public double LowWristHipRatioMax { get; set; } = 0.30;
    [JsonPropertyName("knee_min_angle")]           public double KneeMinAngle        { get; set; } = 150;
}

// ── Enquadramento ───────────────────────────────────────────────────────────
public sealed class EnquadramentoThresholds
{
    [JsonPropertyName("center_min_x")]         public double CenterMinX       { get; set; } = 0.40;
    [JsonPropertyName("center_max_x")]         public double CenterMaxX       { get; set; } = 0.60;
    [JsonPropertyName("shoulder_span_min")]    public double ShoulderSpanMin  { get; set; } = 0.18;
    [JsonPropertyName("shoulder_span_max")]    public double ShoulderSpanMax  { get; set; } = 0.55;
    [JsonPropertyName("shoulder_mid_y_min")]   public double ShoulderMidYMin  { get; set; } = 0.12;
    [JsonPropertyName("shoulder_mid_y_max")]   public double ShoulderMidYMax  { get; set; } = 0.65;
}

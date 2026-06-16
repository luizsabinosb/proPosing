using System.Linq;

namespace ProPosing.Avalonia.Models;

/// <summary>
/// Pose-agnostic measurable metrics emitted alongside the qualitative feedback.
/// All values are aspect-corrected and normalized where indicated. NaN means "n/a".
/// </summary>
public sealed record PoseMetrics
{
    /// <summary>Body yaw in degrees: 0 = facing camera, ±90 = side.</summary>
    public double BodyYawDeg { get; init; } = double.NaN;

    /// <summary>Lateral torso tilt in degrees, 0 = perfectly vertical.</summary>
    public double LateralTiltDeg { get; init; } = double.NaN;

    /// <summary>Torso twist (shoulder yaw vs hip yaw), 0 = aligned.</summary>
    public double TorsoTwistDeg { get; init; } = double.NaN;

    /// <summary>V-taper ≈ shoulderSpan / hipSpan (hip used as waist proxy).</summary>
    public double VTaper { get; init; } = double.NaN;

    /// <summary>Per-frame symmetry score ∈ [0,1], higher = more symmetric.</summary>
    public double Symmetry { get; init; } = double.NaN;

    /// <summary>Temporal tightness ∈ [0,1] — computed by the smoother over recent frames.</summary>
    public double Tightness { get; init; } = double.NaN;

    public static readonly PoseMetrics Empty = new();
}

public sealed record PoseFeedback(
    string Status,               // "correct" | "adjustment_needed" | "incorrect" | "no_detection"
    string Message,
    IReadOnlyList<string> Hints)
{
    public PoseMetrics Metrics { get; init; } = PoseMetrics.Empty;

    public static readonly PoseFeedback NoDetection =
        new("no_detection", "Aguardando detecção...", []);

    public static PoseFeedback FromErrors(IReadOnlyList<string> errors, string correctMessage)
    {
        if (errors.Count == 0)
            return new("correct", correctMessage, []);

        // 1–2 fixable issues = the pose shape is there, fine-tuning needed (amber).
        // 3+ issues = the pose isn't being presented yet (red). Guard-clause failures
        // (wrong orientation, missing defining limbs) bypass this and stay "incorrect".
        var status = errors.Count <= 2 ? "adjustment_needed" : "incorrect";
        return new(status, string.Empty, errors.ToList());
    }

    public PoseFeedback WithMetrics(PoseMetrics metrics) => this with { Metrics = metrics };
}

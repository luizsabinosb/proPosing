using System.Linq;
using ProPosing.Avalonia.Models;

namespace ProPosing.Avalonia.Evaluation;

public sealed class PoseEvaluatorRegistry
{
    private readonly Dictionary<string, IPoseEvaluator> _evaluators;

    public PoseEvaluatorRegistry(IEnumerable<IPoseEvaluator> evaluators)
    {
        _evaluators = evaluators.ToDictionary(e => e.PoseMode);
    }

    /// <summary>
    /// Evaluate a frame. <paramref name="aspect"/> must be the source image
    /// <c>width / height</c> — required for correct angle / distance math.
    /// </summary>
    public PoseFeedback Evaluate(
        string poseMode,
        IReadOnlyList<LandmarkPoint> landmarks,
        double aspect)
    {
        if (landmarks.Count < 29 || !double.IsFinite(aspect) || aspect <= 0)
            return PoseFeedback.NoDetection;

        if (!_evaluators.TryGetValue(poseMode, out var evaluator))
            return PoseFeedback.NoDetection;

        var ctx = PoseContext.FromLandmarks(landmarks, aspect);
        var feedback = evaluator.Evaluate(ctx);

        // Attach cross-cutting metrics so the UI can surface them regardless of pose.
        return feedback.WithMetrics(BuildMetrics(ctx));
    }

    private static PoseMetrics BuildMetrics(PoseContext ctx)
    {
        double vTaper = ctx.HipSpan > 1e-6 ? ctx.ShoulderSpan / ctx.HipSpan : double.NaN;
        double symmetry = SymmetryMetric.Compute(ctx);
        return new PoseMetrics
        {
            BodyYawDeg     = ctx.BodyYawDeg,
            LateralTiltDeg = ctx.LateralTiltDeg,
            TorsoTwistDeg  = ctx.TorsoTwistDeg,
            VTaper         = vTaper,
            Symmetry       = symmetry,
        };
    }
}

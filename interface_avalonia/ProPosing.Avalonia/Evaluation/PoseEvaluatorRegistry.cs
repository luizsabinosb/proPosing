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

    public PoseFeedback Evaluate(string poseMode, IReadOnlyList<LandmarkPoint> landmarks)
    {
        if (landmarks.Count < 29) return PoseFeedback.NoDetection;
        return _evaluators.TryGetValue(poseMode, out var ev)
            ? ev.Evaluate(landmarks)
            : PoseFeedback.NoDetection;
    }
}

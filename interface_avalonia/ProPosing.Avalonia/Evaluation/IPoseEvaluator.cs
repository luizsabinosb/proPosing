using ProPosing.Avalonia.Models;

namespace ProPosing.Avalonia.Evaluation;

public interface IPoseEvaluator
{
    string PoseMode { get; }
    PoseFeedback Evaluate(IReadOnlyList<LandmarkPoint> landmarks);
}

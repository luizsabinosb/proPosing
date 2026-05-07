namespace ProPosing.Avalonia.Evaluation;

public interface IPoseEvaluator
{
    string PoseMode { get; }
    Models.PoseFeedback Evaluate(PoseContext ctx);
}

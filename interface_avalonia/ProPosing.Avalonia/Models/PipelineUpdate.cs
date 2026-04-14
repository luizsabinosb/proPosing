namespace ProPosing.Avalonia.Models;

public sealed class PipelineUpdate
{
    public required byte[] BgraBuffer { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public int Fps { get; init; }
    public IReadOnlyList<LandmarkPoint> Landmarks { get; init; } = [];
    public PoseFeedback? Feedback { get; init; }
    public string PoseMode { get; init; } = "enquadramento";
}

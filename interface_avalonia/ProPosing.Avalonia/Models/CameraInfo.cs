namespace ProPosing.Avalonia.Models;

public sealed class CameraInfo
{
    public int Index { get; init; }
    public string Name { get; init; } = string.Empty;

    public override string ToString() => Name;
}

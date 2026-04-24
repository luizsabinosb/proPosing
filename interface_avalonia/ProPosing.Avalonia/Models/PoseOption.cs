using CommunityToolkit.Mvvm.ComponentModel;

namespace ProPosing.Avalonia.Models;

public sealed partial class PoseOption : ObservableObject
{
    public int Number { get; init; }
    public string Mode { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;

    [ObservableProperty]
    private bool _isSelected;
}

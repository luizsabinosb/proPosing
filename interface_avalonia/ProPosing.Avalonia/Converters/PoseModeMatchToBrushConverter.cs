using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ProPosing.Avalonia.Converters;

public sealed class PoseModeMatchToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var selected = value?.ToString() ?? string.Empty;
        var raw = parameter?.ToString() ?? string.Empty;
        var parts = raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return Brush.Parse("#202B3555");
        }

        var mode = parts[0];
        var role = parts[1];
        var isSelected = string.Equals(selected, mode, StringComparison.OrdinalIgnoreCase);

        return role switch
        {
            "background" => isSelected ? Brush.Parse("#2EEF4444") : Brush.Parse("#161616"),
            "border"     => isSelected ? Brush.Parse("#CCEF4444") : Brush.Parse("#2A2A2A"),
            _ => Brush.Parse("#161616"),
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

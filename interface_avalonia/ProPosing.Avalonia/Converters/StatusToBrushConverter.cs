using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ProPosing.Avalonia.Converters;

public sealed class StatusToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var role = parameter?.ToString() ?? "fill";
        var status = value?.ToString() ?? "no_detection";

        return role switch
        {
            "accent" => status switch
            {
                "correct"            => Brush.Parse("#10B981"),
                "incorrect"          => Brush.Parse("#EF4444"),
                "adjustment_needed"  => Brush.Parse("#F59E0B"),
                _                    => Brush.Parse("#6B7280"),
            },
            "border" => status switch
            {
                "correct"            => Brush.Parse("#4010B981"),
                "incorrect"          => Brush.Parse("#40EF4444"),
                "adjustment_needed"  => Brush.Parse("#40F59E0B"),
                _                    => Brush.Parse("#406B7280"),
            },
            _ => status switch  // "fill" — card background
            {
                "correct"            => Brush.Parse("#1210B981"),
                "incorrect"          => Brush.Parse("#12EF4444"),
                "adjustment_needed"  => Brush.Parse("#12F59E0B"),
                _                    => Brush.Parse("#126B7280"),
            },
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

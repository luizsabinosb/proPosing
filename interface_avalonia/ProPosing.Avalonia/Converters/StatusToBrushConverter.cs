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
            "border" => status switch
            {
                "correct" => Brush.Parse("#58C46B"),
                "incorrect" => Brush.Parse("#EF4444"),
                "adjustment_needed" => Brush.Parse("#F59E0B"),
                _ => Brush.Parse("#6B7280"),
            },
            "text" => Brushes.White,
            _ => status switch
            {
                "correct" => Brush.Parse("#2258C46B"),
                "incorrect" => Brush.Parse("#22EF4444"),
                "adjustment_needed" => Brush.Parse("#22F59E0B"),
                _ => Brush.Parse("#224B5563"),
            },
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

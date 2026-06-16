using System.Globalization;
using Avalonia.Data.Converters;

namespace ProPosing.Avalonia.Converters;

public sealed class StatusToLabelConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Icon glyphs make the status readable without relying on color alone
        // (red/green is indistinguishable under deuteranopia). Plain font glyphs,
        // not emoji, so they render the same on Windows and macOS.
        if (parameter?.ToString() == "icon")
        {
            return value?.ToString() switch
            {
                "correct" => "✓",
                "incorrect" => "✕",
                "adjustment_needed" => "!",
                _ => "○",
            };
        }

        return value?.ToString() switch
        {
            "correct" => "CORRETO",
            "incorrect" => "INCORRETO",
            "adjustment_needed" => "AJUSTE NECESSÁRIO",
            _ => "AGUARDANDO",
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

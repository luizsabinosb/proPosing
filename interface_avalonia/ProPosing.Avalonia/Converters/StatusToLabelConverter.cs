using System.Globalization;
using Avalonia.Data.Converters;

namespace ProPosing.Avalonia.Converters;

public sealed class StatusToLabelConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
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

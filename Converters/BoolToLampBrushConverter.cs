using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace etch_ui.Converters;

/// <summary>램프 On/Off → 배경 Brush (ActiveColor 파라미터).</summary>
public sealed class BoolToLampBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool on = value is true;
        if (!on)
        {
            return new SolidColorBrush(Color.FromRgb(51, 65, 85));
        }

        string p = parameter?.ToString() ?? "Blue";
        return p switch
        {
            "Green" => new SolidColorBrush(Color.FromRgb(22, 163, 74)),
            "Gold" => new SolidColorBrush(Color.FromRgb(217, 119, 6)),
            "Red" => new SolidColorBrush(Color.FromRgb(220, 38, 38)),
            _ => new SolidColorBrush(Color.FromRgb(37, 99, 235)),
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

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
            return new SolidColorBrush(Color.FromRgb(209, 213, 219));
        }

        string p = parameter?.ToString() ?? "Blue";
        return p switch
        {
            "Green" => Brushes.LimeGreen,
            "Gold" => Brushes.Goldenrod,
            "Red" => Brushes.OrangeRed,
            _ => Brushes.DeepSkyBlue,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

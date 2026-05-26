using System.Windows;
using System.Windows.Controls;

namespace etch_ui.Controls;

public partial class SensorRangeBar : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(SensorRangeBar),
            new PropertyMetadata(string.Empty, (d, _) => ((SensorRangeBar)d).Refresh()));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(SensorRangeBar),
            new PropertyMetadata(0.0, (d, _) => ((SensorRangeBar)d).Refresh()));

    public static readonly DependencyProperty RangeMinProperty =
        DependencyProperty.Register(nameof(RangeMin), typeof(double), typeof(SensorRangeBar),
            new PropertyMetadata(0.0, (d, _) => ((SensorRangeBar)d).Refresh()));

    public static readonly DependencyProperty RangeMaxProperty =
        DependencyProperty.Register(nameof(RangeMax), typeof(double), typeof(SensorRangeBar),
            new PropertyMetadata(100.0, (d, _) => ((SensorRangeBar)d).Refresh()));

    public static readonly DependencyProperty ScaleMinProperty =
        DependencyProperty.Register(nameof(ScaleMin), typeof(double), typeof(SensorRangeBar),
            new PropertyMetadata(0.0, (d, _) => ((SensorRangeBar)d).Refresh()));

    public static readonly DependencyProperty ScaleMaxProperty =
        DependencyProperty.Register(nameof(ScaleMax), typeof(double), typeof(SensorRangeBar),
            new PropertyMetadata(100.0, (d, _) => ((SensorRangeBar)d).Refresh()));

    public static readonly DependencyProperty UnitProperty =
        DependencyProperty.Register(nameof(Unit), typeof(string), typeof(SensorRangeBar),
            new PropertyMetadata(string.Empty, (d, _) => ((SensorRangeBar)d).Refresh()));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double RangeMin
    {
        get => (double)GetValue(RangeMinProperty);
        set => SetValue(RangeMinProperty, value);
    }

    public double RangeMax
    {
        get => (double)GetValue(RangeMaxProperty);
        set => SetValue(RangeMaxProperty, value);
    }

    public double ScaleMin
    {
        get => (double)GetValue(ScaleMinProperty);
        set => SetValue(ScaleMinProperty, value);
    }

    public double ScaleMax
    {
        get => (double)GetValue(ScaleMaxProperty);
        set => SetValue(ScaleMaxProperty, value);
    }

    public string Unit
    {
        get => (string)GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public SensorRangeBar()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
        SizeChanged += (_, _) => Refresh();
    }

    private void Refresh()
    {
        TitleBlock.Text = Title;
        ValueBlock.Text = $"{Value:0.##} {Unit}".Trim();

        double track = ActualWidth;
        if (track < 1 || double.IsNaN(track))
        {
            track = 120;
        }

        double span = ScaleMax - ScaleMin;
        if (span < 1e-9)
        {
            span = 1;
        }

        double bandLeft = (RangeMin - ScaleMin) / span * track;
        double bandWidth = (RangeMax - RangeMin) / span * track;
        double fillWidth = (Value - ScaleMin) / span * track;

        bandLeft = Math.Clamp(bandLeft, 0, track);
        bandWidth = Math.Clamp(bandWidth, 0, track - bandLeft);
        fillWidth = Math.Clamp(fillWidth, 0, track);

        BandBorder.Width = bandWidth;
        BandBorder.Margin = new Thickness(bandLeft, 0, 0, 0);

        FillBorder.Width = Math.Max(4, fillWidth);
        bool inRange = Value >= RangeMin && Value <= RangeMax;
        FillBorder.Background = inRange
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 99, 235))
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 38, 38));
    }
}

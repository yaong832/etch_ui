using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace etch_ui.Controls;

public partial class SparklineControl : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(SparklineControl),
            new PropertyMetadata(string.Empty, (d, _) => ((SparklineControl)d).UpdateTitle()));

    public static readonly DependencyProperty ValuesProperty =
        DependencyProperty.Register(nameof(Values), typeof(IEnumerable<double>), typeof(SparklineControl),
            new PropertyMetadata(null, OnValuesChanged));

    public static readonly DependencyProperty YMinProperty =
        DependencyProperty.Register(nameof(YMin), typeof(double), typeof(SparklineControl),
            new PropertyMetadata(0.0, OnScaleChanged));

    public static readonly DependencyProperty YMaxProperty =
        DependencyProperty.Register(nameof(YMax), typeof(double), typeof(SparklineControl),
            new PropertyMetadata(100.0, OnScaleChanged));

    public static readonly DependencyProperty StrokeProperty =
        DependencyProperty.Register(nameof(Stroke), typeof(Brush), typeof(SparklineControl),
            new PropertyMetadata(Brushes.DodgerBlue, OnScaleChanged));

    private Polyline? _line;
    private INotifyCollectionChanged? _boundNotify;

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public IEnumerable<double>? Values
    {
        get => (IEnumerable<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public double YMin
    {
        get => (double)GetValue(YMinProperty);
        set => SetValue(YMinProperty, value);
    }

    public double YMax
    {
        get => (double)GetValue(YMaxProperty);
        set => SetValue(YMaxProperty, value);
    }

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public SparklineControl()
    {
        InitializeComponent();
        UpdateTitle();
    }

    private static void OnValuesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var c = (SparklineControl)d;
        c.UnhookCollection();
        if (e.NewValue is INotifyCollectionChanged ncc)
        {
            c._boundNotify = ncc;
            ncc.CollectionChanged += c.OnCollectionChanged;
        }

        c.Redraw();
    }

    private static void OnScaleChanged(DependencyObject d, DependencyPropertyChangedEventArgs _)
        => ((SparklineControl)d).Redraw();

    private void UnhookCollection()
    {
        if (_boundNotify is not null)
        {
            _boundNotify.CollectionChanged -= OnCollectionChanged;
            _boundNotify = null;
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Redraw();

    private void PlotCanvas_OnSizeChanged(object sender, SizeChangedEventArgs e) => Redraw();

    private void UpdateTitle() => TitleBlock.Text = Title;

    private void Redraw()
    {
        PlotCanvas.Children.Clear();
        _line = null;

        double w = PlotCanvas.ActualWidth;
        double h = PlotCanvas.ActualHeight;
        if (w < 8 || h < 8)
        {
            return;
        }

        IEnumerable<double>? values = Values?.ToList();
        if (values is null || values.Count() < 2)
        {
            return;
        }

        double[] arr = values.ToArray();
        double ySpan = YMax - YMin;
        if (ySpan < 1e-9)
        {
            ySpan = 1;
        }

        var points = new PointCollection();
        for (int i = 0; i < arr.Length; i++)
        {
            double t = arr.Length == 1 ? 0 : (double)i / (arr.Length - 1);
            double x = t * w;
            double norm = Math.Clamp((arr[i] - YMin) / ySpan, 0, 1);
            double y = h - norm * h;
            points.Add(new Point(x, y));
        }

        _line = new Polyline
        {
            Points = points,
            Stroke = Stroke,
            StrokeThickness = 2,
            Fill = Brushes.Transparent,
            SnapsToDevicePixels = true,
        };
        PlotCanvas.Children.Add(_line);

        // 정상 대역 가이드(옵션: YMin/YMax가 인터락 대역이면 배경 띠)
        double bandTop = h - Math.Clamp((YMax - YMin) / ySpan, 0, 1) * h;
        _ = bandTop;
    }
}

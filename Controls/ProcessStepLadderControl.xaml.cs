using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace etch_ui.Controls;

public partial class ProcessStepLadderControl : UserControl
{
    public static readonly DependencyProperty ActiveStepIndexProperty =
        DependencyProperty.Register(nameof(ActiveStepIndex), typeof(int), typeof(ProcessStepLadderControl),
            new PropertyMetadata(0, OnStepChanged));

    public static readonly DependencyProperty IsWarningStepProperty =
        DependencyProperty.Register(nameof(IsWarningStep), typeof(bool), typeof(ProcessStepLadderControl),
            new PropertyMetadata(false, OnStepChanged));

    public int ActiveStepIndex
    {
        get => (int)GetValue(ActiveStepIndexProperty);
        set => SetValue(ActiveStepIndexProperty, value);
    }

    public bool IsWarningStep
    {
        get => (bool)GetValue(IsWarningStepProperty);
        set => SetValue(IsWarningStepProperty, value);
    }

    private readonly TextBlock[] _steps;

    public ProcessStepLadderControl()
    {
        InitializeComponent();
        _steps = [Step0, Step1, Step2, Step3, Step4];
        Loaded += (_, _) => ApplyHighlight();
    }

    private static void OnStepChanged(DependencyObject d, DependencyPropertyChangedEventArgs _)
        => ((ProcessStepLadderControl)d).ApplyHighlight();

    private void ApplyHighlight()
    {
        int idx = Math.Clamp(ActiveStepIndex, 0, _steps.Length - 1);
        for (int i = 0; i < _steps.Length; i++)
        {
            bool active = i == idx;
            _steps[i].FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
            Brush activeBrush = IsWarningStep && idx == 3
                ? new SolidColorBrush(Color.FromRgb(217, 119, 6))
                : new SolidColorBrush(Color.FromRgb(30, 64, 175));
            _steps[i].Foreground = active
                ? activeBrush
                : new SolidColorBrush(Color.FromRgb(107, 114, 128));
            _steps[i].Background = active
                ? new SolidColorBrush(Color.FromRgb(219, 234, 254))
                : Brushes.Transparent;
            _steps[i].Padding = active ? new Thickness(6, 4, 6, 4) : new Thickness(6, 2, 6, 2);
        }
    }
}

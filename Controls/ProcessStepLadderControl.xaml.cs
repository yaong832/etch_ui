using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using etch_ui.Services;

namespace etch_ui.Controls;

public partial class ProcessStepLadderControl : UserControl
{
    public static readonly DependencyProperty ActiveStepIndexProperty =
        DependencyProperty.Register(nameof(ActiveStepIndex), typeof(int), typeof(ProcessStepLadderControl),
            new PropertyMetadata(0, OnVisualChanged));

    public static readonly DependencyProperty IsWarningStepProperty =
        DependencyProperty.Register(nameof(IsWarningStep), typeof(bool), typeof(ProcessStepLadderControl),
            new PropertyMetadata(false, OnVisualChanged));

    public static readonly DependencyProperty ActiveStepCaptionProperty =
        DependencyProperty.Register(nameof(ActiveStepCaption), typeof(string), typeof(ProcessStepLadderControl),
            new PropertyMetadata(string.Empty, OnVisualChanged));

    public static readonly DependencyProperty StepDetailTextProperty =
        DependencyProperty.Register(nameof(StepDetailText), typeof(string), typeof(ProcessStepLadderControl),
            new PropertyMetadata(string.Empty, OnVisualChanged));

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

    public string ActiveStepCaption
    {
        get => (string)GetValue(ActiveStepCaptionProperty);
        set => SetValue(ActiveStepCaptionProperty, value);
    }

    public string StepDetailText
    {
        get => (string)GetValue(StepDetailTextProperty);
        set => SetValue(StepDetailTextProperty, value);
    }

    private readonly TextBlock[] _steps;

    public ProcessStepLadderControl()
    {
        InitializeComponent();
        _steps = [Step0, Step1, Step2, Step3, Step4];
        Loaded += (_, _) => ApplyHighlight();
    }

    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs _)
        => ((ProcessStepLadderControl)d).ApplyHighlight();

    private void ApplyHighlight()
    {
        int idx = Math.Clamp(ActiveStepIndex, 0, _steps.Length - 1);
        IReadOnlyList<string> defaults = ProcessStepMapper.DefaultStepLabels;
        for (int i = 0; i < _steps.Length; i++)
        {
            bool active = i == idx;
            _steps[i].Text = active && !string.IsNullOrWhiteSpace(ActiveStepCaption)
                ? ActiveStepCaption
                : defaults[i];
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

        TxtDetail.Text = StepDetailText ?? string.Empty;
        TxtDetail.Visibility = string.IsNullOrWhiteSpace(TxtDetail.Text)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }
}

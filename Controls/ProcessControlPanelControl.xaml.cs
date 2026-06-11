using System.Windows;
using System.Windows.Controls;

namespace etch_ui.Controls;

public partial class ProcessControlPanelControl : UserControl
{
    public static readonly DependencyProperty CompactModeProperty =
        DependencyProperty.Register(
            nameof(CompactMode),
            typeof(bool),
            typeof(ProcessControlPanelControl),
            new PropertyMetadata(false, OnCompactModeChanged));

    public bool CompactMode
    {
        get => (bool)GetValue(CompactModeProperty);
        set => SetValue(CompactModeProperty, value);
    }

    public ProcessControlPanelControl()
    {
        InitializeComponent();
    }

    private static void OnCompactModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ProcessControlPanelControl panel)
        {
            panel.ApplyCompactMode((bool)e.NewValue);
        }
    }

    private void ApplyCompactMode(bool compact)
    {
        FullButtons.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        CompactButtons.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;
        SideInfo.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;
    }
}

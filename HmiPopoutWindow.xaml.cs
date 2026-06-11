using System.Windows;
using System.Windows.Controls;
using etch_ui.Services.Hmi;

namespace etch_ui;

public partial class HmiPopoutWindow : Window
{
    private readonly HmiPopoutKind _kind;

    public HmiPopoutWindow(
        HmiPopoutKind kind,
        string title,
        string subtitle,
        UIElement content,
        double defaultWidth = 900,
        double defaultHeight = 680)
    {
        _kind = kind;
        InitializeComponent();
        Title = title;
        TxtTitle.Text = title;
        TxtSubtitle.Text = subtitle + " · 위치·항상 위는 종료 시 저장";
        Host.Content = content;
        HmiPopoutLayoutStore.TryApply(this, kind, defaultWidth, defaultHeight);
        ChkTopmost.IsChecked = Topmost;
        Closing += OnClosingSaveLayout;
    }

    private void ChkTopmost_Changed(object sender, RoutedEventArgs e) =>
        Topmost = ChkTopmost.IsChecked == true;

    private void BtnSecondaryMonitor_Click(object sender, RoutedEventArgs e) =>
        HmiPopoutLayoutStore.ApplySecondaryMonitorPreset(this);

    private void OnClosingSaveLayout(object? sender, System.ComponentModel.CancelEventArgs e) =>
        HmiPopoutLayoutStore.Save(this, _kind);
}

using System.Windows;
using System.Windows.Controls;

namespace etch_ui;

public partial class HmiPopoutWindow : Window
{
    public HmiPopoutWindow(string title, string subtitle, UIElement content, double width = 900, double height = 680)
    {
        InitializeComponent();
        Title = title;
        TxtSubtitle.Text = subtitle;
        Width = width;
        Height = height;
        Host.Content = content;
    }
}

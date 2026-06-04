using System.Windows;

namespace etch_ui;

public partial class DemoGuideWindow : Window
{
    public DemoGuideWindow()
    {
        InitializeComponent();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}

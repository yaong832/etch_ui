using System.Windows;
using System.Windows.Controls;

namespace etch_ui.Controls;

public partial class InterlockCompactControl : UserControl
{
    public static readonly RoutedEvent OpenDetailRequestedEvent = EventManager.RegisterRoutedEvent(
        nameof(OpenDetailRequested),
        RoutingStrategy.Bubble,
        typeof(RoutedEventHandler),
        typeof(InterlockCompactControl));

    public event RoutedEventHandler OpenDetailRequested
    {
        add => AddHandler(OpenDetailRequestedEvent, value);
        remove => RemoveHandler(OpenDetailRequestedEvent, value);
    }

    public InterlockCompactControl()
    {
        InitializeComponent();
        BtnOpenAiDetail.Click += (_, _) => RaiseEvent(new RoutedEventArgs(OpenDetailRequestedEvent));
    }
}

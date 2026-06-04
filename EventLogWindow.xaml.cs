using System.Collections.ObjectModel;
using System.Windows;
using etch_ui.Security;

namespace etch_ui;

public partial class EventLogWindow : Window
{
    private readonly DatabaseService _db;
    private readonly ObservableCollection<EventLogRow> _rows = new();

    public EventLogWindow(DatabaseService databaseService)
    {
        InitializeComponent();
        _db = databaseService;
        GrdEvents.ItemsSource = _rows;
        Loaded += (_, _) => RefreshGrid();
    }

    private void RefreshGrid()
    {
        _rows.Clear();
        foreach (EventLogRow row in _db.GetRecentEventLogs(200))
        {
            _rows.Add(row);
        }
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e) => RefreshGrid();

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}

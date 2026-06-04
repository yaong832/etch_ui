using System.Collections.ObjectModel;
using System.Windows;
using etch_ui.Security;

namespace etch_ui;

public partial class AlarmHistoryWindow : Window
{
    private readonly DatabaseService _db;
    private readonly ObservableCollection<AlarmHistoryRow> _rows = new();

    public AlarmHistoryWindow(DatabaseService databaseService)
    {
        InitializeComponent();
        _db = databaseService;
        GrdAlarms.ItemsSource = _rows;
        Loaded += (_, _) => RefreshGrid();
    }

    private void RefreshGrid()
    {
        _rows.Clear();
        foreach (AlarmHistoryRow row in _db.GetRecentAlarmHistory(200))
        {
            _rows.Add(row);
        }
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e) => RefreshGrid();

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}

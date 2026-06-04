using System.Windows;
using etch_ui.Services.Simulation;

namespace etch_ui;

public partial class MaintenanceToolsWindow : Window
{
    private readonly TmTransferSimulator _simulator;
    private readonly bool _benchMode;
    private readonly Action<bool> _setVirtualLoadLockClosed;

    public MaintenanceToolsWindow(
        TmTransferSimulator simulator,
        bool benchMode,
        bool virtualLoadLockClosed,
        Action<bool> setVirtualLoadLockClosed)
    {
        InitializeComponent();
        _simulator = simulator;
        _benchMode = benchMode;
        _setVirtualLoadLockClosed = setVirtualLoadLockClosed;
        ChkVirtualLoadLockClosed.IsChecked = virtualLoadLockClosed;
        TxtSimHint.Text = _benchMode
            ? "시뮬 허용 ON: Start 없이 1틱씩 스케줄러 동작을 확인할 수 있습니다."
            : "시뮬 허용이 꺼져 있으면 이송 1틱은 사용할 수 없습니다.";
    }

    private void ChkVirtualLoadLockClosed_Changed(object sender, RoutedEventArgs e)
    {
        if (ChkVirtualLoadLockClosed.IsChecked is bool closed)
        {
            _setVirtualLoadLockClosed(closed);
        }
    }

    private void BtnSimTick_Click(object sender, RoutedEventArgs e)
    {
        if (!_benchMode)
        {
            MessageBox.Show(this, "「시뮬 허용」을 켠 뒤 사용하세요.", "유지보수", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!_simulator.IsActive)
        {
            _simulator.StartDemoLoop(AppSettings.CreateCapacityConfig());
        }

        _simulator.Tick(1);
        TxtSimHint.Text = $"1틱 실행 — {_simulator.PhaseHint}";
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

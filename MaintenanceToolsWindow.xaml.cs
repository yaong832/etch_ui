using System.Windows;
using etch_ui.Services.Simulation;

namespace etch_ui;

public partial class MaintenanceToolsWindow : Window
{
    private readonly TmTransferSimulator _simulator;
    private readonly bool _benchMode;
    private readonly Action<bool> _setVirtualLoadLockClosed;
    private readonly Action<string>? _onLog;
    private readonly Action? _onStateChanged;

    public MaintenanceToolsWindow(
        TmTransferSimulator simulator,
        bool benchMode,
        bool virtualLoadLockClosed,
        Action<bool> setVirtualLoadLockClosed,
        Action<string>? onLog = null,
        Action? onStateChanged = null)
    {
        InitializeComponent();
        _simulator = simulator;
        _benchMode = benchMode;
        _setVirtualLoadLockClosed = setVirtualLoadLockClosed;
        _onLog = onLog;
        _onStateChanged = onStateChanged;
        ChkVirtualLoadLockClosed.IsChecked = virtualLoadLockClosed;
        TxtSimHint.Text = _benchMode
            ? "시뮬 허용 ON: 라인 초기화 후 1틱으로 스케줄러를 단계 확인할 수 있습니다."
            : "「시뮬 허용」을 켠 뒤 이송 1틱을 사용하세요.";
        RefreshSummary();
    }

    private void RefreshSummary()
    {
        TxtLineSummary.Text = _simulator.DescribeMaintenanceState();
        _onStateChanged?.Invoke();
    }

    private void Log(string message)
    {
        TxtStatus.Text = message;
        _onLog?.Invoke($"[정비] {message}");
    }

    private static bool Confirm(Window owner, string message) =>
        MessageBox.Show(owner, message, "정비 확인", MessageBoxButton.YesNo, MessageBoxImage.Question)
        == MessageBoxResult.Yes;

    private void ChkVirtualLoadLockClosed_Changed(object sender, RoutedEventArgs e)
    {
        if (ChkVirtualLoadLockClosed.IsChecked is bool closed)
        {
            _setVirtualLoadLockClosed(closed);
            Log($"Load Lock 도식: {(closed ? "닫힘" : "열림")} 표시");
        }
    }

    private void BtnClearBm_Click(object sender, RoutedEventArgs e)
    {
        if (!Confirm(this, "BM(Load Lock) 버퍼의 웨이퍼를 제거합니다."))
        {
            return;
        }

        _simulator.MaintenanceClearLoadLock();
        Log("BM(Load Lock) 비움");
        RefreshSummary();
    }

    private void BtnClearAligner_Click(object sender, RoutedEventArgs e)
    {
        if (!Confirm(this, "Aligner 슬롯을 비웁니다."))
        {
            return;
        }

        _simulator.MaintenanceClearAligner();
        Log("Aligner 비움");
        RefreshSummary();
    }

    private void BtnClearPm_Click(object sender, RoutedEventArgs e)
    {
        if (!Confirm(this, "PM1~4 챔버 웨이퍼와 투입 예약을 제거합니다."))
        {
            return;
        }

        _simulator.MaintenanceClearChambers();
        Log("PM1~4 웨이퍼 제거");
        RefreshSummary();
    }

    private void BtnClearSide_Click(object sender, RoutedEventArgs e)
    {
        if (!Confirm(this, "Side Storage 카세트를 비웁니다 (LOT 출하 없음)."))
        {
            return;
        }

        _simulator.MaintenanceClearSideStorage();
        Log("Side Stg 비움");
        RefreshSummary();
    }

    private void BtnRemountFoup_Click(object sender, RoutedEventArgs e)
    {
        if (!Confirm(this,
                "LP1~3 FOUP를 재장착하고 잔량을 충전합니다.\n장내 InFlight 카운트도 0으로 맞춥니다.\n(장비 안 웨이퍼는 그대로일 수 있습니다)"))
        {
            return;
        }

        int cap = _simulator.MaintenanceRemountAllFoups();
        Log($"FOUP 3개 재장착 · 각 {cap}매");
        RefreshSummary();
    }

    private void BtnSideSwap_Click(object sender, RoutedEventArgs e)
    {
        int shipped = _simulator.MaintenanceSideCassetteSwap();
        if (shipped <= 0)
        {
            Log("Side Stg 만석이 아니어서 카세트 교체 없음");
        }
        else
        {
            Log($"Side 카세트 교체 · {shipped}매 출하(LOT 반영)");
        }

        RefreshSummary();
    }

    private void BtnResetLine_Click(object sender, RoutedEventArgs e)
    {
        if (!Confirm(this,
                "가상 라인을 데모 초기 상태로 되돌립니다.\n(FOUP·BM·PM·Side·LOT·로봇 큐 전부 초기화)"))
        {
            return;
        }

        _simulator.MaintenanceResetVirtualLine();
        Log("가상 라인 전체 초기화");
        RefreshSummary();
    }

    private void BtnSimTick_Click(object sender, RoutedEventArgs e)
    {
        if (!_benchMode)
        {
            MessageBox.Show(this, "「시뮬 허용」을 켠 뒤 사용하세요.", "유지보수",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _simulator.MaintenanceAdvanceOneTick(out string hint);
        TxtSimHint.Text = $"1틱 — {hint}";
        Log($"이송 1틱 — {hint}");
        RefreshSummary();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}

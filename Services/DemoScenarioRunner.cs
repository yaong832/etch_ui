using System.Windows.Threading;

namespace etch_ui.Services;

/// <summary>시뮬 데모 자동 진행 — 시뮬 허용·Start·안내 로그.</summary>
public sealed class DemoScenarioRunner
{
    private readonly DispatcherTimer _timer;
    private int _step;
    private DemoScenarioHost? _host;

    public bool IsRunning { get; private set; }

    public event Action? Completed;

    public DemoScenarioRunner()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += OnTick;
    }

    public void Start(DemoScenarioHost host)
    {
        if (IsRunning)
        {
            return;
        }

        _host = host;
        _step = 0;
        IsRunning = true;
        _timer.Start();
        OnTick(null!, EventArgs.Empty);
    }

    public void Stop()
    {
        if (!IsRunning && _host is null)
        {
            return;
        }

        IsRunning = false;
        _timer.Stop();
        _host = null;
        _step = 0;
        Completed?.Invoke();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_host is null)
        {
            Stop();
            return;
        }

        switch (_step++)
        {
            case 0:
                _host.Log("=== 데모 시나리오 시작 ===");
                _host.Log("1/5 Flask 대시보드·모듈/레시피 탭을 열어 두세요.");
                break;
            case 1:
                if (!_host.SimulationEnabled)
                {
                    _host.EnableSimulation();
                    _host.Log("2/5 「시뮬 허용」을 켰습니다.");
                }
                else
                {
                    _host.Log("2/5 시뮬 허용 이미 ON.");
                }
                break;
            case 2:
                if (_host.EquipmentState is not "RUNNING")
                {
                    if (_host.TryStartProcess("데모시나리오"))
                    {
                        _host.Log("3/5 공정 Start — 가상 TM·모듈 상태 갱신.");
                    }
                    else
                    {
                        _host.Log("3/5 Start 불가 — 유지보수 해제·권한 확인 후 수동 Start.");
                    }
                }
                else
                {
                    _host.Log("3/5 이미 RUNNING — 이송·모듈 표시 확인.");
                }
                break;
            case 3:
                _host.Log("4/5 웹 모듈 상태·레시피 탭 / WPF AI 예상 알람 확인.");
                break;
            case 4:
                _host.Log("5/5 데모 시나리오 완료 — Stop·Reset은 수동으로 진행하세요.");
                Stop();
                break;
        }
    }
}

public interface DemoScenarioHost
{
    void Log(string message);
    bool SimulationEnabled { get; }
    void EnableSimulation();
    string EquipmentState { get; }
    bool TryStartProcess(string source);
}

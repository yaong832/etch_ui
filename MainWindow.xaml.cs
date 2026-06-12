using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using etch_ui.Configuration;
using etch_ui.Plc;
using etch_ui.Security;
using etch_ui.Services;
using etch_ui.Services.Hmi;
using etch_ui.Services.Plc;
using etch_ui.Services.Simulation;
using etch_ui.Services.Scheduling;
using etch_ui.Controls;
using etch_ui.Equipment.Views;
using etch_ui.ViewModels;
using System.Threading.Tasks;

namespace etch_ui;

public partial class MainWindow : Window, DemoScenarioHost
{
    private readonly MainViewModel _vm = new();
    private readonly EquipmentMotionBridge _motionBridge;
    private readonly EquipmentMotionAnimator _motionAnimator;
    private readonly TmTransferSimulator _transferSim = new();
    private bool _lotCompleteHandled;
    private readonly DatabaseService _db;
    private readonly PlcPollingService _plcPolling = new(new PlcAdsService());
    private InterlockDecision _interlock = InterlockDecision.Empty;
    private readonly EtchFlaskClient _flask = new();
    private readonly HmiFlaskGateway _flaskGateway;
    private readonly HmiTelemetryPublisher _telemetryPublisher;
    private readonly HmiTelemetryStore _telemetryStore;
    private readonly AiTrainingDataRecorder _aiDataRecorder;
    private bool _maintVirtualLoadLockClosed = true;

    private readonly DispatcherTimer _uiTimer = new();

    private readonly Random _rand = new();
    private const double DemoWarningChancePerTick = 0.02;
    private const int DemoWarningDurationTicks = 8;
    private int _demoWarningTicksLeft;
    private bool _useSimulation;
    private bool _maintenanceMode;

    private readonly bool[] _hwPrev = new bool[4];
    private bool _hwInit;

    private EquipmentState _state = EquipmentState.Idle;
    private EquipmentState _lastLoggedState = EquipmentState.Idle;
    private string? _lastAlarmCode;

    private double _temp;
    private double _humi;
    private double _pressureMtorr;
    private short _pressureRaw;
    private bool _pressureSignalValid;
    private double _vib;
    private bool _accessSafe;
    private bool _accessInputValid;

    private int _flaskCounter;

    private bool _flaskProbeDone;
    private bool _flaskReachable;
    private DateTime? _lastFlaskSuccessUtc;
    private DateTime _nextFlaskFailLogUtc = DateTime.MinValue;
    private DateTime _nextFlaskEventFailLogUtc = DateTime.MinValue;

    private DateTime _lastProcessSampleUtc = DateTime.MinValue;

    private const int SparkHistoryMax = 72;

    /// <summary>EtherCAT 실측 샘플이 있을 때만 센서·접근 표시.</summary>
    private bool HasLiveSensorData =>
        !_useSimulation && _plcPolling.IsConnected && _lastProcessSampleUtc != DateTime.MinValue;

    /// <summary>
    /// 시뮬 허용 ON + TwinCAT 미사용(_useSimulation): 레시피·가상 TM·로직 확인용 데모.
    /// 인터락·실접촉 없이 Start 가능(Flask에는 sensorsLive=false 유지).
    /// </summary>
    private bool IsBenchMode => _useSimulation && _simulationFallbackEnabled;

    /// <summary>appsettings 초깃값·메인 창 버튼으로 바꿀 수 있음. false면 EtherCAT 실패 시 시뮬 대체 안 함.</summary>
    private bool _simulationFallbackEnabled;

    /// <summary>EtherCAT 실데이터 없음을 한 번만 로그했는지(성공 시 리셋).</summary>
    private bool _loggedPlcRequiredOffline;

    private bool _ethercatLinkLostLogged;
    private bool _loadLockOpenWhileRunningLogged;
    private int _aiPollCounter;
    private double _lastAiScore = -1;
    private string _lastAiHint = "Flask AI 대기 중";
    private EtchAiDiagnosis? _lastAiDiagnosis;
    private FlaskAiStatusSnapshot? _flaskAiStatus;
    private HmiPopoutWindow? _popoutEquipment;
    private HmiPopoutWindow? _popoutInterlock;
    private HmiPopoutWindow? _popoutDiagnostics;
    private HmiPopoutWindow? _popoutSensors;
    private HmiPopoutWindow? _popoutControl;
    private DateTime _nextAiHighScoreLogUtc = DateTime.MinValue;
    private readonly DemoScenarioRunner _demoScenarioRunner = new();
    private DateTime _nextAiSnapshotUtc = DateTime.MinValue;

    private enum EquipmentState
    {
        Idle,
        Ready,
        Running,
        Warning,
        Alarm,
        Maintenance
    }

    public MainWindow(DatabaseService databaseService)
    {
        _db = databaseService;
        _motionBridge = new EquipmentMotionBridge(_vm.Equipment);
        _motionAnimator = new EquipmentMotionAnimator(_vm.Equipment, SyncTransferMotionFrame);
        string dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
        _telemetryStore = new HmiTelemetryStore(Path.Combine(dataDir, "etch_hmi.db"));
        _flaskGateway = new HmiFlaskGateway(_flask);
        _telemetryPublisher = new HmiTelemetryPublisher(_flask, _telemetryStore);
        _aiDataRecorder = new AiTrainingDataRecorder(Path.Combine(dataDir, "ai_training_snapshots.jsonl"));
        InitializeComponent();
        DataContext = _vm;
        WireProcessPanel(EmbeddedProcessPanel);
        _simulationFallbackEnabled = AppSettings.SimulationEnabled;
        if (_simulationFallbackEnabled)
        {
            _useSimulation = true;
            SeedSimulationValues();
        }

        Loaded += (_, _) => OnWindowLoaded();
        Closed += (_, _) => OnWindowClosed();
        InitializeRuntime();
        SyncViewModel();
    }

    private void OnWindowLoaded()
    {
        _flaskGateway.BaseUrl = AppSettings.FlaskBaseUrl;
        _ = Task.Run(BackgroundPlcConnect);
        _ = ProbeFlaskOnceAsync();
    }

    private void BackgroundPlcConnect()
    {
        try
        {
            bool connected = _plcPolling.TryConnect(AppSettings.AdsPort);
            Dispatcher.BeginInvoke(() =>
            {
                if (connected)
                {
                    _useSimulation = false;
                    _loggedPlcRequiredOffline = false;
                    _ethercatLinkLostLogged = false;
                    AppendEvent(null, null, "EtherCAT ADS 연결 성공");
                    AddLog($"EtherCAT 연결 성공 (ADS 포트 {AppSettings.AdsPort})");
                }
                else
                {
                    OnPlcConnectFailed(_plcPolling.LastError ?? "알 수 없음");
                }

                SyncViewModel();
            });
        }
        catch (Exception ex)
        {
            Dispatcher.BeginInvoke(() => OnPlcConnectFailed(ex.Message));
        }
    }

    private void OnPlcConnectFailed(string err)
    {
        if (_simulationFallbackEnabled)
        {
            _useSimulation = true;
            SeedSimulationValues();
            AppendEvent( null, "A001", $"EtherCAT 연결 실패(시뮬 전환): {err}");
            AddLog($"EtherCAT 연결 실패 — 시뮬 허용 ON: {err}");
        }
        else
        {
            _useSimulation = false;
            if (!_loggedPlcRequiredOffline)
            {
                AppendEvent( "ALARM", "A001", $"EtherCAT 연결 실패: {err}");
                AddLog($"EtherCAT 연결 실패 — 시뮬 허용 OFF: {err}");
                _loggedPlcRequiredOffline = true;
            }
        }
    }

    private async Task ProbeFlaskOnceAsync()
    {
        _flaskProbeDone = false;
        bool ok;
        try
        {
            ok = await _flaskGateway.ProbeHealthAsync().ConfigureAwait(false);
        }
        catch
        {
            ok = false;
        }

        await Dispatcher.BeginInvoke(() =>
        {
            _flaskReachable = ok;
            _flaskProbeDone = true;
            if (ok)
            {
                _lastFlaskSuccessUtc = DateTime.UtcNow;
                AddLog($"Flask 응답 OK ({_flaskGateway.BaseUrl})");
                _ = RefreshFlaskAiStatusAsync();
            }
            else
            {
                _flaskAiStatus = null;
                AddLog($"Flask 미응답 — C:\\etchflask\\run_flask.bat 확인 ({_flaskGateway.BaseUrl})");
            }

            SyncViewModel();
        });
    }

    private async Task RefreshFlaskAiStatusAsync()
    {
        try
        {
            FlaskAiStatusSnapshot? status = await _flaskGateway.PollAiStatusAsync().ConfigureAwait(false);
            await Dispatcher.BeginInvoke(() =>
            {
                _flaskAiStatus = status;
                SyncAiEngineChip();
            });
        }
        catch
        {
            // ignore
        }
    }

    private void SyncAiEngineChip()
    {
        HmiAiEnginePresenter.Presentation chip = HmiAiEnginePresenter.Describe(
            _flaskReachable,
            _flaskAiStatus,
            _lastAiDiagnosis);
        _vm.AiEngineStatusText = chip.Text;
        _vm.AiEngineStatusBrush = chip.Brush;
        _vm.AiEngineStatusHint = chip.Hint;
    }

    private void OnWindowClosed()
    {
        _demoScenarioRunner.Stop();
        _motionAnimator.Dispose();
        _plcPolling.Dispose();
        _flask.Dispose();
    }

    private void InitializeRuntime()
    {
        AddLog("시스템 초기화 완료");
        AddLog($"AI 학습 스냅샷 기록: {_aiDataRecorder.OutputPath}");
        ApplyRolePermissions();

        _uiTimer.Interval = TimeSpan.FromSeconds(1);
        _uiTimer.Tick += (_, _) => UiTimerOnTick();
        _uiTimer.Start();
    }

    private void UiTimerOnTick()
    {
        try
        {
            UiTimerOnTickCore();
        }
        catch (Exception ex)
        {
            AddLog($"[오류] UI 갱신: {ex.Message}");
        }
    }

    private void UiTimerOnTickCore()
    {
        if (!_useSimulation)
        {
            PlcPollResult poll = _plcPolling.PollConnected();
            switch (poll.Kind)
            {
                case PlcPollKind.Snapshot:
                    if (_ethercatLinkLostLogged)
                    {
                        _ethercatLinkLostLogged = false;
                        AddLog("EtherCAT 통신 복구됨");
                    }

                    ApplyPlcSnapshot(poll.Snapshot);
                    break;
                case PlcPollKind.LinkLost:
                    OnEthercatLinkLost(poll.Error ?? "EtherCAT 읽기 실패");
                    break;
                case PlcPollKind.NeedReconnect:
                    _ = Task.Run(() =>
                    {
                        bool ok = _plcPolling.TryReconnect(AppSettings.AdsPort);
                        Dispatcher.BeginInvoke(() =>
                        {
                            if (ok)
                            {
                                _useSimulation = false;
                                _ethercatLinkLostLogged = false;
                                _loggedPlcRequiredOffline = false;
                                AddLog($"EtherCAT 재연결 성공 (ADS {AppSettings.AdsPort})");
                                SyncViewModel();
                            }
                            else
                            {
                                _plcPolling.ArmReconnectCooldown();
                            }
                        });
                    });
                    break;
            }
        }
        else
        {
            SimulateSensors();
        }

        RefreshInterlockDecision();
        AutoEvaluateState();
        EnforceLoadLockContactDuringTransfer();

        if (_state is EquipmentState.Running or EquipmentState.Warning)
        {
            _transferSim.Tick(etch_ui.Services.Scheduling.EquipmentCapacityConfig.Default.VacuumMotionStepsPerUiTick);
            if (_transferSim.LotCompleteAchieved)
            {
                HandleLotComplete();
            }
        }
        else if (_transferSim.IsRunning)
        {
            _transferSim.PauseTransfer();
        }

        PushOutputsToPlc();
        PushSparkHistory();
        SyncViewModel();

        int flaskEvery = _uiTimer.Interval.TotalMilliseconds <= 250 ? 10 : 2;
        int aiEvery = _uiTimer.Interval.TotalMilliseconds <= 250 ? 15 : 3;

        _flaskCounter++;
        if (_flaskCounter >= flaskEvery)
        {
            _flaskCounter = 0;
            _ = PublishFlaskAsync();
        }

        _aiPollCounter++;
        if (_aiPollCounter >= aiEvery)
        {
            _aiPollCounter = 0;
            _ = PollFlaskAiLatestAsync();
        }

        UpdateUiTimerInterval();
        LogStateTransitionIfNeeded();
    }

    private void UpdateUiTimerInterval()
    {
        bool fastTransfer = !_maintenanceMode
                            && _state is EquipmentState.Running or EquipmentState.Warning
                            && _transferSim.IsRunning;
        TimeSpan want = fastTransfer ? TimeSpan.FromMilliseconds(200) : TimeSpan.FromSeconds(1);
        if (_uiTimer.Interval != want)
        {
            _uiTimer.Interval = want;
        }
    }

    private void SyncTransferMotionFrame()
    {
        if (!ShouldShowVirtualTransfer)
        {
            return;
        }

        if (!_transferSim.IsRunning && !_transferSim.CanResume)
        {
            return;
        }

        _motionBridge.SyncTransferMotion(_transferSim, _vm.StateText);
    }

    private void HandleLotComplete()
    {
        if (_lotCompleteHandled)
        {
            return;
        }

        _lotCompleteHandled = true;
        ThroughputKpiSnapshot kpi = _transferSim.KpiSnapshot;
        _state = EquipmentState.Ready;
        string detail =
            $"LOT COMPLETE · {kpi.CompletedWafers}/{kpi.TargetWafers}매 · WPH~{kpi.EstimatedWph:F0} · {kpi.BottleneckHint}";
        AppendEvent( "READY", null, detail);
        AddLog(detail);
        SyncViewModel();
    }

    private void ClearSparklineHistory()
    {
        _vm.PressureSparkValues.Clear();
        _vm.VibrationSparkValues.Clear();
    }

    private void OnEthercatLinkLost(string err)
    {
        _plcPolling.Disconnect();
        _lastProcessSampleUtc = DateTime.MinValue;

        if (!_ethercatLinkLostLogged)
        {
            _ethercatLinkLostLogged = true;
            _plcPolling.ArmReconnectCooldown();
            if (_simulationFallbackEnabled)
            {
                _useSimulation = true;
                SeedSimulationValues();
                AppendEvent( "ALARM", "A001", $"EtherCAT 통신 끊김(시뮬 전환): {err}");
                AddLog($"EtherCAT 통신 끊김 — 시뮬 허용 ON: {err}");
            }
            else
            {
                _useSimulation = false;
                AppendEvent( "ALARM", "A001", $"EtherCAT 통신 끊김: {err}");
                AddLog($"EtherCAT 통신 끊김: {err}");
            }
        }
    }

    private void ApplyPlcSnapshot(PlcProcessSnapshot snap)
    {
        _loggedPlcRequiredOffline = false;
        _temp = Math.Round(snap.TemperatureC, 2);
        _humi = Math.Round(snap.HumidityPercent, 2);
        _pressureMtorr = snap.PressureMtorr;
        _pressureRaw = snap.PressureRaw;
        _pressureSignalValid = snap.PressureSignalValid;
        _vib = Math.Round(snap.VibrationG, 2);
        _accessInputValid = snap.AccessInputValid;
        _accessSafe = snap.AccessSafe;
        _lastProcessSampleUtc = DateTime.UtcNow;
        ProcessHardwareButtons(snap.DigitalInputBits);
    }

    /// <summary>버튼으로 시뮬 켤 때 등, 데모 시작값.</summary>
    /// <summary>시뮬 허용 ON 시 EtherCAT 미연결이면 데모 센서·가상 이송 모드로 전환.</summary>
    private void EnsureBenchSimulationWhenOffline()
    {
        if (_plcPolling.Plc.TryReadSnapshot(out PlcProcessSnapshot snap))
        {
            _useSimulation = false;
            ApplyPlcSnapshot(snap);
            return;
        }

        if (_plcPolling.TryConnect(AppSettings.AdsPort) && _plcPolling.Plc.TryReadSnapshot(out snap))
        {
            _useSimulation = false;
            ApplyPlcSnapshot(snap);
            return;
        }

        _useSimulation = true;
        SeedSimulationValues();
        SimulateSensors();
        ClearSparklineHistory();
        AddLog("EtherCAT 미연결 — 데모 센서 모드(Start로 가상 이송 확인).");
    }

    private void SeedSimulationValues()
    {
        _temp = 24.0;
        _humi = 45.0;
        _pressureMtorr = PlcAnalogScaling.DefaultSimulationPressureMtorr();
        _pressureRaw = 0;
        _pressureSignalValid = true;
        _vib = 0.10;
        _accessSafe = true;
        _accessInputValid = true;
        _lastProcessSampleUtc = DateTime.UtcNow;
    }

    /// <summary>시뮬 OFF·미연결 시 EtherCAT 라벨이 남지 않도록 실측 타임스탬프·유효 플래그 제거.</summary>
    private void ClearOperationalSampleCache()
    {
        _lastProcessSampleUtc = DateTime.MinValue;
        _accessInputValid = false;
        _pressureSignalValid = false;
    }

    private void SimulateSensors()
    {
        if (IsBenchMode && _demoWarningTicksLeft > 0)
        {
            _demoWarningTicksLeft--;
            _temp = Math.Round(AppSettings.TempCMax + 1.5, 2);
            _humi = Math.Round(AppSettings.HumiMax + 3.0, 2);
        }
        else
        {
            _temp = Math.Round(_temp + (_rand.NextDouble() - 0.5) * 0.35, 2);
            _humi = Math.Round(_humi + (_rand.NextDouble() - 0.5) * 0.7, 2);
            _pressureMtorr = Math.Round(
                _pressureMtorr + (_rand.NextDouble() - 0.5) * 2.0,
                AppSettings.PressureDecimals);
            _vib = Math.Round(Math.Max(0, _vib + (_rand.NextDouble() - 0.5) * 0.06), 2);

            if (IsBenchMode
                && (_state == EquipmentState.Running || _state == EquipmentState.Warning)
                && _transferSim.IsActive
                && _rand.NextDouble() < DemoWarningChancePerTick)
            {
                _demoWarningTicksLeft = DemoWarningDurationTicks;
                _temp = Math.Round(AppSettings.TempCMax + 1.5, 2);
                _humi = Math.Round(AppSettings.HumiMax + 3.0, 2);
                AddLog("[데모] 환경 편향 시뮬 — WARNING (약 8초)");
            }
        }

        if (_rand.NextDouble() < 0.02 && !IsBenchMode)
        {
            _accessSafe = !_accessSafe;
            AddLog(_accessSafe ? "유도형 센서: 닫힘" : "유도형 센서: 열림");
        }

        _lastProcessSampleUtc = DateTime.UtcNow;
    }

    private bool EffectiveAccessSafe =>
        _maintenanceMode ? _maintVirtualLoadLockClosed : _accessSafe;

    private bool ProductionInterlockOk => _interlock.ProductionInterlockOk;

    private bool HasSensorAlarm => _interlock.HasSensorAlarm;

    private bool HasSensorWarning => _interlock.HasSensorWarning;

    private string? ComputePrimaryAlarmCode() => _interlock.PrimaryAlarmCode;

    /// <summary>시뮬 허용 ON이면 EtherCAT·센서 인터락 없이 가상 이송 Start 가능.</summary>
    private bool CanStartProcess() =>
        SessionContext.HasRole(UserRole.Worker)
        && !_maintenanceMode
        && (_simulationFallbackEnabled || ProductionInterlockOk);

    private InterlockSensorContext BuildInterlockContext() => new()
    {
        HasLiveSensorData = HasLiveSensorData,
        IsBenchMode = IsBenchMode,
        SimulationFallbackEnabled = _simulationFallbackEnabled,
        MaintenanceMode = _maintenanceMode,
        AccessInputValid = _accessInputValid,
        EffectiveAccessSafe = EffectiveAccessSafe,
        PressureMtorr = _pressureMtorr,
        PressureSignalValid = _pressureSignalValid,
        VibrationG = _vib,
        TempC = _temp,
        HumidityPct = _humi
    };

    private void RefreshInterlockDecision() =>
        _interlock = InterlockEvaluator.Evaluate(BuildInterlockContext());

    /// <summary>Load Lock 접촉 열림 시 RUNNING·가상 이송 즉시 중단 (Phase 1.2). 데모 모드는 실접촉 미사용.</summary>
    private void EnforceLoadLockContactDuringTransfer()
    {
        if (IsBenchMode || _maintenanceMode)
        {
            return;
        }

        if (!_accessInputValid || EffectiveAccessSafe)
        {
            if (EffectiveAccessSafe)
            {
                _loadLockOpenWhileRunningLogged = false;
            }

            return;
        }

        bool wasTransferring = _transferSim.IsRunning
                               || _transferSim.IsActive
                               || _state is EquipmentState.Running or EquipmentState.Warning;
        if (!wasTransferring)
        {
            return;
        }

        if (_transferSim.IsRunning)
        {
            _transferSim.PauseTransfer();
        }

        if (_state is EquipmentState.Running or EquipmentState.Warning)
        {
            _state = EquipmentState.Alarm;
        }

        if (!_loadLockOpenWhileRunningLogged)
        {
            _loadLockOpenWhileRunningLogged = true;
            AddLog("Load Lock 접촉 열림 — 가상 이송 즉시 정지 (A004)");
            AppendEvent( "ALARM", "A004", "Load Lock 접촉 열림 — RUNNING 중 가상 이송 정지");
        }
    }

    private void AutoEvaluateState()
    {
        if (_maintenanceMode)
        {
            _state = EquipmentState.Maintenance;
            return;
        }

        if (IsBenchMode || _simulationFallbackEnabled)
        {
            if (_state == EquipmentState.Alarm && IsBenchMode)
            {
                _state = EquipmentState.Ready;
                return;
            }

            if (_state == EquipmentState.Alarm)
            {
                return;
            }

            if (_state is EquipmentState.Running or EquipmentState.Warning)
            {
                if (_demoWarningTicksLeft > 0)
                {
                    _state = EquipmentState.Warning;
                }
                else if (_state == EquipmentState.Warning)
                {
                    _state = EquipmentState.Running;
                }

                return;
            }

            if (_state is EquipmentState.Idle or EquipmentState.Warning)
            {
                _state = EquipmentState.Ready;
            }

            return;
        }

        bool severe = !_interlock.PlcLinkOk
            || (_accessInputValid && !EffectiveAccessSafe)
            || HasSensorAlarm;
        if (severe)
        {
            _state = EquipmentState.Alarm;
            return;
        }

        if (_state == EquipmentState.Alarm)
        {
            return;
        }

        if (HasSensorWarning)
        {
            if (_state == EquipmentState.Running)
            {
                _state = EquipmentState.Warning;
            }
            else if (_state is EquipmentState.Idle or EquipmentState.Ready)
            {
                _state = EquipmentState.Ready;
            }

            return;
        }

        if (_state is EquipmentState.Idle or EquipmentState.Warning)
        {
            _state = EquipmentState.Ready;
        }
    }

    private bool TransferMotionActive =>
        _state is EquipmentState.Running or EquipmentState.Warning;

    /// <summary>일시정지·알람·LOT 진행 중에도 도식·웨이퍼 잔량 유지.</summary>
    private bool ShouldShowVirtualTransfer =>
        !_maintenanceMode
        && (TransferMotionActive
            || _transferSim.CanResume
            || _transferSim.LotCompletedCount > 0
            || (_state == EquipmentState.Alarm && _transferSim.HasVisibleLineState()));

    private void PushOutputsToPlc()
    {
        if (_useSimulation || !_plcPolling.IsConnected)
        {
            return;
        }

        ushort bits = _state switch
        {
            EquipmentState.Ready => 1 << 0,
            EquipmentState.Running => 1 << 1,
            EquipmentState.Warning or EquipmentState.Maintenance => 1 << 2,
            EquipmentState.Alarm => 1 << 3,
            _ => 0
        };

        _plcPolling.Plc.WriteDigitalOutputLamps(bits);
    }

    private void ProcessHardwareButtons(ushort bits)
    {
        bool b1 = (bits & 1) != 0;
        bool b2 = (bits & (1 << 1)) != 0;
        bool b3 = (bits & (1 << 2)) != 0;
        bool b4 = (bits & (1 << 3)) != 0;

        if (!_hwInit)
        {
            _hwPrev[0] = b1;
            _hwPrev[1] = b2;
            _hwPrev[2] = b3;
            _hwPrev[3] = b4;
            _hwInit = true;
            return;
        }

        if (b1 && !_hwPrev[0])
        {
            RequestStart("HW Start");
        }

        if (b2 && !_hwPrev[1])
        {
            RequestStop("HW Stop");
        }

        if (b3 && !_hwPrev[2])
        {
            RequestReset("HW Reset");
        }

        if (b4 && !_hwPrev[3])
        {
            RequestMaintenanceToggle("HW Maint");
        }

        _hwPrev[0] = b1;
        _hwPrev[1] = b2;
        _hwPrev[2] = b3;
        _hwPrev[3] = b4;
    }

    private void SyncViewModel()
    {
        _vm.CurrentUserText = SessionContext.CurrentUser is null
            ? "-"
            : $"{SessionContext.CurrentUser.Username} ({SessionContext.CurrentUser.Role.ToDisplayKorean()})";
        _vm.ShowUserManage = SessionContext.HasRole(UserRole.Admin);
        SyncAdminMenuVisibility();
        _vm.SimAllowButtonText = _simulationFallbackEnabled ? "시뮬 허용: 켬" : "시뮬 허용: 끔";
        _vm.MaintenanceModeActive = _maintenanceMode;
        _vm.MaintButtonText = _maintenanceMode ? "✓  유지보수 해제" : "⚙  유지보수";
        _vm.MaintenanceBannerVisible = _maintenanceMode;
        _vm.MaintenanceBannerText = _maintenanceMode
            ? "유지보수 모드 — 공정 Start 차단 · 인터락 미적용(센서 모니터링만)"
            : string.Empty;

        if (_state == EquipmentState.Alarm)
        {
            _vm.SafetyBannerVisible = true;
            string? codeEarly = ComputePrimaryAlarmCode();
            _vm.SafetyBannerText = AlarmCatalog.FormatBanner(codeEarly);
            _vm.SafetyBannerBrush = Brushes.OrangeRed;
            _vm.InterlockPanelCritical = true;
        }
        else if (_state == EquipmentState.Warning)
        {
            _vm.SafetyBannerVisible = true;
            _vm.SafetyBannerText = "⚠ WARNING — 환경(온·습도) 편향 · 공정 모니터링 강화";
            _vm.SafetyBannerBrush = Brushes.DarkGoldenrod;
            _vm.InterlockPanelCritical = false;
        }
        else
        {
            _vm.SafetyBannerVisible = false;
            _vm.SafetyBannerText = string.Empty;
            _vm.InterlockPanelCritical = false;
        }

        HmiConnectionPresenter.StatusPresentation plc = HmiConnectionPresenter.DescribePlc(
            IsBenchMode,
            HasLiveSensorData,
            _plcPolling.IsConnected,
            _simulationFallbackEnabled);
        _vm.PlcStatusText = plc.Text;
        _vm.PlcStatusBrush = plc.Brush;

        HmiFlaskStatusPresenter.FlaskPresentation flask = HmiFlaskStatusPresenter.Describe(
            _flaskProbeDone,
            _flaskReachable,
            _lastFlaskSuccessUtc,
            _flaskGateway.BaseUrl);
        _vm.FlaskStatusText = flask.Text;
        _vm.FlaskStatusBrush = flask.Brush;
        _vm.FlaskStatusHint = flask.Hint;

        _vm.LastUpdateText = DateTime.Now.ToString("HH:mm:ss");

        HmiConnectionPresenter.StatusPresentation data = HmiConnectionPresenter.DescribeDataQuality(
            HasLiveSensorData,
            IsBenchMode,
            _lastProcessSampleUtc);
        _vm.DataQualityText = data.Text;
        _vm.DataQualityBrush = data.Brush;

        _vm.StateText = _state.ToString().ToUpperInvariant();
        _vm.StateBrush = _state switch
        {
            EquipmentState.Running => Brushes.LimeGreen,
            EquipmentState.Warning => Brushes.Goldenrod,
            EquipmentState.Alarm => Brushes.OrangeRed,
            EquipmentState.Maintenance => Brushes.MediumPurple,
            _ => Brushes.DodgerBlue,
        };

        string? code = _state == EquipmentState.Alarm ? ComputePrimaryAlarmCode() : null;
        _vm.AlarmCodeText = code ?? "-";
        _vm.AlarmCodeBrush = code is null ? Brushes.DimGray : Brushes.OrangeRed;

        if (AlarmCatalog.TryGet(code).HasValue)
        {
            _vm.AlarmDetailText = AlarmCatalog.FormatDetailWithAction(code);
            _vm.AlarmDetailBrush = Brushes.DarkRed;
        }
        else if (_state == EquipmentState.Warning)
        {
            _vm.AlarmDetailText = "환경(온·습도)이 편향되었습니다. 공정 유지 시 모니터링을 강화하세요.";
            _vm.AlarmDetailBrush = Brushes.DarkGoldenrod;
        }
        else
        {
            _vm.AlarmDetailText =
                $"정상 대역: 압력 {AppSettings.PressureMtorrMin:F1}–{AppSettings.PressureMtorrMax:F1} mTorr, " +
                $"진동 ≤ {AppSettings.VibrationGMax:F2} g, 온도 {AppSettings.TempCMin:F1}–{AppSettings.TempCMax:F1} ℃, " +
                $"습도 {AppSettings.HumiMin:F1}–{AppSettings.HumiMax:F1} %";
            _vm.AlarmDetailBrush = Brushes.DimGray;
        }

        bool showSensors = HasLiveSensorData || IsBenchMode;
        _vm.TemperatureText = showSensors ? _temp.ToString("F2") : "—";
        _vm.HumidityText = showSensors ? _humi.ToString("F2") : "—";
        _vm.VibrationText = showSensors ? _vib.ToString("F2") : "—";
        if (!showSensors)
        {
            _vm.AccessText = "—";
            _vm.AccessBrush = Brushes.DimGray;
        }
        else if (IsBenchMode)
        {
            _vm.AccessText = _accessSafe ? "닫힘(데모)" : "열림(데모)";
            _vm.AccessBrush = _accessSafe ? Brushes.Goldenrod : Brushes.OrangeRed;
        }
        else if (!_accessInputValid)
        {
            _vm.AccessText = "—";
            _vm.AccessBrush = Brushes.DimGray;
        }
        else
        {
            _vm.AccessText = _accessSafe ? "닫힘" : "열림";
            _vm.AccessBrush = _accessSafe ? Brushes.ForestGreen : Brushes.OrangeRed;
        }

        if (!showSensors || (!IsBenchMode && !_pressureSignalValid))
        {
            _vm.PressureText = "—";
        }
        else
        {
            string fmt = "F" + AppSettings.PressureDecimals;
            _vm.PressureText = _pressureMtorr.ToString(fmt);
        }

        RefreshInterlockDecision();
        InterlockPanelPresenter.Apply(_vm, BuildInterlockContext(), _interlock, EffectiveAccessSafe);
        _vm.CanStart = CanStartProcess();
        _vm.StartButtonToolTip = BuildStartButtonToolTip();

        _vm.LampReadyOn = _state == EquipmentState.Ready;
        _vm.LampRunOn = _state == EquipmentState.Running;
        _vm.LampWarnOn = _state is EquipmentState.Warning or EquipmentState.Maintenance;
        _vm.LampAlarmOn = _state == EquipmentState.Alarm;

        _vm.CanStop = SessionContext.HasRole(UserRole.Worker);
        _vm.CanReset = SessionContext.HasRole(UserRole.Admin);
        _vm.CanProcessReset = SessionContext.HasRole(UserRole.Admin)
            && !_maintenanceMode
            && !_transferSim.IsRunning
            && _state != EquipmentState.Running;
        _vm.CanMaint = SessionContext.HasRole(UserRole.Admin);

        _vm.PressureSparkYMax = AppSettings.PressureMtorrAtRawMax;
        _vm.VibrationSparkYMax = AppSettings.VibrationGMax * 1.5;

        _vm.SensorPressureValue = _pressureMtorr;
        _vm.SensorVibrationValue = _vib;
        _vm.SensorTempValue = _temp;
        _vm.SensorHumiValue = _humi;

        ApplyProcessStepDisplay();

        _vm.ActiveRecipeText = _state == EquipmentState.Running
            ? $"▶ {ProcessRecipeRuntime.Active.SummaryText}"
            : $"대기 · {ProcessRecipeRuntime.Active.Name} ({ProcessRecipeRuntime.Active.EtchPmIds.Count} PM)";

        IReadOnlyList<Equipment.Models.ModuleStateSnapshot> moduleSnapshots = BuildModuleSnapshots();
        _motionBridge.Sync(
            EffectiveAccessSafe,
            _maintenanceMode || _accessInputValid,
            _vm.StateText,
            _vm.LampReadyOn,
            _vm.LampRunOn,
            _vm.LampWarnOn,
            _vm.LampAlarmOn,
            ShouldShowVirtualTransfer ? _transferSim : null,
            moduleSnapshots);

        _vm.SetModuleSnapshots(moduleSnapshots);
        MaybeRecordAiTrainingSnapshot(moduleSnapshots);
        SyncAiInsights();
        SyncAiEngineChip();
        EmbeddedProcessPanel.SetMaintToolsCompactVisible(_vm.MaintenanceBannerVisible);
    }

    private void SyncAiInsights()
    {
        bool showSensors = HasLiveSensorData || IsBenchMode;
        _vm.ReplaceAiInsights(AiInsightComposer.Compose(
            _lastAiDiagnosis,
            _flaskReachable,
            showSensors,
            _pressureMtorr,
            _vib,
            _temp,
            _humi,
            _accessSafe,
            _accessInputValid,
            ShouldShowVirtualTransfer ? _transferSim : null));
    }

    private void WireProcessPanel(ProcessControlPanelControl panel)
    {
        panel.BtnStart.Click += BtnStart_Click;
        panel.BtnStop.Click += BtnStop_Click;
        panel.BtnReset.Click += BtnReset_Click;
        panel.BtnProcessReset.Click += BtnProcessReset_Click;
        panel.BtnMaint.Click += BtnMaint_Click;
        panel.BtnMaintTools.Click += BtnMaintTools_Click;
        panel.BtnMaintToolsCompact.Click += BtnMaintTools_Click;
        panel.BtnStartCompact.Click += BtnStart_Click;
        panel.BtnStopCompact.Click += BtnStop_Click;
        panel.BtnResetCompact.Click += BtnReset_Click;
        panel.BtnProcessResetCompact.Click += BtnProcessReset_Click;
        panel.BtnMaintCompact.Click += BtnMaint_Click;
    }

    private void EmbeddedInterlock_OpenDetailRequested(object sender, RoutedEventArgs e) =>
        BtnPopoutInterlock_Click(sender, e);

    private void BtnPopoutEquipment_Click(object sender, RoutedEventArgs e)
    {
        if (_popoutEquipment?.IsVisible == true)
        {
            _popoutEquipment.Activate();
            return;
        }

        var schematic = new EquipmentSchematicControl { DataContext = _vm.Equipment };
        _popoutEquipment = new HmiPopoutWindow(
            HmiPopoutKind.Equipment,
            "클러스터 도식",
            "가상 TM 전체 화면 — TM·스케줄 진단은 「TM·스케줄」 분리 창",
            schematic,
            1100,
            780)
        {
            Owner = this
        };
        _popoutEquipment.Closed += (_, _) => _popoutEquipment = null;
        _popoutEquipment.Show();
    }

    private void BtnPopoutDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        if (_popoutDiagnostics?.IsVisible == true)
        {
            _popoutDiagnostics.Activate();
            return;
        }

        var panel = new EquipmentDiagnosticsControl { DataContext = _vm.Equipment };
        _popoutDiagnostics = new HmiPopoutWindow(
            HmiPopoutKind.Diagnostics,
            "TM · 스케줄 · 듀얼블레이드",
            "로봇 상태 · 파이프라인 · HOLD · 웨이퍼 타임라인",
            panel,
            760,
            680)
        {
            Owner = this
        };
        _popoutDiagnostics.Closed += (_, _) => _popoutDiagnostics = null;
        _popoutDiagnostics.Show();
    }

    private void BtnPopoutSensors_Click(object sender, RoutedEventArgs e)
    {
        if (_popoutSensors?.IsVisible == true)
        {
            _popoutSensors.Activate();
            return;
        }

        var panel = new SensorMetricsControl { DataContext = _vm };
        _popoutSensors = new HmiPopoutWindow(
            HmiPopoutKind.Sensors,
            "센서 · 추세 · 상태",
            "실측/데모 센서 · 스파크라인 · 정상 대역",
            panel,
            580,
            640)
        {
            Owner = this
        };
        _popoutSensors.Closed += (_, _) => _popoutSensors = null;
        _popoutSensors.Show();
    }

    private void BtnPopoutControl_Click(object sender, RoutedEventArgs e)
    {
        if (_popoutControl?.IsVisible == true)
        {
            _popoutControl.Activate();
            return;
        }

        var panel = new ProcessControlPanelControl { DataContext = _vm, CompactMode = false };
        WireProcessPanel(panel);
        _popoutControl = new HmiPopoutWindow(
            HmiPopoutKind.Control,
            "제어 · 상태 램프",
            "패널 램프 · 시작 · 정지 · 알람 리셋 · 정비",
            panel,
            400,
            560)
        {
            Owner = this
        };
        _popoutControl.Closed += (_, _) => _popoutControl = null;
        _popoutControl.Show();
    }

    private void BtnPopoutInterlock_Click(object sender, RoutedEventArgs e)
    {
        if (_popoutInterlock?.IsVisible == true)
        {
            _popoutInterlock.Activate();
            return;
        }

        var panel = new InterlockAiMonitorControl { DataContext = _vm };
        _popoutInterlock = new HmiPopoutWindow(
            HmiPopoutKind.Interlock,
            "인터락 · AI 모니터",
            "인터락 판정 · AI 세부 근거 · 모듈 상태",
            panel,
            540,
            760)
        {
            Owner = this
        };
        _popoutInterlock.Closed += (_, _) => _popoutInterlock = null;
        _popoutInterlock.Show();
    }

    private void MaybeRecordAiTrainingSnapshot(IReadOnlyList<Equipment.Models.ModuleStateSnapshot> moduleSnapshots)
    {
        DateTime nowUtc = DateTime.UtcNow;
        if (nowUtc < _nextAiSnapshotUtc)
        {
            return;
        }

        _nextAiSnapshotUtc = nowUtc.AddSeconds(1);
        _aiDataRecorder.Append(new AiTrainingDataRecorder.SnapshotInput
        {
            EquipmentState = _state.ToString().ToUpperInvariant(),
            AlarmCode = _state == EquipmentState.Alarm ? ComputePrimaryAlarmCode() : null,
            InterlockOk = ProductionInterlockOk,
            BenchMode = IsBenchMode,
            Temperature = _temp,
            Humidity = _humi,
            Pressure = _pressureMtorr,
            Vibration = _vib,
            AccessSafe = EffectiveAccessSafe,
            Modules = moduleSnapshots
        });
    }

    private IReadOnlyList<Equipment.Models.ModuleStateSnapshot> BuildModuleSnapshots() =>
        ModuleStateAggregator.Build(new ModuleStateAggregator.Context
        {
            EquipmentState = _state.ToString().ToUpperInvariant(),
            MaintenanceMode = _maintenanceMode,
            HasLiveSensorData = HasLiveSensorData,
            InterlockOk = ProductionInterlockOk,
            BenchMode = IsBenchMode,
            AccessSafe = EffectiveAccessSafe,
            AccessInputValid = _accessInputValid,
            AlarmCode = _state == EquipmentState.Alarm ? ComputePrimaryAlarmCode() : null,
            Transfer = ShouldShowVirtualTransfer ? _transferSim : null
        });

    private void PushSparkHistory()
    {
        if (_pressureSignalValid || _useSimulation)
        {
            AppendSpark(_vm.PressureSparkValues, _pressureMtorr);
        }

        AppendSpark(_vm.VibrationSparkValues, _vib);
    }

    private static void AppendSpark(System.Collections.ObjectModel.ObservableCollection<double> series, double value)
    {
        series.Add(value);
        while (series.Count > SparkHistoryMax)
        {
            series.RemoveAt(0);
        }
    }

    private void ApplyProcessStepDisplay()
    {
        ProcessStepMapper.StepState step = _state == EquipmentState.Running
            ? ProcessStepMapper.FromSimPhase(_transferSim.Phase, _transferSim.PhaseHint)
            : ProcessStepMapper.FromEquipmentState(_state.ToString().ToUpperInvariant(), _maintenanceMode);

        _vm.ProcessStepIndex = step.Index;
        _vm.ProcessStepWarning = step.Warning;
        _vm.ProcessStepCaption = step.ActiveCaption;
        _vm.ProcessStepDetailText = step.Detail;
    }

    private string BuildStartButtonToolTip()
    {
        if (!SessionContext.HasRole(UserRole.Worker))
        {
            return "작업자 권한이 필요합니다.";
        }

        if (_maintenanceMode)
        {
            return "유지보수 모드에서는 시작할 수 없습니다.";
        }

        if (_simulationFallbackEnabled)
        {
            return IsBenchMode
                ? "데모 모드: TwinCAT·인터락 없이 가상 TM 이송을 시작합니다."
                : "시뮬 허용 ON: 인터락과 무관하게 가상 이송을 시작합니다 (센서는 EtherCAT 우선).";
        }

        if (!ProductionInterlockOk)
        {
            return "인터락 조건을 모두 만족해야 시작할 수 있습니다.";
        }

        return "공정을 시작합니다.";
    }

    private void BtnOpenFlask_Click(object sender, RoutedEventArgs e)
    {
        string url = AppSettings.FlaskBaseUrl;
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            AddLog($"Flask 대시보드 열기: {url}");
        }
        catch (Exception ex)
        {
            AddLog($"브라우저 실행 실패: {ex.Message}");
        }
    }

    private static string ToMark(bool ok) => ok ? "✓" : "✗";

    private void AddLog(string message)
    {
        _vm.PrependLog($"{DateTime.Now:HH:mm:ss} | {message}");
    }

    private string ResolveFlaskDataSource() =>
        HasLiveSensorData ? "live" : IsBenchMode ? "demo" : "offline";

    private void AppendEvent(string? state, string? code, string message)
    {
        string user = CurrentUserName() ?? "?";
        _db.AppendEventLog(user, state, code, message);
        string dataSource = ResolveFlaskDataSource();
        if (dataSource == "offline")
        {
            return;
        }

        var item = new FlaskEventItem
        {
            Time = DateTime.UtcNow.ToString("o"),
            Kind = "hmi_event",
            Message = message,
            EquipmentState = state ?? _state.ToString().ToUpperInvariant(),
            AlarmCode = code,
            Username = user
        };
        _ = ForwardFlaskEventAsync(item, dataSource);
    }

    private async Task ForwardFlaskEventAsync(FlaskEventItem item, string dataSource)
    {
        bool ok = await _flaskGateway.PublishEventAsync([item], dataSource).ConfigureAwait(false);
        if (!ok && DateTime.UtcNow >= _nextFlaskEventFailLogUtc)
        {
            _nextFlaskEventFailLogUtc = DateTime.UtcNow.AddSeconds(30);
            _ = Dispatcher.BeginInvoke(() =>
                AddLog("Flask 이벤트 전달 실패 (로컬 DB에는 저장됨)"));
        }
    }

    private void BtnMaintTools_Click(object sender, RoutedEventArgs e)
    {
        if (!_maintenanceMode)
        {
            return;
        }

        var dialog = new MaintenanceToolsWindow(
            _transferSim,
            IsBenchMode,
            _maintVirtualLoadLockClosed,
            closed =>
            {
                _maintVirtualLoadLockClosed = closed;
                SyncViewModel();
            },
            onLog: AddLog,
            onStateChanged: SyncViewModel)
        {
            Owner = this
        };
        dialog.ShowDialog();
    }

    private void BtnStart_Click(object sender, RoutedEventArgs e) => RequestStart("UI Start");

    private void BtnStop_Click(object sender, RoutedEventArgs e) => RequestStop("UI Stop");

    private void BtnReset_Click(object sender, RoutedEventArgs e) => RequestReset("UI Reset");

    private void BtnMaint_Click(object sender, RoutedEventArgs e) => RequestMaintenanceToggle("UI Maint");

    private void RequestStart(string source)
    {
        if (!SessionContext.HasRole(UserRole.Worker))
        {
            AddLog("권한 부족: Start 불가");
            return;
        }

        if (_maintenanceMode)
        {
            AddLog("MAINTENANCE 모드에서는 Start 불가");
            return;
        }

        if (!CanStartProcess())
        {
            _state = EquipmentState.Alarm;
            string ac = ComputePrimaryAlarmCode() ?? "A004";
            AppendEvent( "ALARM", ac, $"{source}: 시작 조건 불만족");
            AddLog($"{source}: 시작 불가 (인터락 또는 권한)");
            SyncViewModel();
            return;
        }

        ProcessRecipeRuntime.ReloadFromAppSettings();
        ProcessRecipeDefinition recipe = ProcessRecipeRuntime.Active;
        _state = EquipmentState.Running;
        _lotCompleteHandled = false;

        if (_transferSim.CanResume)
        {
            _transferSim.ResumeTransfer();
            AppendEvent( "RUNNING", null, $"{source}: 운전 재개 · {recipe.SummaryText}");
            AddLog($"{source}: RUNNING · 재개 · {recipe.Name}");
        }
        else
        {
            EquipmentCapacityConfig capacity = AppSettings.CreateCapacityConfig();
            _transferSim.StartDemoLoop(capacity);
            AppendEvent( "RUNNING", null, $"{source}: 운전 시작 · {recipe.SummaryText}");
            AddLog($"{source}: RUNNING · 새 LOT · {recipe.Name} · {recipe.SummaryText}");
        }

        SyncViewModel();
    }

    private void RequestStop(string source)
    {
        if (!SessionContext.HasRole(UserRole.Worker))
        {
            AddLog("권한 부족: 일시정지 불가");
            return;
        }

        _transferSim.PauseTransfer();
        _demoWarningTicksLeft = 0;
        _state = CanStartProcess() ? EquipmentState.Ready : EquipmentState.Idle;
        AppendEvent( _state.ToString().ToUpperInvariant(), null, $"{source}: 일시정지");
        AddLog($"{source}: 일시정지 · 가상 이송 상태 유지 (시작으로 재개)");
        SyncViewModel();
    }

    private void RequestReset(string source)
    {
        if (!SessionContext.HasRole(UserRole.Admin))
        {
            AddLog("권한 부족: Alarm Reset 불가");
            return;
        }

        if (_maintenanceMode)
        {
            AddLog($"{source}: 유지보수 모드 — 알람 리셋은 해제 후 수행");
            return;
        }

        if (_state == EquipmentState.Alarm && ProductionInterlockOk)
        {
            _state = EquipmentState.Ready;
            AppendEvent( "READY", null, $"{source}: Alarm Reset 완료");
            AddLog($"{source}: Alarm Reset 완료");
        }
        else if (_state == EquipmentState.Alarm)
        {
            AddLog($"{source}: Alarm Reset 실패 — 인터락 미충족");
        }
        else
        {
            AddLog($"{source}: 알람 리셋 — 현재 알람 상태 아님");
        }

        SyncViewModel();
    }

    private void BtnProcessReset_Click(object sender, RoutedEventArgs e) => RequestProcessReset("UI Process Reset");

    private void RequestProcessReset(string source)
    {
        if (!SessionContext.HasRole(UserRole.Admin))
        {
            AddLog("권한 부족: 공정 리셋 불가");
            return;
        }

        if (_maintenanceMode)
        {
            AddLog($"{source}: 유지보수 모드 — 공정 리셋은 해제 후 수행");
            return;
        }

        if (_transferSim.IsRunning || _state == EquipmentState.Running)
        {
            AddLog($"{source}: 공정 리셋 — 일시정지 후 수행");
            return;
        }

        if (MessageBox.Show(
                "FOUP·슬롯·LOT·로봇 큐를 데모 초기 상태로 되돌립니다.\n계속하시겠습니까?",
                "공정 리셋",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        _lotCompleteHandled = false;
        _demoWarningTicksLeft = 0;
        _transferSim.ResetDemoLine();
        _state = CanStartProcess() ? EquipmentState.Ready : EquipmentState.Idle;
        AppendEvent(_state.ToString().ToUpperInvariant(), null, $"{source}: 공정 리셋 완료");
        AddLog($"{source}: 공정 리셋 · FOUP·LOT·큐 초기화");
        SyncViewModel();
    }

    private void RequestMaintenanceToggle(string source)
    {
        if (!SessionContext.HasRole(UserRole.Admin))
        {
            AddLog("권한 부족: Maintenance 불가");
            return;
        }

        if (_maintenanceMode)
        {
            ExitMaintenanceMode(source);
        }
        else
        {
            EnterMaintenanceMode(source);
        }

        SyncViewModel();
    }

    private void EnterMaintenanceMode(string source)
    {
        bool stoppedTransfer = _transferSim.IsRunning || _transferSim.IsActive || _state == EquipmentState.Running;
        if (stoppedTransfer)
        {
            _transferSim.PauseTransfer();
            AddLog($"{source}: 가상 이송 일시정지");
        }

        _maintenanceMode = true;
        _state = EquipmentState.Maintenance;
        _loadLockOpenWhileRunningLogged = false;

        string detail = stoppedTransfer
            ? $"{source}: 유지보수 진입 (운전 정지 포함)"
            : $"{source}: 유지보수 진입";
        AppendEvent( "MAINTENANCE", null, detail);
        AddLog($"{source}: 유지보수 모드 — Start·인터락 차단, 센서는 모니터링");
    }

    private void ExitMaintenanceMode(string source)
    {
        _maintenanceMode = false;
        AutoEvaluateState();
        AppendEvent( _state.ToString().ToUpperInvariant(), null, $"{source}: 유지보수 해제");
        AddLog($"{source}: 일반 모드 — 상태 {_state}");
    }

    private static ProcessRecipeTelemetry BuildRecipeTelemetry()
    {
        ProcessRecipeDefinition r = ProcessRecipeRuntime.Active;
        return new ProcessRecipeTelemetry
        {
            Id = r.Id,
            Name = r.Name,
            Version = r.Version,
            EtchPmSequence = ProcessRecipePmMapping.FormatSequence(r.EtchPmIds),
            EtchProcessTicks = r.EtchProcessTicks,
            StripProcessTicks = r.StripProcessTicks,
            AlignProcessTicks = r.AlignProcessTicks
        };
    }

    private async Task PublishFlaskAsync()
    {
        try
        {
            bool live = HasLiveSensorData;
            string dataSource = live ? "live" : IsBenchMode ? "demo" : "offline";
            IReadOnlyList<Equipment.Models.ModuleStateSnapshot> moduleSnapshots = BuildModuleSnapshots();
            var payload = HmiTelemetryPayloadFactory.Create(
                dataSource,
                live,
                IsBenchMode,
                _maintenanceMode,
                live,
                _state.ToString().ToUpperInvariant(),
                _state == EquipmentState.Alarm ? ComputePrimaryAlarmCode() : null,
                ProductionInterlockOk,
                CurrentUserName(),
                _temp,
                _humi,
                _pressureMtorr,
                _vib,
                EffectiveAccessSafe,
                HmiTelemetryPayloadFactory.FromModuleSnapshots(moduleSnapshots),
                BuildRecipeTelemetry());

            if (!EtchTelemetryContractValidator.TryValidate(payload, out string contractError))
            {
                _ = Dispatcher.BeginInvoke(() =>
                {
                    if (DateTime.UtcNow >= _nextFlaskFailLogUtc)
                    {
                        _nextFlaskFailLogUtc = DateTime.UtcNow.AddSeconds(25);
                        AddLog($"Flask payload 검증 실패 — {contractError}");
                    }
                });
                return;
            }

            bool ok = await _telemetryPublisher.PublishAsync(payload).ConfigureAwait(false);
            _ = Dispatcher.BeginInvoke(() =>
            {
                _flaskReachable = ok;
                if (ok)
                {
                    _lastFlaskSuccessUtc = DateTime.UtcNow;
                }
                if (!ok && DateTime.UtcNow >= _nextFlaskFailLogUtc)
                {
                    _nextFlaskFailLogUtc = DateTime.UtcNow.AddSeconds(25);
                    AddLog($"Flask 전송 실패 — {_flaskGateway.BaseUrl} 서버·방화벽 확인 (로컬 telemetry_samples 저장 중)");
                }

                SyncViewModel();
            });
        }
        catch
        {
            // ignore
        }
    }

    private void LogStateTransitionIfNeeded()
    {
        EquipmentState previous = _lastLoggedState;
        if (_state == previous)
        {
            return;
        }

        _lastLoggedState = _state;
        AppendEvent( _state.ToString().ToUpperInvariant(), null, "상태 전이");

        string? ac = ComputePrimaryAlarmCode();
        if (_state == EquipmentState.Alarm && ac is not null)
        {
            if (ac != _lastAlarmCode)
            {
                if (_lastAlarmCode is not null)
                {
                    _db.TryResolveOpenAlarm(_lastAlarmCode, CurrentUserName(), "알람 코드 변경");
                }

                _lastAlarmCode = ac;
                _db.AppendAlarmHistory(ac, AlarmCatalog.FormatLine(ac));
                AppendEvent( "ALARM", ac, "알람 발생/갱신");
            }
        }
        else if (previous == EquipmentState.Alarm && _state != EquipmentState.Alarm)
        {
            if (_lastAlarmCode is not null)
            {
                _db.TryResolveOpenAlarm(_lastAlarmCode, CurrentUserName(), "알람 해제");
            }

            _lastAlarmCode = null;
        }
    }

    private void BtnDemoGuide_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new DemoGuideWindow { Owner = this };
        dialog.ShowDialog();
    }

    private void BtnDemoScenario_Click(object sender, RoutedEventArgs e)
    {
        if (_demoScenarioRunner.IsRunning)
        {
            _demoScenarioRunner.Stop();
            AddLog("데모 시나리오 중지");
            BtnDemoScenario.Content = "데모 진행";
            return;
        }

        BtnDemoScenario.Content = "데모 중지";
        _demoScenarioRunner.Completed += OnDemoScenarioCompleted;
        _demoScenarioRunner.Start(this);
    }

    private void OnDemoScenarioCompleted()
    {
        _demoScenarioRunner.Completed -= OnDemoScenarioCompleted;
        BtnDemoScenario.Content = "데모 진행";
    }

    private void BtnSimAllow_Click(object sender, RoutedEventArgs e)
    {
        _simulationFallbackEnabled = !_simulationFallbackEnabled;
        if (_simulationFallbackEnabled)
        {
            AddLog("시뮬 허용 ON — Start는 인터락 없이 가상 이송 가능. TwinCAT 없으면 데모 센서, 연결되면 실데이터 표시.");
            EnsureBenchSimulationWhenOffline();
        }
        else
        {
            AddLog("시뮬 허용 OFF — EtherCAT 실데이터만 사용합니다.");
            _useSimulation = false;
            ClearOperationalSampleCache();
            if (_plcPolling.TryConnect(AppSettings.AdsPort) && _plcPolling.Plc.TryReadSnapshot(out PlcProcessSnapshot snap))
            {
                ApplyPlcSnapshot(snap);
            }
            else
            {
                _loggedPlcRequiredOffline = true;
                AddLog(HmiConnectionPresenter.BenchModeHint(_simulationFallbackEnabled));
            }
        }

        RefreshInterlockDecision();
        AutoEvaluateState();
        PushOutputsToPlc();
        SyncViewModel();
    }

    private void SyncAdminMenuVisibility()
    {
        bool admin = SessionContext.HasRole(UserRole.Admin);
        Visibility vis = admin ? Visibility.Visible : Visibility.Collapsed;
        MenuItemSettings.Visibility = vis;
        MenuItemUserManage.Visibility = vis;
        MenuAdminSeparator.Visibility = vis;
    }

    private void BtnAppMenu_Click(object sender, RoutedEventArgs e)
    {
        if (BtnAppMenu.ContextMenu is not ContextMenu menu)
        {
            return;
        }

        menu.PlacementTarget = BtnAppMenu;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void BtnEventLog_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new EventLogWindow(_db) { Owner = this };
        dialog.ShowDialog();
    }

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        if (!SessionContext.HasRole(UserRole.Admin))
        {
            return;
        }

        var dialog = new InterlockSettingsWindow(_db, _flask, ResolveFlaskDataSource) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            _flaskGateway.BaseUrl = AppSettings.FlaskBaseUrl;
            _simulationFallbackEnabled = AppSettings.SimulationEnabled;
            if (_simulationFallbackEnabled)
            {
                EnsureBenchSimulationWhenOffline();
            }

            SyncViewModel();
            _vm.NotifyAppSettingsBindings();
            AddLog($"설정 반영 — 압력 {AppSettings.PressureMtorrMin:F0}–{AppSettings.PressureMtorrMax:F0} mTorr, " +
                $"레시피 Etch {AppSettings.EtchProcessTicks} / Strip {AppSettings.StripProcessTicks} (다음 Start)");
        }
    }

    private void BtnUserManage_Click(object sender, RoutedEventArgs e)
    {
        if (!SessionContext.HasRole(UserRole.Admin))
        {
            return;
        }

        var dialog = new UserManagementWindow(_db)
        {
            Owner = this
        };
        dialog.ShowDialog();
    }

    private void BtnAlarmHistory_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AlarmHistoryWindow(_db) { Owner = this };
        dialog.ShowDialog();
    }

    private void BtnChangePassword_Click(object sender, RoutedEventArgs e)
    {
        if (SessionContext.CurrentUser is null)
        {
            return;
        }

        var dialog = new PasswordChangeWindow(_db) { Owner = this };
        dialog.ShowDialog();
    }

    private async Task PollFlaskAiLatestAsync()
    {
        try
        {
            EtchAiDiagnosis? diag = await _flaskGateway.PollAiLatestAsync().ConfigureAwait(false);
            await Dispatcher.BeginInvoke(() =>
            {
                ApplyAiDiagnosis(diag);
            });
        }
        catch
        {
            // ignore
        }
    }

    private void ApplyAiDiagnosis(EtchAiDiagnosis? diag)
    {
        if (diag is null || !diag.Success)
        {
            _vm.AiScoreText = "—";
            _vm.AiHintText = _flaskReachable
                ? "AI 진단 대기 (sensor-data 수신 후 갱신)"
                : "Flask 미연결 — AI 조언 없음";
            _vm.AiScoreBrush = Brushes.DimGray;
            _vm.AiPredictedAlarmText = "예상 알람: —";
            _lastAiDiagnosis = null;
            SyncAiInsights();
            SyncAiEngineChip();
            return;
        }

        _lastAiDiagnosis = diag;
        _lastAiScore = diag.AnomalyScore;
        _lastAiHint = diag.SuggestedAction ?? diag.Note ?? "—";
        _vm.AiScoreText = $"이상 점수: {diag.AnomalyScore:F2}" + (diag.Stub ? " (규칙 스텁)" : " (ML)");
        _vm.AiHintText = _lastAiHint;
        _vm.AiPredictedAlarmText = HmiAiAlarmText.FormatPredictedLine(diag);
        _vm.AiScoreBrush = diag.AnomalyScore switch
        {
            >= 0.75 => Brushes.OrangeRed,
            >= 0.45 => Brushes.Goldenrod,
            _ => Brushes.ForestGreen
        };

        if (diag.AnomalyScore >= 0.75 && DateTime.UtcNow >= _nextAiHighScoreLogUtc)
        {
            _nextAiHighScoreLogUtc = DateTime.UtcNow.AddSeconds(30);
            AddLog($"[AI] 점수 {diag.AnomalyScore:F2} — {_lastAiHint}");
        }

        SyncAiInsights();
        SyncAiEngineChip();
    }

    private void BtnLogout_Click(object sender, RoutedEventArgs e)
    {
        string user = CurrentUserName() ?? "?";
        _uiTimer.Stop();
        _plcPolling.Disconnect();
        _lastProcessSampleUtc = DateTime.MinValue;
        _accessInputValid = false;
        _accessSafe = false;
        ClearSparklineHistory();
        _db.AppendEventLog(user, null, null, "로그아웃");
        SessionContext.Clear();
        SyncViewModel();

        Hide();
        var login = new LoginWindow(_db)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
        };
        bool? loginOk = login.ShowDialog();
        if (loginOk != true || SessionContext.CurrentUser is null)
        {
            Close();
            return;
        }

        Show();
        Activate();
        ApplyRolePermissions();
        AddLog($"재로그인: {SessionContext.CurrentUser.Username} ({SessionContext.CurrentUser.Role.ToDisplayKorean()})");
        _flaskGateway.BaseUrl = AppSettings.FlaskBaseUrl;
        _ = Task.Run(BackgroundPlcConnect);
        _ = ProbeFlaskOnceAsync();
        _uiTimer.Start();
        SyncViewModel();
    }

    private void ApplyRolePermissions()
    {
        if (SessionContext.CurrentUser is null)
        {
            AddLog("로그인 사용자 없음");
            return;
        }

        AddLog($"로그인: {SessionContext.CurrentUser.Username} ({SessionContext.CurrentUser.Role.ToDisplayKorean()})");
    }

    private static string? CurrentUserName() => SessionContext.CurrentUser?.Username;

    void DemoScenarioHost.Log(string message) => AddLog(message);

    bool DemoScenarioHost.SimulationEnabled => _simulationFallbackEnabled;

    void DemoScenarioHost.EnableSimulation()
    {
        if (_simulationFallbackEnabled)
        {
            return;
        }

        BtnSimAllow_Click(this, new RoutedEventArgs());
    }

    string DemoScenarioHost.EquipmentState => _state.ToString().ToUpperInvariant();

    bool DemoScenarioHost.TryStartProcess(string source)
    {
        if (_state == EquipmentState.Running)
        {
            return true;
        }

        RequestStart(source);
        return _state == EquipmentState.Running;
    }
}

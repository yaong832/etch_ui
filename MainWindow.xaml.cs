using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using etch_ui.Plc;
using etch_ui.Security;
using etch_ui.Services;
using etch_ui.ViewModels;
using System.Threading.Tasks;

namespace etch_ui;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();
    private readonly EquipmentMotionBridge _motionBridge;
    private readonly EquipmentMotionAnimator _motionAnimator;
    private readonly DatabaseService _db;
    private readonly PlcAdsService _plc = new();
    private readonly EtchFlaskClient _flask = new();

    private readonly DispatcherTimer _uiTimer = new();

    private readonly Random _rand = new();
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
    private DateTime _nextFlaskFailLogUtc = DateTime.MinValue;

    private DateTime _lastProcessSampleUtc = DateTime.MinValue;

    private const int SparkHistoryMax = 72;

    /// <summary>EtherCAT 실측 샘플이 있을 때만 센서·접근 표시.</summary>
    private bool HasLiveSensorData =>
        !_useSimulation && _plc.IsConnected && _lastProcessSampleUtc != DateTime.MinValue;

    /// <summary>appsettings 초깃값·메인 창 버튼으로 바꿀 수 있음. false면 EtherCAT 실패 시 시뮬 대체 안 함.</summary>
    private bool _simulationFallbackEnabled;

    /// <summary>EtherCAT 실데이터 없음을 한 번만 로그했는지(성공 시 리셋).</summary>
    private bool _loggedPlcRequiredOffline;

    private bool _ethercatLinkLostLogged;
    private int _ethercatReconnectCooldown;

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
        _motionAnimator = new EquipmentMotionAnimator(_vm.Equipment);
        InitializeComponent();
        DataContext = _vm;
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
        _flask.BaseUrl = AppSettings.FlaskBaseUrl;
        _ = Task.Run(BackgroundPlcConnect);
        _ = ProbeFlaskOnceAsync();
    }

    private void BackgroundPlcConnect()
    {
        bool connected = _plc.TryConnect(AppSettings.AdsPort);
        Dispatcher.BeginInvoke(() =>
        {
            if (connected)
            {
                _useSimulation = false;
                _loggedPlcRequiredOffline = false;
                _ethercatLinkLostLogged = false;
                _ethercatReconnectCooldown = 0;
                _db.AppendEventLog(CurrentUserName(), null, null, "EtherCAT ADS 연결 성공");
                AddLog($"EtherCAT 연결 성공 (ADS 포트 {AppSettings.AdsPort})");
            }
            else
            {
                OnPlcConnectFailed(_plc.LastError ?? "알 수 없음");
            }

            SyncViewModel();
        });
    }

    private void OnPlcConnectFailed(string err)
    {
        if (_simulationFallbackEnabled)
        {
            _useSimulation = true;
            SeedSimulationValues();
            _db.AppendEventLog(CurrentUserName(), null, "A001", $"EtherCAT 연결 실패(시뮬 전환): {err}");
            AddLog($"EtherCAT 연결 실패 — 시뮬 허용 ON: {err}");
        }
        else
        {
            _useSimulation = false;
            if (!_loggedPlcRequiredOffline)
            {
                _db.AppendEventLog(CurrentUserName(), "ALARM", "A001", $"EtherCAT 연결 실패: {err}");
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
            ok = await _flask.TryHealthCheckAsync().ConfigureAwait(false);
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
                AddLog($"Flask 응답 OK ({_flask.BaseUrl})");
            }
            else
            {
                AddLog($"Flask 미응답 — C:\\etchflask\\run_flask.bat 확인 ({_flask.BaseUrl})");
            }

            SyncViewModel();
        });
    }

    private void OnWindowClosed()
    {
        _motionAnimator.Dispose();
        _plc.Dispose();
        _flask.Dispose();
    }

    private void InitializeRuntime()
    {
        AddLog("시스템 초기화 완료");
        ApplyRolePermissions();

        _uiTimer.Interval = TimeSpan.FromSeconds(1);
        _uiTimer.Tick += (_, _) => UiTimerOnTick();
        _uiTimer.Start();
    }

    private void UiTimerOnTick()
    {
        if (!_useSimulation)
        {
            if (_plc.IsConnected)
            {
                if (_plc.TryReadSnapshot(out PlcProcessSnapshot snap))
                {
                    if (_ethercatLinkLostLogged)
                    {
                        _ethercatLinkLostLogged = false;
                        AddLog("EtherCAT 통신 복구됨");
                    }

                    ApplyPlcSnapshot(snap);
                }
                else
                {
                    OnEthercatLinkLost(_plc.LastError ?? "EtherCAT 읽기 실패");
                }
            }
            else
            {
                TryEthercatReconnect();
            }
        }
        else if (_useSimulation)
        {
            SimulateSensors();
        }

        AutoEvaluateState();
        PushOutputsToPlc();
        PushSparkHistory();
        SyncViewModel();

        _flaskCounter++;
        if (_flaskCounter >= 2)
        {
            _flaskCounter = 0;
            _ = PublishFlaskAsync();
        }

        LogStateTransitionIfNeeded();
    }

    private void ClearSparklineHistory()
    {
        _vm.PressureSparkValues.Clear();
        _vm.VibrationSparkValues.Clear();
    }

    private void OnEthercatLinkLost(string err)
    {
        _plc.Disconnect();
        _lastProcessSampleUtc = DateTime.MinValue;

        if (!_ethercatLinkLostLogged)
        {
            _ethercatLinkLostLogged = true;
            _ethercatReconnectCooldown = 3;
            if (_simulationFallbackEnabled)
            {
                _useSimulation = true;
                SeedSimulationValues();
                _db.AppendEventLog(CurrentUserName(), "ALARM", "A001", $"EtherCAT 통신 끊김(시뮬 전환): {err}");
                AddLog($"EtherCAT 통신 끊김 — 시뮬 허용 ON: {err}");
            }
            else
            {
                _useSimulation = false;
                _db.AppendEventLog(CurrentUserName(), "ALARM", "A001", $"EtherCAT 통신 끊김: {err}");
                AddLog($"EtherCAT 통신 끊김: {err}");
            }
        }
    }

    private void TryEthercatReconnect()
    {
        if (_ethercatReconnectCooldown > 0)
        {
            _ethercatReconnectCooldown--;
            return;
        }

        _ = Task.Run(() =>
        {
            bool ok = _plc.TryConnect(AppSettings.AdsPort);
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
                    _ethercatReconnectCooldown = 3;
                }
            });
        });
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
    }

    private void SimulateSensors()
    {
        _temp = Math.Round(_temp + (_rand.NextDouble() - 0.5) * 0.35, 2);
        _humi = Math.Round(_humi + (_rand.NextDouble() - 0.5) * 0.7, 2);
        _pressureMtorr = Math.Round(
            _pressureMtorr + (_rand.NextDouble() - 0.5) * 2.0,
            AppSettings.PressureDecimals);
        _vib = Math.Round(Math.Max(0, _vib + (_rand.NextDouble() - 0.5) * 0.06), 2);

        if (_rand.NextDouble() < 0.02)
        {
            _accessSafe = !_accessSafe;
            AddLog(_accessSafe ? "유도형 센서: 닫힘" : "유도형 센서: 열림");
        }

        _lastProcessSampleUtc = DateTime.UtcNow;
    }

    private bool IsPressureNormal =>
        _pressureSignalValid
        && _pressureMtorr >= AppSettings.PressureMtorrMin
        && _pressureMtorr <= AppSettings.PressureMtorrMax;

    private bool IsVibrationNormal => _vib <= AppSettings.VibrationGMax;

    private bool IsTempNormal =>
        _temp >= AppSettings.TempCMin && _temp <= AppSettings.TempCMax;

    private bool IsHumiNormal =>
        _humi >= AppSettings.HumiMin && _humi <= AppSettings.HumiMax;

    private bool PlcLinkOk => _useSimulation || _plc.IsConnected;

    private bool AccessInterlockOk =>
        _useSimulation || (_accessInputValid && _accessSafe);

    private bool InterlockOk =>
        PlcLinkOk
        && (_useSimulation || _pressureSignalValid)
        && IsPressureNormal
        && IsVibrationNormal
        && AccessInterlockOk
        && IsTempNormal
        && IsHumiNormal;

    private string? ComputePrimaryAlarmCode()
    {
        if (_maintenanceMode)
        {
            return null;
        }

        if (!PlcLinkOk)
        {
            return "A001";
        }

        if (_accessInputValid && !_accessSafe)
        {
            return "A004";
        }

        if (!IsPressureNormal)
        {
            return "A002";
        }

        if (!IsVibrationNormal)
        {
            return "A003";
        }

        if (!IsTempNormal)
        {
            return "A005";
        }

        if (!IsHumiNormal)
        {
            return "A006";
        }

        return null;
    }

    private void AutoEvaluateState()
    {
        if (_maintenanceMode)
        {
            _state = EquipmentState.Maintenance;
            return;
        }

        bool severe = !PlcLinkOk
            || (_accessInputValid && !_accessSafe)
            || !IsPressureNormal
            || !IsVibrationNormal;
        if (severe)
        {
            _state = EquipmentState.Alarm;
            return;
        }

        if (_state == EquipmentState.Alarm)
        {
            return;
        }

        if (!IsTempNormal || !IsHumiNormal)
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

    private void PushOutputsToPlc()
    {
        if (_useSimulation || !_plc.IsConnected)
        {
            return;
        }

        ushort bits = _state switch
        {
            EquipmentState.Ready => 1 << 0,
            EquipmentState.Running => 1 << 1,
            EquipmentState.Warning => 1 << 2,
            EquipmentState.Alarm => 1 << 3,
            _ => 0
        };

        _plc.WriteDigitalOutputLamps(bits);
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
        _vm.SimAllowButtonText = _simulationFallbackEnabled ? "시뮬 허용: 켬" : "시뮬 허용: 끔";

        if (_useSimulation)
        {
            _vm.PlcStatusText = "SIMULATION";
            _vm.PlcStatusBrush = Brushes.Goldenrod;
        }
        else if (_plc.IsConnected && _lastProcessSampleUtc != DateTime.MinValue)
        {
            _vm.PlcStatusText = "Connected";
            _vm.PlcStatusBrush = Brushes.LimeGreen;
        }
        else if (_plc.IsConnected)
        {
            _vm.PlcStatusText = "연결 대기";
            _vm.PlcStatusBrush = Brushes.Goldenrod;
        }
        else
        {
            _vm.PlcStatusText = _simulationFallbackEnabled ? "Disconnected" : "Disconnected (EtherCAT 필수)";
            _vm.PlcStatusBrush = Brushes.OrangeRed;
        }

        if (!_flaskProbeDone)
        {
            _vm.FlaskStatusText = "…";
            _vm.FlaskStatusBrush = Brushes.Goldenrod;
        }
        else
        {
            _vm.FlaskStatusText = _flaskReachable ? "OK" : "OFF";
            _vm.FlaskStatusBrush = _flaskReachable ? Brushes.LimeGreen : Brushes.OrangeRed;
        }

        _vm.LastUpdateText = DateTime.Now.ToString("HH:mm:ss");

        if (_lastProcessSampleUtc == DateTime.MinValue)
        {
            _vm.DataQualityText = "유효 샘플 없음";
            _vm.DataQualityBrush = Brushes.OrangeRed;
        }
        else if (_useSimulation)
        {
            _vm.DataQualityText = "시뮬 · " + _lastProcessSampleUtc.ToLocalTime().ToString("HH:mm:ss");
            _vm.DataQualityBrush = Brushes.Goldenrod;
        }
        else
        {
            _vm.DataQualityText = "EtherCAT ADS · " + _lastProcessSampleUtc.ToLocalTime().ToString("HH:mm:ss");
            _vm.DataQualityBrush = Brushes.DeepSkyBlue;
        }

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

        var alarmInfo = AlarmCatalog.TryGet(code);
        if (alarmInfo.HasValue)
        {
            AlarmCatalog.AlarmInfo ai = alarmInfo.Value;
            _vm.AlarmDetailText = $"{ai.Detail}\n▶ 조치: {ai.Action}";
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

        _vm.TemperatureText = HasLiveSensorData ? _temp.ToString("F2") : "—";
        _vm.HumidityText = HasLiveSensorData ? _humi.ToString("F2") : "—";
        _vm.VibrationText = HasLiveSensorData ? _vib.ToString("F2") : "—";
        if (!HasLiveSensorData)
        {
            _vm.AccessText = "—";
            _vm.AccessBrush = Brushes.DimGray;
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

        if (!HasLiveSensorData || (!_pressureSignalValid && !_useSimulation))
        {
            _vm.PressureText = "—";
        }
        else
        {
            string fmt = "F" + AppSettings.PressureDecimals;
            _vm.PressureText = _pressureMtorr.ToString(fmt);
        }

        bool interlockPlcOk = PlcLinkOk;
        bool interlockPressureOk = (_useSimulation || _pressureSignalValid) && IsPressureNormal;
        bool interlockVibOk = IsVibrationNormal;
        bool interlockAccessOk = HasLiveSensorData && _accessInputValid && _accessSafe;
        bool interlockTempOk = IsTempNormal;
        bool interlockHumiOk = IsHumiNormal;

        _vm.InterlockPlcText = $"[{ToMark(interlockPlcOk)}] EtherCAT/시뮬 통신";
        _vm.InterlockPressureText = _useSimulation || _pressureSignalValid
            ? $"[{ToMark(interlockPressureOk)}] 압력 정상"
            : "[✗] 압력 신호 없음";
        _vm.InterlockPressureBrush = InterlockItemBrush(_useSimulation || interlockPressureOk);
        _vm.InterlockVibText = $"[{ToMark(interlockVibOk)}] 진동 정상";
        _vm.InterlockVibBrush = InterlockItemBrush(interlockVibOk);
        if (!HasLiveSensorData || !_accessInputValid)
        {
            _vm.InterlockAccessText = "[－] 도어(유도형) 미측정";
            _vm.InterlockAccessBrush = Brushes.DimGray;
        }
        else
        {
            _vm.InterlockAccessText = $"[{ToMark(interlockAccessOk)}] 도어 닫힘";
            _vm.InterlockAccessBrush = InterlockItemBrush(interlockAccessOk);
        }
        _vm.InterlockTempText = $"[{ToMark(interlockTempOk)}] 온도 정상";
        _vm.InterlockTempBrush = InterlockItemBrush(interlockTempOk);
        _vm.InterlockHumiText = $"[{ToMark(interlockHumiOk)}] 습도 정상";
        _vm.InterlockHumiBrush = InterlockItemBrush(interlockHumiOk);
        _vm.InterlockPlcBrush = InterlockItemBrush(interlockPlcOk);
        _vm.InterlockResultText = InterlockOk ? "공정 시작 가능" : "공정 시작 불가";
        _vm.InterlockResultBrush = InterlockOk ? Brushes.ForestGreen : Brushes.OrangeRed;

        _vm.LampReadyOn = _state == EquipmentState.Ready;
        _vm.LampRunOn = _state == EquipmentState.Running;
        _vm.LampWarnOn = _state == EquipmentState.Warning;
        _vm.LampAlarmOn = _state == EquipmentState.Alarm;

        _vm.CanStart = SessionContext.HasRole(UserRole.Worker) && InterlockOk && !_maintenanceMode;
        _vm.StartButtonToolTip = BuildStartButtonToolTip();
        _vm.CanStop = SessionContext.HasRole(UserRole.Worker);
        _vm.CanReset = SessionContext.HasRole(UserRole.Admin);
        _vm.CanMaint = SessionContext.HasRole(UserRole.Admin);

        _vm.PressureSparkYMax = AppSettings.PressureMtorrAtRawMax;
        _vm.VibrationSparkYMax = AppSettings.VibrationGMax * 1.5;

        _vm.SensorPressureValue = _pressureMtorr;
        _vm.SensorVibrationValue = _vib;
        _vm.SensorTempValue = _temp;
        _vm.SensorHumiValue = _humi;

        (_vm.ProcessStepIndex, _vm.ProcessStepWarning) = MapProcessStep(_state);

        _motionBridge.Sync(
            HasLiveSensorData,
            _useSimulation,
            _plc.IsConnected,
            _accessSafe,
            _accessInputValid,
            _vm.StateText,
            _vm.LampReadyOn,
            _vm.LampRunOn,
            _vm.LampWarnOn,
            _vm.LampAlarmOn);
    }

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

    private static (int Index, bool Warning) MapProcessStep(EquipmentState state) =>
        state switch
        {
            EquipmentState.Maintenance => (4, false),
            EquipmentState.Alarm => (4, false),
            EquipmentState.Warning => (3, true),
            EquipmentState.Running => (2, false),
            EquipmentState.Ready => (1, false),
            _ => (0, false),
        };

    private static Brush InterlockItemBrush(bool ok) =>
        ok ? Brushes.ForestGreen : Brushes.OrangeRed;

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

        if (!InterlockOk)
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

        if (!InterlockOk)
        {
            _state = EquipmentState.Alarm;
            string ac = ComputePrimaryAlarmCode() ?? "A004";
            _db.AppendEventLog(CurrentUserName(), "ALARM", ac, $"{source}: 인터락 불만족 Start 차단");
            AddLog($"{source}: {ac} 인터락 불만족 Start 차단");
            SyncViewModel();
            return;
        }

        _state = EquipmentState.Running;
        _db.AppendEventLog(CurrentUserName(), "RUNNING", null, $"{source}: 운전 시작");
        AddLog($"{source}: RUNNING 진입");
        SyncViewModel();
    }

    private void RequestStop(string source)
    {
        if (!SessionContext.HasRole(UserRole.Worker))
        {
            AddLog("권한 부족: Stop 불가");
            return;
        }

        _state = InterlockOk ? EquipmentState.Ready : EquipmentState.Idle;
        _db.AppendEventLog(CurrentUserName(), _state.ToString().ToUpperInvariant(), null, $"{source}: 정지");
        AddLog($"{source}: Stop");
        SyncViewModel();
    }

    private void RequestReset(string source)
    {
        if (!SessionContext.HasRole(UserRole.Admin))
        {
            AddLog("권한 부족: Alarm Reset 불가");
            return;
        }

        if (_state == EquipmentState.Alarm && InterlockOk)
        {
            _state = EquipmentState.Ready;
            _db.AppendEventLog(CurrentUserName(), "READY", null, $"{source}: Alarm Reset 완료");
            AddLog($"{source}: Alarm Reset 완료");
        }
        else if (_state == EquipmentState.Alarm)
        {
            AddLog($"{source}: Alarm Reset 실패 — 인터락 미충족");
        }
        else
        {
            AddLog($"{source}: Reset");
        }

        SyncViewModel();
    }

    private void RequestMaintenanceToggle(string source)
    {
        if (!SessionContext.HasRole(UserRole.Admin))
        {
            AddLog("권한 부족: Maintenance 불가");
            return;
        }

        _maintenanceMode = !_maintenanceMode;
        _state = _maintenanceMode ? EquipmentState.Maintenance : EquipmentState.Idle;
        _db.AppendEventLog(CurrentUserName(), _state.ToString().ToUpperInvariant(), null,
            _maintenanceMode ? $"{source}: 유지보수 진입" : $"{source}: 유지보수 해제");
        AddLog(_maintenanceMode ? $"{source}: 유지보수 모드" : $"{source}: 일반 모드");
        SyncViewModel();
    }

    private async Task PublishFlaskAsync()
    {
        try
        {
            bool live = !_useSimulation && _plc.IsConnected && _lastProcessSampleUtc != DateTime.MinValue;
            // TwinCAT/장비 전원 OFF 시 읽기 실패 → IsConnected false, Flask에도 미연결로 표시
            var payload = new EtchTelemetryPayload
            {
                EquipmentId = 1,
                PowerOn = true,
                Connected = live,
                SensorsLive = live,
                LastUpdate = DateTime.UtcNow.ToString("o"),
                Temperature = _temp,
                Humidity = _humi,
                Pressure = _pressureMtorr,
                Vibration = _vib,
                AccessSafe = _accessSafe,
                EquipmentState = _state.ToString().ToUpperInvariant(),
                AlarmCode = _state == EquipmentState.Alarm ? ComputePrimaryAlarmCode() : null,
                InterlockOk = InterlockOk,
                Username = CurrentUserName()
            };

            bool ok = await _flask.TryPostEtchSensorDataAsync(payload).ConfigureAwait(false);
            Dispatcher.BeginInvoke(() =>
            {
                _flaskReachable = ok;
                if (!ok && DateTime.UtcNow >= _nextFlaskFailLogUtc)
                {
                    _nextFlaskFailLogUtc = DateTime.UtcNow.AddSeconds(25);
                    AddLog($"Flask 전송 실패 — {_flask.BaseUrl} 서버·방화벽 확인");
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
        _db.AppendEventLog(CurrentUserName(), _state.ToString().ToUpperInvariant(), null, "상태 전이");

        string? ac = ComputePrimaryAlarmCode();
        if (_state == EquipmentState.Alarm && ac is not null && ac != _lastAlarmCode)
        {
            _lastAlarmCode = ac;
            _db.AppendEventLog(CurrentUserName(), "ALARM", ac, "알람 코드 갱신");
        }
        else if (previous == EquipmentState.Alarm && _state != EquipmentState.Alarm)
        {
            _lastAlarmCode = null;
        }
    }

    private void BtnSimAllow_Click(object sender, RoutedEventArgs e)
    {
        _simulationFallbackEnabled = !_simulationFallbackEnabled;
        if (_simulationFallbackEnabled)
        {
            AddLog("시뮬 허용 ON — EtherCAT 끊기면 데모 센서로 대체합니다.");
            if (_plc.TryReadSnapshot(out PlcProcessSnapshot snap))
            {
                _useSimulation = false;
                ApplyPlcSnapshot(snap);
            }
            else if (_plc.TryConnect(AppSettings.AdsPort) && _plc.TryReadSnapshot(out snap))
            {
                _useSimulation = false;
                ApplyPlcSnapshot(snap);
            }
            else
            {
                _useSimulation = true;
                SeedSimulationValues();
                ClearSparklineHistory();
                AddLog("EtherCAT 미연결 — 시뮬 모드(화면 센서 미표시).");
            }
        }
        else
        {
            AddLog("시뮬 허용 OFF — EtherCAT 실데이터만 사용합니다.");
            _useSimulation = false;
            if (_plc.TryConnect(AppSettings.AdsPort) && _plc.TryReadSnapshot(out PlcProcessSnapshot snap))
            {
                ApplyPlcSnapshot(snap);
            }
            else
            {
                _loggedPlcRequiredOffline = true;
                AddLog("EtherCAT 데이터 없음.");
            }
        }

        AutoEvaluateState();
        PushOutputsToPlc();
        SyncViewModel();
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

    private void BtnLogout_Click(object sender, RoutedEventArgs e)
    {
        string user = CurrentUserName();
        _uiTimer.Stop();
        _plc.Disconnect();
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
        _flask.BaseUrl = AppSettings.FlaskBaseUrl;
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
}

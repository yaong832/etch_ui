using System.Collections.ObjectModel;
using System.Windows.Media;
using etch_ui.Equipment.Models;
using etch_ui.Equipment.ViewModels;
using etch_ui.Security;

namespace etch_ui.ViewModels;

/// <summary>메인 HMI 화면 바인딩용 (코드비하인드가 주기적으로 SyncFromRuntime 호출).</summary>
public sealed class MainViewModel : ViewModelBase
{
    public EquipmentMotionViewModel Equipment { get; } = new();

    private string _currentUserText = "-";
    private bool _showUserManage;
    private string _simAllowButtonText = "시뮬 허용: 끔";
    private string _plcStatusText = "Disconnected";
    private Brush _plcStatusBrush = Brushes.OrangeRed;
    private string _flaskStatusText = "확인 중";
    private Brush _flaskStatusBrush = Brushes.Goldenrod;
    private string _lastUpdateText = "-";
    private string _dataQualityText = "-";
    private Brush _dataQualityBrush = Brushes.LightSteelBlue;

    private string _stateText = "IDLE";
    private Brush _stateBrush = Brushes.DodgerBlue;
    private string _alarmCodeText = "-";
    private Brush _alarmCodeBrush = Brushes.DimGray;
    private string _alarmDetailText = "";
    private Brush _alarmDetailBrush = Brushes.DimGray;
    private string _processPhaseText = "LOAD_LOCK · 대기 (IDLE)";

    private string _temperatureText = "0.00";
    private string _humidityText = "0.00";
    private string _pressureText = "—";
    private string _vibrationText = "0.00";
    private string _accessText = "SAFE";
    private Brush _accessBrush = Brushes.ForestGreen;

    private string _interlockPlcText = "[✗] EtherCAT/시뮬 통신";
    private string _interlockPressureText = "[✗] 압력 정상";
    private string _interlockPressureDetailText = "";
    private string _interlockVibText = "[✗] 진동 정상";
    private string _interlockAccessText = "[✗] 접근 안전";
    private string _interlockTempText = "[✗] 온도 정상";
    private string _interlockHumiText = "[✗] 습도 정상";
    private string _interlockResultText = "공정 시작 불가";
    private Brush _interlockResultBrush = Brushes.OrangeRed;
    private Brush _interlockPlcBrush = Brushes.OrangeRed;
    private Brush _interlockPressureBrush = Brushes.OrangeRed;
    private Brush _interlockVibBrush = Brushes.OrangeRed;
    private Brush _interlockAccessBrush = Brushes.OrangeRed;
    private Brush _interlockTempBrush = Brushes.OrangeRed;
    private Brush _interlockHumiBrush = Brushes.OrangeRed;
    private string _startButtonToolTip = "공정을 시작합니다.";

    private bool _lampReadyOn;
    private bool _lampRunOn;
    private bool _lampWarnOn;
    private bool _lampAlarmOn;

    private bool _canStart;
    private bool _canStop;
    private bool _canReset;
    private bool _canMaint;

    private double _sensorPressureValue;
    private double _sensorVibrationValue;
    private double _sensorTempValue;
    private double _sensorHumiValue;
    private double _pressureSparkYMax = 1000;
    private double _vibrationSparkYMax = 1.2;

    private int _processStepIndex;
    private bool _processStepWarning;

    private string _aiScoreText = "—";
    private string _aiHintText = "Flask 서버 연결 후 표시됩니다.";
    private Brush _aiScoreBrush = Brushes.DimGray;

    public ObservableCollection<string> LogLines { get; } = new();
    public ObservableCollection<double> PressureSparkValues { get; } = new();
    public ObservableCollection<double> VibrationSparkValues { get; } = new();
    public ObservableCollection<ModuleStateSnapshot> ModuleStates { get; } = new();

    public void SetModuleSnapshots(IReadOnlyList<ModuleStateSnapshot> snapshots)
    {
        ModuleStates.Clear();
        foreach (ModuleStateSnapshot s in snapshots)
        {
            ModuleStates.Add(s);
        }
    }

    public string CurrentUserText
    {
        get => _currentUserText;
        set => SetField(ref _currentUserText, value);
    }

    public bool ShowUserManage
    {
        get => _showUserManage;
        set => SetField(ref _showUserManage, value);
    }

    public string SimAllowButtonText
    {
        get => _simAllowButtonText;
        set => SetField(ref _simAllowButtonText, value);
    }

    public string PlcStatusText
    {
        get => _plcStatusText;
        set => SetField(ref _plcStatusText, value);
    }

    public Brush PlcStatusBrush
    {
        get => _plcStatusBrush;
        set => SetField(ref _plcStatusBrush, value);
    }

    public string FlaskStatusText
    {
        get => _flaskStatusText;
        set => SetField(ref _flaskStatusText, value);
    }

    public Brush FlaskStatusBrush
    {
        get => _flaskStatusBrush;
        set => SetField(ref _flaskStatusBrush, value);
    }

    public string LastUpdateText
    {
        get => _lastUpdateText;
        set => SetField(ref _lastUpdateText, value);
    }

    public string DataQualityText
    {
        get => _dataQualityText;
        set => SetField(ref _dataQualityText, value);
    }

    public Brush DataQualityBrush
    {
        get => _dataQualityBrush;
        set => SetField(ref _dataQualityBrush, value);
    }

    public string StateText
    {
        get => _stateText;
        set => SetField(ref _stateText, value);
    }

    public Brush StateBrush
    {
        get => _stateBrush;
        set => SetField(ref _stateBrush, value);
    }

    public string AlarmCodeText
    {
        get => _alarmCodeText;
        set => SetField(ref _alarmCodeText, value);
    }

    public Brush AlarmCodeBrush
    {
        get => _alarmCodeBrush;
        set => SetField(ref _alarmCodeBrush, value);
    }

    public string AlarmDetailText
    {
        get => _alarmDetailText;
        set => SetField(ref _alarmDetailText, value);
    }

    public Brush AlarmDetailBrush
    {
        get => _alarmDetailBrush;
        set => SetField(ref _alarmDetailBrush, value);
    }

    public string ProcessPhaseText
    {
        get => _processPhaseText;
        set => SetField(ref _processPhaseText, value);
    }

    public string TemperatureText
    {
        get => _temperatureText;
        set => SetField(ref _temperatureText, value);
    }

    public string HumidityText
    {
        get => _humidityText;
        set => SetField(ref _humidityText, value);
    }

    public string PressureText
    {
        get => _pressureText;
        set => SetField(ref _pressureText, value);
    }

    public string VibrationText
    {
        get => _vibrationText;
        set => SetField(ref _vibrationText, value);
    }

    public string AccessText
    {
        get => _accessText;
        set => SetField(ref _accessText, value);
    }

    public Brush AccessBrush
    {
        get => _accessBrush;
        set => SetField(ref _accessBrush, value);
    }

    public string InterlockPlcText
    {
        get => _interlockPlcText;
        set => SetField(ref _interlockPlcText, value);
    }

    public string InterlockPressureText
    {
        get => _interlockPressureText;
        set => SetField(ref _interlockPressureText, value);
    }

    /// <summary>압력 허용 범위·현재값 (인터락 패널).</summary>
    public string InterlockPressureDetailText
    {
        get => _interlockPressureDetailText;
        set => SetField(ref _interlockPressureDetailText, value);
    }

    /// <summary>appsettings.json Interlock 섹션 요약.</summary>
    public string InterlockThresholdsText =>
        $"압력 {AppSettings.PressureMtorrMin:F0}–{AppSettings.PressureMtorrMax:F0} mTorr  ·  " +
        $"진동 ≤{AppSettings.VibrationGMax:F2} g  ·  " +
        $"온도 {AppSettings.TempCMin:F0}–{AppSettings.TempCMax:F0} ℃  ·  " +
        $"습도 {AppSettings.HumiMin:F0}–{AppSettings.HumiMax:F0} %";

    public string InterlockVibText
    {
        get => _interlockVibText;
        set => SetField(ref _interlockVibText, value);
    }

    public string InterlockAccessText
    {
        get => _interlockAccessText;
        set => SetField(ref _interlockAccessText, value);
    }

    public string InterlockTempText
    {
        get => _interlockTempText;
        set => SetField(ref _interlockTempText, value);
    }

    public string InterlockHumiText
    {
        get => _interlockHumiText;
        set => SetField(ref _interlockHumiText, value);
    }

    public string InterlockResultText
    {
        get => _interlockResultText;
        set => SetField(ref _interlockResultText, value);
    }

    public Brush InterlockResultBrush
    {
        get => _interlockResultBrush;
        set => SetField(ref _interlockResultBrush, value);
    }

    public Brush InterlockPlcBrush
    {
        get => _interlockPlcBrush;
        set => SetField(ref _interlockPlcBrush, value);
    }

    public Brush InterlockPressureBrush
    {
        get => _interlockPressureBrush;
        set => SetField(ref _interlockPressureBrush, value);
    }

    public Brush InterlockVibBrush
    {
        get => _interlockVibBrush;
        set => SetField(ref _interlockVibBrush, value);
    }

    public Brush InterlockAccessBrush
    {
        get => _interlockAccessBrush;
        set => SetField(ref _interlockAccessBrush, value);
    }

    public Brush InterlockTempBrush
    {
        get => _interlockTempBrush;
        set => SetField(ref _interlockTempBrush, value);
    }

    public Brush InterlockHumiBrush
    {
        get => _interlockHumiBrush;
        set => SetField(ref _interlockHumiBrush, value);
    }

    public string StartButtonToolTip
    {
        get => _startButtonToolTip;
        set => SetField(ref _startButtonToolTip, value);
    }

    public bool LampReadyOn
    {
        get => _lampReadyOn;
        set => SetField(ref _lampReadyOn, value);
    }

    public bool LampRunOn
    {
        get => _lampRunOn;
        set => SetField(ref _lampRunOn, value);
    }

    public bool LampWarnOn
    {
        get => _lampWarnOn;
        set => SetField(ref _lampWarnOn, value);
    }

    public bool LampAlarmOn
    {
        get => _lampAlarmOn;
        set => SetField(ref _lampAlarmOn, value);
    }

    public bool CanStart
    {
        get => _canStart;
        set => SetField(ref _canStart, value);
    }

    public bool CanStop
    {
        get => _canStop;
        set => SetField(ref _canStop, value);
    }

    public bool CanReset
    {
        get => _canReset;
        set => SetField(ref _canReset, value);
    }

    public bool CanMaint
    {
        get => _canMaint;
        set => SetField(ref _canMaint, value);
    }

    public double PressureSparkYMin => 0;

    public double PressureSparkYMax
    {
        get => _pressureSparkYMax;
        set => SetField(ref _pressureSparkYMax, value);
    }

    public double VibrationSparkYMin => 0;

    public double VibrationSparkYMax
    {
        get => _vibrationSparkYMax;
        set => SetField(ref _vibrationSparkYMax, value);
    }

    public double SensorPressureValue
    {
        get => _sensorPressureValue;
        set => SetField(ref _sensorPressureValue, value);
    }

    public double SensorVibrationValue
    {
        get => _sensorVibrationValue;
        set => SetField(ref _sensorVibrationValue, value);
    }

    public double SensorTempValue
    {
        get => _sensorTempValue;
        set => SetField(ref _sensorTempValue, value);
    }

    public double SensorHumiValue
    {
        get => _sensorHumiValue;
        set => SetField(ref _sensorHumiValue, value);
    }

    public double SensorPressureRangeMin => AppSettings.PressureMtorrMin;
    public double SensorPressureRangeMax => AppSettings.PressureMtorrMax;
    public double SensorPressureScaleMax => AppSettings.PressureMtorrAtRawMax;

    public double SensorVibrationRangeMax => AppSettings.VibrationGMax;
    public double SensorVibrationScaleMax => AppSettings.VibrationGMax * 1.5;

    public double SensorTempRangeMin => AppSettings.TempCMin;
    public double SensorTempRangeMax => AppSettings.TempCMax;
    public double SensorTempScaleMin => AppSettings.TempCMin - 5;
    public double SensorTempScaleMax => AppSettings.TempCMax + 5;

    public double SensorHumiRangeMin => AppSettings.HumiMin;
    public double SensorHumiRangeMax => AppSettings.HumiMax;
    public double SensorHumiScaleMax => 100;

    public int ProcessStepIndex
    {
        get => _processStepIndex;
        set => SetField(ref _processStepIndex, value);
    }

    public bool ProcessStepWarning
    {
        get => _processStepWarning;
        set => SetField(ref _processStepWarning, value);
    }

    public string AiScoreText
    {
        get => _aiScoreText;
        set => SetField(ref _aiScoreText, value);
    }

    public string AiHintText
    {
        get => _aiHintText;
        set => SetField(ref _aiHintText, value);
    }

    public Brush AiScoreBrush
    {
        get => _aiScoreBrush;
        set => SetField(ref _aiScoreBrush, value);
    }

    public void PrependLog(string line)
    {
        LogLines.Insert(0, line);
        while (LogLines.Count > 200)
        {
            LogLines.RemoveAt(LogLines.Count - 1);
        }
    }
}

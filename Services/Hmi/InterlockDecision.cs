using etch_ui.Services;

namespace etch_ui.Services.Hmi;

/// <summary>인터락 판정 결과 — Start·알람·상태기에 공유.</summary>
public sealed class InterlockDecision
{
    public static InterlockDecision Empty { get; } = new();

    public InterlockSeverity PressureSeverity { get; init; } = InterlockSeverity.Alarm;
    public InterlockSeverity VibrationSeverity { get; init; } = InterlockSeverity.Alarm;
    public InterlockSeverity TemperatureSeverity { get; init; } = InterlockSeverity.Alarm;
    public InterlockSeverity HumiditySeverity { get; init; } = InterlockSeverity.Alarm;

    public bool PlcLinkOk { get; init; }
    public bool AccessInterlockOk { get; init; }
    public bool ProductionInterlockOk { get; init; }
    public bool HasSensorAlarm { get; init; }
    public bool HasSensorWarning { get; init; }

    /// <summary>ALARM 상태일 때 WPF·Flask에 올릴 1차 코드 (데모·정비 시 null).</summary>
    public string? PrimaryAlarmCode { get; init; }
}

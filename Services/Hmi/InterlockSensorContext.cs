namespace etch_ui.Services.Hmi;

/// <summary>인터락 판정 입력 — MainWindow 런타임 센서·모드 스냅샷.</summary>
public sealed class InterlockSensorContext
{
    public bool HasLiveSensorData { get; init; }
    public bool IsBenchMode { get; init; }
    /// <summary>시뮬 허용 ON — 가상 이송 Start는 인터락과 무관.</summary>
    public bool SimulationFallbackEnabled { get; init; }
    public bool MaintenanceMode { get; init; }
    public bool AccessInputValid { get; init; }
    public bool EffectiveAccessSafe { get; init; }
    public double PressureMtorr { get; init; }
    public bool PressureSignalValid { get; init; }
    public double VibrationG { get; init; }
    public double TempC { get; init; }
    public double HumidityPct { get; init; }
}

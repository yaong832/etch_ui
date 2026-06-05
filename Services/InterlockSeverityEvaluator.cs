namespace etch_ui.Services;

public enum InterlockSeverity
{
    Ok,
    Warning,
    Alarm
}

/// <summary>인터락 정상(Start)·경고(RUNNING)·알람(즉시) 3단계 판정.</summary>
public static class InterlockSeverityEvaluator
{
    public static InterlockSeverity Pressure(double mtorr, bool signalValid)
    {
        if (!signalValid)
        {
            return InterlockSeverity.Alarm;
        }

        if (mtorr < AppSettings.PressureMtorrAlarmMin || mtorr > AppSettings.PressureMtorrAlarmMax)
        {
            return InterlockSeverity.Alarm;
        }

        if (mtorr < AppSettings.PressureMtorrMin || mtorr > AppSettings.PressureMtorrMax)
        {
            return InterlockSeverity.Warning;
        }

        return InterlockSeverity.Ok;
    }

    public static InterlockSeverity Vibration(double g) =>
        g > AppSettings.VibrationGAlarmMax ? InterlockSeverity.Alarm
        : g > AppSettings.VibrationGMax ? InterlockSeverity.Warning
        : InterlockSeverity.Ok;

    public static InterlockSeverity Temperature(double c) =>
        c < AppSettings.TempCAlarmMin || c > AppSettings.TempCAlarmMax ? InterlockSeverity.Alarm
        : c < AppSettings.TempCMin || c > AppSettings.TempCMax ? InterlockSeverity.Warning
        : InterlockSeverity.Ok;

    public static InterlockSeverity Humidity(double h) =>
        h < AppSettings.HumiAlarmMin || h > AppSettings.HumiAlarmMax ? InterlockSeverity.Alarm
        : h < AppSettings.HumiMin || h > AppSettings.HumiMax ? InterlockSeverity.Warning
        : InterlockSeverity.Ok;

    public static bool AnyAlarm(params InterlockSeverity[] items) =>
        items.Any(s => s == InterlockSeverity.Alarm);

    public static bool AnyWarning(params InterlockSeverity[] items) =>
        items.Any(s => s == InterlockSeverity.Warning);

    public static bool AllOk(params InterlockSeverity[] items) =>
        items.All(s => s == InterlockSeverity.Ok);
}

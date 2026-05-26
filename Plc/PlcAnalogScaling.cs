namespace etch_ui.Plc;

internal static class PlcAnalogScaling
{
    public static double Clamp(double value, double min, double max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    public static double ScaleLinear(double raw, double rawMin, double rawMax, double valueMin, double valueMax)
    {
        if (Math.Abs(rawMax - rawMin) < double.Epsilon)
            return valueMin;

        double ratio = (raw - rawMin) / (rawMax - rawMin);
        return valueMin + ratio * (valueMax - valueMin);
    }

    public static double ConvertTemperatureCelsius(short raw)
    {
        const double slope = 0.010256;
        const double intercept = -21.0;
        return slope * raw + intercept;
    }

    public static (double HumidityPercent, double TemperatureC, double PressureMtorr, bool PressureValid, double VibrationPercent)
        ToEngineering(AnalogInputData data)
    {
        double humidityPercent = Clamp(ScaleLinear(data.HumiditySensor, 5500, 7550, 68, 94), 0, 100);
        double temperatureC = Clamp(ConvertTemperatureCelsius(data.TemperatureSensor), -10, 60);
        bool pressureValid = TryPressureMtorr(data.PressureSensor, out double pressureMtorr);
        double vibrationPercent = Clamp(ScaleLinear(data.VibrationSensor, 450, 6500, 0, 100), 0, 100);
        return (humidityPercent, temperatureC, pressureMtorr, pressureValid, vibrationPercent);
    }

    /// <summary>
    /// EtherCAT 압력 채널 raw(ADC) → mTorr 직선 환산. (farmui 채광 %·95~100 kPa 맵과 무관)
    /// </summary>
    public static bool TryPressureMtorr(short raw, out double pressureMtorr)
    {
        int rawMin = AppSettings.PressureRawMin;
        int rawMax = AppSettings.PressureRawMax;
        double mMin = AppSettings.PressureMtorrAtRawMin;
        double mMax = AppSettings.PressureMtorrAtRawMax;

        if (raw < rawMin)
        {
            pressureMtorr = 0;
            return false;
        }

        if (raw > rawMax)
        {
            pressureMtorr = mMax;
        }
        else
        {
            pressureMtorr = ScaleLinear(raw, rawMin, rawMax, mMin, mMax);
        }

        pressureMtorr = Math.Round(pressureMtorr, AppSettings.PressureDecimals);
        return true;
    }

    /// <summary>시뮬·초기값 — 인터락 대역 중앙(레시피 확정 전 임시).</summary>
    public static double DefaultSimulationPressureMtorr()
        => Math.Round(
            (AppSettings.PressureMtorrMin + AppSettings.PressureMtorrMax) / 2.0,
            AppSettings.PressureDecimals);

    public static double VibrationPercentToG(double vibrationPercent)
        => vibrationPercent / 100.0 * 2.0;
}

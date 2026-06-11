using etch_ui.Services;

namespace etch_ui.Services.Hmi;

/// <summary>인터락 OK/알람 코드·센서 심각도 단일 판정.</summary>
public static class InterlockEvaluator
{
    public static InterlockDecision Evaluate(InterlockSensorContext ctx)
    {
        InterlockSeverity pressure = InterlockSeverityEvaluator.Pressure(ctx.PressureMtorr, ctx.PressureSignalValid);
        InterlockSeverity vibration = InterlockSeverityEvaluator.Vibration(ctx.VibrationG);
        InterlockSeverity temperature = InterlockSeverityEvaluator.Temperature(ctx.TempC);
        InterlockSeverity humidity = InterlockSeverityEvaluator.Humidity(ctx.HumidityPct);

        bool plcLinkOk = ctx.HasLiveSensorData;
        bool accessOk = ctx.HasLiveSensorData && ctx.AccessInputValid && ctx.EffectiveAccessSafe;
        bool productionOk = plcLinkOk
                            && accessOk
                            && InterlockSeverityEvaluator.AllOk(pressure, vibration, temperature, humidity);

        return new InterlockDecision
        {
            PressureSeverity = pressure,
            VibrationSeverity = vibration,
            TemperatureSeverity = temperature,
            HumiditySeverity = humidity,
            PlcLinkOk = plcLinkOk,
            AccessInterlockOk = accessOk,
            ProductionInterlockOk = productionOk,
            HasSensorAlarm = InterlockSeverityEvaluator.AnyAlarm(pressure, vibration, temperature, humidity),
            HasSensorWarning = InterlockSeverityEvaluator.AnyWarning(pressure, vibration, temperature, humidity),
            PrimaryAlarmCode = ResolvePrimaryAlarmCode(ctx, plcLinkOk, pressure, vibration, temperature, humidity)
        };
    }

    private static string? ResolvePrimaryAlarmCode(
        InterlockSensorContext ctx,
        bool plcLinkOk,
        InterlockSeverity pressure,
        InterlockSeverity vibration,
        InterlockSeverity temperature,
        InterlockSeverity humidity)
    {
        if (ctx.MaintenanceMode || ctx.IsBenchMode)
        {
            return null;
        }

        if (!plcLinkOk)
        {
            return "A001";
        }

        if (ctx.AccessInputValid && !ctx.EffectiveAccessSafe)
        {
            return "A004";
        }

        if (pressure == InterlockSeverity.Alarm)
        {
            return "A002";
        }

        if (vibration == InterlockSeverity.Alarm)
        {
            return "A003";
        }

        if (temperature == InterlockSeverity.Alarm)
        {
            return "A005";
        }

        if (humidity == InterlockSeverity.Alarm)
        {
            return "A006";
        }

        return null;
    }
}

using etch_ui;

namespace etch_ui.Configuration;

/// <summary>appsettings.json 직렬화·편집용 스냅샷.</summary>
public sealed class AppSettingsSnapshot
{
    public string FlaskBaseUrl { get; set; } = "http://127.0.0.1:5000";
    public int AdsPort { get; set; } = 851;
    public bool SimulationEnabled { get; set; }

    public InterlockThresholds Interlock { get; set; } = new();
    public PressureScaleSettings PressureScale { get; set; } = new();
    public ProcessRecipeSettings ProcessRecipe { get; set; } = new();

    public static AppSettingsSnapshot FromCurrent() => new()
    {
        FlaskBaseUrl = AppSettings.FlaskBaseUrl,
        AdsPort = AppSettings.AdsPort,
        SimulationEnabled = AppSettings.SimulationEnabled,
        Interlock = InterlockThresholds.FromCurrent(),
        PressureScale = PressureScaleSettings.FromCurrent(),
        ProcessRecipe = ProcessRecipeSettings.FromCurrent()
    };
}

public sealed class InterlockThresholds
{
    public double PressureMtorrMin { get; set; } = 50;
    public double PressureMtorrMax { get; set; } = 150;
    public double VibrationGMax { get; set; } = 0.8;
    public double TempCMin { get; set; } = 20;
    public double TempCMax { get; set; } = 30;
    public double HumiMin { get; set; } = 30;
    public double HumiMax { get; set; } = 55;

    public static InterlockThresholds FromCurrent() => new()
    {
        PressureMtorrMin = AppSettings.PressureMtorrMin,
        PressureMtorrMax = AppSettings.PressureMtorrMax,
        VibrationGMax = AppSettings.VibrationGMax,
        TempCMin = AppSettings.TempCMin,
        TempCMax = AppSettings.TempCMax,
        HumiMin = AppSettings.HumiMin,
        HumiMax = AppSettings.HumiMax
    };
}

public sealed class PressureScaleSettings
{
    public int RawMin { get; set; } = 5;
    public int RawMax { get; set; } = 3575;
    public double MtorrAtRawMin { get; set; }
    public double MtorrAtRawMax { get; set; } = 1000;
    public int Decimals { get; set; } = 1;

    public static PressureScaleSettings FromCurrent() => new()
    {
        RawMin = AppSettings.PressureRawMin,
        RawMax = AppSettings.PressureRawMax,
        MtorrAtRawMin = AppSettings.PressureMtorrAtRawMin,
        MtorrAtRawMax = AppSettings.PressureMtorrAtRawMax,
        Decimals = AppSettings.PressureDecimals
    };
}

/// <summary>가상 시뮬 PM·Aligner 가공 tick (다음 Start부터 반영).</summary>
public sealed class ProcessRecipeSettings
{
    public int EtchProcessTicks { get; set; } = 120;
    public int StripProcessTicks { get; set; } = 28;
    public int AlignProcessTicks { get; set; } = 2;

    public static ProcessRecipeSettings FromCurrent() => new()
    {
        EtchProcessTicks = AppSettings.EtchProcessTicks,
        StripProcessTicks = AppSettings.StripProcessTicks,
        AlignProcessTicks = AppSettings.AlignProcessTicks
    };
}

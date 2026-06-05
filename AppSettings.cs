using etch_ui.Configuration;
using etch_ui.Plc;
using etch_ui.Services.Scheduling;

namespace etch_ui;

/// <summary>출력 폴더의 appsettings.json (없으면 기본값). 저장 후 <see cref="ReloadFromDisk"/> 호출.</summary>
public static class AppSettings
{
    public static string FlaskBaseUrl { get; private set; } = "http://127.0.0.1:5000";
    public static int AdsPort { get; private set; } = PlcAdsService.DefaultPort;
    public static bool SimulationEnabled { get; private set; }

    public static double PressureMtorrMin { get; private set; } = 40.0;
    public static double PressureMtorrMax { get; private set; } = 180.0;
    public static double VibrationGMax { get; private set; } = 1.0;
    public static double TempCMin { get; private set; } = 18.0;
    public static double TempCMax { get; private set; } = 32.0;
    public static double HumiMin { get; private set; } = 25.0;
    public static double HumiMax { get; private set; } = 60.0;

    public static double PressureMtorrAlarmMin { get; private set; } = 25.0;
    public static double PressureMtorrAlarmMax { get; private set; } = 220.0;
    public static double VibrationGAlarmMax { get; private set; } = 1.5;
    public static double TempCAlarmMin { get; private set; } = 15.0;
    public static double TempCAlarmMax { get; private set; } = 35.0;
    public static double HumiAlarmMin { get; private set; } = 20.0;
    public static double HumiAlarmMax { get; private set; } = 65.0;

    public static int PressureRawMin { get; private set; } = 5;
    public static int PressureRawMax { get; private set; } = 3575;
    public static double PressureMtorrAtRawMin { get; private set; }
    public static double PressureMtorrAtRawMax { get; private set; } = 1000.0;
    public static int PressureDecimals { get; private set; } = 1;

    public static string RecipeId { get; private set; } = "default";
    public static string RecipeName { get; private set; } = "기본 식각";
    public static string RecipeVersion { get; private set; } = "1";
    public static string RecipeDescription { get; private set; } = string.Empty;
    public static string EtchPmSequence { get; private set; } = "PM2,PM3,PM4";

    public static int EtchProcessTicks { get; private set; } = 120;
    public static int StripProcessTicks { get; private set; } = 28;
    public static int AlignProcessTicks { get; private set; } = 2;

    static AppSettings() => ReloadFromDisk();

    public static void ReloadFromDisk()
    {
        try
        {
            Apply(AppSettingsPersistence.Load());
        }
        catch
        {
            // 기본값 유지
        }
    }

    internal static void Apply(AppSettingsSnapshot snapshot)
    {
        FlaskBaseUrl = snapshot.FlaskBaseUrl.Trim().TrimEnd('/');
        AdsPort = snapshot.AdsPort > 0 ? snapshot.AdsPort : PlcAdsService.DefaultPort;
        SimulationEnabled = snapshot.SimulationEnabled;

        InterlockThresholds il = snapshot.Interlock;
        PressureMtorrMin = il.PressureMtorrMin;
        PressureMtorrMax = il.PressureMtorrMax;
        VibrationGMax = il.VibrationGMax;
        TempCMin = il.TempCMin;
        TempCMax = il.TempCMax;
        HumiMin = il.HumiMin;
        HumiMax = il.HumiMax;
        PressureMtorrAlarmMin = il.PressureMtorrAlarmMin;
        PressureMtorrAlarmMax = il.PressureMtorrAlarmMax;
        VibrationGAlarmMax = il.VibrationGAlarmMax;
        TempCAlarmMin = il.TempCAlarmMin;
        TempCAlarmMax = il.TempCAlarmMax;
        HumiAlarmMin = il.HumiAlarmMin;
        HumiAlarmMax = il.HumiAlarmMax;

        PressureScaleSettings ps = snapshot.PressureScale;
        PressureRawMin = ps.RawMin;
        PressureRawMax = ps.RawMax;
        PressureMtorrAtRawMin = ps.MtorrAtRawMin;
        PressureMtorrAtRawMax = ps.MtorrAtRawMax;
        PressureDecimals = Math.Clamp(ps.Decimals, 0, 3);

        ProcessRecipeSettings recipe = snapshot.ProcessRecipe;
        RecipeId = recipe.RecipeId.Trim();
        RecipeName = recipe.RecipeName.Trim();
        RecipeVersion = recipe.RecipeVersion.Trim();
        RecipeDescription = recipe.Description?.Trim() ?? string.Empty;
        EtchPmSequence = recipe.EtchPmSequence.Trim();
        EtchProcessTicks = recipe.EtchProcessTicks;
        StripProcessTicks = recipe.StripProcessTicks;
        AlignProcessTicks = recipe.AlignProcessTicks;
        ProcessRecipeRuntime.ReloadFromAppSettings();
    }

    /// <summary>가상 이송 시뮬용 용량(레시피 tick + 기본 슬롯·TM 동작).</summary>
    public static EquipmentCapacityConfig CreateCapacityConfig()
    {
        EquipmentCapacityConfig d = EquipmentCapacityConfig.Default;
        return new EquipmentCapacityConfig
        {
            FoupSlotCount = d.FoupSlotCount,
            SideStorageSlotCount = d.SideStorageSlotCount,
            AlignerSlotCount = d.AlignerSlotCount,
            LoadLockSlotCount = d.LoadLockSlotCount,
            VacuumBladeSlotCount = d.VacuumBladeSlotCount,
            EfemBladeSlotCount = d.EfemBladeSlotCount,
            EtchProcessTicks = EtchProcessTicks,
            StripProcessTicks = StripProcessTicks,
            AlignProcessTicks = AlignProcessTicks,
            VacuumMotionStepsPerUiTick = d.VacuumMotionStepsPerUiTick,
            VacuumMoveTicks = d.VacuumMoveTicks,
            VacuumDoorTicks = d.VacuumDoorTicks,
            VacuumExtendTicks = d.VacuumExtendTicks,
            VacuumGripTicks = d.VacuumGripTicks,
            VacuumRotateTicks = d.VacuumRotateTicks,
            VacuumRotateStepRatio = d.VacuumRotateStepRatio
        };
    }
}

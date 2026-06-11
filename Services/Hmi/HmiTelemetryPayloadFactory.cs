using etch_ui.Configuration;
using etch_ui.Equipment.Models;

namespace etch_ui.Services.Hmi;

/// <summary>Flask sensor-data 페이로드 조립 (MainWindow·계약 테스트 공용).</summary>
public static class HmiTelemetryPayloadFactory
{
    public static EtchTelemetryPayload Create(
        string dataSource,
        bool sensorsLive,
        bool benchMode,
        bool maintenanceMode,
        bool connected,
        string equipmentState,
        string? alarmCode,
        bool interlockOk,
        string? username,
        double temperature,
        double humidity,
        double pressureMtorr,
        double vibration,
        bool accessSafe,
        IReadOnlyList<ModuleTelemetryModule> modules,
        ProcessRecipeTelemetry recipe) =>
        new()
        {
            EquipmentId = 1,
            PowerOn = true,
            Connected = connected,
            SensorsLive = sensorsLive,
            DataSource = dataSource,
            BenchMode = benchMode,
            MaintenanceMode = maintenanceMode,
            LastUpdate = DateTime.UtcNow.ToString("o"),
            Temperature = temperature,
            Humidity = humidity,
            Pressure = pressureMtorr,
            Vibration = vibration,
            AccessSafe = accessSafe,
            EquipmentState = equipmentState,
            AlarmCode = alarmCode,
            InterlockOk = interlockOk,
            Username = username,
            Modules = modules.ToList(),
            Recipe = recipe
        };

    public static List<ModuleTelemetryModule> FromModuleSnapshots(IEnumerable<ModuleStateSnapshot> snapshots) =>
        snapshots.Select(m => new ModuleTelemetryModule
        {
            Id = m.Id,
            State = m.StateText,
            DoorClosed = m.DoorClosed,
            HasWafer = m.HasWafer,
            Detail = m.Detail
        }).ToList();

    public static ProcessRecipeTelemetry DefaultRecipe() =>
        new()
        {
            Id = ProcessRecipeRuntime.Active.Id,
            Name = ProcessRecipeRuntime.Active.Name,
            Version = ProcessRecipeRuntime.Active.Version,
            EtchPmSequence = ProcessRecipePmMapping.FormatSequence(ProcessRecipeRuntime.Active.EtchPmIds),
            EtchProcessTicks = ProcessRecipeRuntime.Active.EtchProcessTicks,
            StripProcessTicks = ProcessRecipeRuntime.Active.StripProcessTicks,
            AlignProcessTicks = ProcessRecipeRuntime.Active.AlignProcessTicks
        };
}

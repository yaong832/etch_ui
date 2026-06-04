using System.Globalization;
using System.IO;
using System.Text.Json;
using etch_ui.Equipment.Models;

namespace etch_ui.Services;

/// <summary>
/// HMI 런타임 스냅샷을 JSONL로 저장해 오프라인 학습 데이터셋으로 변환한다.
/// </summary>
public sealed class AiTrainingDataRecorder
{
    private readonly string _outputPath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AiTrainingDataRecorder(string outputPath)
    {
        _outputPath = outputPath;
        string? dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    public string OutputPath => _outputPath;

    public void Append(SnapshotInput input)
    {
        ModuleStateSnapshot[] modules = input.Modules.ToArray();
        SnapshotRow row = new()
        {
            TimestampUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            EquipmentState = input.EquipmentState,
            AlarmCode = input.AlarmCode,
            InterlockOk = input.InterlockOk,
            BenchMode = input.BenchMode,
            Temperature = input.Temperature,
            Humidity = input.Humidity,
            Pressure = input.Pressure,
            Vibration = input.Vibration,
            AccessSafe = input.AccessSafe,
            ModuleRunningCount = modules.Count(m => m.State == ModuleOperationalState.Running),
            ModuleAlarmCount = modules.Count(m => m.State == ModuleOperationalState.Alarm),
            ModuleProcessingCount = modules.Count(m => m.State == ModuleOperationalState.Processing),
            ChamberProcessingCount = modules.Count(m =>
                (m.ModuleId == EquipmentModuleId.Pm1
                 || m.ModuleId == EquipmentModuleId.Pm2
                 || m.ModuleId == EquipmentModuleId.Pm3
                 || m.ModuleId == EquipmentModuleId.Pm4)
                && m.State == ModuleOperationalState.Processing),
            Modules = modules.Select(m => new SnapshotModule
            {
                Id = m.Id.ToString(),
                State = m.StateText,
                DoorClosed = m.DoorClosed,
                HasWafer = m.HasWafer ?? false,
                Detail = m.Detail
            }).ToArray()
        };

        string json = JsonSerializer.Serialize(row, JsonOptions);
        File.AppendAllText(_outputPath, json + Environment.NewLine);
    }

    public sealed class SnapshotInput
    {
        public required string EquipmentState { get; init; }
        public string? AlarmCode { get; init; }
        public required bool InterlockOk { get; init; }
        public required bool BenchMode { get; init; }
        public required double Temperature { get; init; }
        public required double Humidity { get; init; }
        public required double Pressure { get; init; }
        public required double Vibration { get; init; }
        public required bool AccessSafe { get; init; }
        public required IReadOnlyList<ModuleStateSnapshot> Modules { get; init; }
    }

    private sealed class SnapshotRow
    {
        public required string TimestampUtc { get; init; }
        public required string EquipmentState { get; init; }
        public string? AlarmCode { get; init; }
        public required bool InterlockOk { get; init; }
        public required bool BenchMode { get; init; }
        public required double Temperature { get; init; }
        public required double Humidity { get; init; }
        public required double Pressure { get; init; }
        public required double Vibration { get; init; }
        public required bool AccessSafe { get; init; }
        public required int ModuleRunningCount { get; init; }
        public required int ModuleAlarmCount { get; init; }
        public required int ModuleProcessingCount { get; init; }
        public required int ChamberProcessingCount { get; init; }
        public required SnapshotModule[] Modules { get; init; }
    }

    private sealed class SnapshotModule
    {
        public required string Id { get; init; }
        public required string State { get; init; }
        public bool? DoorClosed { get; init; }
        public bool HasWafer { get; init; }
        public string? Detail { get; init; }
    }
}

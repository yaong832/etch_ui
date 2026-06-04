using etch_ui.Equipment.Models;

namespace etch_ui.Services.Scheduling;

public sealed class TransferJob
{
    public required WaferTrack Wafer { get; init; }
    public required EquipmentRegion Pickup { get; init; }
    public required EquipmentRegion Dropoff { get; init; }

    /// <summary>블레이드 슬롯 (0=A, 1=B). 듀얼 블레이드 시 스케줄러가 지정.</summary>
    public int BladeSlotIndex { get; init; }
}

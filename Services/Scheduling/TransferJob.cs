using etch_ui.Equipment.Models;

namespace etch_ui.Services.Scheduling;

public sealed class TransferJob
{
    public required WaferTrack Wafer { get; init; }
    public required EquipmentRegion Pickup { get; init; }
    public required EquipmentRegion Dropoff { get; init; }

    /// <summary>미사용(-1). 슬롯은 시뮬 실행 시 PickNearestFreeBlade로 확정 → ResolvedBladeSlot.</summary>
    public int BladeSlotIndex { get; init; } = VacuumDualBladePlanner.UnresolvedBladeSlot;

    public int ResolvedBladeSlot { get; set; } = -1;
}

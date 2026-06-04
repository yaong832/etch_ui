using etch_ui.Equipment.Models;

namespace etch_ui.Services.Scheduling;

/// <summary>PM1~4 · Aligner · BM 슬롯 (1매).</summary>
public sealed class PmChamberState
{
    public EquipmentRegion Region { get; init; }
    public bool IsEtchPm { get; init; }

    public WaferTrack? CurrentWafer { get; set; }
    public int RemainingProcessTicks { get; set; }
    public bool PickupScheduled { get; set; }
    public bool ReservedForIncoming { get; set; }

    public bool IsEmpty => CurrentWafer is null && !ReservedForIncoming;

    public bool IsReadyForPickup =>
        CurrentWafer is not null && RemainingProcessTicks <= 0 && !PickupScheduled;

    public void ClearWafer()
    {
        CurrentWafer = null;
        RemainingProcessTicks = 0;
        PickupScheduled = false;
    }
}

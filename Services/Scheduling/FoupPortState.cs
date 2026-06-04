using etch_ui.Equipment.Models;

namespace etch_ui.Services.Scheduling;

/// <summary>LP별 FOUP 재고·장내 유입 매수.</summary>
public sealed class FoupPortState
{
    public LoadPortId PortId { get; init; }
    public EquipmentRegion FoupRegion { get; init; }
    public EquipmentRegion NextProcessFoupRegion { get; init; }

    public bool IsMounted { get; set; } = true;
    public int RemainingInFoup { get; set; }
    public int InFlightCount { get; set; }
    public int LotGeneration { get; set; }

    public FoupPortState(LoadPortId portId)
    {
        PortId = portId;
        (FoupRegion, NextProcessFoupRegion) = portId switch
        {
            LoadPortId.Lp1 => (EquipmentRegion.FoupA, EquipmentRegion.NextProcessFoupA),
            LoadPortId.Lp2 => (EquipmentRegion.FoupB, EquipmentRegion.NextProcessFoupB),
            LoadPortId.Lp3 => (EquipmentRegion.FoupC, EquipmentRegion.NextProcessFoupC),
            _ => (EquipmentRegion.FoupA, EquipmentRegion.NextProcessFoupA)
        };
    }

    public void OnWaferPickedFromFoup()
    {
        if (RemainingInFoup > 0)
        {
            RemainingInFoup--;
        }

        InFlightCount++;
    }

    public void OnWaferLeftClusterToNextProcess()
    {
        InFlightCount = Math.Max(0, InFlightCount - 1);
    }

    public void OnNewFoupMounted(int slotCount)
    {
        LotGeneration++;
        RemainingInFoup = slotCount;
        IsMounted = true;
    }
}

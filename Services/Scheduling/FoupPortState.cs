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
    /// <summary>스케줄됐으나 아직 EFEM 그립 전(카세트 내 물리 매수).</summary>
    public int ReservedForPickupCount { get; set; }
    public int InFlightCount { get; set; }
    public int LotGeneration { get; set; }

    public int PhysicallyInFoup => RemainingInFoup + ReservedForPickupCount;

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

    /// <summary>스케줄 시 FOUP 잔량 차감 + 픽업 예약 (InFlight는 실제 그립 시).</summary>
    public bool OnWaferReservedFromFoup()
    {
        if (RemainingInFoup <= 0)
        {
            return false;
        }

        RemainingInFoup--;
        ReservedForPickupCount++;
        return true;
    }

    public void OnWaferPickedFromFoup()
    {
        if (ReservedForPickupCount > 0)
        {
            ReservedForPickupCount--;
        }

        InFlightCount++;
    }

    public void OnWaferPickupReservationReleased()
    {
        if (ReservedForPickupCount > 0)
        {
            ReservedForPickupCount--;
            RemainingInFoup++;
        }
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

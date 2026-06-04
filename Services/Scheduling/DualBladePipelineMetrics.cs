namespace etch_ui.Services.Scheduling;

/// <summary>듀얼 블레이드 파이프라인 시뮬 검증용 카운터.</summary>
public sealed class DualBladePipelineMetrics
{
    public int MaxBladesOccupied { get; set; }
    public int ChainPickupCount { get; set; }
    public int RotateBladeCount { get; set; }
    public int SlotAPlaceCount { get; set; }
    public int SlotBPlaceCount { get; set; }
    public int DualBatchEnqueueCount { get; set; }

    public bool BothSlotsUsed => SlotAPlaceCount > 0 && SlotBPlaceCount > 0;

    public void OnBladePlace(int slotIndex, int occupiedAfter)
    {
        if (slotIndex == 0)
        {
            SlotAPlaceCount++;
        }
        else if (slotIndex == 1)
        {
            SlotBPlaceCount++;
        }

        MaxBladesOccupied = Math.Max(MaxBladesOccupied, occupiedAfter);
    }

    public void Reset()
    {
        MaxBladesOccupied = 0;
        ChainPickupCount = 0;
        RotateBladeCount = 0;
        SlotAPlaceCount = 0;
        SlotBPlaceCount = 0;
        DualBatchEnqueueCount = 0;
    }
}

using etch_ui.Equipment.Models;

namespace etch_ui.Services.Scheduling;

/// <summary>
/// PM2~4 식각 파이프라인 — 병렬 챔버를 순차적으로 채우고, 가운데 공석을 우선 메움.
/// </summary>
public static class EtchPmSelector
{
    private static readonly EquipmentRegion[] EtchOrder =
    [
        EquipmentRegion.ChamberB,
        EquipmentRegion.ChamberC,
        EquipmentRegion.ChamberD
    ];

    public static bool IsEtchRegion(EquipmentRegion region) =>
        region is EquipmentRegion.ChamberB or EquipmentRegion.ChamberC or EquipmentRegion.ChamberD;

    /// <summary>라인에 웨이퍼·진행 중 공정이 있으면 true (UI·램프 보조).</summary>
    public static bool IsEtchLineEngaged(ClusterEquipmentState state, bool globalRunning)
    {
        if (!globalRunning)
        {
            return false;
        }

        foreach (EquipmentRegion region in EtchOrder)
        {
            if (IsBusy(state.Chambers, region))
            {
                return true;
            }
        }

        if (state.AlignerBuffer.HasWafer || state.LoadLockBuffer.HasWafer)
        {
            return true;
        }

        foreach (FoupPortState port in state.FoupPorts)
        {
            if (port.RemainingInFoup > 0 || port.InFlightCount > 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>BM→Etch 투입 · Ready 표시용 — 다음 1슬롯만 반환.</summary>
    public static EquipmentRegion? SelectNextPipelineTarget(IReadOnlyDictionary<EquipmentRegion, PmChamberState> chambers)
    {
        bool pm2Busy = IsBusy(chambers, EquipmentRegion.ChamberB);
        bool pm3Busy = IsBusy(chambers, EquipmentRegion.ChamberC);
        bool pm4Busy = IsBusy(chambers, EquipmentRegion.ChamberD);

        if (pm2Busy && pm3Busy && pm4Busy)
        {
            return null;
        }

        // 2·3 가동 → 4,  2·4 가동 → 3,  3·4 가동 → 2
        if (pm2Busy && pm3Busy && CanAccept(chambers, EquipmentRegion.ChamberD))
        {
            return EquipmentRegion.ChamberD;
        }

        if (pm2Busy && pm4Busy && CanAccept(chambers, EquipmentRegion.ChamberC))
        {
            return EquipmentRegion.ChamberC;
        }

        if (pm3Busy && pm4Busy && CanAccept(chambers, EquipmentRegion.ChamberB))
        {
            return EquipmentRegion.ChamberB;
        }

        // 2만 가동 → 3,  3만 가동 → 2,  4만 가동 → 2
        if (pm2Busy && !pm3Busy && CanAccept(chambers, EquipmentRegion.ChamberC))
        {
            return EquipmentRegion.ChamberC;
        }

        if (!pm2Busy && pm3Busy && !pm4Busy && CanAccept(chambers, EquipmentRegion.ChamberB))
        {
            return EquipmentRegion.ChamberB;
        }

        if (!pm2Busy && !pm3Busy && pm4Busy && CanAccept(chambers, EquipmentRegion.ChamberB))
        {
            return EquipmentRegion.ChamberB;
        }

        // 모두 비었거나 2·3·4 중 일부만 비어 시작/재가동 → 2 우선
        if (CanAccept(chambers, EquipmentRegion.ChamberB))
        {
            return EquipmentRegion.ChamberB;
        }

        return null;
    }

    public static bool IsNextPipelineReadySlot(EquipmentRegion region, ClusterEquipmentState state, bool globalRunning) =>
        globalRunning
        && IsEtchLineEngaged(state, globalRunning)
        && SelectNextPipelineTarget(state.Chambers) == region;

    public static bool HasPipelineEtchCapacity(IReadOnlyDictionary<EquipmentRegion, PmChamberState> chambers) =>
        SelectNextPipelineTarget(chambers) is not null;

    public static bool IsBusy(IReadOnlyDictionary<EquipmentRegion, PmChamberState> chambers, EquipmentRegion region)
    {
        if (!chambers.TryGetValue(region, out PmChamberState? ch))
        {
            return false;
        }

        return ch.CurrentWafer is not null || ch.ReservedForIncoming;
    }

    private static bool CanAccept(IReadOnlyDictionary<EquipmentRegion, PmChamberState> chambers, EquipmentRegion region) =>
        chambers.TryGetValue(region, out PmChamberState? ch) && ch.IsEmpty;
}

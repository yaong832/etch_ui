using etch_ui.Equipment.Models;

namespace etch_ui.Services.Scheduling;

/// <summary>
/// 진공 TM BM(Pre-Etch) 인입 제한 — Strip(→PM1) 대기와 듀얼 블레이드 Pre-Etch 병행을 구분.
/// </summary>
public static class VacuumInboundPolicy
{
    /// <summary>
    /// true면 BM→Etch 스케줄을 막음 (PM1 Strip 회수·듀얼 적재 중 Etch 완료 매는 예외).
    /// </summary>
    public static bool ShouldRestrictBmPickup(
        RobotBladeSlots blades,
        int bladeCapacity,
        IEnumerable<TransferJob> pendingDropoffs,
        IEnumerable<TransferJob> queuedJobs)
    {
        if (bladeCapacity >= 2)
        {
            // 듀얼: 빈 슬롯이 있으면 PM1 Strip 대기·Etch완료 적재와 무관하게 BM→Etch 인입 허용
            return CountInboundBmSlots(blades, queuedJobs) >= bladeCapacity;
        }

        if (pendingDropoffs.Any(j => j.Dropoff == EquipmentRegion.ChamberA))
        {
            return true;
        }

        if (BladesHoldEtchComplete(blades))
        {
            return true;
        }

        return blades.OccupiedCount > 0
            || pendingDropoffs.Any()
            || HasInboundBmJob(queuedJobs);
    }

    private static bool BladesHoldEtchComplete(RobotBladeSlots blades)
    {
        for (int i = 0; i < blades.Capacity; i++)
        {
            if (blades.Get(i)?.HasCompletedEtch == true)
            {
                return true;
            }
        }

        return false;
    }

    private static int CountInboundBmSlots(RobotBladeSlots blades, IEnumerable<TransferJob> queuedJobs) =>
        blades.OccupiedCount + CountQueuedBmPickupReservations(blades, queuedJobs);

    private static int CountQueuedBmPickupReservations(RobotBladeSlots blades, IEnumerable<TransferJob> queuedJobs)
    {
        int n = 0;
        foreach (TransferJob job in queuedJobs)
        {
            if (job.Pickup != EquipmentRegion.LoadLock)
            {
                continue;
            }

            if (job.ResolvedBladeSlot >= 0)
            {
                continue;
            }

            if (VacuumDualBladePlanner.WaferOnBlades(blades, job.Wafer))
            {
                continue;
            }

            n++;
        }

        return n;
    }

    private static bool HasInboundBmJob(IEnumerable<TransferJob> queuedJobs) =>
        queuedJobs.Any(j => j.Pickup == EquipmentRegion.LoadLock);
}

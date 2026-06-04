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
        if (pendingDropoffs.Any(j => j.Dropoff == EquipmentRegion.ChamberA))
        {
            return true;
        }

        if (BladesHoldEtchComplete(blades))
        {
            return true;
        }

        if (bladeCapacity < 2)
        {
            return blades.OccupiedCount > 0
                || pendingDropoffs.Any()
                || HasInboundBmJob(queuedJobs);
        }

        int inboundReserved = CountInboundBmSlots(blades, queuedJobs);
        return inboundReserved >= bladeCapacity;
    }

    /// <summary>BM→Etch 신규 Job에 할당할 슬롯 (뒤·A 우선).</summary>
    public static int PickBmToEtchBladeSlot(RobotBladeSlots blades, int bladeCapacity, IEnumerable<TransferJob> queuedJobs)
    {
        if (bladeCapacity < 2)
        {
            return VacuumDualBladePlanner.FrontBladeSlot;
        }

        if (!blades.HasWafer(VacuumDualBladePlanner.BackBladeSlot)
            && !QueueReservesSlot(queuedJobs, VacuumDualBladePlanner.BackBladeSlot))
        {
            return VacuumDualBladePlanner.BackBladeSlot;
        }

        return VacuumDualBladePlanner.FrontBladeSlot;
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

    private static int CountInboundBmSlots(RobotBladeSlots blades, IEnumerable<TransferJob> queuedJobs)
    {
        int n = 0;
        for (int slot = 0; slot < blades.Capacity; slot++)
        {
            if (blades.HasWafer(slot) || QueueReservesSlot(queuedJobs, slot))
            {
                n++;
            }
        }

        return n;
    }

    private static bool QueueReservesSlot(IEnumerable<TransferJob> queuedJobs, int slot) =>
        queuedJobs.Any(j => j.Pickup == EquipmentRegion.LoadLock && j.BladeSlotIndex == slot);

    private static bool HasInboundBmJob(IEnumerable<TransferJob> queuedJobs) =>
        queuedJobs.Any(j => j.Pickup == EquipmentRegion.LoadLock);
}

using etch_ui.Equipment.Models;

namespace etch_ui.Services.Scheduling;

/// <summary>Load Lock(BM)은 EFEM·진공 TM이 동시에 접근하지 않도록 상호 배제 (진행 중 job만).</summary>
public static class LoadLockTransferGate
{
    public static bool CanSchedule(
        TransferRobotKind requester,
        TransferJob? efemActive,
        TransferJob? vacuumActive,
        IEnumerable<TransferJob>? efemQueued = null,
        IEnumerable<TransferJob>? vacuumQueued = null)
    {
        _ = efemQueued;
        _ = vacuumQueued;

        if (requester != TransferRobotKind.EfemAtmospheric && InvolvesLoadLock(vacuumActive))
        {
            return false;
        }

        if (requester != TransferRobotKind.VacuumTm && InvolvesLoadLock(efemActive))
        {
            return false;
        }

        return true;
    }

    public static bool InvolvesLoadLock(TransferJob? job) =>
        job is not null
        && (job.Pickup == EquipmentRegion.LoadLock || job.Dropoff == EquipmentRegion.LoadLock);
}

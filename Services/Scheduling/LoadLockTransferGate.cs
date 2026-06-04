using etch_ui.Equipment.Models;

namespace etch_ui.Services.Scheduling;

/// <summary>Load Lock(BM)은 EFEM·진공 TM이 동시에 접근하지 않도록 상호 배제.</summary>
public static class LoadLockTransferGate
{
    public static bool CanSchedule(
        TransferRobotKind requester,
        TransferJob? efemActive,
        TransferJob? vacuumActive,
        IEnumerable<TransferJob>? efemQueued = null,
        IEnumerable<TransferJob>? vacuumQueued = null)
    {
        if (requester != TransferRobotKind.EfemAtmospheric
            && (InvolvesLoadLock(efemActive) || AnyInvolvesLoadLock(efemQueued)))
        {
            return false;
        }

        if (requester != TransferRobotKind.VacuumTm
            && (InvolvesLoadLock(vacuumActive) || AnyInvolvesLoadLock(vacuumQueued)))
        {
            return false;
        }

        return true;
    }

    public static bool InvolvesLoadLock(TransferJob? job) =>
        job is not null
        && (job.Pickup == EquipmentRegion.LoadLock || job.Dropoff == EquipmentRegion.LoadLock);

    private static bool AnyInvolvesLoadLock(IEnumerable<TransferJob>? jobs)
    {
        if (jobs is null)
        {
            return false;
        }

        foreach (TransferJob job in jobs)
        {
            if (InvolvesLoadLock(job))
            {
                return true;
            }
        }

        return false;
    }
}

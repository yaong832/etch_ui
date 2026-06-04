using etch_ui.Equipment.Models;

namespace etch_ui.Services.Scheduling;

/// <summary>
/// Load Lock 좌측 · EFEM TM: FOUP → Aligner(5) → BM(2) → Side Stg(25) → 카세트 교체 출하.
/// </summary>
public sealed class EfemTransferScheduler
{
    public string LastHint { get; private set; } = "EFEM · 대기";

    public int TryScheduleOne(
        ClusterEquipmentState state,
        Queue<TransferJob> queue,
        TransferJob? activeJob,
        TransferJob? vacuumActiveJob,
        IEnumerable<TransferJob>? vacuumQueued = null)
    {
        if (activeJob is not null || queue.Count > 0)
        {
            return 0;
        }

        state.PickScheduler.RefreshFreshMountBlocks();

        if (TryScheduleSideStorageCassetteSwap(state))
        {
            return 1;
        }

        if (TryScheduleBmToSideStorage(state, queue, vacuumActiveJob, vacuumQueued))
        {
            return 1;
        }

        if (TryScheduleAlignerToBm(state, queue, vacuumActiveJob, vacuumQueued))
        {
            return 1;
        }

        if (state.SideStorage.IsFull)
        {
            LastHint = "EFEM · Side Stg 25/25 · 카세트 교체 대기";
            return 0;
        }

        if (TryScheduleFoupToAligner(state, queue))
        {
            return 1;
        }

        LastHint = "EFEM · 대기";
        return 0;
    }

    private bool TryScheduleSideStorageCassetteSwap(ClusterEquipmentState state)
    {
        if (!state.SideStorage.IsFull)
        {
            return false;
        }

        int shipped = state.PerformSideStorageCassetteSwap();
        if (shipped <= 0)
        {
            return false;
        }

        LastHint = $"EFEM Side Stg 카세트 교체 · {shipped}매 출하 (새 카세트)";
        return true;
    }

    private bool TryScheduleBmToSideStorage(
        ClusterEquipmentState state,
        Queue<TransferJob> queue,
        TransferJob? vacuumActiveJob,
        IEnumerable<TransferJob>? vacuumQueued)
    {
        if (!LoadLockTransferGate.CanSchedule(
                TransferRobotKind.EfemAtmospheric,
                null,
                vacuumActiveJob,
                vacuumQueued: vacuumQueued))
        {
            return false;
        }

        if (!state.LoadLockBuffer.TryPeekReadyWhere(w => w.HasCompletedStrip, out WaferTrack? w))
        {
            return false;
        }

        if (state.SideStorage.IsFull)
        {
            LastHint = "EFEM · Side Stg 만석 · BM→Side HOLD";
            return false;
        }

        if (!state.LoadLockBuffer.TryMarkPickupScheduled(w) || !state.SideStorage.TryEnqueue(w))
        {
            return false;
        }

        Enqueue(queue, EquipmentRegion.LoadLock, EquipmentRegion.SideStorage, w);
        LastHint = $"EFEM BM → Side Stg (#{w.Id})";
        return true;
    }

    private bool TryScheduleAlignerToBm(
        ClusterEquipmentState state,
        Queue<TransferJob> queue,
        TransferJob? vacuumActiveJob,
        IEnumerable<TransferJob>? vacuumQueued)
    {
        if (state.LoadLockBuffer.IsFull)
        {
            LastHint = "EFEM · BM 만석 HOLD";
            return false;
        }

        if (!LoadLockAdmissionPolicy.CanAcceptPreEtchFromAligner(state, out string? blockReason))
        {
            LastHint = blockReason is null ? "EFEM · BM HOLD" : $"EFEM · BM HOLD ({blockReason})";
            return false;
        }

        if (!LoadLockTransferGate.CanSchedule(
                TransferRobotKind.EfemAtmospheric,
                null,
                vacuumActiveJob,
                vacuumQueued: vacuumQueued))
        {
            return false;
        }

        if (!state.AlignerBuffer.TryPeekReady(out WaferTrack? w))
        {
            return false;
        }

        if (!state.AlignerBuffer.TryMarkPickupScheduled(w))
        {
            return false;
        }

        Enqueue(queue, EquipmentRegion.Aligner, EquipmentRegion.LoadLock, w);
        LastHint = $"EFEM Aligner → BM ({state.AlignerBuffer.Count}/{state.Capacity.AlignerSlotCount})";
        return true;
    }

    private bool TryScheduleFoupToAligner(ClusterEquipmentState state, Queue<TransferJob> queue)
    {
        if (state.AlignerBuffer.IsFull)
        {
            return false;
        }

        FoupPortState? port = state.PickScheduler.SelectNextPickSource();
        if (port is null)
        {
            LastHint = "EFEM · 픽업 FOUP 없음";
            return false;
        }

        port.OnWaferPickedFromFoup();
        var wafer = new WaferTrack(port.PortId, port.NextProcessFoupRegion);
        Enqueue(queue, port.FoupRegion, EquipmentRegion.Aligner, wafer);
        LastHint = $"EFEM LP{(int)port.PortId + 1} → Aligner (#{wafer.Id})";
        return true;
    }

    private static void Enqueue(Queue<TransferJob> queue, EquipmentRegion pickup, EquipmentRegion dropoff, WaferTrack wafer) =>
        queue.Enqueue(new TransferJob { Wafer = wafer, Pickup = pickup, Dropoff = dropoff });
}

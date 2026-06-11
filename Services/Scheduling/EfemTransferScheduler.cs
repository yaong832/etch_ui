using etch_ui.Equipment.Models;

namespace etch_ui.Services.Scheduling;

/// <summary>
/// Load Lock 좌측 · EFEM TM: FOUP → Aligner → BM(3: Pre2+Strip1) → Side Stg → 카세트 교체 출하.
/// </summary>
public sealed class EfemTransferScheduler
{
    private const double EfemDefaultFacing = -90;

    public string LastHint { get; private set; } = "EFEM · 대기";

    public int TryScheduleOne(
        ClusterEquipmentState state,
        Queue<TransferJob> queue,
        TransferJob? activeJob,
        TransferJob? vacuumActiveJob,
        IEnumerable<TransferJob>? vacuumQueued = null,
        bool queuedWorkBlocksScheduling = false,
        RobotBladeSlots? efemBlades = null,
        int efemBladeCapacity = 1,
        IEnumerable<TransferJob>? efemPendingDropoffs = null)
    {
        if (activeJob is not null)
        {
            return 0;
        }

        if (queuedWorkBlocksScheduling)
        {
            return 0;
        }

        state.PickScheduler.RefreshFreshMountBlocks();

        if (TryScheduleSideStorageCassetteSwap(state))
        {
            return 1;
        }

        if (TryScheduleBmToSideStorage(state, queue, vacuumActiveJob, vacuumQueued, efemBlades, efemBladeCapacity))
        {
            return 1;
        }

        // Aligner·BM Pre-Etch 모두 만석 → FOUP는 듀얼 블레이드 대기 적재.
        if (!state.SideStorage.IsFull
            && TryScheduleFoupBladeBuffer(
                state,
                queue,
                efemBlades,
                efemBladeCapacity,
                efemPendingDropoffs))
        {
            return 1;
        }

        if (!LoadLockAdmissionPolicy.IsPreEtchBmFull(state)
            && TryScheduleAlignerToBm(state, queue, vacuumActiveJob, vacuumQueued, efemBlades, efemBladeCapacity))
        {
            return 1;
        }

        if (!state.SideStorage.IsFull
            && TryScheduleFoupToAligner(state, queue, efemBlades, efemBladeCapacity))
        {
            return 1;
        }

        if (LoadLockAdmissionPolicy.IsPreEtchBmFull(state) && state.AlignerBuffer.IsFull)
        {
            LastHint = "EFEM · BM Pre 2/2 · Aligner 만석 · 블레이드 대기";
        }
        else if (LoadLockAdmissionPolicy.IsPreEtchBmFull(state) && state.AlignerBuffer.TryPeekReady(out _))
        {
            LastHint = "EFEM · BM Pre 2/2 · Aligner 완료 대기";
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
        IEnumerable<TransferJob>? vacuumQueued,
        RobotBladeSlots? efemBlades,
        int efemBladeCapacity)
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

        if (!VacuumDualBladePlanner.HasFreeBladeForPickup(efemBlades, efemBladeCapacity, queue))
        {
            LastHint = "EFEM · 블레이드 만석 · BM→Side HOLD";
            return false;
        }

        if (!state.LoadLockBuffer.TryMarkPickupScheduled(w))
        {
            return false;
        }

        Enqueue(queue, EquipmentRegion.LoadLock, EquipmentRegion.SideStorage, w);
        string slotTag = VacuumDualBladePlanner.PredictNearestSlotLabel(
            EquipmentRegion.LoadLock, TransferRobotKind.EfemAtmospheric, EfemDefaultFacing, efemBlades, efemBladeCapacity);
        LastHint = $"EFEM BM → Side Stg (#{w.Id}){slotTag}";
        return true;
    }

    private bool TryScheduleAlignerToBm(
        ClusterEquipmentState state,
        Queue<TransferJob> queue,
        TransferJob? vacuumActiveJob,
        IEnumerable<TransferJob>? vacuumQueued,
        RobotBladeSlots? efemBlades,
        int efemBladeCapacity)
    {
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

        if (!VacuumDualBladePlanner.HasFreeBladeForPickup(efemBlades, efemBladeCapacity, queue))
        {
            LastHint = "EFEM · 블레이드 만석 · Aligner→BM HOLD";
            return false;
        }

        if (!state.AlignerBuffer.TryMarkPickupScheduled(w))
        {
            return false;
        }

        Enqueue(queue, EquipmentRegion.Aligner, EquipmentRegion.LoadLock, w);
        string slotTag = VacuumDualBladePlanner.PredictNearestSlotLabel(
            EquipmentRegion.Aligner, TransferRobotKind.EfemAtmospheric, EfemDefaultFacing, efemBlades, efemBladeCapacity);
        LastHint = $"EFEM Aligner → BM{slotTag} ({state.AlignerBuffer.Count}/{state.Capacity.AlignerSlotCount})";
        return true;
    }

    private bool TryScheduleFoupToAligner(
        ClusterEquipmentState state,
        Queue<TransferJob> queue,
        RobotBladeSlots? efemBlades,
        int efemBladeCapacity)
    {
        if (state.AlignerBuffer.IsFull)
        {
            LastHint = state.AlignerBuffer.HasWafer
                ? "EFEM · Aligner 적재 중 · FOUP HOLD"
                : "EFEM · Aligner 만석 · FOUP HOLD";
            return false;
        }

        if (!VacuumDualBladePlanner.HasFreeBladeForPickup(efemBlades, efemBladeCapacity, queue))
        {
            LastHint = "EFEM · 블레이드 만석 · FOUP HOLD";
            return false;
        }

        FoupPortState? port = state.PickScheduler.SelectNextPickSource();
        if (port is null)
        {
            LastHint = "EFEM · 픽업 FOUP 없음";
            return false;
        }

        port.OnWaferReservedFromFoup();
        var wafer = new WaferTrack(port.PortId, port.NextProcessFoupRegion);
        Enqueue(queue, port.FoupRegion, EquipmentRegion.Aligner, wafer);
        LastHint = LoadLockAdmissionPolicy.IsPreEtchBmFull(state)
            ? $"EFEM LP{(int)port.PortId + 1} → Aligner (#{wafer.Id}) · BM Pre 2/2 버퍼"
            : $"EFEM LP{(int)port.PortId + 1} → Aligner (#{wafer.Id})";
        return true;
    }

    /// <summary>Aligner 만석 + BM Pre-Etch 2/2 — FOUP 웨이퍼를 블레이드에 선적재(Aligner 대기).</summary>
    private bool TryScheduleFoupBladeBuffer(
        ClusterEquipmentState state,
        Queue<TransferJob> queue,
        RobotBladeSlots? efemBlades,
        int efemBladeCapacity,
        IEnumerable<TransferJob>? efemPendingDropoffs)
    {
        if (!state.AlignerBuffer.IsFull || !LoadLockAdmissionPolicy.IsPreEtchBmFull(state))
        {
            return false;
        }

        if (state.SideStorage.IsFull)
        {
            return false;
        }

        if (!VacuumDualBladePlanner.HasFreeBladeForPickup(efemBlades, efemBladeCapacity, queue))
        {
            LastHint = "EFEM · 블레이드 만석 · FOUP HOLD";
            return false;
        }

        if (queue.Any(j => j.Dropoff == EquipmentRegion.Aligner))
        {
            return false;
        }

        if (efemPendingDropoffs is not null
            && efemBlades is not null
            && efemBladeCapacity >= 2
            && efemPendingDropoffs.Count(j => j.Dropoff == EquipmentRegion.Aligner) >= efemBlades.Capacity)
        {
            LastHint = "EFEM · 블레이드 대기 중 · FOUP HOLD";
            return false;
        }

        FoupPortState? port = state.PickScheduler.SelectNextPickSource();
        if (port is null)
        {
            return false;
        }

        port.OnWaferReservedFromFoup();
        var wafer = new WaferTrack(port.PortId, port.NextProcessFoupRegion);
        Enqueue(queue, port.FoupRegion, EquipmentRegion.Aligner, wafer);
        LastHint = $"EFEM LP{(int)port.PortId + 1} → 블레이드 대기 (#{wafer.Id}) · Align+BM Pre 만석";
        return true;
    }

    private static void Enqueue(
        Queue<TransferJob> queue,
        EquipmentRegion pickup,
        EquipmentRegion dropoff,
        WaferTrack wafer) =>
        queue.Enqueue(new TransferJob
        {
            Wafer = wafer,
            Pickup = pickup,
            Dropoff = dropoff
        });
}

using etch_ui.Equipment.Models;

namespace etch_ui.Services.Scheduling;

/// <summary>
/// Load Lock 우측 · 진공 TM: BM(2) ↔ PM2~4(Etch), PM2~4 → PM1(Strip) 직행, PM1 완료 → BM.
/// </summary>
public sealed class VacuumTransferScheduler
{
    public string LastHint { get; private set; } = "TM · 대기";

    public int TryScheduleOne(
        ClusterEquipmentState state,
        Queue<TransferJob> queue,
        TransferJob? activeJob,
        TransferJob? efemActiveJob,
        IEnumerable<TransferJob>? efemQueued = null,
        RobotBladeSlots? vacuumBlades = null,
        int vacuumBladeCapacity = 1,
        bool restrictInbound = false,
        IEnumerable<TransferJob>? vacuumPending = null,
        bool queuedWorkBlocksScheduling = false)
    {
        if (activeJob is not null)
        {
            return 0;
        }

        // BM 만석 + PM1 Strip 완료 대기 시 BM→Etch로 슬롯 확보 후 Strip 인입.
        if (state.LoadLockBuffer.IsFull
            && state.Pm1.IsReadyForPickup
            && state.Pm1.CurrentWafer?.HasCompletedStrip == true
            && TryScheduleBmToEtchPm(state, queue, efemActiveJob, efemQueued, vacuumBlades, vacuumBladeCapacity))
        {
            return 1;
        }

        // 듀얼: 블레이드 Strip→BM 대기 + BM 만석 → Pre-Etch 1매 BM 픽업(Etch)으로 슬롯 확보 후 드롭.
        if (vacuumBladeCapacity >= 2
            && vacuumBlades is not null
            && vacuumBlades.FreeCount > 0
            && VacuumDualBladePlanner.HasBlockedStripDropToBm(state, vacuumPending, vacuumBlades)
            && TryScheduleBmToEtchPm(state, queue, efemActiveJob, efemQueued, vacuumBlades, vacuumBladeCapacity))
        {
            return 1;
        }

        if (TrySchedulePmStripToBm(state, queue, efemActiveJob, efemQueued, vacuumBlades, vacuumPending))
        {
            return 1;
        }

        if (queuedWorkBlocksScheduling)
        {
            return 0;
        }

        if (restrictInbound)
        {
            return 0;
        }

        if (TryScheduleEtchPmToPm1Strip(state, queue, vacuumBlades, vacuumBladeCapacity))
        {
            return 1;
        }

        if (TryScheduleBmToEtchPm(state, queue, efemActiveJob, efemQueued, vacuumBlades, vacuumBladeCapacity))
        {
            return 1;
        }

        LastHint = "TM · 대기";
        return 0;
    }

    private bool TrySchedulePmStripToBm(
        ClusterEquipmentState state,
        Queue<TransferJob> queue,
        TransferJob? efemActiveJob,
        IEnumerable<TransferJob>? efemQueued,
        RobotBladeSlots? vacuumBlades,
        IEnumerable<TransferJob>? vacuumPending)
    {
        if (!state.Pm1.IsReadyForPickup || state.Pm1.CurrentWafer is null || !state.Pm1.CurrentWafer.HasCompletedStrip)
        {
            return false;
        }

        if (!LoadLockAdmissionPolicy.CanAcceptAnotherStripBmDrop(
                state,
                additionalSlots: 1,
                blades: vacuumBlades,
                pendingDropoffs: vacuumPending,
                queuedJobs: queue))
        {
            LastHint = "TM · BM Strip 슬롯·예약 만석 HOLD";
            return false;
        }

        if (!LoadLockAdmissionPolicy.CanAcceptStripFromPm1(state, out string? blockReason))
        {
            LastHint = blockReason is null ? "TM · BM HOLD" : $"TM · BM HOLD ({blockReason})";
            return false;
        }

        if (!LoadLockTransferGate.CanSchedule(
                TransferRobotKind.VacuumTm,
                efemActiveJob,
                null,
                efemQueued: efemQueued))
        {
            return false;
        }

        WaferTrack w = state.Pm1.CurrentWafer;
        state.Pm1.PickupScheduled = true;
        Enqueue(queue, EquipmentRegion.ChamberA, EquipmentRegion.LoadLock, w);
        LastHint = $"TM PM1 Strip → BM (#{w.Id})";
        return true;
    }

    private bool TryScheduleEtchPmToPm1Strip(
        ClusterEquipmentState state,
        Queue<TransferJob> queue,
        RobotBladeSlots? vacuumBlades,
        int vacuumBladeCapacity)
    {
        if (vacuumBladeCapacity >= 2 && vacuumBlades is not null)
        {
            int n = VacuumDualBladePlanner.TryScheduleEtchToPm1Batch(state, queue, vacuumBlades, h => LastHint = h);
            return n > 0;
        }

        if (!state.Pm1.IsEmpty)
        {
            LastHint = "TM · PM1 사용 중 (Etch→Strip 대기)";
            return false;
        }

        foreach (EquipmentRegion region in new[] { EquipmentRegion.ChamberB, EquipmentRegion.ChamberC, EquipmentRegion.ChamberD })
        {
            PmChamberState? src = state.GetChamber(region);
            if (src is null || !src.IsReadyForPickup || src.CurrentWafer is null)
            {
                continue;
            }

            WaferTrack w = src.CurrentWafer;
            if (!w.HasCompletedEtch)
            {
                continue;
            }

            src.PickupScheduled = true;
            state.Pm1.ReservedForIncoming = true;
            Enqueue(queue, region, EquipmentRegion.ChamberA, w);
            LastHint = $"TM PM{RegionToPmNumber(region)} → PM1 Strip (#{w.Id})";
            return true;
        }

        return false;
    }

    private bool TryScheduleBmToEtchPm(
        ClusterEquipmentState state,
        Queue<TransferJob> queue,
        TransferJob? efemActiveJob,
        IEnumerable<TransferJob>? efemQueued,
        RobotBladeSlots? vacuumBlades,
        int vacuumBladeCapacity)
    {
        if (!LoadLockTransferGate.CanSchedule(
                TransferRobotKind.VacuumTm,
                efemActiveJob,
                null,
                efemQueued: efemQueued))
        {
            return false;
        }

        if (!state.LoadLockBuffer.TryPeekReadyWhere(w => !w.HasCompletedEtch && !w.HasCompletedStrip, out WaferTrack? w))
        {
            return false;
        }

        EquipmentRegion? etchTarget = EtchPmSelector.SelectNextPipelineTarget(state.Chambers);
        if (etchTarget is null)
        {
            if (state.LoadLockBuffer.CountMatching(w => !w.HasCompletedEtch && !w.HasCompletedStrip) > 0)
            {
                LastHint = "TM · Etch PM 파이프라인 만석 (BM Pre-Etch 대기)";
            }

            return false;
        }

        PmChamberState? dst = state.GetChamber(etchTarget.Value);
        if (dst is null)
        {
            return false;
        }

        if (vacuumBladeCapacity >= 2
            && !VacuumDualBladePlanner.HasFreeBladeForPickup(vacuumBlades, vacuumBladeCapacity, queue))
        {
            LastHint = "TM · 블레이드 만석 · BM→Etch HOLD";
            return false;
        }

        if (!state.LoadLockBuffer.TryMarkPickupScheduled(w))
        {
            return false;
        }

        dst.ReservedForIncoming = true;
        Enqueue(queue, EquipmentRegion.LoadLock, etchTarget.Value, w);
        string slotTag = VacuumDualBladePlanner.PredictNearestSlotLabel(
            EquipmentRegion.LoadLock,
            TransferRobotKind.VacuumTm,
            -125,
            vacuumBlades,
            vacuumBladeCapacity);
        LastHint = $"TM BM → PM{RegionToPmNumber(etchTarget.Value)}{slotTag} (#{w.Id})";
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

    private static int RegionToPmNumber(EquipmentRegion region) => region switch
    {
        EquipmentRegion.ChamberB => 2,
        EquipmentRegion.ChamberC => 3,
        EquipmentRegion.ChamberD => 4,
        EquipmentRegion.ChamberA => 1,
        _ => 0
    };
}

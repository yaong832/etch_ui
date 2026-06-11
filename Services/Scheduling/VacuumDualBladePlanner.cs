using etch_ui.Equipment.Helpers;
using etch_ui.Equipment.Models;

namespace etch_ui.Services.Scheduling;

/// <summary>듀얼 블레이드 — 실행 시점에 가까운 빈 팔 선택, 점유 시 반대 팔.</summary>
public static class VacuumDualBladePlanner
{
    /// <summary>뒤 블레이드(로봇 -X) — 챔버 맞출 때 기준 각도 +180°.</summary>
    public const int BackBladeSlot = 0;

    /// <summary>앞 블레이드(로봇 +X) — 챔버 맞출 때 기준 각도.</summary>
    public const int FrontBladeSlot = 1;

    /// <summary>스케줄 큐 — 슬롯은 시뮬 실행 시 확정.</summary>
    public const int UnresolvedBladeSlot = -1;

    private static readonly EquipmentRegion[] EtchPickOrder =
    [
        EquipmentRegion.ChamberB,
        EquipmentRegion.ChamberC,
        EquipmentRegion.ChamberD
    ];

    public static int BladeCapacity => 2;

    /// <summary>완료 Etch → PM1 Strip Job을 슬롯까지 고려해 최대 2건 enqueue.</summary>
    public static int TryScheduleEtchToPm1Batch(
        ClusterEquipmentState state,
        Queue<TransferJob> queue,
        RobotBladeSlots blades,
        Action<string> setHint)
    {
        if (!state.Pm1.IsEmpty)
        {
            return 0;
        }

        int pipelineRoom = blades.Capacity - blades.OccupiedCount - CountPickupReservations(blades, queue);
        if (pipelineRoom <= 0)
        {
            return 0;
        }

        int scheduled = 0;
        foreach (EquipmentRegion region in EtchPickOrder)
        {
            if (scheduled >= pipelineRoom)
            {
                break;
            }

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

            if (queue.Any(j => j.Pickup == region && j.Wafer.Id == w.Id))
            {
                continue;
            }

            bool placeDirectToPm1 = state.Pm1.IsEmpty && scheduled == 0 && blades.OccupiedCount == 0 && queue.Count == 0;
            if (placeDirectToPm1)
            {
                state.Pm1.ReservedForIncoming = true;
            }

            src.PickupScheduled = true;
            queue.Enqueue(new TransferJob
            {
                Wafer = w,
                Pickup = region,
                Dropoff = EquipmentRegion.ChamberA,
                BladeSlotIndex = UnresolvedBladeSlot
            });
            scheduled++;
        }

        if (scheduled > 0)
        {
            string batchNote = scheduled >= 2 ? " · 듀얼 배치×2" : string.Empty;
            setHint($"TM Etch→PM1 Strip {scheduled}건{batchNote}");
        }

        return scheduled;
    }

    /// <summary>슬롯별 TM 회전각 — 앞(+X)=포트 방위, 뒤(-X)=+180° (EFEM·진공 공통).</summary>
    public static double AngleForBlade(EquipmentRegion faceRegion, TransferRobotKind robot, int bladeSlot)
    {
        double portAngle = RegionAngleHelper.ToDegrees(faceRegion, robot);
        return bladeSlot == BackBladeSlot ? NormalizeAngle(portAngle + 180.0) : portAngle;
    }

    public static double AngleForBlade(EquipmentRegion faceRegion, int bladeSlot) =>
        AngleForBlade(faceRegion, TransferRobotKind.VacuumTm, bladeSlot);

    public static string SlotLabel(int bladeSlot) => bladeSlot == BackBladeSlot ? "뒤·A" : "앞·B";

    /// <summary>목표 모듈에 회전이 가장 적은 빈 블레이드. 한쪽 점유 시 반대 팔만 후보.</summary>
    public static int PickNearestFreeBlade(
        EquipmentRegion faceRegion,
        TransferRobotKind robot,
        double currentFacingDegrees,
        RobotBladeSlots blades,
        int bladeCapacity)
    {
        if (bladeCapacity < 2)
        {
            return FrontBladeSlot;
        }

        int bestSlot = -1;
        double bestDiff = double.MaxValue;
        for (int slot = 0; slot < bladeCapacity; slot++)
        {
            if (blades.HasWafer(slot))
            {
                continue;
            }

            double target = AngleForBlade(faceRegion, robot, slot);
            double diff = Math.Abs(NormalizeAngleDiff(target - currentFacingDegrees));
            if (diff < bestDiff)
            {
                bestDiff = diff;
                bestSlot = slot;
            }
        }

        return bestSlot;
    }

    /// <summary>픽업 Job이 아직 블레이드에 적재되지 않은 큐 예약 수.</summary>
    public static int CountPickupReservations(RobotBladeSlots? blades, IEnumerable<TransferJob> queuedJobs)
    {
        int n = 0;
        foreach (TransferJob job in queuedJobs)
        {
            if (job.ResolvedBladeSlot >= 0)
            {
                continue;
            }

            if (blades is not null && WaferOnBlades(blades, job.Wafer))
            {
                continue;
            }

            n++;
        }

        return n;
    }

    /// <summary>물리적으로 비어 있는 블레이드가 있는지 (큐 예약 무시 · BM/Aligner 회수 우선).</summary>
    public static bool HasPhysicalBladeFree(RobotBladeSlots? blades, int bladeCapacity) =>
        (blades?.FreeCount ?? bladeCapacity) > 0;

    /// <summary>신규 픽업 Job을 큐에 넣을 빈 블레이드 여유.</summary>
    public static bool HasFreeBladeForPickup(
        RobotBladeSlots? blades,
        int bladeCapacity,
        IEnumerable<TransferJob> queuedJobs)
    {
        if (bladeCapacity < 2)
        {
            return (blades?.FreeCount ?? 1) > CountPickupReservations(blades, queuedJobs);
        }

        int free = blades?.FreeCount ?? bladeCapacity;
        return free > CountPickupReservations(blades, queuedJobs);
    }

    /// <summary>힌트용 — 로봇 기본 facing에서 가장 가까운 빈 팔 라벨.</summary>
    public static string PredictNearestSlotLabel(
        EquipmentRegion faceRegion,
        TransferRobotKind robot,
        double defaultFacingDegrees,
        RobotBladeSlots? blades,
        int bladeCapacity)
    {
        if (bladeCapacity < 2)
        {
            return string.Empty;
        }

        var slotBlades = blades ?? new RobotBladeSlots(bladeCapacity);
        int slot = PickNearestFreeBlade(faceRegion, robot, defaultFacingDegrees, slotBlades, bladeCapacity);
        return slot < 0 ? string.Empty : $" · {SlotLabel(slot)}";
    }

    public static bool WaferOnBlades(RobotBladeSlots blades, WaferTrack wafer)
    {
        for (int slot = 0; slot < blades.Capacity; slot++)
        {
            if (blades.Get(slot)?.Id == wafer.Id)
            {
                return true;
            }
        }

        return false;
    }

    private static double NormalizeAngle(double degrees)
    {
        while (degrees > 180)
        {
            degrees -= 360;
        }

        while (degrees < -180)
        {
            degrees += 360;
        }

        return degrees;
    }

    private static double NormalizeAngleDiff(double diff)
    {
        while (diff > 180)
        {
            diff -= 360;
        }

        while (diff < -180)
        {
            diff += 360;
        }

        return diff;
    }

    /// <summary>듀얼 블레이드 Strip→BM 드롭이 BM 만석으로 막힌 상태.</summary>
    public static bool HasBlockedStripDropToBm(
        ClusterEquipmentState state,
        IEnumerable<TransferJob>? pendingDropoffs,
        RobotBladeSlots? blades)
    {
        if (!state.LoadLockBuffer.IsFull)
        {
            return false;
        }

        if (pendingDropoffs is not null)
        {
            foreach (TransferJob job in pendingDropoffs)
            {
                if (job.Dropoff == EquipmentRegion.LoadLock && job.Wafer.HasCompletedStrip)
                {
                    return true;
                }
            }
        }

        if (blades is null)
        {
            return false;
        }

        var pendingIds = pendingDropoffs?.Select(j => j.Wafer.Id).ToHashSet() ?? [];
        for (int slot = 0; slot < blades.Capacity; slot++)
        {
            if (blades.Get(slot) is WaferTrack wafer
                && wafer.HasCompletedStrip
                && !pendingIds.Contains(wafer.Id))
            {
                return true;
            }
        }

        return false;
    }

    public static bool CanChainPickup(
        RobotBladeSlots blades,
        TransferJob current,
        TransferJob next)
    {
        if (blades.Capacity < 2 || blades.FreeCount <= 0)
        {
            return false;
        }

        // BM(Pre-Etch) → Etch PM: 동일 Load Lock에서 2매 연속 픽업
        if (current.Pickup == EquipmentRegion.LoadLock
            && next.Pickup == EquipmentRegion.LoadLock
            && IsEtchChamber(current.Dropoff)
            && IsEtchChamber(next.Dropoff))
        {
            return true;
        }

        // Etch PM → PM1 Strip: 서로 다른 Etch PM 연속 픽업
        if (current.Dropoff == EquipmentRegion.ChamberA
            && next.Dropoff == EquipmentRegion.ChamberA
            && IsEtchChamber(current.Pickup)
            && IsEtchChamber(next.Pickup)
            && next.Pickup != current.Pickup)
        {
            return true;
        }

        return false;
    }

    private static bool IsEtchChamber(EquipmentRegion region) =>
        region is EquipmentRegion.ChamberB or EquipmentRegion.ChamberC or EquipmentRegion.ChamberD;
}

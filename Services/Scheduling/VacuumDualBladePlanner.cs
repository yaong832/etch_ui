using etch_ui.Equipment.Helpers;
using etch_ui.Equipment.Models;

namespace etch_ui.Services.Scheduling;

/// <summary>진공 TM 2슬롯 — Etch PM 연속 픽업·PM1 Strip 대기 적재.</summary>
public static class VacuumDualBladePlanner
{
    /// <summary>뒤 블레이드(로봇 -X) — 챔버 맞출 때 기준 각도 +180°.</summary>
    public const int BackBladeSlot = 0;

    /// <summary>앞 블레이드(로봇 +X) — 챔버 맞출 때 기준 각도.</summary>
    public const int FrontBladeSlot = 1;

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
        int pipelineRoom = blades.FreeCount - queue.Count;
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

            int slot = scheduled == 0 ? BackBladeSlot : FrontBladeSlot;
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
                BladeSlotIndex = slot
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

    public static bool CanChainPickup(
        RobotBladeSlots blades,
        TransferJob current,
        TransferJob next)
    {
        if (blades.Capacity < 2 || blades.FreeCount <= 0)
        {
            return false;
        }

        if (next.Pickup == current.Pickup)
        {
            return false;
        }

        if (current.Dropoff != EquipmentRegion.ChamberA || next.Dropoff != EquipmentRegion.ChamberA)
        {
            return false;
        }

        return IsEtchChamber(current.Pickup) && IsEtchChamber(next.Pickup);
    }

    private static bool IsEtchChamber(EquipmentRegion region) =>
        region is EquipmentRegion.ChamberB or EquipmentRegion.ChamberC or EquipmentRegion.ChamberD;

    private static int RegionToPmNumber(EquipmentRegion region) => region switch
    {
        EquipmentRegion.ChamberB => 2,
        EquipmentRegion.ChamberC => 3,
        EquipmentRegion.ChamberD => 4,
        EquipmentRegion.ChamberA => 1,
        _ => 0
    };
}

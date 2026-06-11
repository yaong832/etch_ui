using etch_ui.Equipment.Models;
using etch_ui.Services.Scheduling;

namespace etch_ui.Services.Simulation;

public sealed partial class TmTransferSimulator
{
    public string EfemSchedulerHint => _efemScheduler.LastHint;
    public string VacuumSchedulerHint => _vacuumScheduler.LastHint;

    public bool IsSchedulerHold =>
        ContainsHold(_efemScheduler.LastHint)
        || ContainsHold(_vacuumScheduler.LastHint)
        || ContainsHold(PhaseHint);

    public string DescribeWaferFlowPipeline()
    {
        ClusterEquipmentState s = _state;
        int foupRem = s.FoupPorts.Sum(p => p.RemainingInFoup);
        int inflight = s.FoupPorts.Sum(p => p.InFlightCount);
        int etchBusy = new[] { s.Pm2, s.Pm3, s.Pm4 }.Count(p => p.CurrentWafer is not null);
        int stripBusy = s.Pm1.CurrentWafer is not null ? 1 : 0;
        return string.Join(" → ",
        [
            $"FOUP {foupRem}(↑{inflight})",
            $"Align {s.AlignerBuffer.Count}/{s.AlignerBuffer.Capacity}",
            $"BM {s.LoadLockBuffer.Count}/{s.LoadLockBuffer.Capacity}",
            $"Etch×{etchBusy}",
            $"Strip×{stripBusy}",
            $"Side {s.SideStorage.Count}/{s.SideStorage.Capacity}",
            $"LOT {s.Lot.CompletedCount}/{s.Lot.TargetCount}"
        ]);
    }

    public string DescribeDualBladeStatus()
    {
        var parts = new List<string>(2);
        if (_vacuumBladeCapacity >= 2)
        {
            parts.Add(FormatRobotBlades(_vacuum, "진공 TM"));
        }
        else if (_vacuum.Carrying)
        {
            parts.Add($"진공 TM 단일 · {_vacuum.Blades.OccupiedCount}매");
        }

        if (_efemBladeCapacity >= 2)
        {
            parts.Add(FormatRobotBlades(_efem, "EFEM"));
        }
        else if (_efem.Carrying)
        {
            parts.Add("EFEM 단일 팔");
        }

        return parts.Count == 0 ? "단일 블레이드" : string.Join("  |  ", parts);
    }

    public IReadOnlyList<WaferTimelineEntry> GetActiveWaferTimeline()
    {
        var rows = new List<WaferTimelineEntry>();
        ClusterEquipmentState s = _state;

        foreach (FoupPortState port in s.FoupPorts)
        {
            if (port.InFlightCount > 0)
            {
                rows.Add(new WaferTimelineEntry(
                    0,
                    $"FOUP {port.PortId}",
                    "장내 유입",
                    $"{port.InFlightCount}매"));
            }
        }

        var bufferSnapshots = new List<WaferBufferEntrySnapshot>();
        s.AlignerBuffer.CollectEntries(bufferSnapshots, "Aligner");
        s.LoadLockBuffer.CollectEntries(bufferSnapshots, "Load Lock");
        foreach (WaferBufferEntrySnapshot snap in bufferSnapshots)
        {
            rows.Add(new WaferTimelineEntry(
                snap.Wafer.Id,
                snap.Location,
                DescribeWaferStage(snap.Wafer),
                snap.Status));
        }

        AddChamberWafer(rows, s.Pm1, "PM1 Strip");
        AddChamberWafer(rows, s.Pm2, "PM2 Etch");
        AddChamberWafer(rows, s.Pm3, "PM3 Etch");
        AddChamberWafer(rows, s.Pm4, "PM4 Etch");

        foreach (WaferTrack wafer in s.SideStorage.SnapshotFifo())
        {
            rows.Add(new WaferTimelineEntry(wafer.Id, "Side Stg", DescribeWaferStage(wafer), "FIFO"));
        }

        CollectRobotBlades(rows, _efem, "EFEM TM");
        CollectRobotBlades(rows, _vacuum, "진공 TM");

        return rows
            .OrderBy(r => r.WaferId == 0 ? int.MaxValue : r.WaferId)
            .ThenBy(r => r.Location, StringComparer.Ordinal)
            .ToList();
    }

    public string DescribeActiveWaferTimeline()
    {
        IReadOnlyList<WaferTimelineEntry> rows = GetActiveWaferTimeline();
        if (rows.Count == 0)
        {
            return "장내 웨이퍼 없음";
        }

        return string.Join("  |  ", rows.Select(FormatTimelineEntry));
    }

    private static void AddChamberWafer(List<WaferTimelineEntry> rows, PmChamberState chamber, string label)
    {
        if (chamber.CurrentWafer is not WaferTrack wafer)
        {
            return;
        }

        string detail = chamber.RemainingProcessTicks > 0
            ? $"공정 {chamber.RemainingProcessTicks}t"
            : chamber.ReservedForIncoming ? "투입 예약" : "대기";
        rows.Add(new WaferTimelineEntry(wafer.Id, label, DescribeWaferStage(wafer), detail));
    }

    private static void CollectRobotBlades(List<WaferTimelineEntry> rows, RobotRun run, string label)
    {
        for (int slot = 0; slot < run.Blades.Capacity; slot++)
        {
            WaferTrack? wafer = run.Blades.Get(slot);
            if (wafer is null)
            {
                continue;
            }

            string blade = VacuumDualBladePlanner.SlotLabel(slot);
            string detail = ResolveRobotWaferDetail(run, wafer);
            rows.Add(new WaferTimelineEntry(wafer.Id, $"{label} {blade}", DescribeWaferStage(wafer), detail));
        }
    }

    private static string ResolveRobotWaferDetail(RobotRun run, WaferTrack wafer)
    {
        if (run.Active is not TransferJob job || job.Wafer.Id != wafer.Id)
        {
            return "블레이드 적재";
        }

        return run.Phase switch
        {
            SimPhase.MoveToPickup or SimPhase.WaitDoorPickupOpen or SimPhase.PickupExtend
                or SimPhase.PickupGrip or SimPhase.PickupRetract or SimPhase.WaitDoorPickupClose
                => $"픽업 @ {Label(job.Pickup)}",
            SimPhase.RotateBlade => "180° 회전",
            SimPhase.MoveToDropoff or SimPhase.WaitDoorDropoffOpen or SimPhase.DropoffExtend
                or SimPhase.DropoffRelease or SimPhase.DropoffRetract or SimPhase.WaitDoorDropoffClose
                => $"→ {Label(job.Dropoff)}",
            _ => $"→ {Label(job.Dropoff)}"
        };
    }

    private static string DescribeWaferStage(WaferTrack wafer)
    {
        if (wafer.HasCompletedStrip)
        {
            return "완료→FOUP";
        }

        if (wafer.HasCompletedEtch)
        {
            return "Strip 대기";
        }

        return "Etch 전";
    }

    private static string FormatTimelineEntry(WaferTimelineEntry entry)
    {
        if (entry.WaferId == 0)
        {
            return $"{entry.Location} · {entry.Stage} ({entry.Detail})";
        }

        return string.IsNullOrEmpty(entry.Detail)
            ? $"#{entry.WaferId} {entry.Location} · {entry.Stage}"
            : $"#{entry.WaferId} {entry.Location} · {entry.Stage} · {entry.Detail}";
    }

    private static bool ContainsHold(string text) =>
        text.Contains("HOLD", StringComparison.OrdinalIgnoreCase)
        || text.Contains("만석", StringComparison.OrdinalIgnoreCase)
        || text.Contains("교체 대기", StringComparison.OrdinalIgnoreCase);

    private string FormatRobotBlades(RobotRun run, string label)
    {
        string SlotText(int slot)
        {
            WaferTrack? w = run.Blades.Get(slot);
            if (w is null)
            {
                return "—";
            }

            string dest = run.Active is TransferJob job && w.Id == job.Wafer.Id
                ? $"→{Label(job.Dropoff)}"
                : string.Empty;
            return $"#{w.Id}{dest}";
        }

        string active = VacuumDualBladePlanner.SlotLabel(run.ActiveBladeSlot);
        string rotate = run.Phase == SimPhase.RotateBlade ? " · 180°회전" : string.Empty;
        return $"{label} A[{SlotText(0)}] B[{SlotText(1)}] · 활성 {active}{rotate}";
    }
}

using etch_ui.Equipment.Models;

namespace etch_ui.Services.Scheduling;

/// <summary>FOUP InFlight를 장내 실물 웨이퍼 수와 동기화 (유실·조기 증가 교정).</summary>
public static class FoupInFlightReconciler
{
    public static void Reconcile(
        ClusterEquipmentState state,
        IEnumerable<WaferTrack?> carriedOnRobots,
        IEnumerable<TransferJob> inFlightJobs)
    {
        var counts = state.FoupPorts.ToDictionary(p => p.PortId, _ => 0);

        void Tally(WaferTrack? wafer)
        {
            if (wafer is null)
            {
                return;
            }

            if (counts.ContainsKey(wafer.OriginPort))
            {
                counts[wafer.OriginPort]++;
            }
        }

        TallyBuffer(state.AlignerBuffer, Tally);
        TallyBuffer(state.LoadLockBuffer, Tally);
        foreach (WaferTrack wafer in state.SideStorage.SnapshotFifo())
        {
            Tally(wafer);
        }

        foreach (PmChamberState chamber in state.Chambers.Values)
        {
            Tally(chamber.CurrentWafer);
        }

        foreach (WaferTrack? wafer in carriedOnRobots)
        {
            Tally(wafer);
        }

        foreach (TransferJob job in inFlightJobs)
        {
            Tally(job.Wafer);
        }

        foreach (FoupPortState port in state.FoupPorts)
        {
            port.InFlightCount = counts[port.PortId];
        }
    }

    private static void TallyBuffer(WaferSlotBuffer buffer, Action<WaferTrack?> tally)
    {
        var rows = new List<WaferBufferEntrySnapshot>();
        buffer.CollectEntries(rows, string.Empty);
        foreach (WaferBufferEntrySnapshot row in rows)
        {
            tally(row.Wafer);
        }
    }
}

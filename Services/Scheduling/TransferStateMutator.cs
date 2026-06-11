using etch_ui.Equipment.Models;

namespace etch_ui.Services.Scheduling;

/// <summary>픽업/드롭 시 슬롯·버퍼 상태 갱신 (EFEM·진공 TM 공통).</summary>
public static class TransferStateMutator
{
    public static void OnPickup(ClusterEquipmentState state, EquipmentRegion pickup, WaferTrack wafer)
    {
        if (pickup is EquipmentRegion.FoupA or EquipmentRegion.FoupB or EquipmentRegion.FoupC)
        {
            FoupPortState? port = state.FoupPorts.FirstOrDefault(p => p.FoupRegion == pickup);
            port?.OnWaferPickedFromFoup();
            return;
        }

        if (pickup == EquipmentRegion.SideStorage)
        {
            return;
        }

        if (pickup == EquipmentRegion.Aligner)
        {
            state.AlignerBuffer.TryRemove(wafer);
            return;
        }

        if (pickup == EquipmentRegion.LoadLock)
        {
            state.LoadLockBuffer.TryRemove(wafer);
            return;
        }

        PmChamberState? ch = state.GetChamber(pickup);
        if (ch is null)
        {
            return;
        }

        ch.ClearWafer();
        ch.PickupScheduled = false;
    }

    public static void OnDropoff(ClusterEquipmentState state, EquipmentRegion dropoff, WaferTrack wafer, int processTicks)
    {
        if (dropoff == EquipmentRegion.ExternalProcess)
        {
            state.Lot.RecordWaferCompleted();
            FoupPortState? port = state.FoupPorts.FirstOrDefault(p => p.PortId == wafer.OriginPort);
            port?.OnWaferLeftClusterToNextProcess();
            return;
        }

        if (dropoff == EquipmentRegion.SideStorage)
        {
            if (!state.SideStorage.TryEnqueue(wafer))
            {
                throw new InvalidOperationException($"Side Stg full on dropoff · wafer #{wafer.Id}");
            }

            return;
        }

        if (dropoff == EquipmentRegion.Aligner)
        {
            if (!state.AlignerBuffer.TryEnqueue(wafer, processTicks))
            {
                throw new InvalidOperationException($"Aligner full on dropoff · wafer #{wafer.Id}");
            }

            return;
        }

        if (dropoff == EquipmentRegion.LoadLock)
        {
            if (wafer.HasCompletedStrip)
            {
                if (!LoadLockAdmissionPolicy.CanAcceptStripFromPm1(state, out string? stripReason))
                {
                    throw new InvalidOperationException(
                        $"BM Strip dropoff rejected · wafer #{wafer.Id} · {stripReason ?? "policy"}");
                }
            }
            else if (!LoadLockAdmissionPolicy.CanAcceptPreEtchFromAligner(state, out string? preReason))
            {
                throw new InvalidOperationException(
                    $"BM Pre-Etch dropoff rejected · wafer #{wafer.Id} · {preReason ?? "policy"}");
            }

            if (!state.LoadLockBuffer.TryEnqueue(wafer, 0))
            {
                throw new InvalidOperationException($"BM full on dropoff · wafer #{wafer.Id}");
            }

            return;
        }

        PmChamberState? ch = state.GetChamber(dropoff);
        if (ch is null)
        {
            return;
        }

        ch.ReservedForIncoming = false;
        ch.CurrentWafer = wafer;
        ch.RemainingProcessTicks = processTicks;

        if (ch.IsEtchPm)
        {
            state.RecordEtchInbound(dropoff);
        }
    }
}

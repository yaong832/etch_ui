using etch_ui.Equipment.Models;

namespace etch_ui.Services.Scheduling;

/// <summary>
/// BM(Load Lock) 수용 — Pre-Etch 최대 2 · Strip 완료 1 · 총 3매.
/// </summary>
public static class LoadLockAdmissionPolicy
{
    public static int MaxPreEtchSlots(ClusterEquipmentState state) =>
        state.Capacity.LoadLockSlotCount >= EquipmentCapacityConfig.DefaultLoadLockSlotCount
            ? EquipmentCapacityConfig.DefaultBmMaxPreEtchSlots
            : Math.Max(1, state.Capacity.LoadLockSlotCount - 1);

    public static int MaxStripSlots(ClusterEquipmentState state) =>
        state.Capacity.LoadLockSlotCount >= EquipmentCapacityConfig.DefaultLoadLockSlotCount
            ? EquipmentCapacityConfig.DefaultBmMaxStripSlots
            : 1;

    public static int PreEtchCount(ClusterEquipmentState state) =>
        state.LoadLockBuffer.CountMatching(IsPreEtch);

    public static int StripCount(ClusterEquipmentState state) =>
        state.LoadLockBuffer.CountMatching(w => w.HasCompletedStrip);

    public static bool IsPreEtchBmFull(ClusterEquipmentState state) =>
        PreEtchCount(state) >= MaxPreEtchSlots(state);

    public static bool IsStripBmSlotTaken(ClusterEquipmentState state) =>
        StripCount(state) >= MaxStripSlots(state);

    /// <summary>Aligner → BM (미식각). Etch PM 여유 없으면 Pre-Etch 1매만 대기.</summary>
    public static bool CanAcceptPreEtchFromAligner(ClusterEquipmentState state, out string? blockReason)
    {
        if (IsPreEtchBmFull(state))
        {
            blockReason = $"BM Pre-Etch {PreEtchCount(state)}/{MaxPreEtchSlots(state)}";
            return false;
        }

        if (state.LoadLockBuffer.Count >= state.LoadLockBuffer.Capacity)
        {
            blockReason = "BM 만석";
            return false;
        }

        if (EtchPmSelector.SelectNextPipelineTarget(state.Chambers) is not null)
        {
            blockReason = null;
            return true;
        }

        if (PreEtchCount(state) >= 1)
        {
            blockReason = "Etch PM 만석·BM Pre-Etch 1매 대기 중";
            return false;
        }

        blockReason = null;
        return true;
    }

    /// <summary>PM1 Strip 완료 → BM Strip 슬롯(1). Side Stg 여유 연동.</summary>
    public static bool CanAcceptStripFromPm1(ClusterEquipmentState state, out string? blockReason)
    {
        if (IsStripBmSlotTaken(state))
        {
            blockReason = $"BM Strip {StripCount(state)}/{MaxStripSlots(state)}";
            return false;
        }

        if (state.LoadLockBuffer.Count >= state.LoadLockBuffer.Capacity)
        {
            blockReason = "BM 만석";
            return false;
        }

        int stripInBm = StripCount(state);
        int sideFree = state.SideStorage.Capacity - state.SideStorage.Count;

        if (state.SideStorage.IsFull)
        {
            if (stripInBm >= 1)
            {
                blockReason = "Side Stg 25/25 · 카세트 교체 대기";
                return false;
            }

            blockReason = null;
            return true;
        }

        if (stripInBm >= sideFree)
        {
            blockReason = $"Side Stg 여유 {sideFree}·BM Strip {stripInBm} HOLD";
            return false;
        }

        blockReason = null;
        return true;
    }

    public static int CountReservedBmIncomingSlots(
        ClusterEquipmentState state,
        RobotBladeSlots? blades,
        IEnumerable<TransferJob>? pendingDropoffs,
        IEnumerable<TransferJob>? queuedJobs) =>
        CountReservedPreEtchToBm(pendingDropoffs, queuedJobs, blades)
        + CountReservedStripToBm(pendingDropoffs, queuedJobs, blades);

    public static int CountReservedPreEtchToBm(
        IEnumerable<TransferJob>? pendingDropoffs,
        IEnumerable<TransferJob>? queuedJobs,
        RobotBladeSlots? blades = null)
    {
        var tracked = new HashSet<int>();
        int reserved = 0;

        void Tally(TransferJob job)
        {
            if (job.Dropoff != EquipmentRegion.LoadLock || job.Wafer.HasCompletedStrip)
            {
                return;
            }

            if (tracked.Add(job.Wafer.Id))
            {
                reserved++;
            }
        }

        if (pendingDropoffs is not null)
        {
            foreach (TransferJob job in pendingDropoffs)
            {
                Tally(job);
            }
        }

        if (queuedJobs is not null)
        {
            foreach (TransferJob job in queuedJobs)
            {
                Tally(job);
            }
        }

        return reserved;
    }

    public static int CountReservedStripToBm(
        IEnumerable<TransferJob>? pendingDropoffs,
        IEnumerable<TransferJob>? queuedJobs,
        RobotBladeSlots? blades)
    {
        var tracked = new HashSet<int>();
        int reserved = 0;

        void Tally(TransferJob job)
        {
            if (job.Dropoff != EquipmentRegion.LoadLock || !job.Wafer.HasCompletedStrip)
            {
                return;
            }

            if (tracked.Add(job.Wafer.Id))
            {
                reserved++;
            }
        }

        if (pendingDropoffs is not null)
        {
            foreach (TransferJob job in pendingDropoffs)
            {
                Tally(job);
            }
        }

        if (queuedJobs is not null)
        {
            foreach (TransferJob job in queuedJobs)
            {
                Tally(job);
            }
        }

        if (blades is not null)
        {
            for (int slot = 0; slot < blades.Capacity; slot++)
            {
                if (blades.Get(slot) is WaferTrack wafer
                    && wafer.HasCompletedStrip
                    && tracked.Add(wafer.Id))
                {
                    reserved++;
                }
            }
        }

        return reserved;
    }

    public static bool CanAcceptAnotherPreEtchBmDrop(
        ClusterEquipmentState state,
        int additionalSlots = 1,
        IEnumerable<TransferJob>? pendingDropoffs = null,
        IEnumerable<TransferJob>? queuedJobs = null)
    {
        int preUsed = PreEtchCount(state) + CountReservedPreEtchToBm(pendingDropoffs, queuedJobs);
        if (preUsed + additionalSlots > MaxPreEtchSlots(state))
        {
            return false;
        }

        return state.LoadLockBuffer.Count + additionalSlots <= state.LoadLockBuffer.Capacity;
    }

    public static bool CanAcceptAnotherStripBmDrop(
        ClusterEquipmentState state,
        int additionalSlots = 1,
        RobotBladeSlots? blades = null,
        IEnumerable<TransferJob>? pendingDropoffs = null,
        IEnumerable<TransferJob>? queuedJobs = null)
    {
        int stripUsed = StripCount(state) + CountReservedStripToBm(pendingDropoffs, queuedJobs, blades);
        if (stripUsed + additionalSlots > MaxStripSlots(state))
        {
            return false;
        }

        return state.LoadLockBuffer.Count + additionalSlots <= state.LoadLockBuffer.Capacity;
    }

    public static bool CanAcceptAnotherBmDrop(
        ClusterEquipmentState state,
        int additionalSlots = 1,
        RobotBladeSlots? blades = null,
        IEnumerable<TransferJob>? pendingDropoffs = null,
        IEnumerable<TransferJob>? queuedJobs = null,
        bool isStripDrop = false) =>
        isStripDrop
            ? CanAcceptAnotherStripBmDrop(state, additionalSlots, blades, pendingDropoffs, queuedJobs)
            : CanAcceptAnotherPreEtchBmDrop(state, additionalSlots, pendingDropoffs, queuedJobs);

    public static bool HasEtchPmCapacity(ClusterEquipmentState state) =>
        EtchPmSelector.HasPipelineEtchCapacity(state.Chambers);

    public static string FormatBmInventory(ClusterEquipmentState state)
    {
        int cap = state.LoadLockBuffer.Capacity;
        int pre = PreEtchCount(state);
        int strip = StripCount(state);
        int maxPre = MaxPreEtchSlots(state);
        int maxStrip = MaxStripSlots(state);
        return $"{pre + strip}/{cap} (P{pre}/{maxPre}+S{strip}/{maxStrip})";
    }

    public static string DescribeBmStatus(ClusterEquipmentState state)
    {
        int total = state.LoadLockBuffer.Count;
        int cap = state.LoadLockBuffer.Capacity;
        int pre = PreEtchCount(state);
        int strip = StripCount(state);
        int maxPre = MaxPreEtchSlots(state);
        int maxStrip = MaxStripSlots(state);
        int sideFree = state.SideStorage.Capacity - state.SideStorage.Count;

        if (total >= cap)
        {
            if (strip > 0 && state.SideStorage.IsFull)
            {
                return $"슬롯 {total}/{cap} · Strip {strip}/{maxStrip} · Side 25/25 교체 대기";
            }

            if (pre > 0 && !HasEtchPmCapacity(state))
            {
                return $"슬롯 {total}/{cap} · Pre {pre}/{maxPre} · PM2~4 만석 HOLD";
            }

            return $"슬롯 {total}/{cap} · 만석 (P{pre}+S{strip})";
        }

        if (pre > 0 && !HasEtchPmCapacity(state))
        {
            return $"슬롯 {total}/{cap} · Pre {pre}/{maxPre} · PM2~4 만석 대기";
        }

        return $"슬롯 {total}/{cap} · Pre {pre}/{maxPre} · Strip {strip}/{maxStrip} · Side 여유 {sideFree}";
    }

    private static bool IsPreEtch(WaferTrack w) => !w.HasCompletedEtch && !w.HasCompletedStrip;
}

using etch_ui.Equipment.Models;

namespace etch_ui.Services.Scheduling;

/// <summary>
/// BM(Load Lock 2매) 수용 조건 — PM·Side Stg 만석 시 데드락 방지.
/// </summary>
public static class LoadLockAdmissionPolicy
{
    /// <summary>Aligner → BM (미식각). Etch PM 여유 없으면 BM에 1매만 대기 허용.</summary>
    public static bool CanAcceptPreEtchFromAligner(ClusterEquipmentState state, out string? blockReason)
    {
        if (state.LoadLockBuffer.IsFull)
        {
            blockReason = "BM 만석";
            return false;
        }

        if (EtchPmSelector.SelectNextPipelineTarget(state.Chambers) is not null)
        {
            blockReason = null;
            return true;
        }

        int preEtchWaiting = state.LoadLockBuffer.CountMatching(IsPreEtch);
        if (preEtchWaiting >= 1)
        {
            blockReason = "Etch PM 만석·BM 1매 대기 중";
            return false;
        }

        blockReason = null;
        return true;
    }

    /// <summary>PM1 Strip 완료 → BM. Side Stg 여유 없으면 BM Strip 적재 상한.</summary>
    public static bool CanAcceptStripFromPm1(ClusterEquipmentState state, out string? blockReason)
    {
        if (state.LoadLockBuffer.IsFull)
        {
            blockReason = "BM 만석";
            return false;
        }

        int stripInBm = state.LoadLockBuffer.CountMatching(w => w.HasCompletedStrip);
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

    public static bool HasEtchPmCapacity(ClusterEquipmentState state) =>
        EtchPmSelector.HasPipelineEtchCapacity(state.Chambers);

    public static string DescribeBmStatus(ClusterEquipmentState state)
    {
        int total = state.LoadLockBuffer.Count;
        int cap = state.LoadLockBuffer.Capacity;
        int pre = state.LoadLockBuffer.CountMatching(IsPreEtch);
        int strip = state.LoadLockBuffer.CountMatching(w => w.HasCompletedStrip);
        int sideFree = state.SideStorage.Capacity - state.SideStorage.Count;

        if (total >= cap)
        {
            if (strip > 0 && state.SideStorage.IsFull)
            {
                return $"슬롯 {total}/{cap} · Strip {strip} · Side 25/25 교체 대기";
            }

            if (pre > 0 && !HasEtchPmCapacity(state))
            {
                return $"슬롯 {total}/{cap} · Pre-Etch {pre} · PM2~4 만석 HOLD";
            }

            return $"슬롯 {total}/{cap} · 만석";
        }

        if (pre > 0 && !HasEtchPmCapacity(state))
        {
            return $"슬롯 {total}/{cap} · Pre-Etch {pre} · PM2~4 만석 대기";
        }

        return $"슬롯 {total}/{cap} · Pre {pre} · Strip {strip} · Side 여유 {sideFree}";
    }

    private static bool IsPreEtch(WaferTrack w) => !w.HasCompletedEtch && !w.HasCompletedStrip;
}

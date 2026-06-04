namespace etch_ui.Services.Scheduling;

/// <summary>
/// EFEM·TM FOUP 픽업 우선순위 (TM 자율 판단 1차 규칙).
/// 1) FOUP 내 남은 매수가 많은 LP 우선
/// 2) 동률이면 LP1 → LP2 → LP3
/// 3) 풀 FOUP 차단(전 LP 공통): 잔량이 슬롯 만매(25)인 LP는,
///    다른 LP에 **FOUP 잔량이 남은** 진행 Lot(잔량 1~24 또는 잔량+InFlight)이 있으면 픽업 불가
///    (잔량 0·InFlight만 남은 LP는 마무리 중이므로 다음 풀 FOUP 허용)
/// </summary>
public sealed class FoupPickScheduler
{
    private readonly FoupPortState[] _ports;
    private readonly int _foupSlotCount;

    public FoupPickScheduler(IEnumerable<FoupPortState> ports, int foupSlotCount)
    {
        _ports = ports.OrderBy(p => p.PortId).ToArray();
        _foupSlotCount = foupSlotCount;
    }

    public FoupPortState? SelectNextPickSource()
    {
        var candidates = _ports
            .Where(p => p.IsMounted && p.RemainingInFoup > 0 && IsEligible(p))
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        int maxRemaining = candidates.Max(p => p.RemainingInFoup);
        var tier = candidates.Where(p => p.RemainingInFoup == maxRemaining).ToList();

        return tier.OrderBy(p => p.PortId).First();
    }

    /// <summary>
    /// LP1·2·3 동일 적용: 풀 잔량 FOUP는 다른 FOUP에 미완 Lot이 있으면 대기.
    /// (신규 재장착뿐 아니라, 아직 손대지 않은 25매 FOUP도 동일)
    /// </summary>
    private bool IsEligible(FoupPortState port)
    {
        if (!IsFullStock(port))
        {
            return true;
        }

        return !OthersHavePartialLotInProgress(port);
    }

    private bool IsFullStock(FoupPortState port) => port.RemainingInFoup >= _foupSlotCount;

    private bool OthersHavePartialLotInProgress(FoupPortState exclude) =>
        _ports.Any(p =>
            p.PortId != exclude.PortId
            && p.IsMounted
            && p.RemainingInFoup > 0
            && (p.InFlightCount > 0 || p.RemainingInFoup < _foupSlotCount));

    /// <summary>풀 FOUP 차단은 매 선택 시 IsEligible로 계산 (상태 플래그 불필요).</summary>
    public void RefreshFreshMountBlocks()
    {
    }

    /// <summary>데모: LP 비운 뒤 신규 FOUP 장착.</summary>
    public void SimulateRemountIfEmpty(LoadPortId portId, int slotCount = 0)
    {
        var port = _ports.First(p => p.PortId == portId);
        if (port.RemainingInFoup > 0 || port.InFlightCount > 0)
        {
            return;
        }

        port.OnNewFoupMounted(slotCount > 0 ? slotCount : _foupSlotCount);
    }
}

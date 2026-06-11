namespace etch_ui.Services.Scheduling;

/// <summary>
/// EFEM·TM FOUP 픽업 우선순위 (실장비 계열: 잔량 많은 카세트 우선).
/// 1) <see cref="FoupPortState.RemainingInFoup"/> 가 가장 큰 LP
/// 2) 동률이면 LP1 → LP2 → LP3
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

    public FoupPortState? SelectNextPickSource() =>
        _ports
            .Where(p => p.IsMounted && p.RemainingInFoup > 0)
            .OrderByDescending(p => p.RemainingInFoup)
            .ThenBy(p => p.PortId)
            .FirstOrDefault();

    /// <summary>정비 도구 전용: LP 비운 뒤 신규 FOUP 장착 (자동 리필 없음).</summary>
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

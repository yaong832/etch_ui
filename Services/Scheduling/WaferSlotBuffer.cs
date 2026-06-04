namespace etch_ui.Services.Scheduling;

/// <summary>Aligner·Load Lock 등 다매 슬롯 버퍼 (FIFO).</summary>
public sealed class WaferSlotBuffer
{
    private readonly List<Entry> _entries = [];
    private readonly int _capacity;

    public WaferSlotBuffer(int capacity) => _capacity = capacity;

    public int Capacity => _capacity;
    public int Count => _entries.Count;
    public bool IsFull => Count >= _capacity;
    public bool HasWafer => Count > 0;
    public int FreeSlots => _capacity - Count;

    public bool TryEnqueue(WaferTrack wafer, int processTicks = 0)
    {
        if (IsFull)
        {
            return false;
        }

        _entries.Add(new Entry(wafer, processTicks));
        return true;
    }

    public bool TryPeekReadyWhere(Func<WaferTrack, bool> predicate, out WaferTrack wafer)
    {
        Entry? entry = _entries.FirstOrDefault(e =>
            e.RemainingTicks <= 0 && !e.PickupScheduled && predicate(e.Wafer));
        if (entry is null)
        {
            wafer = null!;
            return false;
        }

        wafer = entry.Wafer;
        return true;
    }

    public bool TryPeekReady(out WaferTrack wafer) => TryPeekReadyWhere(_ => true, out wafer);

    public bool TryMarkPickupScheduled(WaferTrack wafer)
    {
        Entry? entry = _entries.FirstOrDefault(e => e.Wafer.Id == wafer.Id && e.RemainingTicks <= 0 && !e.PickupScheduled);
        if (entry is null)
        {
            return false;
        }

        entry.PickupScheduled = true;
        return true;
    }

    public bool TryRemove(WaferTrack wafer)
    {
        int idx = _entries.FindIndex(e => e.Wafer.Id == wafer.Id);
        if (idx < 0)
        {
            return false;
        }

        _entries.RemoveAt(idx);
        return true;
    }

    public void DecrementProcessTimes()
    {
        foreach (Entry entry in _entries)
        {
            if (entry.RemainingTicks > 0)
            {
                entry.RemainingTicks--;
            }
        }
    }

    public void Clear() => _entries.Clear();

    public int CountMatching(Func<WaferTrack, bool> predicate) =>
        _entries.Count(e => predicate(e.Wafer));

    private sealed class Entry(WaferTrack wafer, int remainingTicks)
    {
        public WaferTrack Wafer { get; } = wafer;
        public int RemainingTicks { get; set; } = remainingTicks;
        public bool PickupScheduled { get; set; }
    }
}

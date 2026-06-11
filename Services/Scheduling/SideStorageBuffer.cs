namespace etch_ui.Services.Scheduling;

/// <summary>Side Storage 슬롯(최대 N) — FIFO.</summary>
public sealed class SideStorageBuffer
{
    private readonly Queue<WaferTrack> _fifo = new();
    private readonly int _capacity;

    public SideStorageBuffer(int capacity) => _capacity = capacity;

    public int Count => _fifo.Count;
    public int Capacity => _capacity;
    public bool IsFull => Count >= _capacity;
    public bool HasWafer => Count > 0;

    public bool TryEnqueue(WaferTrack wafer)
    {
        if (IsFull)
        {
            return false;
        }

        _fifo.Enqueue(wafer);
        return true;
    }

    public bool TryDequeue(out WaferTrack wafer)
    {
        if (_fifo.Count == 0)
        {
            wafer = null!;
            return false;
        }

        wafer = _fifo.Dequeue();
        return true;
    }

    public WaferTrack[] SnapshotFifo() => _fifo.ToArray();
}

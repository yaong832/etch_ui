namespace etch_ui.Services.Scheduling;

/// <summary>회전 TM 블레이드 양끝 슬롯 (A=0, B=1).</summary>
public sealed class RobotBladeSlots
{
    private readonly WaferTrack?[] _slots;

    public RobotBladeSlots(int capacity)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        Capacity = capacity;
        _slots = new WaferTrack?[capacity];
    }

    public int Capacity { get; }

    public int OccupiedCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < Capacity; i++)
            {
                if (_slots[i] is not null)
                {
                    n++;
                }
            }

            return n;
        }
    }

    public int FreeCount => Capacity - OccupiedCount;

    public bool HasWafer(int slot) => slot >= 0 && slot < Capacity && _slots[slot] is not null;

    public WaferTrack? Get(int slot) => slot >= 0 && slot < Capacity ? _slots[slot] : null;

    public int FirstFreeSlot()
    {
        for (int i = 0; i < Capacity; i++)
        {
            if (_slots[i] is null)
            {
                return i;
            }
        }

        return -1;
    }

    public void Place(int slot, WaferTrack wafer)
    {
        if (slot < 0 || slot >= Capacity)
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }

        _slots[slot] = wafer;
    }

    public void Remove(int slot)
    {
        if (slot >= 0 && slot < Capacity)
        {
            _slots[slot] = null;
        }
    }

    public void Clear()
    {
        for (int i = 0; i < Capacity; i++)
        {
            _slots[i] = null;
        }
    }
}

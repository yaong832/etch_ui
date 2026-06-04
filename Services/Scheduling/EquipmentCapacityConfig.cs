namespace etch_ui.Services.Scheduling;

/// <summary>장비 용량·레시피 기본값 (현장 합의 · 시뮬 튜닝 전제).</summary>
public sealed class EquipmentCapacityConfig
{
    public const int DefaultFoupSlotCount = 25;
    public const int DefaultSideStorageSlotCount = 25;
    public const int DefaultAlignerSlotCount = 5;
    public const int DefaultLoadLockSlotCount = 2;

    public int FoupSlotCount { get; init; } = DefaultFoupSlotCount;
    public int SideStorageSlotCount { get; init; } = DefaultSideStorageSlotCount;
    public int AlignerSlotCount { get; init; } = DefaultAlignerSlotCount;
    public int LoadLockSlotCount { get; init; } = DefaultLoadLockSlotCount;

    /// <summary>PM2~4 식각 — TM 이송(~50tick)보다 길게 잡아 2·3·4 병렬 가동이 보이도록.</summary>
    public int EtchProcessTicks { get; init; } = 75;

    /// <summary>PM1 Strip — 후가공, 2~4보다 짧게 (시뮬에서 조정).</summary>
    public int StripProcessTicks { get; init; } = 20;

    /// <summary>Aligner 정렬 대기 tick.</summary>
    public int AlignProcessTicks { get; init; } = 2;

    public static EquipmentCapacityConfig Default { get; } = new();
}

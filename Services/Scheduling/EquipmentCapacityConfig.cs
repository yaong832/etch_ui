namespace etch_ui.Services.Scheduling;

/// <summary>장비 용량·레시피 기본값 (현장 합의 · 시뮬 튜닝 전제).</summary>
public sealed class EquipmentCapacityConfig
{
    public const int DefaultFoupSlotCount = 25;
    public const int DefaultSideStorageSlotCount = 25;
    /// <summary>실장비: Aligner 1매 정렬 (FIFO 다매 버퍼 아님).</summary>
    public const int DefaultAlignerSlotCount = 1;
    public const int DefaultLoadLockSlotCount = 2;

    public int FoupSlotCount { get; init; } = DefaultFoupSlotCount;
    public int SideStorageSlotCount { get; init; } = DefaultSideStorageSlotCount;
    public int AlignerSlotCount { get; init; } = DefaultAlignerSlotCount;
    public int LoadLockSlotCount { get; init; } = DefaultLoadLockSlotCount;

    /// <summary>진공 TM 블레이드 슬롯 수 (듀얼=2).</summary>
    public int VacuumBladeSlotCount { get; init; } = 2;

    /// <summary>EFEM TM 블레이드 슬롯 (실장비 단일 팔=1 · 시뮬도 1매만 적재).</summary>
    public int EfemBladeSlotCount { get; init; } = 1;

    /// <summary>PM2~4 식각 — TM 이송 tick과 분리(가공만 길게).</summary>
    public int EtchProcessTicks { get; init; } = 120;

    /// <summary>PM1 Strip — Etch보다 짧게, TM 이송과 분리.</summary>
    public int StripProcessTicks { get; init; } = 28;

    /// <summary>Aligner 정렬 대기 tick.</summary>
    public int AlignProcessTicks { get; init; } = 2;

    /// <summary>RUNNING UI 1초당 진공 TM 모션 sub-tick (EFEM 체감 맞춤 · UI는 보간으로 동기).</summary>
    public int VacuumMotionStepsPerUiTick { get; init; } = 1;

    /// <summary>진공 TM 1이송 단계 tick (EFEM legacy 3~4와 동일).</summary>
    public int VacuumMoveTicks { get; init; } = 4;

    public int VacuumDoorTicks { get; init; } = 3;

    public int VacuumExtendTicks { get; init; } = 4;

    public int VacuumGripTicks { get; init; } = 2;

    public int VacuumRotateTicks { get; init; } = 4;

    /// <summary>RotateBlade 구간 각도 보간 비율(1 tick당).</summary>
    public double VacuumRotateStepRatio { get; init; } = 0.55;

    public static EquipmentCapacityConfig Default { get; } = new();
}

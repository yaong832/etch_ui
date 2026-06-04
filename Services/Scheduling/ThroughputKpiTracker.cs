namespace etch_ui.Services.Scheduling;

/// <summary>
/// 처리량 KPI (시뮬·향후 Flask 연동). 파이프라인 데모와 별도로 병목·WPH 추정용.
/// tick ≈ 16ms(HMI 타이머) 가정 시 WPH 환산.
/// </summary>
public sealed class ThroughputKpiTracker
{
    public const double TickDurationSeconds = 0.016;

    private int _efemBusyTicks;
    private int _vacuumBusyTicks;
    private int _bmFullTicks;
    private int _sideStgFullTicks;
    private int _etchPipelineFullTicks;
    private int? _firstCompletionTick;
    private int? _lastCompletionTick;

    public int ElapsedTicks { get; private set; }

    public void Reset()
    {
        ElapsedTicks = 0;
        _efemBusyTicks = 0;
        _vacuumBusyTicks = 0;
        _bmFullTicks = 0;
        _sideStgFullTicks = 0;
        _etchPipelineFullTicks = 0;
        _firstCompletionTick = null;
        _lastCompletionTick = null;
    }

    public void OnTick(ClusterEquipmentState state, bool efemBusy, bool vacuumBusy)
    {
        ElapsedTicks++;
        if (efemBusy)
        {
            _efemBusyTicks++;
        }

        if (vacuumBusy)
        {
            _vacuumBusyTicks++;
        }

        if (state.LoadLockBuffer.IsFull)
        {
            _bmFullTicks++;
        }

        if (state.SideStorage.IsFull)
        {
            _sideStgFullTicks++;
        }

        if (!EtchPmSelector.HasPipelineEtchCapacity(state.Chambers)
            && state.LoadLockBuffer.CountMatching(w => !w.HasCompletedEtch && !w.HasCompletedStrip) > 0)
        {
            _etchPipelineFullTicks++;
        }
    }

    public void OnWaferLotCompleted(int completedCount)
    {
        _lastCompletionTick = ElapsedTicks;
        _firstCompletionTick ??= ElapsedTicks;
    }

    public ThroughputKpiSnapshot Snapshot(LotCompletionTracker lot) =>
        new(
            ElapsedTicks,
            lot.CompletedCount,
            lot.TargetCount,
            _efemBusyTicks,
            _vacuumBusyTicks,
            _bmFullTicks,
            _sideStgFullTicks,
            _etchPipelineFullTicks,
            _firstCompletionTick,
            _lastCompletionTick);

    public static double ToWafersPerHour(int completed, int elapsedTicks)
    {
        if (completed <= 0 || elapsedTicks <= 0)
        {
            return 0;
        }

        double hours = elapsedTicks * TickDurationSeconds / 3600.0;
        return completed / hours;
    }
}

public readonly record struct ThroughputKpiSnapshot(
    int ElapsedTicks,
    int CompletedWafers,
    int TargetWafers,
    int EfemBusyTicks,
    int VacuumBusyTicks,
    int BmFullTicks,
    int SideStgFullTicks,
    int EtchPipelineFullTicks,
    int? FirstCompletionTick,
    int? LastCompletionTick)
{
    public double EfemUtilization => ElapsedTicks > 0 ? (double)EfemBusyTicks / ElapsedTicks : 0;
    public double VacuumUtilization => ElapsedTicks > 0 ? (double)VacuumBusyTicks / ElapsedTicks : 0;
    public double EstimatedWph => ThroughputKpiTracker.ToWafersPerHour(CompletedWafers, ElapsedTicks);

    public int? AverageTicksPerWafer =>
        CompletedWafers > 0 && FirstCompletionTick is not null && LastCompletionTick is not null
            ? (LastCompletionTick.Value - FirstCompletionTick.Value) / Math.Max(1, CompletedWafers - 1)
            : null;

    public string BottleneckHint
    {
        get
        {
            (string label, int ticks)[] candidates =
            [
                ("BM 만석", BmFullTicks),
                ("Side Stg 만석", SideStgFullTicks),
                ("Etch PM 파이프라인", EtchPipelineFullTicks),
                ("진공 TM 대기", ElapsedTicks - VacuumBusyTicks),
                ("EFEM TM 대기", ElapsedTicks - EfemBusyTicks)
            ];
            (string label, int ticks) top = candidates.MaxBy(c => c.ticks);
            return $"{top.label} ({top.ticks} tick)";
        }
    }

    public override string ToString() =>
        $"lot={CompletedWafers}/{TargetWafers} wph~{EstimatedWph:F0} efem={EfemUtilization:P0} vac={VacuumUtilization:P0} avg={AverageTicksPerWafer} bn={BottleneckHint}";
}

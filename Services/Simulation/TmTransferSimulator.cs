using etch_ui.Equipment.Helpers;
using etch_ui.Equipment.Models;
using etch_ui.Services.Scheduling;

namespace etch_ui.Services.Simulation;

/// <summary>
/// 이중 TM 시뮬 — EFEM(좌) / 진공 TM(우) 스케줄러 분리, Load Lock(2) 경계.
/// </summary>
public sealed class TmTransferSimulator
{
    public enum SimPhase
    {
        Idle,
        MoveToPickup,
        WaitDoorPickupOpen,
        PickupExtend,
        PickupGrip,
        PickupRetract,
        WaitDoorPickupClose,
        MoveToDropoff,
        WaitDoorDropoffOpen,
        DropoffExtend,
        DropoffRelease,
        DropoffRetract,
        WaitDoorDropoffClose
    }

    private sealed class RobotRun
    {
        public readonly Queue<TransferJob> Queue = new();
        public TransferJob? Active;
        public SimPhase Phase = SimPhase.Idle;
        public int TicksLeft;
        public readonly TransferRobotKind Robot;
        public EquipmentRegion Region = EquipmentRegion.TM;
        public double Extension = 0.65;
        public bool Carrying;

        public RobotRun(TransferRobotKind robot) => Robot = robot;

        public bool IsBusy => Active is not null;
    }

    private readonly EquipmentCapacityConfig _capacity;
    private readonly ClusterEquipmentState _state;
    private readonly EfemTransferScheduler _efemScheduler = new();
    private readonly VacuumTransferScheduler _vacuumScheduler = new();
    private readonly RobotRun _efem = new(TransferRobotKind.EfemAtmospheric);
    private readonly RobotRun _vacuum = new(TransferRobotKind.VacuumTm);
    private readonly ThroughputKpiTracker _kpi = new();
    private bool _running;

    public TmTransferSimulator(EquipmentCapacityConfig? capacity = null)
    {
        _capacity = capacity ?? EquipmentCapacityConfig.Default;
        _state = new ClusterEquipmentState(_capacity);
    }

    public bool IsActive => _running && (_efem.IsBusy || _vacuum.IsBusy);
    public SimPhase Phase => _vacuum.Phase;
    public EquipmentRegion TmRegion => _vacuum.Region;
    public TransferRobotKind ActiveRobot => _vacuum.IsBusy ? TransferRobotKind.VacuumTm : TransferRobotKind.EfemAtmospheric;
    public double BladeExtension => _vacuum.Extension;
    public bool CarryingWafer => _vacuum.Carrying;
    public int VacuumBladeCapacity { get; } = 1;
    public int EfemBladeCapacity { get; } = 1;

    public bool IsEfemBusy => _efem.IsBusy;
    public bool IsVacuumBusy => _vacuum.IsBusy;
    public EquipmentRegion EfemRegion => _efem.Region;
    public double EfemExtension => _efem.Extension;
    public bool EfemCarryingWafer => _efem.Carrying;

    public string PhaseHint { get; private set; } = "가상 이송 · 대기";
    public int SideStorageOccupancy => _state.SideStorage.Count;
    public int SideStorageCapacity => _state.SideStorage.Capacity;
    public ClusterEquipmentState ClusterState => _state;
    public bool IsLotComplete => LotCompleteAchieved;
    public bool LotCompleteAchieved { get; private set; }
    public int LotCompletedCount => _state.Lot.CompletedCount;
    public int LotTargetCount => _state.Lot.TargetCount;
    public ThroughputKpiSnapshot KpiSnapshot => _kpi.Snapshot(_state.Lot);

    public void StartDemoLoop()
    {
        Stop();
        LotCompleteAchieved = false;
        _running = true;
        _kpi.Reset();
        _state.ResetForDemo();
        TrySchedule(_efem, _efemScheduler);
        TrySchedule(_vacuum, _vacuumScheduler);
    }

    public void Stop()
    {
        _running = false;
        LotCompleteAchieved = false;
        ResetRobot(_efem);
        ResetRobot(_vacuum);
        PhaseHint = "가상 이송 · 정지";
        _state.ResetForDemo();
    }

    public void Tick()
    {
        if (!_running)
        {
            return;
        }

        _state.DecrementProcessTimes();

        if (!_efem.IsBusy)
        {
            TrySchedule(_efem, _efemScheduler);
        }

        if (!_vacuum.IsBusy)
        {
            TrySchedule(_vacuum, _vacuumScheduler);
        }

        AdvanceRobot(_efem);
        AdvanceRobot(_vacuum);

        _kpi.OnTick(_state, _efem.IsBusy, _vacuum.IsBusy);

        if (_state.IsLotComplete())
        {
            LotCompleteAchieved = true;
            PhaseHint = $"LOT COMPLETE · {_state.Lot.CompletedCount}/{_state.Lot.TargetCount} · {_kpi.Snapshot(_state.Lot)}";
            _running = false;
            return;
        }

        PhaseHint = BuildPhaseHint();
    }

    public bool IsVirtualDoorClosed(EquipmentRegion region) =>
        !IsDoorOpenForRobot(_efem, region) && !IsDoorOpenForRobot(_vacuum, region);

    public bool HasWaferAt(EquipmentRegion region)
    {
        if (region == EquipmentRegion.Aligner)
        {
            return _state.AlignerBuffer.HasWafer;
        }

        if (region == EquipmentRegion.LoadLock)
        {
            return _state.LoadLockBuffer.HasWafer;
        }

        if (region == EquipmentRegion.SideStorage)
        {
            return _state.SideStorage.HasWafer;
        }

        if (region == EquipmentRegion.FoupA)
        {
            return _state.FoupPorts[0].RemainingInFoup > 0;
        }

        if (region == EquipmentRegion.FoupB)
        {
            return _state.FoupPorts[1].RemainingInFoup > 0;
        }

        if (region == EquipmentRegion.FoupC)
        {
            return _state.FoupPorts[2].RemainingInFoup > 0;
        }

        if (IsNextProcessFoup(region))
        {
            return false;
        }

        return _state.GetChamber(region)?.CurrentWafer is not null;
    }

    private void TrySchedule(RobotRun run, EfemTransferScheduler scheduler)
    {
        int lotBefore = _state.Lot.CompletedCount;
        scheduler.TryScheduleOne(_state, run.Queue, run.Active, _vacuum.Active, _vacuum.Queue);
        int lotDelta = _state.Lot.CompletedCount - lotBefore;
        if (lotDelta > 0)
        {
            _kpi.OnWaferLotCompleted(_state.Lot.CompletedCount);
        }

        StartNextJob(run);
    }

    private void TrySchedule(RobotRun run, VacuumTransferScheduler scheduler)
    {
        scheduler.TryScheduleOne(_state, run.Queue, run.Active, _efem.Active, _efem.Queue);
        StartNextJob(run);
    }

    private void StartNextJob(RobotRun run)
    {
        if (run.Active is not null || run.Queue.Count == 0)
        {
            return;
        }

        run.Active = run.Queue.Dequeue();
        Enter(run, SimPhase.MoveToPickup, 4, run.Active.Pickup, 0.65, false, "픽업 이동");
    }

    private void AdvanceRobot(RobotRun run)
    {
        if (!run.IsBusy)
        {
            return;
        }

        if (run.TicksLeft > 0)
        {
            run.TicksLeft--;
            if (run.TicksLeft > 0)
            {
                return;
            }
        }

        TransferJob job = run.Active!;
        EquipmentRegion pickup = job.Pickup;
        EquipmentRegion dropoff = job.Dropoff;

        switch (run.Phase)
        {
            case SimPhase.MoveToPickup:
                Enter(run, SimPhase.WaitDoorPickupOpen, 4, pickup, 0.60, false, $"{Label(pickup)} 도어 열림");
                break;
            case SimPhase.WaitDoorPickupOpen:
                Enter(run, SimPhase.PickupExtend, 4, pickup, 1.18, false, $"{Label(pickup)} 블레이드 전진");
                break;
            case SimPhase.PickupExtend:
                Enter(run, SimPhase.PickupGrip, 2, pickup, 1.18, false, $"{Label(pickup)} 웨이퍼 그립");
                break;
            case SimPhase.PickupGrip:
                TransferStateMutator.OnPickup(_state, pickup, job.Wafer);
                Enter(run, SimPhase.PickupRetract, 4, pickup, 0.65, true, "픽업 후퇴");
                break;
            case SimPhase.PickupRetract:
                Enter(run, SimPhase.WaitDoorPickupClose, 3, pickup, 0.65, true, $"{Label(pickup)} 도어 닫힘");
                break;
            case SimPhase.WaitDoorPickupClose:
                Enter(run, SimPhase.MoveToDropoff, 4, dropoff, 0.65, true, "드롭 이동");
                break;
            case SimPhase.MoveToDropoff:
                Enter(run, SimPhase.WaitDoorDropoffOpen, 4, dropoff, 0.60, true, $"{Label(dropoff)} 도어 열림");
                break;
            case SimPhase.WaitDoorDropoffOpen:
                Enter(run, SimPhase.DropoffExtend, 4, dropoff, 1.18, true, $"{Label(dropoff)} 블레이드 전진");
                break;
            case SimPhase.DropoffExtend:
                Enter(run, SimPhase.DropoffRelease, 2, dropoff, 1.18, true, $"{Label(dropoff)} 웨이퍼 릴리즈");
                break;
            case SimPhase.DropoffRelease:
                TransferStateMutator.OnDropoff(_state, dropoff, job.Wafer, ProcessTicksFor(dropoff));
                Enter(run, SimPhase.DropoffRetract, 4, dropoff, 0.65, false, "드롭 후퇴");
                break;
            case SimPhase.DropoffRetract:
                Enter(run, SimPhase.WaitDoorDropoffClose, 3, dropoff, 0.65, false, $"{Label(dropoff)} 도어 닫힘");
                break;
            case SimPhase.WaitDoorDropoffClose:
                run.Active = null;
                run.Phase = SimPhase.Idle;
                ParkRobotAtHome(run);
                if (run.Robot == TransferRobotKind.EfemAtmospheric)
                {
                    TrySchedule(_efem, _efemScheduler);
                }
                else
                {
                    TrySchedule(_vacuum, _vacuumScheduler);
                }
                break;
            default:
                run.Active = null;
                run.Phase = SimPhase.Idle;
                break;
        }
    }

    private int ProcessTicksFor(EquipmentRegion dropoff) => dropoff switch
    {
        EquipmentRegion.ChamberA => _capacity.StripProcessTicks,
        EquipmentRegion.ChamberB or EquipmentRegion.ChamberC or EquipmentRegion.ChamberD => _capacity.EtchProcessTicks,
        EquipmentRegion.Aligner => _capacity.AlignProcessTicks,
        _ => 0
    };

    private static void Enter(RobotRun run, SimPhase phase, int ticks, EquipmentRegion region, double ext, bool carrying, string hint)
    {
        run.Phase = phase;
        run.TicksLeft = ticks;
        run.Region = region;
        run.Extension = ext;
        run.Carrying = carrying;
    }

    private static bool IsDoorOpenForRobot(RobotRun run, EquipmentRegion region)
    {
        if (run.Active is null)
        {
            return false;
        }

        if (region == run.Active.Pickup)
        {
            return run.Phase is SimPhase.WaitDoorPickupOpen
                or SimPhase.PickupExtend
                or SimPhase.PickupGrip
                or SimPhase.PickupRetract;
        }

        if (region == run.Active.Dropoff)
        {
            return run.Phase is SimPhase.WaitDoorDropoffOpen
                or SimPhase.DropoffExtend
                or SimPhase.DropoffRelease
                or SimPhase.DropoffRetract;
        }

        return false;
    }

    private string BuildPhaseHint()
    {
        string efem = _efem.IsBusy
            ? $"EFEM·TM @ {Label(_efem.Region)}"
            : _efemScheduler.LastHint;
        string vac = _vacuum.IsBusy
            ? $"TM·진공 @ {Label(_vacuum.Region)}"
            : _vacuumScheduler.LastHint;
        return $"{efem} | {vac}";
    }

    private static void ParkRobotAtHome(RobotRun run)
    {
        run.Region = run.Robot == TransferRobotKind.EfemAtmospheric
            ? EquipmentRegion.EfemRobot
            : EquipmentRegion.TM;
        run.Extension = 0.65;
        run.Carrying = false;
    }

    private static void ResetRobot(RobotRun run)
    {
        run.Queue.Clear();
        run.Active = null;
        run.Phase = SimPhase.Idle;
        run.TicksLeft = 0;
        ParkRobotAtHome(run);
    }

    private static bool IsNextProcessFoup(EquipmentRegion region) =>
        region is EquipmentRegion.NextProcessFoupA
            or EquipmentRegion.NextProcessFoupB
            or EquipmentRegion.NextProcessFoupC
            or EquipmentRegion.ExternalProcess;

    private static string Label(EquipmentRegion r) => r switch
    {
        EquipmentRegion.FoupA => "LP1·FOUP",
        EquipmentRegion.FoupB => "LP2·FOUP",
        EquipmentRegion.FoupC => "LP3·FOUP",
        EquipmentRegion.Aligner => "Aligner",
        EquipmentRegion.SideStorage => "Side Stg",
        EquipmentRegion.NextProcessFoupA => "LP1·다음 FOUP",
        EquipmentRegion.NextProcessFoupB => "LP2·다음 FOUP",
        EquipmentRegion.NextProcessFoupC => "LP3·다음 FOUP",
        EquipmentRegion.LoadLock => "BM",
        EquipmentRegion.ChamberA => "PM1 Strip",
        EquipmentRegion.ChamberB => "PM2 Etch",
        EquipmentRegion.ChamberC => "PM3 Etch",
        EquipmentRegion.ChamberD => "PM4 Etch",
        _ => r.ToString()
    };
}

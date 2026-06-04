using etch_ui.Equipment.Helpers;
using etch_ui.Equipment.Models;
using etch_ui.Services.Scheduling;

namespace etch_ui.Services.Simulation;

/// <summary>
/// 이중 TM 시뮬 — EFEM(좌) / 진공 TM(우) 스케줄러 분리, Load Lock(2) 경계.
/// 진공 TM 듀얼 블레이드(2슬롯): Etch 연속 픽업 후 PM1 Strip 순차 드롭.
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
        WaitDoorDropoffClose,
        RotateBlade
    }

    private sealed class RobotRun
    {
        public readonly Queue<TransferJob> Queue = new();
        public readonly Queue<TransferJob> PendingDropoffs = new();
        public TransferJob? Active;
        public SimPhase Phase = SimPhase.Idle;
        public int TicksLeft;
        public readonly TransferRobotKind Robot;
        public readonly RobotBladeSlots Blades;
        public EquipmentRegion Region = EquipmentRegion.TM;
        public double Extension = 0.65;
        public double FacingAngleDegrees = -125;
        public double TargetFacingAngleDegrees;
        public int ActiveBladeSlot = VacuumDualBladePlanner.FrontBladeSlot;
        public bool LegIsPickup = true;

        public RobotRun(TransferRobotKind robot, int bladeCapacity)
        {
            Robot = robot;
            Blades = new RobotBladeSlots(bladeCapacity);
        }

        public bool IsBusy => Active is not null;
        public bool Carrying => Blades.OccupiedCount > 0;
    }

    private readonly EquipmentCapacityConfig _capacity;
    private readonly ClusterEquipmentState _state;
    private readonly EfemTransferScheduler _efemScheduler = new();
    private readonly VacuumTransferScheduler _vacuumScheduler = new();
    private readonly RobotRun _efem;
    private readonly RobotRun _vacuum;
    private readonly ThroughputKpiTracker _kpi = new();
    private readonly int _vacuumBladeCapacity;
    private readonly int _efemBladeCapacity;
    private readonly DualBladePipelineMetrics _dualBladeMetrics = new();
    private bool _running;

    public TmTransferSimulator(EquipmentCapacityConfig? capacity = null, int? vacuumBladeCapacity = null, int efemBladeCapacity = 1)
    {
        _capacity = capacity ?? EquipmentCapacityConfig.Default;
        _vacuumBladeCapacity = Math.Max(1, vacuumBladeCapacity ?? _capacity.VacuumBladeSlotCount);
        _efemBladeCapacity = Math.Max(1, efemBladeCapacity);
        _state = new ClusterEquipmentState(_capacity);
        _efem = new RobotRun(TransferRobotKind.EfemAtmospheric, _efemBladeCapacity);
        _vacuum = new RobotRun(TransferRobotKind.VacuumTm, _vacuumBladeCapacity);
    }

    public bool IsActive => _running && (_efem.IsBusy || _vacuum.IsBusy || _efem.Carrying || _vacuum.Carrying);
    public SimPhase Phase => _vacuum.Phase;
    public EquipmentRegion TmRegion => _vacuum.Region;
    public TransferRobotKind ActiveRobot => _vacuum.IsBusy ? TransferRobotKind.VacuumTm : TransferRobotKind.EfemAtmospheric;
    public double BladeExtension => _vacuum.Extension;
    public bool CarryingWafer => _vacuum.Carrying;
    public bool VacuumCarryingSlotA => _vacuum.Blades.HasWafer(0);
    public bool VacuumCarryingSlotB => _vacuum.Blades.HasWafer(1);
    public int VacuumBladeCapacity => _vacuumBladeCapacity;
    public int EfemBladeCapacity => _efemBladeCapacity;
    public double VacuumFacingAngleDegrees => _vacuum.FacingAngleDegrees;
    public int VacuumActiveBladeSlot => _vacuum.ActiveBladeSlot;
    public bool VacuumIsRotatingBlade => _vacuum.Phase == SimPhase.RotateBlade;
    public DualBladePipelineMetrics DualBladeMetrics => _dualBladeMetrics;

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
        _dualBladeMetrics.Reset();
        _vacuum.FacingAngleDegrees = -125;
        _vacuum.ActiveBladeSlot = VacuumDualBladePlanner.FrontBladeSlot;
        _state.ResetForDemo();
        TrySchedule(_efem, _efemScheduler);
        TrySchedule(_vacuum, _vacuumScheduler);
    }

    /// <summary>듀얼 블레이드 헤드리스 검증 — PM2·PM3 Etch 완료 2매를 즉시 배치 가능 상태로 둠.</summary>
    public void SeedDualBladePipelineProbe()
    {
        _state.Pm1.ClearWafer();
        _state.Pm1.ReservedForIncoming = false;
        SeedEtchReadyChamber(_state.Pm2);
        SeedEtchReadyChamber(_state.Pm3);
        ResetRobot(_vacuum);
        TrySchedule(_vacuum, _vacuumScheduler);
        StartNextJob(_vacuum);
    }

    private static void SeedEtchReadyChamber(PmChamberState chamber)
    {
        chamber.ClearWafer();
        chamber.CurrentWafer = new WaferTrack(LoadPortId.Lp1, EquipmentRegion.FoupA)
        {
            HasCompletedEtch = true
        };
        chamber.RemainingProcessTicks = 0;
        chamber.PickupScheduled = false;
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
            TryResumePendingDrops(_vacuum);
        }

        AdvanceRobot(_efem);
        AdvanceRobot(_vacuum);

        _kpi.OnTick(_state, _efem.IsBusy || _efem.Carrying, _vacuum.IsBusy || _vacuum.Carrying);

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
        bool restrictInbound = run.Blades.OccupiedCount > 0 || run.PendingDropoffs.Count > 0;
        int queuedBefore = run.Queue.Count;
        scheduler.TryScheduleOne(
            _state,
            run.Queue,
            run.Active,
            _efem.Active,
            _efem.Queue,
            run.Blades,
            _vacuumBladeCapacity,
            restrictInbound);
        if (run.Queue.Count - queuedBefore >= 2)
        {
            _dualBladeMetrics.DualBatchEnqueueCount++;
        }

        StartNextJob(run);
    }

    private void TryResumePendingDrops(RobotRun run)
    {
        if (run.Active is not null || run.PendingDropoffs.Count == 0)
        {
            return;
        }

        TransferJob next = run.PendingDropoffs.Peek();
        if (next.Dropoff == EquipmentRegion.ChamberA && !CanPlaceOnPm1())
        {
            return;
        }

        StartNextJob(run);
    }

    private void StartNextJob(RobotRun run)
    {
        if (run.Active is not null)
        {
            return;
        }

        if (run.PendingDropoffs.Count > 0)
        {
            TransferJob drop = run.PendingDropoffs.Dequeue();
            if (drop.Dropoff == EquipmentRegion.ChamberA && !CanPlaceOnPm1())
            {
                run.PendingDropoffs.Enqueue(drop);
                return;
            }

            run.Active = drop;
            if (TryBeginVacuumLeg(run, drop, isPickup: false))
            {
                return;
            }

            Enter(run, SimPhase.MoveToDropoff, 4, run.Active.Dropoff, 0.65, true, "블레이드 드롭 이동");
            return;
        }

        if (run.Queue.Count == 0)
        {
            return;
        }

        run.Active = run.Queue.Dequeue();
        if (run.Queue.Count > 0 && run.Robot == TransferRobotKind.VacuumTm && _vacuumBladeCapacity >= 2)
        {
            _dualBladeMetrics.DualBatchEnqueueCount = Math.Max(_dualBladeMetrics.DualBatchEnqueueCount, run.Queue.Count + 1);
        }

        if (TryBeginVacuumLeg(run, run.Active, isPickup: true))
        {
            return;
        }

        Enter(run, SimPhase.MoveToPickup, 4, run.Active.Pickup, 0.65, run.Carrying, "픽업 이동");
    }

    private bool TryBeginVacuumLeg(RobotRun run, TransferJob job, bool isPickup)
    {
        if (run.Robot != TransferRobotKind.VacuumTm || _vacuumBladeCapacity < 2)
        {
            run.ActiveBladeSlot = job.BladeSlotIndex;
            return false;
        }

        EquipmentRegion face = isPickup ? job.Pickup : job.Dropoff;
        double targetAngle = VacuumDualBladePlanner.AngleForBlade(face, job.BladeSlotIndex);
        run.ActiveBladeSlot = job.BladeSlotIndex;
        run.LegIsPickup = isPickup;

        if (Math.Abs(NormalizeAngleDiff(targetAngle - run.FacingAngleDegrees)) < 10.0)
        {
            run.FacingAngleDegrees = targetAngle;
            return false;
        }

        run.TargetFacingAngleDegrees = targetAngle;
        run.Phase = SimPhase.RotateBlade;
        run.TicksLeft = 12;
        run.Region = face;
        run.Extension = 0.65;
        _dualBladeMetrics.RotateBladeCount++;
        return true;
    }

    private static double NormalizeAngleDiff(double diff)
    {
        while (diff > 180)
        {
            diff -= 360;
        }

        while (diff < -180)
        {
            diff += 360;
        }

        return diff;
    }

    private bool CanPlaceOnPm1() => _state.Pm1.CurrentWafer is null;

    private void AdvanceRobot(RobotRun run)
    {
        if (!run.IsBusy)
        {
            return;
        }

        if (run.TicksLeft > 0)
        {
            if (run.Phase == SimPhase.RotateBlade && run.Robot == TransferRobotKind.VacuumTm)
            {
                run.FacingAngleDegrees += NormalizeAngleDiff(run.TargetFacingAngleDegrees - run.FacingAngleDegrees) * 0.28;
            }

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
            case SimPhase.RotateBlade:
                run.FacingAngleDegrees = run.TargetFacingAngleDegrees;
                if (run.LegIsPickup)
                {
                    Enter(run, SimPhase.MoveToPickup, 4, job.Pickup, 0.65, run.Carrying, $"픽업 · {VacuumDualBladePlanner.SlotLabel(job.BladeSlotIndex)}");
                }
                else
                {
                    Enter(run, SimPhase.MoveToDropoff, 4, job.Dropoff, 0.65, true, $"드롭 · {VacuumDualBladePlanner.SlotLabel(job.BladeSlotIndex)}");
                }

                break;
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
                run.Blades.Place(job.BladeSlotIndex, job.Wafer);
                _dualBladeMetrics.OnBladePlace(job.BladeSlotIndex, run.Blades.OccupiedCount);
                Enter(run, SimPhase.PickupRetract, 4, pickup, 0.65, true, "픽업 후퇴");
                break;
            case SimPhase.PickupRetract:
                Enter(run, SimPhase.WaitDoorPickupClose, 3, pickup, 0.65, true, $"{Label(pickup)} 도어 닫힘");
                break;
            case SimPhase.WaitDoorPickupClose:
                run.PendingDropoffs.Enqueue(job);
                run.Active = null;
                if (TryChainEtchPickup(run))
                {
                    break;
                }

                StartNextJob(run);
                break;
            case SimPhase.MoveToDropoff:
                if (dropoff == EquipmentRegion.ChamberA && !CanPlaceOnPm1())
                {
                    run.PendingDropoffs.Enqueue(job);
                    run.Active = null;
                    run.Phase = SimPhase.Idle;
                    ParkRobotAtHome(run);
                    break;
                }

                if (dropoff == EquipmentRegion.ChamberA && _state.Pm1.ReservedForIncoming && _state.Pm1.CurrentWafer is null)
                {
                    _state.Pm1.ReservedForIncoming = false;
                }

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
                run.Blades.Remove(job.BladeSlotIndex);
                if (dropoff == EquipmentRegion.ChamberA)
                {
                    _state.Pm1.ReservedForIncoming = false;
                }

                Enter(run, SimPhase.DropoffRetract, 4, dropoff, 0.65, run.Carrying, "드롭 후퇴");
                break;
            case SimPhase.DropoffRetract:
                Enter(run, SimPhase.WaitDoorDropoffClose, 3, dropoff, 0.65, run.Carrying, $"{Label(dropoff)} 도어 닫힘");
                break;
            case SimPhase.WaitDoorDropoffClose:
                FinishLeg(run);
                break;
            default:
                FinishLeg(run);
                break;
        }
    }

    private bool TryChainEtchPickup(RobotRun run)
    {
        if (run.Robot != TransferRobotKind.VacuumTm || _vacuumBladeCapacity < 2 || run.Queue.Count == 0)
        {
            return false;
        }

        TransferJob? lastPending = null;
        foreach (TransferJob j in run.PendingDropoffs)
        {
            lastPending = j;
        }

        if (lastPending is null)
        {
            return false;
        }

        TransferJob next = run.Queue.Peek();
        if (!VacuumDualBladePlanner.CanChainPickup(run.Blades, lastPending, next))
        {
            return false;
        }

        run.Active = run.Queue.Dequeue();
        _dualBladeMetrics.ChainPickupCount++;
        if (TryBeginVacuumLeg(run, run.Active, isPickup: true))
        {
            return true;
        }

        Enter(run, SimPhase.MoveToPickup, 4, run.Active.Pickup, 0.65, true, "듀얼 픽업 연속");
        return true;
    }

    private void FinishLeg(RobotRun run)
    {
        run.Active = null;
        run.Phase = SimPhase.Idle;
        if (run.Carrying)
        {
            if (run.PendingDropoffs.Count > 0)
            {
                StartNextJob(run);
                return;
            }

            ParkRobotAtHome(run);
            if (run.Robot == TransferRobotKind.VacuumTm)
            {
                TrySchedule(_vacuum, _vacuumScheduler);
            }

            return;
        }

        ParkRobotAtHome(run);
        if (run.Robot == TransferRobotKind.EfemAtmospheric)
        {
            TrySchedule(_efem, _efemScheduler);
        }
        else
        {
            TrySchedule(_vacuum, _vacuumScheduler);
        }

        StartNextJob(run);
    }

    private int ProcessTicksFor(EquipmentRegion dropoff) => dropoff switch
    {
        EquipmentRegion.ChamberA => _capacity.StripProcessTicks,
        EquipmentRegion.ChamberB or EquipmentRegion.ChamberC or EquipmentRegion.ChamberD => _capacity.EtchProcessTicks,
        EquipmentRegion.Aligner => _capacity.AlignProcessTicks,
        _ => 0
    };

    private void Enter(RobotRun run, SimPhase phase, int ticks, EquipmentRegion region, double ext, bool carrying, string hint)
    {
        run.Phase = phase;
        run.TicksLeft = ticks;
        run.Region = region;
        run.Extension = ext;
        if (run.Robot == TransferRobotKind.VacuumTm && run.Active is TransferJob job)
        {
            run.ActiveBladeSlot = job.BladeSlotIndex;
            run.FacingAngleDegrees = VacuumDualBladePlanner.AngleForBlade(region, job.BladeSlotIndex);
        }

        _ = carrying;
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
        if (_vacuumBladeCapacity >= 2)
        {
            if (_vacuum.Phase == SimPhase.RotateBlade)
            {
                vac = $"TM 180° 회전 · {VacuumDualBladePlanner.SlotLabel(_vacuum.ActiveBladeSlot)}";
            }
            else if (_vacuum.Blades.OccupiedCount > 0)
            {
                vac += $" · {VacuumDualBladePlanner.SlotLabel(_vacuum.ActiveBladeSlot)} · 슬롯 {_vacuum.Blades.OccupiedCount}/{_vacuumBladeCapacity}";
            }
        }

        return $"{efem} | {vac}";
    }

    private static void ParkRobotAtHome(RobotRun run)
    {
        run.Region = run.Robot == TransferRobotKind.EfemAtmospheric
            ? EquipmentRegion.EfemRobot
            : EquipmentRegion.TM;
        run.Extension = 0.65;
    }

    private static void ResetRobot(RobotRun run)
    {
        run.Queue.Clear();
        run.PendingDropoffs.Clear();
        run.Active = null;
        run.Blades.Clear();
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

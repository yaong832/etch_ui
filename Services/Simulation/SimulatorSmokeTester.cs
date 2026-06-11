using System.IO;
using System.Text.Json;
using etch_ui.Equipment.Models;
using etch_ui.Services.Hmi;
using etch_ui.Services.Scheduling;
using etch_ui.ViewModels;

namespace etch_ui.Services.Simulation;

public static class SimulatorSmokeTester
{
    public sealed class Result
    {
        public bool Success { get; init; }
        public int Ticks { get; init; }
        public int Runs { get; init; } = 1;
        public int MaxSideStorage { get; init; }
        public string Message { get; init; } = string.Empty;
        public string? Report { get; init; }
    }

    public static Result Run(int ticks, EquipmentCapacityConfig? capacity = null, bool report = false)
    {
        if (ticks <= 0)
        {
            return new Result { Success = false, Ticks = 0, Message = "ticks must be > 0" };
        }

        if (!ValidatePickPolicy(out string pickError))
        {
            return new Result { Success = false, Ticks = ticks, Message = pickError };
        }

        if (!ValidatePipelineSelector(out string pipelineError))
        {
            return new Result { Success = false, Ticks = ticks, Message = pipelineError };
        }

        if (!ValidateAdmissionPolicy(out string admissionError))
        {
            return new Result { Success = false, Ticks = ticks, Message = admissionError };
        }

        if (!ValidateVacuumInboundPolicy(out string inboundError))
        {
            return new Result { Success = false, Ticks = ticks, Message = inboundError };
        }

        if (!ValidateEfemBladeBufferPolicy(out string efemBufferError))
        {
            return new Result { Success = false, Ticks = ticks, Message = efemBufferError };
        }

        if (!ValidateFoupPickupPolicy(out string foupPickupError))
        {
            return new Result { Success = false, Ticks = ticks, Message = foupPickupError };
        }

        if (!ValidateAlarmCatalog(out string alarmCatalogError))
        {
            return new Result { Success = false, Ticks = ticks, Message = alarmCatalogError };
        }

        if (!ValidateConnectionPresenter(out string connectionError))
        {
            return new Result { Success = false, Ticks = ticks, Message = connectionError };
        }

        if (!ValidateFlaskTelemetryContract(out string flaskContractError))
        {
            return new Result { Success = false, Ticks = ticks, Message = flaskContractError };
        }

        var sim = new TmTransferSimulator(capacity);
        sim.StartDemoLoop();

        int maxSideStorage = 0;
        try
        {
            int motionSteps = capacity?.VacuumMotionStepsPerUiTick
                ?? EquipmentCapacityConfig.Default.VacuumMotionStepsPerUiTick;
            for (int i = 0; i < ticks; i++)
            {
                sim.Tick(motionSteps);
                ValidateState(sim.ClusterState);
                maxSideStorage = Math.Max(maxSideStorage, sim.SideStorageOccupancy);
            }
        }
        catch (Exception ex)
        {
            return new Result
            {
                Success = false,
                Ticks = ticks,
                MaxSideStorage = maxSideStorage,
                Message = $"failed: {ex.Message}"
            };
        }

        string? reportText = report ? BuildReport(sim, ticks, maxSideStorage) : null;
        if (report
            && ticks >= 120_000
            && sim.LotCompletedCount == 0
            && !sim.LotCompleteAchieved
            && sim.ClusterState.SideStorage.Count == 0
            && sim.ClusterState.FoupPorts.Sum(p => p.RemainingInFoup) >= sim.ClusterState.Capacity.FoupSlotCount * 3 - 2)
        {
            return new Result
            {
                Success = false,
                Ticks = ticks,
                MaxSideStorage = maxSideStorage,
                Message = "pipeline stall: no wafer reached Side Stg (check PM1/TM queue)",
                Report = reportText
            };
        }

        return new Result
        {
            Success = true,
            Ticks = ticks,
            MaxSideStorage = maxSideStorage,
            Message = "ok",
            Report = reportText
        };
    }

    public static string BuildReport(TmTransferSimulator sim, int ticks, int maxSideStorage)
    {
        (int efemQ, int vacQ, int vacPending, int vacBlades) = sim.GetQueueDiagnostics();
        ClusterEquipmentState s = sim.ClusterState;
        int foupRem = s.FoupPorts.Sum(p => p.RemainingInFoup);
        int inflight = s.FoupPorts.Sum(p => p.InFlightCount);
        int etchBusy = new[] { s.Pm2, s.Pm3, s.Pm4 }.Count(p => p.CurrentWafer is not null);
        var kpi = sim.KpiSnapshot;

        return string.Join(" | ",
        [
            $"ticks={ticks}",
            $"etch={s.Capacity.EtchProcessTicks}",
            $"strip={s.Capacity.StripProcessTicks}",
            $"lot={s.Lot.CompletedCount}/{s.Lot.TargetCount}",
            $"foup_rem={foupRem}/75",
            $"inflight={inflight}",
            $"aligner={s.AlignerBuffer.Count}/{s.AlignerBuffer.Capacity} max_obs={Math.Min(s.AlignerBuffer.Count, s.AlignerBuffer.Capacity)}",
            $"bm={s.LoadLockBuffer.Count}/{s.LoadLockBuffer.Capacity}",
            $"side={s.SideStorage.Count}/{s.SideStorage.Capacity}",
            $"max_side={maxSideStorage}",
            $"etch_pm_busy={etchBusy}/3",
            $"lot_done={sim.LotCompleteAchieved}",
            $"kpi={kpi}",
            $"hint={sim.PhaseHint}",
            $"q=efem{efemQ}/vac{vacQ} pend{vacPending} blade{vacBlades}",
            $"efem_max_blades={sim.MaxEfemBladesOccupied}",
            $"pipeline={DiagnosePipeline(s)}",
            $"etch_inbound=PM2:{s.Pm2EtchInboundCount} PM3:{s.Pm3EtchInboundCount} PM4:{s.Pm4EtchInboundCount}"
        ]);
    }

    private static string DiagnosePipeline(ClusterEquipmentState s)
    {
        var parts = new List<string>
        {
            $"side={s.SideStorage.Count}",
            $"align={s.AlignerBuffer.Count}",
            $"bm={s.LoadLockBuffer.Count}"
        };

        foreach (KeyValuePair<EquipmentRegion, PmChamberState> kv in s.Chambers)
        {
            WaferTrack? w = kv.Value.CurrentWafer;
            if (w is null)
            {
                continue;
            }

            parts.Add(
                $"{kv.Key}=#{w.Id} e={w.HasCompletedEtch} s={w.HasCompletedStrip} t={kv.Value.RemainingProcessTicks} ps={kv.Value.PickupScheduled} ri={kv.Value.ReservedForIncoming}");
        }

        return string.Join(" ", parts);
    }

    /// <summary>EFEM 듀얼(2슬롯) 가정 시뮬 — 동시 2매 적재가 발생하는지 검증.</summary>
    public static Result RunEfemDualBladeAudit(int ticks = 40_000)
    {
        if (ticks <= 0)
        {
            return new Result { Success = false, Message = "ticks must be > 0" };
        }

        var cfg = new EquipmentCapacityConfig { EfemBladeSlotCount = 2 };
        var sim = new TmTransferSimulator(cfg, efemBladeCapacity: 2);
        sim.StartDemoLoop();

        try
        {
            int motionSteps = cfg.VacuumMotionStepsPerUiTick;
            for (int i = 0; i < ticks; i++)
            {
                sim.Tick(motionSteps);
                ValidateState(sim.ClusterState);
            }
        }
        catch (Exception ex)
        {
            return new Result { Success = false, Ticks = ticks, Message = $"efem-audit failed: {ex.Message}" };
        }

        bool dualNeeded = sim.MaxEfemBladesOccupied >= 2;
        return new Result
        {
            Success = true,
            Ticks = ticks,
            Message = dualNeeded
                ? $"efem dual used: max_blades={sim.MaxEfemBladesOccupied}"
                : $"efem dual NOT needed: max_blades={sim.MaxEfemBladesOccupied}/2 (serial FOUP→BM)",
            Report = BuildReport(sim, ticks, sim.SideStorageOccupancy)
        };
    }

    public static Result RunDualBlade(int ticks = 12000)
    {
        if (ticks <= 0)
        {
            return new Result { Success = false, Message = "ticks must be > 0" };
        }

        EquipmentCapacityConfig cfg = new()
        {
            FoupSlotCount = 25,
            SideStorageSlotCount = 25,
            EtchProcessTicks = 12,
            StripProcessTicks = 6,
            VacuumBladeSlotCount = 2
        };

        var sim = new TmTransferSimulator(cfg, vacuumBladeCapacity: 2);
        sim.StartDemoLoop();
        sim.SeedDualBladePipelineProbe();

        try
        {
            for (int i = 0; i < ticks; i++)
            {
                sim.Tick();
                ValidateState(sim.ClusterState);
            }
        }
        catch (Exception ex)
        {
            return new Result { Success = false, Ticks = ticks, Message = $"dual-blade failed: {ex.Message}" };
        }

        DualBladePipelineMetrics m = sim.DualBladeMetrics;
        if (m.MaxBladesOccupied < 2)
        {
            return new Result
            {
                Success = false,
                Ticks = ticks,
                Message = $"dual-blade: max occupied {m.MaxBladesOccupied}/2"
            };
        }

        if (!m.BothSlotsUsed)
        {
            return new Result
            {
                Success = false,
                Ticks = ticks,
                Message = $"dual-blade: slot A={m.SlotAPlaceCount} B={m.SlotBPlaceCount}"
            };
        }

        if (m.ChainPickupCount < 1)
        {
            return new Result
            {
                Success = false,
                Ticks = ticks,
                Message = $"dual-blade: chain pickups {m.ChainPickupCount}"
            };
        }

        if (m.RotateBladeCount < 1)
        {
            return new Result
            {
                Success = false,
                Ticks = ticks,
                Message = $"dual-blade: 180° rotates {m.RotateBladeCount}"
            };
        }

        return new Result
        {
            Success = true,
            Ticks = ticks,
            Message = $"ok max={m.MaxBladesOccupied} chain={m.ChainPickupCount} rot={m.RotateBladeCount} A={m.SlotAPlaceCount} B={m.SlotBPlaceCount} batch={m.DualBatchEnqueueCount}"
        };
    }

    public static Result RunBatch(int runs, int ticksPerRun)
    {
        if (runs <= 0 || ticksPerRun <= 0)
        {
            return new Result { Success = false, Message = "runs/ticks must be > 0" };
        }

        int maxStorage = 0;
        for (int i = 0; i < runs; i++)
        {
            EquipmentCapacityConfig cfg = new()
            {
                FoupSlotCount = 25,
                SideStorageSlotCount = 25,
                EtchProcessTicks = 75,
                StripProcessTicks = 20
            };
            Result result = Run(ticksPerRun, cfg);
            if (!result.Success)
            {
                return new Result
                {
                    Success = false,
                    Runs = i + 1,
                    Ticks = ticksPerRun,
                    MaxSideStorage = maxStorage,
                    Message = $"run#{i + 1} failed: {result.Message}"
                };
            }

            maxStorage = Math.Max(maxStorage, result.MaxSideStorage);
        }

        return new Result
        {
            Success = true,
            Runs = runs,
            Ticks = ticksPerRun,
            MaxSideStorage = maxStorage,
            Message = "ok"
        };
    }

    private static bool ValidatePickPolicy(out string error)
    {
        error = string.Empty;
        FoupPortState[] ports =
        [
            new(LoadPortId.Lp1) { IsMounted = true, RemainingInFoup = 25, InFlightCount = 0 },
            new(LoadPortId.Lp2) { IsMounted = true, RemainingInFoup = 10, InFlightCount = 0 },
            new(LoadPortId.Lp3) { IsMounted = true, RemainingInFoup = 0, InFlightCount = 0 }
        ];
        var scheduler = new FoupPickScheduler(ports, 25);
        FoupPortState? selected = scheduler.SelectNextPickSource();
        if (selected?.PortId != LoadPortId.Lp2)
        {
            error = "FOUP policy mismatch: full-stock block did not prefer partial lot";
            return false;
        }

        ports[0].RemainingInFoup = 20;
        ports[1].RemainingInFoup = 20;
        ports[2].RemainingInFoup = 20;
        selected = scheduler.SelectNextPickSource();
        if (selected?.PortId != LoadPortId.Lp1)
        {
            error = "FOUP policy mismatch: tie-break (LP1->2->3) failed";
            return false;
        }

        ports[0].RemainingInFoup = 0;
        ports[0].InFlightCount = 25;
        ports[1].RemainingInFoup = 25;
        ports[2].RemainingInFoup = 25;
        selected = scheduler.SelectNextPickSource();
        if (selected?.PortId != LoadPortId.Lp2)
        {
            error = "FOUP policy mismatch: LP1 drain-only inflight should allow LP2 full FOUP";
            return false;
        }

        return true;
    }

    private static bool ValidatePipelineSelector(out string error)
    {
        var chambers = new ClusterEquipmentState(new EquipmentCapacityConfig()).Chambers;

        static void SetBusy(IReadOnlyDictionary<EquipmentRegion, PmChamberState> ch, EquipmentRegion r, bool busy)
        {
            PmChamberState pm = ch[r];
            if (busy)
            {
                pm.CurrentWafer = new WaferTrack(LoadPortId.Lp1, EquipmentRegion.NextProcessFoupA);
                pm.RemainingProcessTicks = 50;
            }
            else
            {
                pm.ClearWafer();
                pm.ReservedForIncoming = false;
            }
        }

        SetBusy(chambers, EquipmentRegion.ChamberB, true);
        SetBusy(chambers, EquipmentRegion.ChamberC, false);
        SetBusy(chambers, EquipmentRegion.ChamberD, false);
        if (EtchPmSelector.SelectNextPipelineTarget(chambers) != EquipmentRegion.ChamberC)
        {
            error = "pipeline: PM2 busy should target PM3";
            return false;
        }

        SetBusy(chambers, EquipmentRegion.ChamberB, true);
        SetBusy(chambers, EquipmentRegion.ChamberC, true);
        SetBusy(chambers, EquipmentRegion.ChamberD, false);
        if (EtchPmSelector.SelectNextPipelineTarget(chambers) != EquipmentRegion.ChamberD)
        {
            error = "pipeline: PM2,3 busy should target PM4";
            return false;
        }

        SetBusy(chambers, EquipmentRegion.ChamberB, true);
        SetBusy(chambers, EquipmentRegion.ChamberC, false);
        SetBusy(chambers, EquipmentRegion.ChamberD, true);
        if (EtchPmSelector.SelectNextPipelineTarget(chambers) != EquipmentRegion.ChamberC)
        {
            error = "pipeline: PM2,4 busy should target PM3";
            return false;
        }

        SetBusy(chambers, EquipmentRegion.ChamberB, false);
        SetBusy(chambers, EquipmentRegion.ChamberC, true);
        SetBusy(chambers, EquipmentRegion.ChamberD, false);
        if (EtchPmSelector.SelectNextPipelineTarget(chambers) != EquipmentRegion.ChamberB)
        {
            error = "pipeline: PM3 only busy should target PM2";
            return false;
        }

        SetBusy(chambers, EquipmentRegion.ChamberB, true);
        SetBusy(chambers, EquipmentRegion.ChamberC, true);
        SetBusy(chambers, EquipmentRegion.ChamberD, true);
        if (EtchPmSelector.SelectNextPipelineTarget(chambers) is not null)
        {
            error = "pipeline: all busy should return null";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool ValidateEfemBladeBufferPolicy(out string error)
    {
        var cfg = new EquipmentCapacityConfig { EfemBladeSlotCount = 2, AlignerSlotCount = 2, LoadLockSlotCount = 3 };
        var state = new ClusterEquipmentState(cfg);
        FillAllEtchPms(state);

        for (int i = 0; i < cfg.AlignerSlotCount; i++)
        {
            state.AlignerBuffer.TryEnqueue(new WaferTrack(LoadPortId.Lp1, EquipmentRegion.NextProcessFoupA), i);
        }

        var pre = new WaferTrack(LoadPortId.Lp2, EquipmentRegion.NextProcessFoupB);
        var pre2 = new WaferTrack(LoadPortId.Lp3, EquipmentRegion.NextProcessFoupC);
        state.LoadLockBuffer.TryEnqueue(pre, 0);
        state.LoadLockBuffer.TryEnqueue(pre2, 1);

        var blades = new RobotBladeSlots(2);
        blades.Place(0, new WaferTrack(LoadPortId.Lp1, EquipmentRegion.FoupA));
        blades.Place(1, new WaferTrack(LoadPortId.Lp1, EquipmentRegion.FoupA));

        var scheduler = new EfemTransferScheduler();
        var queue = new Queue<TransferJob>();
        var pending = new Queue<TransferJob>();
        pending.Enqueue(new TransferJob
        {
            Wafer = blades.Get(0)!,
            Pickup = EquipmentRegion.FoupA,
            Dropoff = EquipmentRegion.Aligner
        });

        if (scheduler.TryScheduleOne(
                state,
                queue,
                activeJob: null,
                vacuumActiveJob: null,
                efemBlades: blades,
                efemBladeCapacity: 2,
                efemPendingDropoffs: pending) > 0)
        {
            error = "efem-buffer: should not schedule FOUP when blades are full";
            return false;
        }

        blades = new RobotBladeSlots(2);
        blades.Place(0, new WaferTrack(LoadPortId.Lp1, EquipmentRegion.FoupA));
        pending = new Queue<TransferJob>();
        pending.Enqueue(new TransferJob
        {
            Wafer = blades.Get(0)!,
            Pickup = EquipmentRegion.FoupA,
            Dropoff = EquipmentRegion.Aligner
        });
        pending.Enqueue(new TransferJob
        {
            Wafer = new WaferTrack(LoadPortId.Lp1, EquipmentRegion.FoupA),
            Pickup = EquipmentRegion.FoupA,
            Dropoff = EquipmentRegion.Aligner
        });

        if (scheduler.TryScheduleOne(
                state,
                queue,
                activeJob: null,
                vacuumActiveJob: null,
                efemBlades: blades,
                efemBladeCapacity: 2,
                efemPendingDropoffs: pending) > 0)
        {
            error = "efem-buffer: should not schedule FOUP when pending aligner drops saturate blades";
            return false;
        }

        var alignState = new ClusterEquipmentState(new EquipmentCapacityConfig
        {
            EfemBladeSlotCount = 2,
            AlignerSlotCount = 2,
            LoadLockSlotCount = 3
        });
        var ready = new WaferTrack(LoadPortId.Lp1, EquipmentRegion.NextProcessFoupA);
        alignState.AlignerBuffer.TryEnqueue(ready, 0);

        var alignScheduler = new EfemTransferScheduler();
        var alignQueue = new Queue<TransferJob>();
        alignScheduler.TryScheduleOne(
            alignState,
            alignQueue,
            activeJob: null,
            vacuumActiveJob: null,
            efemBlades: new RobotBladeSlots(2),
            efemBladeCapacity: 2);

        if (alignQueue.Count == 0)
        {
            error = "efem-aligner-pick: expected Aligner→BM job";
            return false;
        }

        TransferJob alignJob = alignQueue.Peek();
        if (alignJob.BladeSlotIndex != VacuumDualBladePlanner.UnresolvedBladeSlot)
        {
            error = "efem-aligner-pick: slot should be unresolved at enqueue";
            return false;
        }

        var efemBlades = new RobotBladeSlots(2);
        int nearest = VacuumDualBladePlanner.PickNearestFreeBlade(
            EquipmentRegion.Aligner,
            TransferRobotKind.EfemAtmospheric,
            -90,
            efemBlades,
            2);
        if (nearest < 0)
        {
            error = "efem-aligner-pick: expected nearest free blade at Aligner";
            return false;
        }

        efemBlades.Place(nearest, ready);
        int opposite = VacuumDualBladePlanner.PickNearestFreeBlade(
            EquipmentRegion.Aligner,
            TransferRobotKind.EfemAtmospheric,
            -90,
            efemBlades,
            2);
        if (opposite == nearest || opposite < 0)
        {
            error = "efem-aligner-pick: occupied blade should force opposite slot";
            return false;
        }

        var bufferState = new ClusterEquipmentState(new EquipmentCapacityConfig
        {
            EfemBladeSlotCount = 2,
            AlignerSlotCount = 2,
            LoadLockSlotCount = 3
        });
        for (int i = 0; i < bufferState.Capacity.AlignerSlotCount; i++)
        {
            bufferState.AlignerBuffer.TryEnqueue(
                new WaferTrack(LoadPortId.Lp1, EquipmentRegion.NextProcessFoupA),
                i);
        }

        var bmPre1 = new WaferTrack(LoadPortId.Lp2, EquipmentRegion.NextProcessFoupB);
        var bmPre2 = new WaferTrack(LoadPortId.Lp3, EquipmentRegion.NextProcessFoupC);
        bufferState.LoadLockBuffer.TryEnqueue(bmPre1, 0);
        bufferState.LoadLockBuffer.TryEnqueue(bmPre2, 1);
        bufferState.FoupPorts[0].RemainingInFoup = 5;

        var bufferQueue = new Queue<TransferJob>();
        var bufferScheduler = new EfemTransferScheduler();
        if (bufferScheduler.TryScheduleOne(
                bufferState,
                bufferQueue,
                activeJob: null,
                vacuumActiveJob: null,
                efemBlades: new RobotBladeSlots(2),
                efemBladeCapacity: 2) <= 0
            || bufferQueue.Count == 0)
        {
            error = "efem-buffer: expected FOUP blade-buffer job when Align+BM Pre full";
            return false;
        }

        TransferJob bufferJob = bufferQueue.Peek();
        if (bufferJob.Pickup != EquipmentRegion.FoupA || bufferJob.Dropoff != EquipmentRegion.Aligner)
        {
            error = "efem-buffer: blade-buffer must stay FOUP→Aligner (not Load Lock)";
            return false;
        }

        if (bufferState.FoupPorts[0].ReservedForPickupCount != 1)
        {
            error = "efem-buffer: FOUP reservation missing after blade-buffer schedule";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool ValidateFoupPickupPolicy(out string error)
    {
        var cfg = new EquipmentCapacityConfig { EfemBladeSlotCount = 2, AlignerSlotCount = 2 };
        var state = new ClusterEquipmentState(cfg);
        FoupPortState port = state.FoupPorts[0];
        port.RemainingInFoup = 0;
        port.ReservedForPickupCount = 0;

        var queue = new Queue<TransferJob>();
        var scheduler = new EfemTransferScheduler();
        scheduler.TryScheduleOne(
            state,
            queue,
            activeJob: null,
            vacuumActiveJob: null,
            efemBlades: new RobotBladeSlots(2),
            efemBladeCapacity: 2);

        if (queue.Any(j => j.Pickup is EquipmentRegion.FoupA or EquipmentRegion.FoupB or EquipmentRegion.FoupC))
        {
            error = "foup-pickup: empty FOUP must not schedule pickup";
            return false;
        }

        port.RemainingInFoup = 3;
        state.AlignerBuffer.TryEnqueue(new WaferTrack(LoadPortId.Lp1, EquipmentRegion.NextProcessFoupA), 0);
        state.AlignerBuffer.TryEnqueue(new WaferTrack(LoadPortId.Lp2, EquipmentRegion.NextProcessFoupB), 1);

        queue.Clear();
        for (int attempt = 0; attempt < 5; attempt++)
        {
            scheduler.TryScheduleOne(
                state,
                queue,
                activeJob: null,
                vacuumActiveJob: null,
                efemBlades: new RobotBladeSlots(2),
                efemBladeCapacity: 2);
        }

        if (queue.Any(j =>
                j.Pickup is EquipmentRegion.FoupA or EquipmentRegion.FoupB or EquipmentRegion.FoupC
                && j.Dropoff == EquipmentRegion.LoadLock))
        {
            error = "foup-pickup: FOUP must not bypass Aligner to Load Lock";
            return false;
        }

        if (!port.OnWaferReservedFromFoup())
        {
            error = "foup-pickup: reserve should succeed when remaining > 0";
            return false;
        }

        if (port.RemainingInFoup != 2 || port.ReservedForPickupCount != 1 || port.PhysicallyInFoup != 3)
        {
            error = "foup-pickup: reserve accounting mismatch";
            return false;
        }

        port.OnWaferPickedFromFoup();
        if (port.ReservedForPickupCount != 0 || port.InFlightCount != 1 || port.PhysicallyInFoup != 2)
        {
            error = "foup-pickup: grip accounting mismatch";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool ValidateAlarmCatalog(out string error)
    {
        foreach (string code in new[] { "A001", "A002", "A003", "A004", "A005", "A006" })
        {
            if (!AlarmCatalog.TryGet(code).HasValue)
            {
                error = $"alarm-catalog: missing {code}";
                return false;
            }

            if (!AlarmCatalog.FormatBanner(code).Contains(code, StringComparison.Ordinal))
            {
                error = $"alarm-catalog: banner missing {code}";
                return false;
            }

            if (!AlarmCatalog.FormatDetailWithAction(code).Contains("조치:", StringComparison.Ordinal))
            {
                error = $"alarm-catalog: action missing for {code}";
                return false;
            }
        }

        if (!AlarmCatalog.IsEnvironmentWarningCode("A005") || AlarmCatalog.IsEnvironmentWarningCode("A002"))
        {
            error = "alarm-catalog: environment warning codes";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool ValidateConnectionPresenter(out string error)
    {
        var bench = HmiConnectionPresenter.DescribePlc(true, false, false, true);
        if (!bench.Text.Contains("벤치", StringComparison.Ordinal))
        {
            error = "connection: bench label";
            return false;
        }

        var live = HmiConnectionPresenter.DescribePlc(false, true, true, false);
        if (!live.Text.Contains("실측", StringComparison.Ordinal))
        {
            error = "connection: live label";
            return false;
        }

        var offline = HmiConnectionPresenter.DescribeDataQuality(false, false, DateTime.MinValue);
        if (!offline.Text.Contains("시뮬 허용", StringComparison.Ordinal))
        {
            error = "connection: offline hint";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool ValidateFlaskTelemetryContract(out string error)
    {
        var sim = new TmTransferSimulator();
        sim.StartDemoLoop();
        for (int i = 0; i < 24; i++)
        {
            sim.Tick();
        }

        var ctx = new ModuleStateAggregator.Context
        {
            EquipmentState = "RUNNING",
            MaintenanceMode = false,
            HasLiveSensorData = false,
            InterlockOk = true,
            BenchMode = true,
            AccessSafe = true,
            AccessInputValid = true,
            Transfer = sim
        };
        List<ModuleTelemetryModule> modules =
            HmiTelemetryPayloadFactory.FromModuleSnapshots(ModuleStateAggregator.Build(ctx));
        ProcessRecipeTelemetry recipe = HmiTelemetryPayloadFactory.DefaultRecipe();

        EtchTelemetryPayload demo = HmiTelemetryPayloadFactory.Create(
            "demo", false, true, false, false, "RUNNING", null, true, "smoke",
            25, 40, 5, 0.1, true, modules, recipe);
        if (!EtchTelemetryContractValidator.TryValidate(demo, out error))
        {
            return false;
        }

        EtchTelemetryPayload live = HmiTelemetryPayloadFactory.Create(
            "live", true, false, false, true, "RUNNING", null, true, "smoke",
            25, 40, 5, 0.1, true, modules, recipe);
        if (!EtchTelemetryContractValidator.TryValidate(live, out error))
        {
            return false;
        }

        EtchTelemetryPayload offline = HmiTelemetryPayloadFactory.Create(
            "offline", false, false, false, false, "IDLE", null, false, "smoke",
            0, 0, 0, 0, true, modules, recipe);
        if (!EtchTelemetryContractValidator.TryValidate(offline, out error))
        {
            return false;
        }

        HmiFlaskStatusPresenter.FlaskPresentation ok = HmiFlaskStatusPresenter.Describe(
            true, true, DateTime.UtcNow, "http://127.0.0.1:5000");
        if (!ok.Text.StartsWith("OK", StringComparison.Ordinal))
        {
            error = "flask-status: reachable label";
            return false;
        }

        HmiFlaskStatusPresenter.FlaskPresentation off = HmiFlaskStatusPresenter.Describe(
            true, false, null, "http://127.0.0.1:5000");
        if (!off.Hint.Contains("미연결", StringComparison.Ordinal))
        {
            error = "flask-status: offline hint";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool ValidateVacuumInboundPolicy(out string error)
    {
        var blades = new RobotBladeSlots(2);
        blades.Place(VacuumDualBladePlanner.BackBladeSlot, new WaferTrack(LoadPortId.Lp1, EquipmentRegion.FoupA));

        if (VacuumInboundPolicy.ShouldRestrictBmPickup(blades, 2, [], []))
        {
            error = "inbound: pre-etch on back slot should allow BM pickup";
            return false;
        }

        var pending = new Queue<TransferJob>();
        pending.Enqueue(new TransferJob
        {
            Wafer = blades.Get(VacuumDualBladePlanner.BackBladeSlot)!,
            Pickup = EquipmentRegion.ChamberB,
            Dropoff = EquipmentRegion.ChamberA,
            ResolvedBladeSlot = VacuumDualBladePlanner.BackBladeSlot
        });

        if (VacuumInboundPolicy.ShouldRestrictBmPickup(blades, 2, pending, []))
        {
            error = "inbound: dual blade with PM1 pending on other slot should allow BM pickup";
            return false;
        }

        blades.Place(
            VacuumDualBladePlanner.FrontBladeSlot,
            new WaferTrack(LoadPortId.Lp1, EquipmentRegion.FoupA) { HasCompletedEtch = true });
        if (!VacuumInboundPolicy.ShouldRestrictBmPickup(blades, 2, pending, []))
        {
            error = "inbound: dual full blades should block BM pickup";
            return false;
        }

        blades = new RobotBladeSlots(2);
        blades.Place(
            VacuumDualBladePlanner.BackBladeSlot,
            new WaferTrack(LoadPortId.Lp1, EquipmentRegion.FoupA) { HasCompletedEtch = true });
        if (VacuumInboundPolicy.ShouldRestrictBmPickup(blades, 2, [], []))
        {
            error = "inbound: etch-complete on one slot should allow BM pickup on free slot";
            return false;
        }

        if (!VacuumDualBladePlanner.CanChainPickup(
                blades,
                new TransferJob
                {
                    Pickup = EquipmentRegion.LoadLock,
                    Dropoff = EquipmentRegion.ChamberB,
                    Wafer = new WaferTrack(LoadPortId.Lp1, EquipmentRegion.FoupA)
                },
                new TransferJob
                {
                    Pickup = EquipmentRegion.LoadLock,
                    Dropoff = EquipmentRegion.ChamberC,
                    Wafer = new WaferTrack(LoadPortId.Lp1, EquipmentRegion.FoupA)
                }))
        {
            error = "chain: BM→Etch consecutive pickup should be allowed";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool ValidateAdmissionPolicy(out string error)
    {
        var state = new ClusterEquipmentState(new EquipmentCapacityConfig { LoadLockSlotCount = 3 });
        FillAllEtchPms(state);

        var wafer = new WaferTrack(LoadPortId.Lp1, EquipmentRegion.NextProcessFoupA);
        state.LoadLockBuffer.TryEnqueue(wafer, 0);

        if (LoadLockAdmissionPolicy.CanAcceptPreEtchFromAligner(state, out string? reason))
        {
            error = "admission: should block 2nd pre-etch when BM has one waiting and PM2~4 full";
            return false;
        }

        if (reason is null || !reason.Contains("Etch PM", StringComparison.Ordinal))
        {
            error = $"admission: unexpected block reason: {reason}";
            return false;
        }

        state = new ClusterEquipmentState(new EquipmentCapacityConfig { LoadLockSlotCount = 3, SideStorageSlotCount = 1 });
        wafer = new WaferTrack(LoadPortId.Lp2, EquipmentRegion.NextProcessFoupB);
        wafer.HasCompletedStrip = true;
        state.LoadLockBuffer.TryEnqueue(wafer, 0);
        state.SideStorage.TryEnqueue(new WaferTrack(LoadPortId.Lp1, EquipmentRegion.NextProcessFoupA));

        if (LoadLockAdmissionPolicy.CanAcceptStripFromPm1(state, out reason))
        {
            error = "admission: should block PM1→BM when side full and BM has strip";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static void FillAllEtchPms(ClusterEquipmentState state)
    {
        foreach (PmChamberState ch in new[] { state.Pm2, state.Pm3, state.Pm4 })
        {
            ch.CurrentWafer = new WaferTrack(LoadPortId.Lp1, EquipmentRegion.NextProcessFoupA);
            ch.RemainingProcessTicks = 10;
        }
    }

    private static void ValidateState(ClusterEquipmentState state)
    {
        foreach (FoupPortState port in state.FoupPorts)
        {
            if (port.RemainingInFoup < 0 || port.RemainingInFoup > state.Capacity.FoupSlotCount)
            {
                throw new InvalidOperationException($"foup count out of range: {port.PortId}");
            }
        }

        if (state.SideStorage.Count < 0 || state.SideStorage.Count > state.SideStorage.Capacity)
        {
            throw new InvalidOperationException("side storage capacity overflow");
        }

        var seenWaferIds = new HashSet<int>();
        foreach (PmChamberState chamber in state.Chambers.Values)
        {
            WaferTrack? wafer = chamber.CurrentWafer;
            if (wafer is null)
            {
                continue;
            }

            if (!seenWaferIds.Add(wafer.Id))
            {
                throw new InvalidOperationException($"duplicate wafer id in chambers: {wafer.Id}");
            }

            if (chamber.Region == EquipmentRegion.ChamberA && !wafer.HasCompletedEtch)
            {
                throw new InvalidOperationException($"PM1 strip received pre-etch wafer: #{wafer.Id}");
            }

            if (chamber.IsEtchPm && wafer.HasCompletedEtch && chamber.RemainingProcessTicks > 0)
            {
                // 식각 중 — OK
            }
            else if (chamber.IsEtchPm && !wafer.HasCompletedEtch && chamber.RemainingProcessTicks >= 0)
            {
                // 식각 대기/진행 — OK
            }
        }

        int etchVisits = state.Chambers.Values.Count(ch =>
            ch.CurrentWafer is not null && ch.IsEtchPm);
        if (etchVisits > 3)
        {
            throw new InvalidOperationException("too many concurrent etch wafers");
        }
    }

    /// <summary>Stop 일시정지·재개·정비 API 불변식 검증.</summary>
    public static Result RunMaintenanceAudit(int warmupTicks = 800)
    {
        if (warmupTicks <= 0)
        {
            return new Result { Success = false, Message = "warmupTicks must be > 0" };
        }

        var sim = new TmTransferSimulator();
        sim.StartDemoLoop();
        int motion = EquipmentCapacityConfig.Default.VacuumMotionStepsPerUiTick;
        for (int i = 0; i < warmupTicks; i++)
        {
            sim.Tick(motion);
            ValidateState(sim.ClusterState);
        }

        string beforePause = sim.DescribeMaintenanceState();
        int lotBefore = sim.LotCompletedCount;
        sim.PauseTransfer();
        if (sim.IsRunning)
        {
            return new Result { Success = false, Message = "pause: IsRunning should be false" };
        }

        if (!sim.CanResume)
        {
            return new Result { Success = false, Message = "pause: CanResume should be true mid-lot" };
        }

        if (sim.DescribeMaintenanceState() != beforePause)
        {
            return new Result { Success = false, Message = "pause: line state changed after PauseTransfer" };
        }

        sim.ResumeTransfer();
        if (!sim.IsRunning)
        {
            return new Result { Success = false, Message = "resume: IsRunning should be true" };
        }

        for (int i = 0; i < 200; i++)
        {
            sim.Tick(motion);
            ValidateState(sim.ClusterState);
        }

        sim.PauseTransfer();
        sim.MaintenanceClearLoadLock();
        sim.MaintenanceClearAligner();
        sim.MaintenanceClearChambers();
        sim.MaintenanceClearSideStorage();
        sim.MaintenanceRemountAllFoups();
        int shipped = sim.MaintenanceSideCassetteSwap();
        sim.MaintenanceAdvanceOneTick(out string tickHint);
        ValidateState(sim.ClusterState);

        sim.MaintenanceResetVirtualLine();
        ValidateState(sim.ClusterState);
        if (sim.CanResume)
        {
            return new Result { Success = false, Message = "reset: CanResume should be false after MaintenanceResetVirtualLine" };
        }

        sim.StartDemoLoop();
        for (int i = 0; i < 300; i++)
        {
            sim.Tick(motion);
            ValidateState(sim.ClusterState);
        }

        return new Result
        {
            Success = true,
            Ticks = warmupTicks,
            Message =
                $"ok pause/resume lot_before={lotBefore} side_swap={shipped} tick={tickHint}"
        };
    }

    /// <summary>알람 일시정지 후 라인·모듈 ALM 배지·웨이퍼 표시 불변식.</summary>
    public static Result RunAlarmAudit(int warmupTicks = 500)
    {
        if (warmupTicks <= 0)
        {
            return new Result { Success = false, Message = "warmupTicks must be > 0" };
        }

        var sim = new TmTransferSimulator();
        sim.StartDemoLoop();
        int motion = EquipmentCapacityConfig.Default.VacuumMotionStepsPerUiTick;
        for (int i = 0; i < warmupTicks; i++)
        {
            sim.Tick(motion);
            ValidateState(sim.ClusterState);
        }

        string beforePause = sim.DescribeMaintenanceState();
        sim.PauseTransfer();
        if (sim.IsRunning)
        {
            return new Result { Success = false, Message = "alarm: IsRunning should be false after pause" };
        }

        if (!sim.HasVisibleLineState())
        {
            return new Result { Success = false, Message = "alarm: HasVisibleLineState should be true after mid-lot pause" };
        }

        if (!sim.CanResume)
        {
            return new Result { Success = false, Message = "alarm: CanResume should be true after mid-lot pause" };
        }

        if (sim.DescribeMaintenanceState() != beforePause)
        {
            return new Result { Success = false, Message = "alarm: line state changed after PauseTransfer" };
        }

        if (!sim.HasWaferAt(EquipmentRegion.FoupA))
        {
            return new Result { Success = false, Message = "alarm: FOUP A should still show wafer inventory" };
        }

        var baseCtx = new ModuleStateAggregator.Context
        {
            EquipmentState = "ALARM",
            HasLiveSensorData = true,
            InterlockOk = false,
            BenchMode = true,
            AccessSafe = true,
            AccessInputValid = true,
            Transfer = sim
        };

        foreach ((string code, EquipmentModuleId alarmModule, EquipmentModuleId quietModule) in new[]
                 {
                     ("A002", EquipmentModuleId.BufferModule, EquipmentModuleId.Pm2),
                     ("A003", EquipmentModuleId.TransferModule, EquipmentModuleId.Pm1),
                     ("A004", EquipmentModuleId.BufferModule, EquipmentModuleId.Pm3),
                     ("A005", EquipmentModuleId.Efem, EquipmentModuleId.Pm4),
                 })
        {
            IReadOnlyList<ModuleStateSnapshot> snaps = ModuleStateAggregator.Build(
                new ModuleStateAggregator.Context
                {
                    EquipmentState = baseCtx.EquipmentState,
                    HasLiveSensorData = baseCtx.HasLiveSensorData,
                    InterlockOk = baseCtx.InterlockOk,
                    BenchMode = baseCtx.BenchMode,
                    AccessSafe = baseCtx.AccessSafe,
                    AccessInputValid = baseCtx.AccessInputValid,
                    Transfer = baseCtx.Transfer,
                    AlarmCode = code
                });

            ModuleStateSnapshot alarmSnap = snaps.First(s => s.ModuleId == alarmModule);
            ModuleStateSnapshot quietSnap = snaps.First(s => s.ModuleId == quietModule);
            if (alarmSnap.State != ModuleOperationalState.Alarm)
            {
                return new Result
                {
                    Success = false,
                    Message = $"alarm: {code} expected {alarmModule}=ALM got {alarmSnap.State}"
                };
            }

            if (quietSnap.State == ModuleOperationalState.Alarm)
            {
                return new Result
                {
                    Success = false,
                    Message = $"alarm: {code} expected {quietModule} without ALM got Alarm"
                };
            }
        }

        return new Result
        {
            Success = true,
            Ticks = warmupTicks,
            Message = "ok alarm pause line+module badges"
        };
    }

    /// <summary>AI 학습 JSONL — 시뮬 틱마다 스냅샷 기록·행 스키마 검증.</summary>
    public static Result RunAiJsonlAudit(int ticks = 120)
    {
        if (ticks <= 0)
        {
            return new Result { Success = false, Message = "ticks must be > 0" };
        }

        string path = Path.Combine(Path.GetTempPath(), $"etch_ai_jsonl_{Guid.NewGuid():N}.jsonl");
        var recorder = new AiTrainingDataRecorder(path);
        var sim = new TmTransferSimulator();
        sim.StartDemoLoop();
        int motion = EquipmentCapacityConfig.Default.VacuumMotionStepsPerUiTick;

        for (int i = 0; i < ticks; i++)
        {
            sim.Tick(motion);
            ValidateState(sim.ClusterState);

            var ctx = new ModuleStateAggregator.Context
            {
                EquipmentState = "RUNNING",
                MaintenanceMode = false,
                HasLiveSensorData = false,
                InterlockOk = true,
                BenchMode = true,
                AccessSafe = true,
                AccessInputValid = true,
                Transfer = sim
            };

            recorder.Append(new AiTrainingDataRecorder.SnapshotInput
            {
                EquipmentState = "RUNNING",
                AlarmCode = null,
                InterlockOk = true,
                BenchMode = true,
                Temperature = 25,
                Humidity = 40,
                Pressure = 5,
                Vibration = 0.1,
                AccessSafe = true,
                Modules = ModuleStateAggregator.Build(ctx)
            });
        }

        if (!File.Exists(path))
        {
            return new Result { Success = false, Ticks = ticks, Message = "ai-jsonl: output file missing" };
        }

        string[] lines = File.ReadAllLines(path);
        if (lines.Length < ticks)
        {
            return new Result
            {
                Success = false,
                Ticks = ticks,
                Message = $"ai-jsonl: expected {ticks} lines, got {lines.Length}"
            };
        }

        foreach (string line in lines)
        {
            if (!ValidateAiJsonlLine(line, out string lineError))
            {
                return new Result { Success = false, Ticks = ticks, Message = lineError };
            }
        }

        var diag = new EtchAiDiagnosis
        {
            Success = true,
            AnomalyScore = 0.62,
            PredictedAlarm = "A002",
            PredictionConfidence = 0.81,
            SuggestedAction = "압력 트렌드 모니터",
            Stub = true
        };
        IReadOnlyList<AiInsightRow> rows = AiInsightComposer.Compose(
            diag, true, false, 5, 0.1, 25, 40, true, true, sim);
        AiInsightRow? predRow = rows.FirstOrDefault(r => r.Category == "예측");
        if (predRow is null || !predRow.Detail.Contains("조치:", StringComparison.Ordinal))
        {
            return new Result { Success = false, Ticks = ticks, Message = "ai-jsonl: AlarmCatalog link in AiInsightComposer" };
        }

        try
        {
            File.Delete(path);
        }
        catch
        {
            // ignore temp cleanup
        }

        return new Result
        {
            Success = true,
            Ticks = ticks,
            Message = $"ok ai-jsonl {lines.Length} lines + catalog insight"
        };
    }

    private static bool ValidateAiJsonlLine(string line, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(line))
        {
            error = "ai-jsonl: empty line";
            return false;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(line);
            JsonElement root = doc.RootElement;
            string[] required =
            [
                "timestampUtc", "equipmentState", "interlockOk", "benchMode",
                "temperature", "humidity", "pressure", "vibration", "accessSafe",
                "moduleRunningCount", "modules"
            ];

            foreach (string key in required)
            {
                if (!root.TryGetProperty(key, out _))
                {
                    error = $"ai-jsonl: missing field {key}";
                    return false;
                }
            }

            if (root.GetProperty("modules").ValueKind != JsonValueKind.Array
                || root.GetProperty("modules").GetArrayLength() < 1)
            {
                error = "ai-jsonl: modules array empty";
                return false;
            }
        }
        catch (Exception ex)
        {
            error = $"ai-jsonl: parse {ex.Message}";
            return false;
        }

        return true;
    }

    /// <summary>UI hint API — 파이프라인·듀얼 블레이드·웨이퍼 타임라인 불변식.</summary>
    public static Result RunUiHintsAudit(int warmupTicks = 600)
    {
        if (warmupTicks <= 0)
        {
            return new Result { Success = false, Message = "warmupTicks must be > 0" };
        }

        var sim = new TmTransferSimulator();
        sim.StartDemoLoop();
        int motion = EquipmentCapacityConfig.Default.VacuumMotionStepsPerUiTick;
        bool sawPipeline = false;
        bool sawDualBlade = false;
        bool sawTimelineWafer = false;

        for (int i = 0; i < warmupTicks; i++)
        {
            sim.Tick(motion);
            ValidateState(sim.ClusterState);

            string pipeline = sim.DescribeWaferFlowPipeline();
            if (pipeline.Contains("FOUP", StringComparison.Ordinal) && pipeline.Contains("LOT", StringComparison.Ordinal))
            {
                sawPipeline = true;
            }

            string dual = sim.DescribeDualBladeStatus();
            if (dual.Contains("EFEM A[", StringComparison.Ordinal) || dual.Contains("진공 TM A[", StringComparison.Ordinal))
            {
                sawDualBlade = true;
            }

            if (sim.GetActiveWaferTimeline().Any(e => e.WaferId > 0))
            {
                sawTimelineWafer = true;
            }
        }

        if (!sawPipeline)
        {
            return new Result { Success = false, Message = "ui-hints: DescribeWaferFlowPipeline never populated" };
        }

        if (!sawDualBlade)
        {
            return new Result { Success = false, Message = "ui-hints: DescribeDualBladeStatus missing dual blade text" };
        }

        if (!sawTimelineWafer)
        {
            return new Result { Success = false, Message = "ui-hints: GetActiveWaferTimeline never had wafer id" };
        }

        string sample = sim.DescribeActiveWaferTimeline();
        if (sample.Length < 8)
        {
            return new Result { Success = false, Message = "ui-hints: DescribeActiveWaferTimeline too short" };
        }

        return new Result
        {
            Success = true,
            Ticks = warmupTicks,
            Message = $"ok pipeline+dual+timeline sample={sample[..Math.Min(sample.Length, 80)]}"
        };
    }

    /// <summary>
    /// 다중 시나리오 스트레스 감사 — 정체(무한 대기)·듀얼블레이드 미활용·파이프라인 지연 탐지.
    /// </summary>
    public static Result RunStressAudit(int runsPerProfile = 5)
    {
        if (runsPerProfile <= 0)
        {
            return new Result { Success = false, Message = "runsPerProfile must be > 0" };
        }

        var profiles = new (string Name, EquipmentCapacityConfig Cfg, int Ticks)[]
        {
            ("default-dual", EquipmentCapacityConfig.Default, 160_000),
            ("fast-etch", new EquipmentCapacityConfig
            {
                EtchProcessTicks = 12,
                StripProcessTicks = 6,
                VacuumMoveTicks = 8
            }, 50_000),
            ("slow-etch", new EquipmentCapacityConfig
            {
                EtchProcessTicks = 180,
                StripProcessTicks = 40,
                VacuumMoveTicks = 8
            }, 120_000)
        };

        var lines = new List<string>();
        int totalStalls = 0;
        int totalDualMiss = 0;
        int profilesOk = 0;

        foreach ((string name, EquipmentCapacityConfig cfg, int ticks) in profiles)
        {
            for (int run = 1; run <= runsPerProfile; run++)
            {
                StressRunReport report = ExecuteStressRun($"{name}#{run}", cfg, ticks);
                lines.Add(report.SummaryLine);
                totalStalls += report.StallEvents.Count;
                totalDualMiss += report.DualMissedOpportunityTicks;
                if (!report.Success)
                {
                    return new Result
                    {
                        Success = false,
                        Runs = run,
                        Ticks = ticks,
                        Message = report.FailureReason ?? "stress run failed",
                        Report = string.Join(Environment.NewLine, lines)
                    };
                }
            }

            profilesOk++;
        }

        // 배치 변형 — Etch/Strip 조합을 바꿔 반복
        for (int i = 0; i < runsPerProfile * 2; i++)
        {
            int etch = 40 + (i % 5) * 25;
            int strip = 10 + (i % 4) * 8;
            var cfg = new EquipmentCapacityConfig { EtchProcessTicks = etch, StripProcessTicks = strip };
            StressRunReport report = ExecuteStressRun($"batch-var#{i + 1}", cfg, 12_000);
            lines.Add(report.SummaryLine);
            totalStalls += report.StallEvents.Count;
            totalDualMiss += report.DualMissedOpportunityTicks;
            if (!report.Success)
            {
                return new Result
                {
                    Success = false,
                    Runs = i + 1,
                    Ticks = 12_000,
                    Message = report.FailureReason ?? "batch stress failed",
                    Report = string.Join(Environment.NewLine, lines)
                };
            }
        }

        string header =
            $"profiles={profilesOk}/{profiles.Length} batch_runs={runsPerProfile * 2} stalls={totalStalls} dual_miss_ticks={totalDualMiss}";
        return new Result
        {
            Success = true,
            Runs = profiles.Length * runsPerProfile + runsPerProfile * 2,
            Message = header,
            Report = string.Join(Environment.NewLine, lines.Prepend(header))
        };
    }

    /// <summary>Aligner 2매 + BM 3매 파이프라인 — LOT 완료 및 중간 이상 감사.</summary>
    public static Result RunAligner2PipelineAudit(int ticks = 160_000)
    {
        if (ticks <= 0)
        {
            return new Result { Success = false, Message = "ticks must be > 0" };
        }

        var tracker = new PipelineAnomalyTracker();
        var sim = new TmTransferSimulator(EquipmentCapacityConfig.Default);
        sim.StartDemoLoop();
        int motion = EquipmentCapacityConfig.Default.VacuumMotionStepsPerUiTick;

        try
        {
            for (int t = 0; t < ticks && sim.IsRunning; t++)
            {
                sim.Tick(motion);
                ValidateState(sim.ClusterState);
                if (!tracker.OnTick(sim, t, out string? fatal))
                {
                    return new Result
                    {
                        Success = false,
                        Ticks = t,
                        Message = fatal ?? "pipeline fatal",
                        Report = tracker.FormatReport(sim, t)
                    };
                }
            }
        }
        catch (Exception ex)
        {
            return new Result { Success = false, Ticks = ticks, Message = $"aligner-audit exception: {ex.Message}" };
        }

        ClusterEquipmentState s = sim.ClusterState;
        string report = tracker.FormatReport(sim, ticks);
        int lotTargetTicks = ticks >= 100_000
            ? (int)Math.Ceiling(s.Lot.TargetCount
                * (s.Capacity.EtchProcessTicks + s.Capacity.StripProcessTicks + 120) * 1.15)
            : 0;
        if (!sim.LotCompleteAchieved && ticks >= lotTargetTicks && lotTargetTicks > 0)
        {
            return new Result
            {
                Success = false,
                Ticks = ticks,
                Message = $"aligner-audit: lot incomplete {s.Lot.CompletedCount}/{s.Lot.TargetCount}",
                Report = report
            };
        }

        if (tracker.ErrorCount > 0)
        {
            return new Result { Success = false, Ticks = ticks, Message = tracker.FirstError ?? "pipeline error", Report = report };
        }

        return new Result
        {
            Success = true,
            Ticks = ticks,
            Message = $"ok aligner={s.Capacity.AlignerSlotCount} lot={s.Lot.CompletedCount}/{s.Lot.TargetCount}"
                      + $" warns={tracker.WarningCount} max_align={tracker.MaxAlignerObserved}",
            Report = report
        };
    }

    private sealed class StressRunReport
    {
        public bool Success { get; init; } = true;
        public string? FailureReason { get; init; }
        public string SummaryLine { get; init; } = string.Empty;
        public List<string> StallEvents { get; } = [];
        public int DualMissedOpportunityTicks { get; init; }
        public PipelineAnomalyTracker Anomalies { get; init; } = new();
    }

    private sealed class PipelineAnomalyTracker
    {
        private const int GridlockWarnTicks = 1_500;
        private const int FoupFlowWarnTicks = 2_000;
        private const int BladeOrphanWarnTicks = 800;
        private const int HoldHintWarnTicks = 2_500;

        public List<string> Events { get; } = [];
        public int WarningCount { get; private set; }
        public int ErrorCount { get; private set; }
        public int MaxAlignerObserved { get; private set; }
        public string? FirstError { get; private set; }

        private int _gridlockTicks;
        private int _foupFlowStuckTicks;
        private int _vacBladeOrphanTicks;
        private int _efemBladeOrphanTicks;
        private int _holdHintTicks;
        private string? _lastHint;

        public bool OnTick(TmTransferSimulator sim, int tick, out string? fatalError)
        {
            fatalError = null;
            ClusterEquipmentState s = sim.ClusterState;
            MaxAlignerObserved = Math.Max(MaxAlignerObserved, s.AlignerBuffer.Count);

            if (s.AlignerBuffer.Count > s.AlignerBuffer.Capacity)
            {
                return Fail(tick, $"ERROR aligner_overflow {s.AlignerBuffer.Count}/{s.AlignerBuffer.Capacity}", out fatalError);
            }

            if (LoadLockAdmissionPolicy.PreEtchCount(s) > LoadLockAdmissionPolicy.MaxPreEtchSlots(s)
                || LoadLockAdmissionPolicy.StripCount(s) > LoadLockAdmissionPolicy.MaxStripSlots(s)
                || s.LoadLockBuffer.Count > s.LoadLockBuffer.Capacity)
            {
                return Fail(tick,
                    $"ERROR bm_zone P{LoadLockAdmissionPolicy.PreEtchCount(s)}"
                    + $" S{LoadLockAdmissionPolicy.StripCount(s)}"
                    + $" /{s.LoadLockBuffer.Capacity}",
                    out fatalError);
            }

            foreach (FoupPortState port in s.FoupPorts)
            {
                if (port.RemainingInFoup < 0 || port.InFlightCount < 0)
                {
                    return Fail(tick, $"ERROR foup_accounting LP{(int)port.PortId + 1}", out fatalError);
                }
            }

            int efemCap = s.Capacity.EfemBladeSlotCount;
            int efemBladeLive = (sim.EfemCarryingSlotA ? 1 : 0) + (sim.EfemCarryingSlotB ? 1 : 0);
            bool efemAtFoup = sim.IsEfemBusy
                              && sim.EfemRegion is EquipmentRegion.FoupA
                                  or EquipmentRegion.FoupB
                                  or EquipmentRegion.FoupC;
            if (efemCap >= 2
                && efemBladeLive >= efemCap
                && efemAtFoup
                && s.AlignerBuffer.IsFull
                && LoadLockAdmissionPolicy.IsPreEtchBmFull(s))
            {
                return Fail(
                    tick,
                    $"ERROR efem_foup_pump blades={efemBladeLive}/{efemCap} @ {sim.EfemRegion}",
                    out fatalError);
            }

            ScanDurations(sim, tick);
            return true;
        }

        private void ScanDurations(TmTransferSimulator sim, int tick)
        {
            if (!sim.IsRunning || sim.LotCompleteAchieved)
            {
                ResetDurations();
                return;
            }

            ClusterEquipmentState s = sim.ClusterState;
            (int efemQ, int vacQ, int vacPend, int vacBlades) = sim.GetQueueDiagnostics();
            int foupRem = s.FoupPorts.Sum(p => p.RemainingInFoup);
            int efemBladeLive = (sim.EfemCarryingSlotA ? 1 : 0) + (sim.EfemCarryingSlotB ? 1 : 0);

            bool gridlock = s.AlignerBuffer.IsFull
                            && LoadLockAdmissionPolicy.IsPreEtchBmFull(s)
                            && !sim.IsEfemBusy
                            && !sim.IsVacuumBusy
                            && foupRem > 0
                            && efemBladeLive == 0
                            && efemQ == 0;
            TickDuration(ref _gridlockTicks, gridlock, GridlockWarnTicks,
                $"WARN tick~{tick} aligner+bm_pre 만석·EFEM idle·FOUP 잔량·블레이드 미사용");

            bool foupFlowStuck = !s.AlignerBuffer.IsFull
                                 && !LoadLockAdmissionPolicy.IsPreEtchBmFull(s)
                                 && foupRem > 0
                                 && !sim.IsEfemBusy
                                 && efemQ == 0
                                 && !s.SideStorage.IsFull
                                 && efemBladeLive == 0;
            TickDuration(ref _foupFlowStuckTicks, foupFlowStuck, FoupFlowWarnTicks,
                $"WARN tick~{tick} FOUP 잔량·Aligner/BM 여유·EFEM 미동작");

            bool vacOrphan = vacBlades > 0 && vacQ == 0 && vacPend == 0 && !sim.IsVacuumBusy && sim.ActiveVacuumJob is null;
            TickDuration(ref _vacBladeOrphanTicks, vacOrphan, BladeOrphanWarnTicks,
                $"WARN tick~{tick} 진공 블레이드 고아 blade={vacBlades}");

            bool efemOrphan = efemBladeLive > 0 && efemQ == 0 && !sim.IsEfemBusy && sim.ActiveEfemJob is null
                              && sim.EfemPendingDropCount == 0;
            TickDuration(ref _efemBladeOrphanTicks, efemOrphan, BladeOrphanWarnTicks,
                $"WARN tick~{tick} EFEM 블레이드 고아 blade={efemBladeLive}");

            string hint = sim.PhaseHint;
            if (HasWorkRemaining(sim)
                && (hint.Contains("HOLD", StringComparison.OrdinalIgnoreCase)
                    || hint.Contains("만석", StringComparison.OrdinalIgnoreCase)))
            {
                if (hint == _lastHint)
                {
                    _holdHintTicks++;
                }
                else
                {
                    _holdHintTicks = 1;
                    _lastHint = hint;
                }

                if (_holdHintTicks == HoldHintWarnTicks)
                {
                    Warn($"WARN tick~{tick} hint 지속 {HoldHintWarnTicks}t · {hint}");
                }
            }
            else
            {
                _holdHintTicks = 0;
                _lastHint = hint;
            }
        }

        private void TickDuration(ref int counter, bool condition, int threshold, string message)
        {
            if (condition)
            {
                counter++;
                if (counter == threshold)
                {
                    Warn(message);
                }
            }
            else
            {
                counter = 0;
            }
        }

        private void ResetDurations()
        {
            _gridlockTicks = 0;
            _foupFlowStuckTicks = 0;
            _vacBladeOrphanTicks = 0;
            _efemBladeOrphanTicks = 0;
            _holdHintTicks = 0;
        }

        private bool Fail(int tick, string message, out string? fatal)
        {
            ErrorCount++;
            if (FirstError is null)
            {
                FirstError = $"t={tick} {message}";
            }

            Events.Add($"t={tick} {message}");
            fatal = FirstError;
            return false;
        }

        private void Warn(string message)
        {
            WarningCount++;
            if (Events.Count < 40)
            {
                Events.Add(message);
            }
        }

        public string FormatReport(TmTransferSimulator sim, int ticks)
        {
            ClusterEquipmentState s = sim.ClusterState;
            var lines = new List<string>
            {
                $"ticks={ticks} lot={s.Lot.CompletedCount}/{s.Lot.TargetCount} done={sim.LotCompleteAchieved}",
                $"aligner_max={MaxAlignerObserved}/{s.AlignerBuffer.Capacity}",
                $"bm={LoadLockAdmissionPolicy.FormatBmInventory(s)}",
                $"errors={ErrorCount} warnings={WarningCount}",
                $"efem_max_blades={sim.MaxEfemBladesOccupied} vac_max={sim.DualBladeMetrics.MaxBladesOccupied}"
            };
            if (Events.Count > 0)
            {
                lines.Add("events:");
                lines.AddRange(Events.Take(25));
            }

            return string.Join(Environment.NewLine, lines);
        }
    }

    private static StressRunReport ExecuteStressRun(string label, EquipmentCapacityConfig cfg, int ticks)
    {
        const int stallThreshold = 2_500;
        const int dualMissReportThreshold = 800;

        var sim = new TmTransferSimulator(cfg);
        sim.StartDemoLoop();
        int motion = cfg.VacuumMotionStepsPerUiTick;

        string? lastFp = null;
        int unchanged = 0;
        int stallStart = -1;
        int dualMissTicks = 0;
        int dualMissPeak = 0;
        int lotAtStallStart = 0;
        var stallEvents = new List<string>();
        var anomalyTracker = new PipelineAnomalyTracker();

        try
        {
            for (int t = 0; t < ticks; t++)
            {
                sim.Tick(motion);
                ValidateState(sim.ClusterState);
                if (!anomalyTracker.OnTick(sim, t, out string? fatal))
                {
                    return new StressRunReport
                    {
                        Success = false,
                        FailureReason = $"{label}: {fatal}",
                        SummaryLine = $"{label}: ERROR {fatal}",
                        Anomalies = anomalyTracker
                    };
                }

                string fp = BuildStressFingerprint(sim);
                if (fp == lastFp)
                {
                    unchanged++;
                    if (stallStart < 0)
                    {
                        stallStart = t;
                        lotAtStallStart = sim.LotCompletedCount;
                    }
                }
                else
                {
                    if (unchanged >= stallThreshold && HasWorkRemaining(sim))
                    {
                        stallEvents.Add(
                            $"tick {stallStart}~{t} ({unchanged}t) lot={lotAtStallStart} fp={lastFp}");
                    }

                    unchanged = 0;
                    stallStart = -1;
                    lastFp = fp;
                }

                if (IsDualBladeMissedOpportunity(sim))
                {
                    dualMissTicks++;
                    dualMissPeak = Math.Max(dualMissPeak, dualMissTicks);
                }
                else
                {
                    dualMissTicks = 0;
                }
            }

            if (unchanged >= stallThreshold && HasWorkRemaining(sim))
            {
                stallEvents.Add($"tick {stallStart}~{ticks} ({unchanged}t) lot={lotAtStallStart} fp={lastFp}");
            }
        }
        catch (Exception ex)
        {
            return new StressRunReport
            {
                Success = false,
                FailureReason = $"{label}: exception {ex.Message}",
                SummaryLine = $"{label}: EXCEPTION {ex.Message}"
            };
        }

        if (stallEvents.Count > 0)
        {
            return new StressRunReport
            {
                Success = false,
                FailureReason = $"{label}: pipeline stall — {stallEvents[0]}",
                SummaryLine = $"{label}: STALL {stallEvents[0]}",
                DualMissedOpportunityTicks = dualMissPeak
            };
        }

        DualBladePipelineMetrics m = sim.DualBladeMetrics;
        ClusterEquipmentState s = sim.ClusterState;
        (int efemQ, int vacQ, int vacPend, int vacBlades) = sim.GetQueueDiagnostics();
        string dualNote = m.MaxBladesOccupied < 2 && ticks >= 20_000
            ? " WARN:dual-never-2"
            : string.Empty;
        string missNote = dualMissPeak >= dualMissReportThreshold
            ? $" WARN:dual-miss-peak={dualMissPeak}t"
            : string.Empty;
        string anomalyNote = anomalyTracker.WarningCount > 0
            ? $" pipeline_warns={anomalyTracker.WarningCount}"
            : string.Empty;

        return new StressRunReport
        {
            SummaryLine =
                $"{label}: ok ticks={ticks} lot={s.Lot.CompletedCount}/{s.Lot.TargetCount} done={sim.LotCompleteAchieved}"
                + $" align_max={anomalyTracker.MaxAlignerObserved}/{s.AlignerBuffer.Capacity}"
                + $" vac_max={m.MaxBladesOccupied} efem_max={sim.MaxEfemBladesOccupied}"
                + $" chain={m.ChainPickupCount} rot={m.RotateBladeCount}"
                + $" bm={s.LoadLockBuffer.Count}/{s.LoadLockBuffer.Capacity}"
                + $" q=v{vacQ}/e{efemQ} pend{vacPend} blade{vacBlades}"
                + $" hint={sim.PhaseHint}{dualNote}{missNote}{anomalyNote}",
            DualMissedOpportunityTicks = dualMissPeak,
            Anomalies = anomalyTracker
        };
    }

    private static string BuildStressFingerprint(TmTransferSimulator sim)
    {
        ClusterEquipmentState s = sim.ClusterState;
        (int efemQ, int vacQ, int vacPend, int vacBlades) = sim.GetQueueDiagnostics();
        int foupRem = s.FoupPorts.Sum(p => p.RemainingInFoup);
        int inflight = s.FoupPorts.Sum(p => p.InFlightCount);
        string pm = string.Join(
            ".",
            s.Chambers.Values.Select(c =>
                $"{c.Region}:{c.CurrentWafer?.Id ?? 0}:{c.RemainingProcessTicks}:{c.ReservedForIncoming}"));
        return string.Join(
            "|",
            s.Lot.CompletedCount,
            foupRem,
            inflight,
            s.AlignerBuffer.Count,
            s.LoadLockBuffer.Count,
            s.SideStorage.Count,
            vacBlades,
            vacQ,
            vacPend,
            efemQ,
            sim.IsVacuumBusy ? 1 : 0,
            sim.IsEfemBusy ? 1 : 0,
            pm,
            sim.PhaseHint);
    }

    private static bool HasWorkRemaining(TmTransferSimulator sim)
    {
        if (!sim.IsRunning || sim.LotCompleteAchieved)
        {
            return false;
        }

        ClusterEquipmentState s = sim.ClusterState;
        if (s.Lot.CompletedCount >= s.Lot.TargetCount)
        {
            return false;
        }

        int foup = s.FoupPorts.Sum(p => p.RemainingInFoup + p.ReservedForPickupCount + p.InFlightCount);
        if (foup > 0)
        {
            return true;
        }

        if (s.AlignerBuffer.HasWafer || s.LoadLockBuffer.HasWafer)
        {
            return true;
        }

        if (s.SideStorage.Count > 0)
        {
            return true;
        }

        foreach (PmChamberState ch in s.Chambers.Values)
        {
            if (ch.CurrentWafer is not null)
            {
                return true;
            }
        }

        (int _, int vacQ, int vacPend, int vacBlades) = sim.GetQueueDiagnostics();
        return vacQ > 0 || vacPend > 0 || vacBlades > 0
               || sim.IsVacuumBusy || sim.IsEfemBusy
               || sim.EfemCarryingWafer;
    }

    private static bool IsDualBladeMissedOpportunity(TmTransferSimulator sim)
    {
        if (sim.VacuumBladeCapacity < 2 || !sim.IsRunning || sim.LotCompleteAchieved)
        {
            return false;
        }

        ClusterEquipmentState s = sim.ClusterState;
        if (!s.LoadLockBuffer.IsFull)
        {
            return false;
        }

        (int _, int vacQ, int vacPend, int vacBlades) = sim.GetQueueDiagnostics();
        if (vacBlades != 1 || sim.IsVacuumBusy || vacQ > 0)
        {
            return false;
        }

        if (s.LoadLockBuffer.CountMatching(w => !w.HasCompletedEtch && !w.HasCompletedStrip) < 1)
        {
            return false;
        }

        if (!EtchPmSelector.HasPipelineEtchCapacity(s.Chambers))
        {
            return false;
        }

        // PM1 Strip 대기 중이면 단일 슬롯 점유가 정상일 수 있음
        if (vacPend > 0 && vacPend <= 1)
        {
            return false;
        }

        return true;
    }
}

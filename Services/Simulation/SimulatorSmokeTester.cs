using etch_ui.Equipment.Models;
using etch_ui.Services.Scheduling;

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

        string reportText = report ? BuildReport(sim, ticks, maxSideStorage) : null;
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
            $"aligner={s.AlignerBuffer.Count}/{s.AlignerBuffer.Capacity}",
            $"bm={s.LoadLockBuffer.Count}/{s.LoadLockBuffer.Capacity}",
            $"side={s.SideStorage.Count}/{s.SideStorage.Capacity}",
            $"max_side={maxSideStorage}",
            $"etch_pm_busy={etchBusy}/3",
            $"lot_done={sim.LotCompleteAchieved}",
            $"kpi={kpi}",
            $"hint={sim.PhaseHint}",
            $"q=efem{efemQ}/vac{vacQ} pend{vacPending} blade{vacBlades}",
            $"efem_max_blades={sim.MaxEfemBladesOccupied}",
            $"pipeline={DiagnosePipeline(s)}"
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
            BladeSlotIndex = VacuumDualBladePlanner.BackBladeSlot
        });

        if (!VacuumInboundPolicy.ShouldRestrictBmPickup(blades, 2, pending, []))
        {
            error = "inbound: PM1 pending drop should block BM pickup";
            return false;
        }

        blades.Place(VacuumDualBladePlanner.FrontBladeSlot, new WaferTrack(LoadPortId.Lp1, EquipmentRegion.FoupA));
        if (!VacuumInboundPolicy.ShouldRestrictBmPickup(blades, 2, [], []))
        {
            error = "inbound: dual full blades should block BM pickup";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool ValidateAdmissionPolicy(out string error)
    {
        var state = new ClusterEquipmentState(new EquipmentCapacityConfig { LoadLockSlotCount = 2 });
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

        state = new ClusterEquipmentState(new EquipmentCapacityConfig { LoadLockSlotCount = 2, SideStorageSlotCount = 1 });
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
}

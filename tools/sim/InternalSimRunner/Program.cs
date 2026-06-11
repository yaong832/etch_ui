using System.Diagnostics;
using etch_ui;
using etch_ui.Services.Scheduling;
using etch_ui.Services.Simulation;

if (args.Any(a => a.Equals("--diag-stall", StringComparison.OrdinalIgnoreCase)))
{
    return RunStallDiagnostic();
}

bool quick = args.Any(a => a.Equals("--quick", StringComparison.OrdinalIgnoreCase));
int stressRuns = quick ? 1 : 3;
int alignerTicks = quick ? 40_000 : 160_000;

var failures = new List<string>();
var sw = Stopwatch.StartNew();

void Run(string name, Func<SimulatorSmokeTester.Result> action)
{
    sw.Restart();
    try
    {
        SimulatorSmokeTester.Result result = action();
        string status = result.Success ? "PASS" : "FAIL";
        Console.WriteLine($"[{status}] {name,-22} {sw.Elapsed.TotalSeconds,6:F1}s  {result.Message}");
        if (!result.Success)
        {
            failures.Add($"{name}: {result.Message}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[FAIL] {name,-22} {sw.Elapsed.TotalSeconds,6:F1}s  {ex.Message}");
        failures.Add($"{name}: {ex.Message}");
    }
}

Console.WriteLine("=== InternalSimRunner (WPF/App 미기동 · SimulatorSmokeTester 직접 호출) ===");
Console.WriteLine(quick ? "mode=quick" : "mode=full");
Console.WriteLine();

Run("smoke", () => SimulatorSmokeTester.Run(8000));
Run("policy-batch", () => SimulatorSmokeTester.RunBatch(5, 5000));
Run("maintenance", () => SimulatorSmokeTester.RunMaintenanceAudit(800));
Run("dual-blade", () => SimulatorSmokeTester.RunDualBlade(12_000));
Run("alarm", () => SimulatorSmokeTester.RunAlarmAudit(500));
Run("ui-hints", () => SimulatorSmokeTester.RunUiHintsAudit(600));
Run("ai-jsonl", () => SimulatorSmokeTester.RunAiJsonlAudit(120));

AppSettings.ReloadFromDisk();
var capacity = AppSettings.CreateCapacityConfig();
Run("app-settings", () => SimulatorSmokeTester.Run(15_000, capacity, report: true));

Run("report", () => SimulatorSmokeTester.Run(40_000, report: true));
Run("efem-audit", () => SimulatorSmokeTester.RunEfemDualBladeAudit(40_000));
Run("stress", () => SimulatorSmokeTester.RunStressAudit(stressRuns));
Run("aligner-audit", () => SimulatorSmokeTester.RunAligner2PipelineAudit(alignerTicks));

Console.WriteLine();
if (failures.Count == 0)
{
    Console.WriteLine("ALL PASSED");
    return 0;
}

Console.WriteLine($"FAILED ({failures.Count}):");
foreach (string line in failures)
{
    Console.WriteLine($"  - {line}");
}

return 1;

static int RunStallDiagnostic()
{
    var cfg = EquipmentCapacityConfig.Default;
    var sim = new TmTransferSimulator(cfg);
    sim.StartDemoLoop();
    int motion = cfg.VacuumMotionStepsPerUiTick;
    string? lastFp = null;
    int unchanged = 0;
    int stallAt = -1;

    for (int t = 0; t < 20_000; t++)
    {
        sim.Tick(motion);
        string fp = BuildFp(sim);
        if (fp == lastFp)
        {
            unchanged++;
            if (unchanged == 2_500 && stallAt < 0)
            {
                stallAt = t;
                break;
            }
        }
        else
        {
            unchanged = 0;
            lastFp = fp;
        }
    }

    var s = sim.ClusterState;
    (int efemQ, int vacQ, int vacPend, int vacBlades) = sim.GetQueueDiagnostics();
    Console.WriteLine($"stall_like_at={stallAt} lot={s.Lot.CompletedCount} hint={sim.PhaseHint}");
    Console.WriteLine($"foup rem={s.FoupPorts.Sum(p => p.RemainingInFoup)} res={s.FoupPorts.Sum(p => p.ReservedForPickupCount)} inflight={s.FoupPorts.Sum(p => p.InFlightCount)}");
    Console.WriteLine($"align={s.AlignerBuffer.Count} bm={s.LoadLockBuffer.Count} side={s.SideStorage.Count}");
    Console.WriteLine($"vac q={vacQ} pend={vacPend} blades={vacBlades} efemQ={efemQ} efemPend={sim.EfemPendingDropCount}");
    foreach (var ch in s.Chambers.Values)
    {
        var w = ch.CurrentWafer;
        Console.WriteLine($"  {ch.Region}: wafer=#{w?.Id} etch={w?.HasCompletedEtch} strip={w?.HasCompletedStrip} t={ch.RemainingProcessTicks} ps={ch.PickupScheduled} ri={ch.ReservedForIncoming}");
    }

    Console.WriteLine($"queues: {sim.DescribeTransferQueues()}");

    return stallAt >= 0 ? 1 : 0;
}

static string BuildFp(TmTransferSimulator sim)
{
    var s = sim.ClusterState;
    (int efemQ, int vacQ, int vacPend, int vacBlades) = sim.GetQueueDiagnostics();
    string pm = string.Join(".", s.Chambers.Values.Select(c =>
        $"{c.Region}:{c.CurrentWafer?.Id ?? 0}:{c.RemainingProcessTicks}:{c.ReservedForIncoming}"));
    return string.Join("|",
        s.Lot.CompletedCount,
        s.FoupPorts.Sum(p => p.RemainingInFoup),
        s.FoupPorts.Sum(p => p.InFlightCount),
        s.AlignerBuffer.Count,
        s.LoadLockBuffer.Count,
        s.SideStorage.Count,
        vacBlades, vacQ, vacPend, efemQ,
        sim.IsVacuumBusy ? 1 : 0,
        sim.IsEfemBusy ? 1 : 0,
        pm,
        sim.PhaseHint);
}

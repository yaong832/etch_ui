using System.Windows;
using System.IO;
using etch_ui.Security;
using etch_ui.Services.Hmi;
using etch_ui.Services.Simulation;

namespace etch_ui
{
    public partial class App : Application
    {
        private const string DefaultAdminUsername = "admin";
        private const string DefaultAdminPassword = "Admin1234";

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            RegisterGlobalExceptionHandlers();

            if (e.Args.Any(a => a.Equals("--sim-report", StringComparison.OrdinalIgnoreCase)))
            {
                int ticks = 40000;
                string? ticksArg = e.Args.FirstOrDefault(a => a.StartsWith("--ticks=", StringComparison.OrdinalIgnoreCase));
                if (ticksArg is not null && int.TryParse(ticksArg.Split('=')[1], out int parsed) && parsed > 0)
                {
                    ticks = parsed;
                }

                SimulatorSmokeTester.Result result = SimulatorSmokeTester.Run(ticks, report: true);
                Console.WriteLine($"sim_report success={result.Success} {result.Report}");
                if (!result.Success)
                {
                    Console.WriteLine($"sim_report error={result.Message}");
                }

                Shutdown(result.Success ? 0 : 4);
                return;
            }

            if (e.Args.Any(a => a.Equals("--sim-app-settings", StringComparison.OrdinalIgnoreCase)))
            {
                int ticks = 30_000;
                string? ticksArg = e.Args.FirstOrDefault(a => a.StartsWith("--ticks=", StringComparison.OrdinalIgnoreCase));
                if (ticksArg is not null && int.TryParse(ticksArg.Split('=')[1], out int parsed) && parsed > 0)
                {
                    ticks = parsed;
                }

                AppSettings.ReloadFromDisk();
                var capacity = AppSettings.CreateCapacityConfig();
                SimulatorSmokeTester.Result result = SimulatorSmokeTester.Run(ticks, capacity, report: true);
                Console.WriteLine($"sim_app_settings success={result.Success} etch={capacity.EtchProcessTicks} strip={capacity.StripProcessTicks}");
                Console.WriteLine(result.Report ?? result.Message);
                Shutdown(result.Success ? 0 : 7);
                return;
            }

            if (e.Args.Any(a => a.Equals("--sim-smoke", StringComparison.OrdinalIgnoreCase)))
            {
                int ticks = 2000;
                string? ticksArg = e.Args.FirstOrDefault(a => a.StartsWith("--ticks=", StringComparison.OrdinalIgnoreCase));
                if (ticksArg is not null && int.TryParse(ticksArg.Split('=')[1], out int parsed) && parsed > 0)
                {
                    ticks = parsed;
                }

                SimulatorSmokeTester.Result result = SimulatorSmokeTester.Run(ticks);
                Console.WriteLine($"sim_smoke ticks={result.Ticks} success={result.Success} max_side_storage={result.MaxSideStorage} message={result.Message}");
                Shutdown(result.Success ? 0 : 2);
                return;
            }

            if (e.Args.Any(a => a.Equals("--sim-ai-jsonl", StringComparison.OrdinalIgnoreCase)))
            {
                int ticks = 120;
                string? ticksArg = e.Args.FirstOrDefault(a => a.StartsWith("--ticks=", StringComparison.OrdinalIgnoreCase));
                if (ticksArg is not null && int.TryParse(ticksArg.Split('=')[1], out int parsed) && parsed > 0)
                {
                    ticks = parsed;
                }

                SimulatorSmokeTester.Result result = SimulatorSmokeTester.RunAiJsonlAudit(ticks);
                Console.WriteLine($"sim_ai_jsonl ticks={result.Ticks} success={result.Success} message={result.Message}");
                Shutdown(result.Success ? 0 : 10);
                return;
            }

            if (e.Args.Any(a => a.Equals("--sim-flask-e2e", StringComparison.OrdinalIgnoreCase)))
            {
                string flaskUrl = "http://127.0.0.1:5000";
                string? urlArg = e.Args.FirstOrDefault(a => a.StartsWith("--flask-url=", StringComparison.OrdinalIgnoreCase));
                if (urlArg is not null)
                {
                    flaskUrl = urlArg.Split('=', 2)[1];
                }

                int simTicks = 80;
                string? ticksArg = e.Args.FirstOrDefault(a => a.StartsWith("--ticks=", StringComparison.OrdinalIgnoreCase));
                if (ticksArg is not null && int.TryParse(ticksArg.Split('=')[1], out int parsedTicks) && parsedTicks > 0)
                {
                    simTicks = parsedTicks;
                }

                bool requireMl = e.Args.Any(a => a.Equals("--require-ml", StringComparison.OrdinalIgnoreCase));
                FlaskE2eTester.Result result = FlaskE2eTester.Run(flaskUrl, simTicks, requireMl);
                Console.WriteLine($"sim_flask_e2e success={result.Success} ai_ready={result.AiReady} engine={result.AiEngine} message={result.Message}");
                if (result.Report is not null)
                {
                    Console.WriteLine(result.Report);
                }

                Shutdown(result.Success ? 0 : 13);
                return;
            }

            if (e.Args.Any(a => a.Equals("--sim-efem-audit", StringComparison.OrdinalIgnoreCase)))
            {
                int ticks = 40_000;
                string? ticksArg = e.Args.FirstOrDefault(a => a.StartsWith("--ticks=", StringComparison.OrdinalIgnoreCase));
                if (ticksArg is not null && int.TryParse(ticksArg.Split('=')[1], out int parsed) && parsed > 0)
                {
                    ticks = parsed;
                }

                SimulatorSmokeTester.Result result = SimulatorSmokeTester.RunEfemDualBladeAudit(ticks);
                Console.WriteLine($"sim_efem_audit success={result.Success} {result.Message}");
                if (result.Report is not null)
                {
                    Console.WriteLine(result.Report);
                }

                Shutdown(result.Success ? 0 : 6);
                return;
            }

            if (e.Args.Any(a => a.Equals("--sim-dual-blade", StringComparison.OrdinalIgnoreCase)))
            {
                int ticks = 12000;
                string? ticksArg = e.Args.FirstOrDefault(a => a.StartsWith("--ticks=", StringComparison.OrdinalIgnoreCase));
                if (ticksArg is not null && int.TryParse(ticksArg.Split('=')[1], out int parsed) && parsed > 0)
                {
                    ticks = parsed;
                }

                SimulatorSmokeTester.Result result = SimulatorSmokeTester.RunDualBlade(ticks);
                Console.WriteLine($"sim_dual_blade ticks={result.Ticks} success={result.Success} {result.Message}");
                Shutdown(result.Success ? 0 : 5);
                return;
            }

            if (e.Args.Any(a => a.Equals("--sim-alarm", StringComparison.OrdinalIgnoreCase)))
            {
                int warmup = 500;
                string? ticksArg = e.Args.FirstOrDefault(a => a.StartsWith("--ticks=", StringComparison.OrdinalIgnoreCase));
                if (ticksArg is not null && int.TryParse(ticksArg.Split('=')[1], out int parsed) && parsed > 0)
                {
                    warmup = parsed;
                }

                SimulatorSmokeTester.Result result = SimulatorSmokeTester.RunAlarmAudit(warmup);
                Console.WriteLine($"sim_alarm success={result.Success} {result.Message}");
                Shutdown(result.Success ? 0 : 9);
                return;
            }

            if (e.Args.Any(a => a.Equals("--sim-maintenance", StringComparison.OrdinalIgnoreCase)))
            {
                int warmup = 800;
                string? ticksArg = e.Args.FirstOrDefault(a => a.StartsWith("--ticks=", StringComparison.OrdinalIgnoreCase));
                if (ticksArg is not null && int.TryParse(ticksArg.Split('=')[1], out int parsed) && parsed > 0)
                {
                    warmup = parsed;
                }

                SimulatorSmokeTester.Result result = SimulatorSmokeTester.RunMaintenanceAudit(warmup);
                Console.WriteLine($"sim_maintenance success={result.Success} {result.Message}");
                Shutdown(result.Success ? 0 : 8);
                return;
            }

            if (e.Args.Any(a => a.Equals("--sim-ui-hints", StringComparison.OrdinalIgnoreCase)))
            {
                int warmup = 600;
                string? ticksArg = e.Args.FirstOrDefault(a => a.StartsWith("--ticks=", StringComparison.OrdinalIgnoreCase));
                if (ticksArg is not null && int.TryParse(ticksArg.Split('=')[1], out int parsed) && parsed > 0)
                {
                    warmup = parsed;
                }

                SimulatorSmokeTester.Result result = SimulatorSmokeTester.RunUiHintsAudit(warmup);
                Console.WriteLine($"sim_ui_hints success={result.Success} {result.Message}");
                Shutdown(result.Success ? 0 : 10);
                return;
            }

            if (e.Args.Any(a => a.Equals("--sim-aligner-audit", StringComparison.OrdinalIgnoreCase)))
            {
                int ticks = 160_000;
                string? ticksArg = e.Args.FirstOrDefault(a => a.StartsWith("--ticks=", StringComparison.OrdinalIgnoreCase));
                if (ticksArg is not null && int.TryParse(ticksArg.Split('=')[1], out int parsed) && parsed > 0)
                {
                    ticks = parsed;
                }

                SimulatorSmokeTester.Result result = SimulatorSmokeTester.RunAligner2PipelineAudit(ticks);
                Console.WriteLine($"sim_aligner_audit success={result.Success} {result.Message}");
                if (result.Report is not null)
                {
                    Console.WriteLine(result.Report);
                }

                Shutdown(result.Success ? 0 : 12);
                return;
            }

            if (e.Args.Any(a => a.Equals("--sim-stress", StringComparison.OrdinalIgnoreCase)))
            {
                int runs = 3;
                string? runsArg = e.Args.FirstOrDefault(a => a.StartsWith("--runs=", StringComparison.OrdinalIgnoreCase));
                if (runsArg is not null && int.TryParse(runsArg.Split('=')[1], out int parsedRuns) && parsedRuns > 0)
                {
                    runs = parsedRuns;
                }

                SimulatorSmokeTester.Result result = SimulatorSmokeTester.RunStressAudit(runs);
                Console.WriteLine($"sim_stress success={result.Success} runs={result.Runs} {result.Message}");
                if (result.Report is not null)
                {
                    Console.WriteLine(result.Report);
                }

                Shutdown(result.Success ? 0 : 11);
                return;
            }

            if (e.Args.Any(a => a.Equals("--sim-policy-batch", StringComparison.OrdinalIgnoreCase)))
            {
                int runs = 20;
                int ticks = 5000;
                string? runsArg = e.Args.FirstOrDefault(a => a.StartsWith("--runs=", StringComparison.OrdinalIgnoreCase));
                string? ticksArg = e.Args.FirstOrDefault(a => a.StartsWith("--ticks=", StringComparison.OrdinalIgnoreCase));
                if (runsArg is not null && int.TryParse(runsArg.Split('=')[1], out int parsedRuns) && parsedRuns > 0)
                {
                    runs = parsedRuns;
                }

                if (ticksArg is not null && int.TryParse(ticksArg.Split('=')[1], out int parsedTicks) && parsedTicks > 0)
                {
                    ticks = parsedTicks;
                }

                SimulatorSmokeTester.Result result = SimulatorSmokeTester.RunBatch(runs, ticks);
                Console.WriteLine($"sim_policy_batch runs={result.Runs} ticks={result.Ticks} success={result.Success} max_side_storage={result.MaxSideStorage} message={result.Message}");
                Shutdown(result.Success ? 0 : 3);
                return;
            }

            string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "etch_hmi.db");
            DatabaseService databaseService = new(dbPath);
            databaseService.Initialize();
            databaseService.EnsureDefaultAdmin(DefaultAdminUsername, DefaultAdminPassword);
            databaseService.EnsureDefaultWorker("worker", "Worker1234");

            LoginWindow loginWindow = new(databaseService);
            bool? loginResult = loginWindow.ShowDialog();

            if (loginResult != true || SessionContext.CurrentUser is null)
            {
                Shutdown();
                return;
            }

            MainWindow mainWindow = new(databaseService);
            MainWindow = mainWindow;
            // 로그인 창만 닫힌 순간 열린 창이 없으므로, 기본 OnLastWindowClose면 여기서 앱이 종료됨
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.Show();
        }

        private static void RegisterGlobalExceptionHandlers()
        {
            Current.DispatcherUnhandledException += (_, args) =>
            {
                LogCrash("UI", args.Exception);
                MessageBox.Show(
                    $"처리되지 않은 오류:\n{args.Exception.Message}\n\n자세한 내용: data/crash.log",
                    "Etch HMI",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                args.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                {
                    LogCrash("AppDomain", ex);
                }
            };

            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                LogCrash("Task", args.Exception);
                args.SetObserved();
            };
        }

        private static void LogCrash(string source, Exception ex)
        {
            try
            {
                string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "crash.log");
                string text =
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}\n{ex}\n\n";
                File.AppendAllText(path, text);
            }
            catch
            {
                // ignore
            }
        }
    }

}

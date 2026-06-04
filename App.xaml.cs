using System.Windows;
using System.IO;
using etch_ui.Security;
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
    }

}

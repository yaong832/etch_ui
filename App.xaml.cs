using System.Windows;
using System.IO;
using etch_ui.Security;

namespace etch_ui
{
    public partial class App : Application
    {
        private const string DefaultAdminUsername = "admin";
        private const string DefaultAdminPassword = "Admin1234";

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

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

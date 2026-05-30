using System.Threading.Tasks;
using System.Windows;

namespace ReWrite
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private static System.Threading.Mutex? _singleInstanceMutex;
        private const string SINGLE_INSTANCE_MUTEX_NAME = "Global\\ReWrite_SingleInstance_v1";

        protected override async void OnStartup(StartupEventArgs e)
        {
            bool createdNew = false;
            try
            {
                _singleInstanceMutex = new System.Threading.Mutex(initiallyOwned: false, name: SINGLE_INSTANCE_MUTEX_NAME, createdNew: out createdNew);
            }
            catch
            {
                createdNew = true;
            }

            if (!createdNew)
            {
                try
                {
                    uint msg = NativeMethods.RegisterWindowMessage("ReWrite_SingleInstance_ShowAlreadyRunning");
                    if (msg != 0)
                        NativeMethods.PostMessage(NativeMethods.HWND_BROADCAST, msg, IntPtr.Zero, IntPtr.Zero);
                }
                catch { }

                System.Windows.MessageBox.Show(
                    "A previous instance of ReWrite is already running. Look for ReWrite icon at the bottom right of the screen.",
                    "Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                Shutdown();
                return;
            }

            base.OnStartup(e);

            // Initialize localization early so windows can read localized strings
            string? persistedLocale = null;
            try
            {
                var settingsPath = System.IO.Path.Combine(AppPaths.SettingsDirectory, "appsettings.json");
                if (System.IO.File.Exists(settingsPath))
                {
                    var json = System.IO.File.ReadAllText(settingsPath);
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("locale", out var prop))
                        persistedLocale = prop.GetString();
                }
            }
            catch { }

            Localization.Initialize(persistedLocale);

            var controllerWindow = new MainWindow();
            MainWindow = controllerWindow;
            controllerWindow.Show();
            controllerWindow.Hide();

            var welcomeWindow = new WelcomeWindow();
            welcomeWindow.Show();

            await Task.Delay(1400);
            welcomeWindow.MarkReady();
        }
    }
}


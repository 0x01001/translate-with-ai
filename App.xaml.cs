using System.Threading.Tasks;
using System.Windows;

namespace ReWrite
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
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


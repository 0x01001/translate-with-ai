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


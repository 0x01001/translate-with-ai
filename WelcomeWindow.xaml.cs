// WelcomeWindow.xaml.cs

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;

namespace ReWrite
{
    public partial class WelcomeWindow : Window
    {
        private const string TutorialStateFileName = "tutorial.json";
        private bool _hasSeenTutorial = false;

        private sealed class TutorialState
        {
            public bool HasSeenVideo { get; set; }
        }

        public WelcomeWindow()
        {
            InitializeComponent();

            Icon = MainWindow.LoadWindowIcon();
            LogoImage.Source = EmbeddedUiContent.LoadImageSource("logo.png");

            LoadTutorialState();

            Loaded += WelcomeWindow_Loaded;
        }

        private void WelcomeWindow_Loaded(object sender, RoutedEventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;

            EnableRoundedCorners(hwnd);
            EnableDarkMode(hwnd);
        }

        private static void EnableRoundedCorners(IntPtr hwnd)
        {
            const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
            const int DWMWCP_ROUND = 2;

            int preference = DWMWCP_ROUND;

            DwmSetWindowAttribute(
                hwnd,
                DWMWA_WINDOW_CORNER_PREFERENCE,
                ref preference,
                sizeof(int));
        }

        private static void EnableDarkMode(IntPtr hwnd)
        {
            const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

            int enabled = 1;

            DwmSetWindowAttribute(
                hwnd,
                DWMWA_USE_IMMERSIVE_DARK_MODE,
                ref enabled,
                sizeof(int));
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd,
            int dwAttribute,
            ref int pvAttribute,
            int cbAttribute);

        public void MarkReady()
        {
            StatusText.Text = "Da san sang. Ban co the dong cua so nay.";

            if (LoadingBar != null)
            {
                LoadingBar.Visibility = Visibility.Collapsed;
            }

            CloseButton.Content = _hasSeenTutorial ? "Dong" : "Xem huong dan";
            CloseButton.Visibility = Visibility.Visible;
            CloseButton.IsEnabled = true;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_hasSeenTutorial)
            {
                var tutorialWindow = new TutorialWindow();
                tutorialWindow.Show();
                tutorialWindow.WindowState = WindowState.Normal;
                tutorialWindow.Activate();
                tutorialWindow.Focus();

                _hasSeenTutorial = true;
                SaveTutorialState();
            }

            Close();
        }

        private void LoadTutorialState()
        {
            try
            {
                string path = GetTutorialStatePath();
                if (!File.Exists(path))
                {
                    _hasSeenTutorial = false;
                    return;
                }

                string json = File.ReadAllText(path);
                var state = JsonSerializer.Deserialize<TutorialState>(json);
                _hasSeenTutorial = state?.HasSeenVideo ?? false;
            }
            catch
            {
                _hasSeenTutorial = false;
            }
        }

        private void SaveTutorialState()
        {
            try
            {
                string dir = GetSettingsDirectory();
                Directory.CreateDirectory(dir);
                string path = GetTutorialStatePath();

                var state = new TutorialState { HasSeenVideo = _hasSeenTutorial };
                string json = JsonSerializer.Serialize(state);
                File.WriteAllText(path, json);
            }
            catch
            {
            }
        }

        private static string GetSettingsDirectory()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "ReWrite");
        }

        private static string GetTutorialStatePath()
        {
            return Path.Combine(GetSettingsDirectory(), TutorialStateFileName);
        }
    }
}
// WelcomeWindow.xaml.cs

using System;
using System.Windows;
using System.Windows.Interop;

namespace ReWrite
{
    /// <summary>
    /// Splash / welcome screen shown at first launch.
    /// Tutorial state I/O delegated to TutorialStateStore;
    /// DWM window styling delegated to NativeMethods.
    /// </summary>
    public partial class WelcomeWindow : Window
    {
        private bool _hasSeenTutorial;

        public WelcomeWindow()
        {
            InitializeComponent();
            Icon = MainWindow.LoadWindowIcon();
            LogoImage.Source = EmbeddedUiContent.LoadImageSource("logo.png");

            // Localize visible strings
            UpdateLocalization();
            Localization.LocaleChanged += OnLocaleChanged;

            _hasSeenTutorial = TutorialStateStore.HasSeenTutorial();

            Loaded += WelcomeWindow_Loaded;
        }

        private void OnLocaleChanged(string locale)
        {
            // Ensure UI update runs on dispatcher thread
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => UpdateLocalization()));
        }

        private void UpdateLocalization()
        {
            try { this.Title = Localization.Get("title.welcome"); } catch { }
            try { StatusText.Text = Localization.Get("welcome.loading"); } catch { }
            try { CloseButton.Content = Localization.Get("button.close"); } catch { }
        }

        private void WelcomeWindow_Loaded(object sender, RoutedEventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            ApplyWindowChrome(hwnd);
        }

        // ── Window chrome ─────────────────────────────────────────────────────────

        private static void ApplyWindowChrome(IntPtr hwnd)
        {
            int roundCorners = NativeMethods.DWMWCP_ROUND;
            NativeMethods.DwmSetWindowAttribute(
                hwnd,
                NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE,
                ref roundCorners,
                sizeof(int));

            int darkMode = 1;
            NativeMethods.DwmSetWindowAttribute(
                hwnd,
                NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE,
                ref darkMode,
                sizeof(int));
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Called by App.xaml.cs once the background warm-up is complete.
        /// Updates status text and reveals the close/tutorial button.
        /// </summary>
        public void MarkReady()
        {
            StatusText.Text = Localization.Get("welcome.ready");

            if (LoadingBar != null)
                LoadingBar.Visibility = Visibility.Collapsed;

            CloseButton.Content    = _hasSeenTutorial ? Localization.Get("button.close") : Localization.Get("button.view_tutorial");
            CloseButton.Visibility = Visibility.Visible;
            CloseButton.IsEnabled  = true;
        }

        // ── Button handler ────────────────────────────────────────────────────────

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
                TutorialStateStore.MarkSeen();
            }

            Close();
        }
    }
}
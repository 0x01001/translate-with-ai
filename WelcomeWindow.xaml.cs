// WelcomeWindow.xaml.cs

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ReWrite
{
    public partial class WelcomeWindow : Window
    {
        public WelcomeWindow()
        {
            InitializeComponent();

            Icon = MainWindow.LoadWindowIcon();

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

            CloseButton.Visibility = Visibility.Visible;
            CloseButton.IsEnabled = true;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
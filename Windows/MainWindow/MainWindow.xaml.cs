using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace ReWrite
{
    /// <summary>
    /// Hidden controller window. Owns the global hotkey, system tray icon,
    /// and orchestrates the popup and settings windows.
    /// Hotkey parsing/persistence and all Win32 declarations live in
    /// Core/ and Infrastructure/ respectively.
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int HOTKEY_ID = 9000;

        private HwndSource? _hwndSource;
        private System.Windows.Forms.NotifyIcon? _trayIcon;
        private PopupWindow? _popupWindow;
        private SettingsWindow? _settingsWindow;
        private IntPtr _mainHwnd = IntPtr.Zero;

        private uint _hotkeyModifiers = HotKeyManager.MOD_CONTROL | HotKeyManager.MOD_SHIFT;
        private uint _hotkeyVk = 0x41; // A
        private string _hotkeyText = "Ctrl+Shift+A";

        public MainWindow()
        {
            InitializeComponent();
            Icon = LoadWindowIcon();

            // Window acts as a hidden controller — size/position are set off-screen in XAML
            this.Width = 0;
            this.Height = 0;
            this.ShowInTaskbar = false;

            // Auto-start on boot right after installation (first-run logic)
            if (!StartupManager.IsAutostartEnabled())
                StartupManager.EnableAutostart();

            InitializeTrayIcon();

            // Warm up the popup so WebView2 loads instantly when the hotkey fires
            _popupWindow = new PopupWindow(this);
            _popupWindow.Hide();

            _settingsWindow = CreateSettingsWindow();
        }

        internal static System.Windows.Media.ImageSource? LoadWindowIcon()
        {
            try   { return EmbeddedUiContent.LoadImageSource("logo.ico"); }
            catch { return null; }
        }

        // ── Hotkey registration ───────────────────────────────────────────────────

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var wih = new WindowInteropHelper(this);
            _mainHwnd = wih.Handle;

            _hwndSource = HwndSource.FromHwnd(_mainHwnd);
            _hwndSource?.AddHook(HwndHook);

            LoadHotkeyConfig();

            if (!RegisterCurrentHotkey())
            {
                System.Windows.MessageBox.Show(
                    $"Could not register the global hotkey {_hotkeyText}.\n" +
                    "Please ensure no other application is using it.",
                    "ReWrite - Hotkey Register Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == HotKeyManager.WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                OnHotKeyPressed();
                handled = true;
            }
            return IntPtr.Zero;
        }

        private bool RegisterCurrentHotkey()
        {
            if (_mainHwnd == IntPtr.Zero) return false;
            HotKeyManager.Unregister(_mainHwnd, HOTKEY_ID);
            return HotKeyManager.Register(_mainHwnd, HOTKEY_ID, _hotkeyModifiers, _hotkeyVk);
        }

        private void LoadHotkeyConfig()
        {
            var loaded = HotkeyPersistence.Load();
            if (loaded.HasValue)
            {
                _hotkeyModifiers = loaded.Value.modifiers;
                _hotkeyVk        = loaded.Value.vk;
                _hotkeyText      = loaded.Value.normalized;
            }
        }

        // ── Public API for SettingsWindow / PopupWindow ───────────────────────────

        public string GetCurrentHotkeyText() => _hotkeyText;

        public bool TryUpdateHotkey(string hotkeyText, out string error)
        {
            if (!HotkeyParser.TryParse(hotkeyText, out uint modifiers, out uint vk, out string normalized, out error))
                return false;

            if (_mainHwnd == IntPtr.Zero)
            {
                error = "Ứng dụng chưa sẵn sàng để đăng ký phím tắt.";
                return false;
            }

            HotKeyManager.Unregister(_mainHwnd, HOTKEY_ID);
            if (!HotKeyManager.Register(_mainHwnd, HOTKEY_ID, modifiers, vk))
            {
                // Restore previous hotkey if registration fails
                if (_hotkeyVk != 0)
                    HotKeyManager.Register(_mainHwnd, HOTKEY_ID, _hotkeyModifiers, _hotkeyVk);
                error = "Không thể đăng ký phím tắt. Có thể đang bị ứng dụng khác sử dụng.";
                return false;
            }

            _hotkeyModifiers = modifiers;
            _hotkeyVk        = vk;
            _hotkeyText      = normalized;
            HotkeyPersistence.Save(normalized);
            return true;
        }

        // ── Hotkey handler ────────────────────────────────────────────────────────

        private async void OnHotKeyPressed()
        {
            // 1. Remember the active window that had focus
            IntPtr activeWindowHwnd = NativeMethods.GetForegroundWindow();

            // 2. Capture selected text via Ctrl+C
            string selectedText = "";
            string? originalText = null;

            try
            {
                if (System.Windows.Clipboard.ContainsText())
                    originalText = System.Windows.Clipboard.GetText();
            }
            catch { }

            try
            {
                System.Windows.Clipboard.Clear();
                await KeyboardSimulator.SimulateCopyAsync();

                // Wait up to 250 ms for the clipboard to populate
                for (int i = 0; i < 10; i++)
                {
                    if (System.Windows.Clipboard.ContainsText())
                    {
                        selectedText = System.Windows.Clipboard.GetText();
                        break;
                    }
                    await Task.Delay(25);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error capturing text: {ex.Message}");
            }
            finally
            {
                // Restore the user's original clipboard so we don't pollute their history
                try
                {
                    if (originalText != null)
                        System.Windows.Clipboard.SetText(originalText);
                }
                catch { }
            }

            // 3. Open the floating popup and feed it the captured text
            ShowPopup(selectedText, activeWindowHwnd);
        }

        // ── Popup positioning ─────────────────────────────────────────────────────

        private void ShowPopup(string selectedText, IntPtr targetHwnd)
        {
            if (_popupWindow == null) return;

            NativeMethods.GetCursorPos(out NativeMethods.POINT mousePos);

            double screenWidth  = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            double popupWidth   = 460;
            double popupHeight  = 360;

            // Centre the popup horizontally under the cursor, offset downward
            double left = mousePos.X - (popupWidth / 2);
            double top  = mousePos.Y + 15;

            // Constrain to screen edges
            if (left < 10)                           left = 10;
            if (left + popupWidth > screenWidth)     left = screenWidth - popupWidth - 10;
            if (top + popupHeight > screenHeight)    top  = mousePos.Y - popupHeight - 15;
            if (top < 10)                            top  = 10;

            _popupWindow.Left = left;
            _popupWindow.Top  = top;
            _popupWindow.PrepareShow(selectedText, targetHwnd);
            _popupWindow.Show();
            _popupWindow.WindowState = WindowState.Normal;
            _popupWindow.Activate();
            _popupWindow.Focus();
        }

        // ── System tray ───────────────────────────────────────────────────────────

        private void InitializeTrayIcon()
        {
            _trayIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon    = CreateTrayIcon(),
                Text    = "ReWrite - Active",
                Visible = true
            };

            var contextMenu = new System.Windows.Forms.ContextMenuStrip();

            var openSettingsItem = new System.Windows.Forms.ToolStripMenuItem("Settings & Configuration");
            openSettingsItem.Click += (s, e) => OpenSettings();
            contextMenu.Items.Add(openSettingsItem);

            contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

            var autostartItem = new System.Windows.Forms.ToolStripMenuItem("Start with Windows")
            {
                Checked = StartupManager.IsAutostartEnabled()
            };
            autostartItem.Click += (s, e) =>
            {
                if (StartupManager.IsAutostartEnabled())
                {
                    StartupManager.DisableAutostart();
                    autostartItem.Checked = false;
                }
                else
                {
                    StartupManager.EnableAutostart();
                    autostartItem.Checked = true;
                }
            };
            contextMenu.Items.Add(autostartItem);

            contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

            var exitItem = new System.Windows.Forms.ToolStripMenuItem("Exit");
            exitItem.Click += (s, e) => ExitApp();
            contextMenu.Items.Add(exitItem);

            _trayIcon.ContextMenuStrip = contextMenu;
            _trayIcon.DoubleClick += (s, e) => OpenSettings();
        }

        private static System.Drawing.Icon CreateTrayIcon()
        {
            try
            {
                var icon = EmbeddedUiContent.LoadDrawingIcon("logo.ico");
                if (icon != null) return icon;
            }
            catch { }
            return System.Drawing.SystemIcons.Application;
        }

        private void OpenSettings()
        {
            if (_settingsWindow == null)
                _settingsWindow = CreateSettingsWindow();

            if (_settingsWindow.IsVisible)
            {
                _settingsWindow.Activate();
                _settingsWindow.Focus();
                return;
            }
            _settingsWindow.ShowSettings();
        }

        private SettingsWindow CreateSettingsWindow()
        {
            var window = new SettingsWindow(this);
            window.Closed += (s, e) =>
            {
                if (ReferenceEquals(_settingsWindow, window))
                    _settingsWindow = null;
            };
            return window;
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void ExitApp()
        {
            DisposeTrayIcon();
            if (_mainHwnd != IntPtr.Zero)
                HotKeyManager.Unregister(_mainHwnd, HOTKEY_ID);
            _popupWindow?.Close();
            _settingsWindow?.Close();
            System.Windows.Application.Current.Shutdown();
        }

        protected override void OnClosed(EventArgs e)
        {
            DisposeTrayIcon();
            if (_mainHwnd != IntPtr.Zero)
                HotKeyManager.Unregister(_mainHwnd, HOTKEY_ID);
            base.OnClosed(e);
        }

        private void DisposeTrayIcon()
        {
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }
        }
    }
}
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
        private const uint TRAY_MENU_OPEN_SETTINGS = 1001;
        private const uint TRAY_MENU_AUTOSTART = 1002;
        private const uint TRAY_MENU_EXIT = 1003;
        private static int _singleInstanceMsg;

        private HwndSource? _hwndSource;
        private System.Windows.Forms.NotifyIcon? _trayIcon;
        private PopupWindow? _popupWindow;
        private SettingsWindow? _settingsWindow;
        private IntPtr _mainHwnd = IntPtr.Zero;

        private uint _hotkeyModifiers = HotKeyManager.MOD_ALT;
        private uint _hotkeyVk = 0x58; // X
        private string _hotkeyText = "Alt+X";

        public MainWindow()
        {
            InitializeComponent();
            Icon = LoadWindowIcon();

            try
            {
                NativeMethods.SetPreferredAppMode(NativeMethods.PreferredAppMode.ForceDark);
                NativeMethods.FlushMenuThemes();
            }
            catch { }

            // Window acts as a hidden controller — size/position are set off-screen in XAML
            this.Width = 0;
            this.Height = 0;
            this.ShowInTaskbar = false;

            InitializeTrayIcon();
            // Localize tray/menu and update on locale change
            UpdateLocalization();
            Localization.LocaleChanged += OnLocaleChanged;

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

            try
            {
                uint msg = NativeMethods.RegisterWindowMessage("ReWrite_SingleInstance_ShowAlreadyRunning");
                if (msg != 0)
                    _singleInstanceMsg = (int)msg;
            }
            catch { }

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
                return IntPtr.Zero;
            }

            if (_singleInstanceMsg != 0 && msg == _singleInstanceMsg)
            {
                try
                {
                    if (_trayIcon != null)
                    {
                        _trayIcon.ShowBalloonTip(
                            3000,
                            "Warning",
                            "A previous instance of ReWrite is already running. Look for ReWrite icon at the bottom right of the screen.",
                            System.Windows.Forms.ToolTipIcon.Warning);
                    }
                }
                catch { }

                handled = true;
                return IntPtr.Zero;
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

            // 3. Start quick translation. The popup WebView handles provider calls in the background.
            if (_popupWindow == null)
                _popupWindow = new PopupWindow(this);
            _popupWindow.BeginQuickTranslate(selectedText, activeWindowHwnd);
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

            _trayIcon.MouseUp += TrayIcon_MouseUp;
            _trayIcon.DoubleClick += (s, e) => OpenSettings();
        }

        private async void TrayIcon_MouseUp(object? sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Right)
                await ShowTrayMenuAsync();
        }

        private async Task ShowTrayMenuAsync()
        {
            if (_mainHwnd == IntPtr.Zero)
                return;

            IntPtr menu = NativeMethods.CreatePopupMenu();
            if (menu == IntPtr.Zero)
                return;

            try
            {   
                NativeMethods.AppendMenu(menu, NativeMethods.MF_STRING, TRAY_MENU_OPEN_SETTINGS, Localization.Get("tray.open_settings"));
                uint autostartFlags = NativeMethods.MF_STRING;
                if (await StartupManager.IsAutostartEnabledAsync())
                    autostartFlags |= NativeMethods.MF_CHECKED;
                NativeMethods.AppendMenu(menu, autostartFlags, TRAY_MENU_AUTOSTART, Localization.Get("tray.autostart"));

                NativeMethods.AppendMenu(menu, NativeMethods.MF_SEPARATOR, 0, IntPtr.Zero);
                NativeMethods.AppendMenu(menu, NativeMethods.MF_STRING, TRAY_MENU_EXIT, Localization.Get("tray.exit"));

                try
                {
                    NativeMethods.FlushMenuThemes();
                }
                catch { }

                NativeMethods.GetCursorPos(out NativeMethods.POINT cursorPos);
                NativeMethods.SetForegroundWindow(_mainHwnd);

                uint commandId = NativeMethods.TrackPopupMenuEx(
                    menu,
                    NativeMethods.TPM_RIGHTBUTTON | NativeMethods.TPM_RETURNCMD,
                    cursorPos.X,
                    cursorPos.Y,
                    _mainHwnd,
                    IntPtr.Zero);

                if (commandId == TRAY_MENU_OPEN_SETTINGS)
                {
                    OpenSettings();
                }
                else if (commandId == TRAY_MENU_AUTOSTART)
                {
                    if (await StartupManager.IsAutostartEnabledAsync())
                        await StartupManager.DisableAutostartAsync();
                    else
                        await StartupManager.EnableAutostartAsync();
                }
                else if (commandId == TRAY_MENU_EXIT)
                {
                    ExitApp();
                }
            }
            finally
            {
                NativeMethods.PostMessage(_mainHwnd, (uint)NativeMethods.WM_NULL, IntPtr.Zero, IntPtr.Zero);
                NativeMethods.DestroyMenu(menu);
            }
        }

        private void OnLocaleChanged(string locale)
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => UpdateLocalization()));
        }

        private void UpdateLocalization()
        {
            try
            {
                if (_trayIcon != null)
                    _trayIcon.Text = Localization.Get("tray.tooltip");
            }
            catch { }
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
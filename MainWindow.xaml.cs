using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;

namespace ReWrite
{
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

        private const string HotkeyConfigFileName = "hotkey.json";

        private sealed class HotkeyConfig
        {
            public string Hotkey { get; set; } = "";
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        public MainWindow()
        {
            InitializeComponent();
            Icon = LoadWindowIcon();

            // Window is positioned off-screen in XAML to act as a hidden controller
            this.Width = 0;
            this.Height = 0;
            this.ShowInTaskbar = false;

            // Auto-start on boot right after installation (first-run logic)
            if (!StartupManager.IsAutostartEnabled())
            {
                StartupManager.EnableAutostart();
            }

            // Setup system tray icon
            InitializeTrayIcon();

            // Warm up the popup window so WebView2 loads instantly when hotkey is pressed
            _popupWindow = new PopupWindow(this);
            _popupWindow.Hide();

            _settingsWindow = CreateSettingsWindow();
        }

        internal static System.Windows.Media.ImageSource? LoadWindowIcon()
        {
            try
            {
                return EmbeddedUiContent.LoadImageSource("logo.ico");
            }
            catch
            {
                return null;
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var wih = new WindowInteropHelper(this);
            IntPtr hwnd = wih.Handle;

            _mainHwnd = hwnd;

            _hwndSource = HwndSource.FromHwnd(hwnd);
            _hwndSource?.AddHook(HwndHook);

            LoadHotkeyConfig();
            if (!RegisterCurrentHotkey())
            {
                System.Windows.MessageBox.Show(
                    $"Could not register the global hotkey {_hotkeyText}.\n" +
                    "Please ensure no other application is using it.",
                    "ReWrite - Hotkey Register Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
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

        private async void OnHotKeyPressed()
        {
            // 1. Remember the active window that had the cursor
            IntPtr activeWindowHwnd = GetForegroundWindow();

            // 2. Capture selected text from that window
            string selectedText = "";
            string? originalText = null;

            try
            {
                if (System.Windows.Clipboard.ContainsText())
                {
                    originalText = System.Windows.Clipboard.GetText();
                }
            }
            catch { }

            try
            {
                System.Windows.Clipboard.Clear();

                // Trigger Ctrl+C
                await KeyboardSimulator.SimulateCopyAsync();

                // Wait up to 250ms for clipboard content to populate
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
                // Restore original clipboard so we don't pollute the user's history
                try
                {
                    if (originalText != null)
                    {
                        System.Windows.Clipboard.SetText(originalText);
                    }
                }
                catch { }
            }

            // 3. Open the floating popup window and feed it the captured text
            ShowPopup(selectedText, activeWindowHwnd);
        }

        private void ShowPopup(string selectedText, IntPtr targetHwnd)
        {
            if (_popupWindow == null) return;

            // Get mouse position to show window near it
            POINT mousePos;
            GetCursorPos(out mousePos);

            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;

            double popupWidth = 460;
            double popupHeight = 360;

            // Center popup horizontally under the cursor, and offset vertically
            double left = mousePos.X - (popupWidth / 2);
            double top = mousePos.Y + 15;

            // Constrain left/right boundaries
            if (left < 10) left = 10;
            if (left + popupWidth > screenWidth) left = screenWidth - popupWidth - 10;

            // Constrain top/bottom boundaries: flip above cursor if it would render off-screen
            if (top + popupHeight > screenHeight)
            {
                top = mousePos.Y - popupHeight - 15;
            }
            if (top < 10) top = 10;

            _popupWindow.Left = left;
            _popupWindow.Top = top;

            // Load values into popup and show it
            _popupWindow.PrepareShow(selectedText, targetHwnd);
            _popupWindow.Show();
            _popupWindow.WindowState = WindowState.Normal;
            _popupWindow.Activate();
            _popupWindow.Focus();
        }

        private void InitializeTrayIcon()
        {
            _trayIcon = new System.Windows.Forms.NotifyIcon();
            _trayIcon.Icon = CreatePremiumTrayIcon();
            _trayIcon.Text = "ReWrite - Active";
            _trayIcon.Visible = true;

            var contextMenu = new System.Windows.Forms.ContextMenuStrip();

            var openSettingsItem = new System.Windows.Forms.ToolStripMenuItem("Settings & Configuration");
            openSettingsItem.Click += (s, e) => OpenSettings();
            contextMenu.Items.Add(openSettingsItem);

            contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

            var autostartItem = new System.Windows.Forms.ToolStripMenuItem("Start with Windows");
            autostartItem.Checked = StartupManager.IsAutostartEnabled();
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

        private void OpenSettings()
        {
            if (_settingsWindow == null)
            {
                _settingsWindow = CreateSettingsWindow();
            }

            if (_settingsWindow.IsVisible)
            {
                _settingsWindow.Activate();
                _settingsWindow.Focus();
                return;
            }

            _settingsWindow.ShowSettings();
        }

        private System.Drawing.Icon CreatePremiumTrayIcon()
        {
            try
            {
                var icon = EmbeddedUiContent.LoadDrawingIcon("logo.ico");
                if (icon != null)
                {
                    return icon;
                }

                return System.Drawing.SystemIcons.Application;
            }
            catch
            {
                // Fallback to standard Application icon
                return System.Drawing.SystemIcons.Application;
            }
        }

        private SettingsWindow CreateSettingsWindow()
        {
            var window = new SettingsWindow(this);
            window.Closed += (s, e) =>
            {
                if (ReferenceEquals(_settingsWindow, window))
                {
                    _settingsWindow = null;
                }
            };
            return window;
        }

        private void ExitApp()
        {
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }

            if (_mainHwnd != IntPtr.Zero)
            {
                HotKeyManager.Unregister(_mainHwnd, HOTKEY_ID);
            }

            _popupWindow?.Close();
            _settingsWindow?.Close();
            System.Windows.Application.Current.Shutdown();
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
            if (_mainHwnd != IntPtr.Zero)
            {
                HotKeyManager.Unregister(_mainHwnd, HOTKEY_ID);
            }
            base.OnClosed(e);
        }

        public string GetCurrentHotkeyText()
        {
            return _hotkeyText;
        }

        public bool TryUpdateHotkey(string hotkeyText, out string error)
        {
            if (!TryParseHotkey(hotkeyText, out uint modifiers, out uint vk, out string normalized, out error))
            {
                return false;
            }

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
                {
                    HotKeyManager.Register(_mainHwnd, HOTKEY_ID, _hotkeyModifiers, _hotkeyVk);
                }
                error = "Không thể đăng ký phím tắt. Có thể đang bị ứng dụng khác sử dụng.";
                return false;
            }

            _hotkeyModifiers = modifiers;
            _hotkeyVk = vk;
            _hotkeyText = normalized;
            SaveHotkeyConfig(normalized);
            return true;
        }

        private bool RegisterCurrentHotkey()
        {
            if (_mainHwnd == IntPtr.Zero) return false;
            HotKeyManager.Unregister(_mainHwnd, HOTKEY_ID);
            return HotKeyManager.Register(_mainHwnd, HOTKEY_ID, _hotkeyModifiers, _hotkeyVk);
        }

        private void LoadHotkeyConfig()
        {
            try
            {
                string path = GetHotkeyConfigPath();
                if (!File.Exists(path)) return;

                string json = File.ReadAllText(path);
                var config = JsonSerializer.Deserialize<HotkeyConfig>(json);
                if (config == null || string.IsNullOrWhiteSpace(config.Hotkey)) return;

                if (TryParseHotkey(config.Hotkey, out uint modifiers, out uint vk, out string normalized, out _))
                {
                    _hotkeyModifiers = modifiers;
                    _hotkeyVk = vk;
                    _hotkeyText = normalized;
                }
            }
            catch { }
        }

        private void SaveHotkeyConfig(string hotkeyText)
        {
            try
            {
                string dir = GetSettingsDirectory();
                Directory.CreateDirectory(dir);
                string path = GetHotkeyConfigPath();

                var config = new HotkeyConfig { Hotkey = hotkeyText };
                string json = JsonSerializer.Serialize(config);
                File.WriteAllText(path, json);
            }
            catch { }
        }

        private string GetSettingsDirectory()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "ReWrite");
        }

        private string GetHotkeyConfigPath()
        {
            return Path.Combine(GetSettingsDirectory(), HotkeyConfigFileName);
        }

        private bool TryParseHotkey(string hotkeyText, out uint modifiers, out uint vk, out string normalized, out string error)
        {
            modifiers = HotKeyManager.MOD_NONE;
            vk = 0;
            normalized = "";
            error = "";

            if (string.IsNullOrWhiteSpace(hotkeyText))
            {
                error = "Phím tắt không được để trống.";
                return false;
            }

            string[] parts = hotkeyText.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            string? keyToken = null;

            foreach (string raw in parts)
            {
                string token = raw.Trim();
                string lower = token.ToLowerInvariant();

                if (lower == "ctrl" || lower == "control")
                {
                    modifiers |= HotKeyManager.MOD_CONTROL;
                    continue;
                }
                if (lower == "shift")
                {
                    modifiers |= HotKeyManager.MOD_SHIFT;
                    continue;
                }
                if (lower == "alt")
                {
                    modifiers |= HotKeyManager.MOD_ALT;
                    continue;
                }
                if (lower == "win" || lower == "windows")
                {
                    modifiers |= HotKeyManager.MOD_WIN;
                    continue;
                }

                if (keyToken != null)
                {
                    error = "Chỉ được phép một phím chính.";
                    return false;
                }
                keyToken = token;
            }

            if (keyToken == null)
            {
                error = "Thiếu phím chính.";
                return false;
            }

            if (modifiers == HotKeyManager.MOD_NONE)
            {
                error = "Cần ít nhất một phím bổ trợ (Ctrl/Shift/Alt/Win).";
                return false;
            }

            if (!TryParseKeyToken(keyToken, out Key key, out string keyDisplay))
            {
                error = "Phím chính không hợp lệ.";
                return false;
            }

            vk = (uint)KeyInterop.VirtualKeyFromKey(key);
            normalized = BuildHotkeyText(modifiers, keyDisplay);
            return true;
        }

        private bool TryParseKeyToken(string token, out Key key, out string keyDisplay)
        {
            key = Key.None;
            keyDisplay = "";

            string upper = token.Trim().ToUpperInvariant();

            if (upper.Length == 1)
            {
                char c = upper[0];
                if (c >= 'A' && c <= 'Z')
                {
                    key = (Key)Enum.Parse(typeof(Key), c.ToString());
                    keyDisplay = c.ToString();
                    return true;
                }
                if (c >= '0' && c <= '9')
                {
                    key = (Key)Enum.Parse(typeof(Key), "D" + c);
                    keyDisplay = c.ToString();
                    return true;
                }
            }

            if (upper.StartsWith("F") && int.TryParse(upper.Substring(1), out int f) && f >= 1 && f <= 24)
            {
                key = (Key)((int)Key.F1 + (f - 1));
                keyDisplay = "F" + f;
                return true;
            }

            switch (upper)
            {
                case "SPACE":
                case "SPACEBAR":
                    key = Key.Space;
                    keyDisplay = "Space";
                    return true;
                case "ENTER":
                case "RETURN":
                    key = Key.Enter;
                    keyDisplay = "Enter";
                    return true;
                case "TAB":
                    key = Key.Tab;
                    keyDisplay = "Tab";
                    return true;
                case "ESC":
                case "ESCAPE":
                    key = Key.Escape;
                    keyDisplay = "Esc";
                    return true;
                case "BACK":
                case "BACKSPACE":
                    key = Key.Back;
                    keyDisplay = "Backspace";
                    return true;
                case "DEL":
                case "DELETE":
                    key = Key.Delete;
                    keyDisplay = "Delete";
                    return true;
                case "INS":
                case "INSERT":
                    key = Key.Insert;
                    keyDisplay = "Insert";
                    return true;
                case "HOME":
                    key = Key.Home;
                    keyDisplay = "Home";
                    return true;
                case "END":
                    key = Key.End;
                    keyDisplay = "End";
                    return true;
                case "PGUP":
                case "PAGEUP":
                    key = Key.PageUp;
                    keyDisplay = "PageUp";
                    return true;
                case "PGDN":
                case "PAGEDOWN":
                    key = Key.PageDown;
                    keyDisplay = "PageDown";
                    return true;
                case "UP":
                    key = Key.Up;
                    keyDisplay = "Up";
                    return true;
                case "DOWN":
                    key = Key.Down;
                    keyDisplay = "Down";
                    return true;
                case "LEFT":
                    key = Key.Left;
                    keyDisplay = "Left";
                    return true;
                case "RIGHT":
                    key = Key.Right;
                    keyDisplay = "Right";
                    return true;
            }

            return false;
        }

        private string BuildHotkeyText(uint modifiers, string keyDisplay)
        {
            string text = "";
            if ((modifiers & HotKeyManager.MOD_CONTROL) != 0) text += "Ctrl+";
            if ((modifiers & HotKeyManager.MOD_SHIFT) != 0) text += "Shift+";
            if ((modifiers & HotKeyManager.MOD_ALT) != 0) text += "Alt+";
            if ((modifiers & HotKeyManager.MOD_WIN) != 0) text += "Win+";
            return text + keyDisplay;
        }
    }
}
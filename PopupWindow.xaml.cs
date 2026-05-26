using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace ReWrite
{
    public partial class PopupWindow : Window
    {
        private Microsoft.Web.WebView2.Wpf.WebView2 webView;

        private readonly MainWindow _parent;
        private IntPtr _targetHwnd = IntPtr.Zero;
        private bool _isInitialized = false;
        private string _pendingText = "";
        private bool _pendingSettings = false;

        public PopupWindow(MainWindow parent)
        {
            InitializeComponent();
            Icon = MainWindow.LoadWindowIcon();
            webView = (Microsoft.Web.WebView2.Wpf.WebView2)FindName("webViewControl");
            _parent = parent;

            if (LoadingOverlay != null)
            {
                LoadingOverlay.Visibility = Visibility.Visible;
            }
            webView.Visibility = Visibility.Hidden;

            this.LocationChanged += (s, e) => ClampToScreen();

            // Register Escape key to hide the window
            this.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Escape)
                {
                    HidePopup();
                }
            };

            // Set up webview initialization
            InitializeWebViewAsync();
        }

        private async void InitializeWebViewAsync()
        {
            try
            {
                // Set custom user data folder in AppData Local to ensure write permission and isolate profile
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string userDir = Path.Combine(localAppData, "ReWrite");
                
                var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, userDir);
                await webView.EnsureCoreWebView2Async(env);
                // webView.CoreWebView2.OpenDevToolsWindow();
                webView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(15, 17, 21); // Match #0F1115 dark theme

                EmbeddedUiContent.ConfigureWebView(webView.CoreWebView2);

                if (LoadingOverlay != null)
                {
                    LoadingOverlay.Visibility = Visibility.Visible;
                }

                webView.Source = new Uri("https://rewrite.local/index.html");
                webView.WebMessageReceived += WebView_WebMessageReceived;

                _isInitialized = true;

                // If there was text waiting to be shown while webview was loading, push it now
                if (!string.IsNullOrEmpty(_pendingText) || _pendingSettings)
                {
                    PushShowMessage(_pendingText, _pendingSettings);
                    _pendingText = "";
                    _pendingSettings = false;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to initialize WebView2: {ex.Message}\nMake sure Edge WebView2 Runtime is installed.", "ReWrite Initialization Error");
            }
        }

        private void HideLoadingOverlay()
        {
            if (LoadingOverlay != null)
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
            webView.Visibility = Visibility.Visible;
        }

        public void PrepareShow(string selectedText, IntPtr targetHwnd, bool openSettingsDirectly = false)
        {
            _targetHwnd = targetHwnd;

            if (_isInitialized)
            {
                PushShowMessage(selectedText, openSettingsDirectly);
            }
            else
            {
                _pendingText = selectedText;
                _pendingSettings = openSettingsDirectly;
            }
        }

        private void PushShowMessage(string selectedText, bool openSettingsDirectly)
        {
            try
            {
                var payload = new
                {
                    @event = "show",
                    text = selectedText,
                    settingsDirectly = openSettingsDirectly,
                    settingsOnly = false
                };
                string json = JsonSerializer.Serialize(payload);
                webView.CoreWebView2.PostWebMessageAsJson(json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error pushing show message: {ex.Message}");
            }
        }

        private void WebView_WebMessageReceived(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string json = e.WebMessageAsJson;
                using JsonDocument doc = JsonDocument.Parse(json);
                string action = doc.RootElement.GetProperty("action").GetString() ?? "";

                if (action == "ui_ready")
                {
                    HideLoadingOverlay();
                    return;
                }
                if (action == "paste")
                {
                    string text = doc.RootElement.GetProperty("text").GetString() ?? "";
                    PasteAndHide(text);
                }
                else if (action == "start_drag")
                {
                    try
                    {
                        var wih = new System.Windows.Interop.WindowInteropHelper(this);
                        ReleaseCapture();
                        SendMessage(wih.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
                        ClampToScreen();
                    }
                    catch { }
                }
                else if (action == "close")
                {
                    HidePopup();
                }
                else if (action == "get_startup")
                {
                    SendStartupStatus();
                }
                else if (action == "get_hotkey")
                {
                    SendHotkeyStatus();
                }
                else if (action == "set_hotkey")
                {
                    string hotkey = doc.RootElement.GetProperty("hotkey").GetString() ?? "";
                    if (!_parent.TryUpdateHotkey(hotkey, out string error))
                    {
                        SendHotkeyError(error);
                    }
                    SendHotkeyStatus();
                }
                else if (action == "set_startup")
                {
                    bool enabled = doc.RootElement.GetProperty("enabled").GetBoolean();
                    if (enabled) StartupManager.EnableAutostart();
                    else StartupManager.DisableAutostart();
                    SendStartupStatus();
                }
                else if (action == "resize_popup")
                {
                    double height = doc.RootElement.GetProperty("height").GetDouble();
                    ResizePopup(height);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing web message: {ex.Message}");
            }
        }

        private void SendStartupStatus()
        {
            if (!_isInitialized) return;
            try
            {
                var payload = new
                {
                    @event = "startup_status",
                    enabled = StartupManager.IsAutostartEnabled()
                };
                string json = JsonSerializer.Serialize(payload);
                webView.CoreWebView2.PostWebMessageAsJson(json);
            }
            catch { }
        }

        private void SendHotkeyStatus()
        {
            if (!_isInitialized) return;
            try
            {
                var payload = new
                {
                    @event = "hotkey_status",
                    hotkey = _parent.GetCurrentHotkeyText()
                };
                string json = JsonSerializer.Serialize(payload);
                webView.CoreWebView2.PostWebMessageAsJson(json);
            }
            catch { }
        }

        private void SendHotkeyError(string message)
        {
            if (!_isInitialized) return;
            try
            {
                var payload = new
                {
                    @event = "hotkey_error",
                    message = message
                };
                string json = JsonSerializer.Serialize(payload);
                webView.CoreWebView2.PostWebMessageAsJson(json);
            }
            catch { }
        }

        private async void PasteAndHide(string text)
        {
            // Hide the popup immediately for snappy UX
            this.Hide();

            if (_targetHwnd != IntPtr.Zero)
            {
                // Bring the target text editor back into focus and paste the text
                await Task.Run(async () =>
                {
                    // Focus target editor
                    SetForegroundWindow(_targetHwnd);
                    await Task.Delay(80); // Wait for focus to settle

                    // Save current clipboard contents
                    string? originalClipboard = null;
                    bool hadText = false;
                    
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            if (System.Windows.Clipboard.ContainsText())
                            {
                                originalClipboard = System.Windows.Clipboard.GetText();
                                hadText = true;
                            }
                        }
                        catch { }
                    });

                    // Set clipboard and paste
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            System.Windows.Clipboard.SetText(text);
                        }
                        catch { }
                    });

                    await KeyboardSimulator.SimulatePasteAsync();
                    await Task.Delay(150); // Wait for paste action to complete

                    // Restore user's previous clipboard content
                    if (hadText && originalClipboard != null)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            try
                            {
                                System.Windows.Clipboard.SetText(originalClipboard);
                            }
                            catch { }
                        });
                    }
                });
            }
        }

        private void HidePopup()
        {
            this.Hide();
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            // Auto-hide when user clicks away
            HidePopup();
        }

        private void ClampToScreen()
        {
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;

            double width = this.ActualWidth > 0 ? this.ActualWidth : this.Width;
            double height = this.ActualHeight > 0 ? this.ActualHeight : this.Height;

            double left = this.Left;
            double top = this.Top;

            if (left < 10) left = 10;
            if (top < 10) top = 10;
            if (left + width > screenWidth - 10) left = screenWidth - width - 10;
            if (top + height > screenHeight - 10) top = screenHeight - height - 10;

            this.Left = left;
            this.Top = top;
        }

        private void ResizePopup(double height)
        {
            if (height < 240)
            {
                height = 240;
            }

            if (Math.Abs(Height - height) < 1)
            {
                return;
            }

            Height = height;
            ClampToScreen();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            try
            {
                var wih = new System.Windows.Interop.WindowInteropHelper(this);
                int attribute = DWMWA_WINDOW_CORNER_PREFERENCE;
                int preference = DWMWCP_ROUND; // 2 = Round (standard)
                DwmSetWindowAttribute(wih.Handle, attribute, ref preference, sizeof(int));
            }
            catch { }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int pvAttribute, int cbAttribute);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;
    }
}

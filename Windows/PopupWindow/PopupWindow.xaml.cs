using System;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace ReWrite
{
    /// <summary>
    /// Floating AI-rewrite popup. Hosts WebView2 and routes messages between
    /// the front-end and the host application.
    /// WebView2 initialisation lives in WebViewHost; all DllImports in NativeMethods.
    /// </summary>
    public partial class PopupWindow : Window
    {
        private Microsoft.Web.WebView2.Wpf.WebView2 webView;

        private readonly MainWindow _parent;
        private IntPtr _targetHwnd = IntPtr.Zero;
        private bool _uiReady = false;       // Front-end has sent ui_ready
        private string _pendingText = "";
        private bool _pendingSettings = false;

        public PopupWindow(MainWindow parent)
        {
            InitializeComponent();
            Icon = MainWindow.LoadWindowIcon();
            webView = (Microsoft.Web.WebView2.Wpf.WebView2)FindName("webViewControl");
            _parent = parent;

            if (LoadingOverlay != null)
                LoadingOverlay.Visibility = Visibility.Visible;
            webView.Visibility = Visibility.Hidden;

            this.LocationChanged += (s, e) => ClampToScreen();

            // Escape hides the popup
            this.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Escape)
                    HidePopup();
            };

            InitializeWebViewAsync();
        }

        // ── WebView2 initialisation ───────────────────────────────────────────────

        private async void InitializeWebViewAsync()
        {
            try
            {
                var env = await WebViewHost.CreateEnvironmentAsync();
                await webView.EnsureCoreWebView2Async(env);
                // webView.CoreWebView2.OpenDevToolsWindow();
                webView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(15, 17, 21); // #0F1115

                EmbeddedUiContent.ConfigureWebView(webView.CoreWebView2);

                if (LoadingOverlay != null)
                    LoadingOverlay.Visibility = Visibility.Visible;

                webView.WebMessageReceived += WebView_WebMessageReceived;
                webView.Source = new Uri("https://rewrite.local/popup.html");
                // Pending text will be delivered after ui_ready arrives from the front-end
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to initialize WebView2: {ex.Message}\nMake sure Edge WebView2 Runtime is installed.",
                    "ReWrite Initialization Error");
            }
        }

        private void HideLoadingOverlay()
        {
            if (LoadingOverlay != null)
                LoadingOverlay.Visibility = Visibility.Collapsed;
            webView.Visibility = Visibility.Visible;
        }

        // ── Show / hide ───────────────────────────────────────────────────────────

        public void PrepareShow(string selectedText, IntPtr targetHwnd, bool openSettingsDirectly = false)
        {
            _targetHwnd = targetHwnd;

            if (_uiReady)
            {
                // UI is fully ready — send immediately
                PushShowMessage(selectedText, openSettingsDirectly);
            }
            else
            {
                // Store and send once ui_ready arrives
                _pendingText     = selectedText;
                _pendingSettings = openSettingsDirectly;
            }
        }

        private void HidePopup() => this.Hide();

        private void Window_Deactivated(object sender, EventArgs e) => HidePopup();

        // ── Web message routing ───────────────────────────────────────────────────

        private void WebView_WebMessageReceived(
            object? sender,
            Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string json = e.WebMessageAsJson;
                using JsonDocument doc = JsonDocument.Parse(json);
                string action = doc.RootElement.GetProperty("action").GetString() ?? "";

                if (action == "ui_ready")
                {
                    _uiReady = true;
                    HideLoadingOverlay();

                    // Flush any text / settings queued before the UI was ready
                    if (!string.IsNullOrEmpty(_pendingText) || _pendingSettings)
                    {
                        PushShowMessage(_pendingText, _pendingSettings);
                        _pendingText     = "";
                        _pendingSettings = false;
                    }
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
                        NativeMethods.ReleaseCapture();
                        NativeMethods.SendMessage(wih.Handle, NativeMethods.WM_NCLBUTTONDOWN, NativeMethods.HTCAPTION, 0);
                        ClampToScreen();
                    }
                    catch { }
                }
                else if (action == "close")        { HidePopup(); }
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

        // ── Messages to front-end ─────────────────────────────────────────────────

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
                webView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(payload));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error pushing show message: {ex.Message}");
            }
        }

        // ── Paste & hide ──────────────────────────────────────────────────────────

        private async void PasteAndHide(string text)
        {
            this.Hide(); // Hide immediately for snappy UX

            if (_targetHwnd != IntPtr.Zero)
            {
                await Task.Run(async () =>
                {
                    NativeMethods.SetForegroundWindow(_targetHwnd);
                    await Task.Delay(80); // Wait for focus to settle

                    // Save current clipboard
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

                    // Set new clipboard and paste
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        try { System.Windows.Clipboard.SetText(text); }
                        catch { }
                    });

                    await KeyboardSimulator.SimulatePasteAsync();
                    await Task.Delay(150); // Wait for paste action to complete

                    // Restore user's original clipboard
                    if (hadText && originalClipboard != null)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            try { System.Windows.Clipboard.SetText(originalClipboard); }
                            catch { }
                        });
                    }
                });
            }
        }

        // ── Layout helpers ────────────────────────────────────────────────────────

        private void ClampToScreen()
        {
            double screenWidth  = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;

            double width  = this.ActualWidth  > 0 ? this.ActualWidth  : this.Width;
            double height = this.ActualHeight > 0 ? this.ActualHeight : this.Height;

            double left = this.Left;
            double top  = this.Top;

            if (left < 10)                       left = 10;
            if (top  < 10)                       top  = 10;
            if (left + width  > screenWidth  - 10) left = screenWidth  - width  - 10;
            if (top  + height > screenHeight - 10) top  = screenHeight - height - 10;

            this.Left = left;
            this.Top  = top;
        }

        private void ResizePopup(double height)
        {
            if (height < 240) height = 240;
            if (Math.Abs(Height - height) < 1) return;
            Height = height;
            ClampToScreen();
        }

        // ── Window chrome (rounded corners) ───────────────────────────────────────

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            try
            {
                var wih = new System.Windows.Interop.WindowInteropHelper(this);
                int preference = NativeMethods.DWMWCP_ROUND;
                NativeMethods.DwmSetWindowAttribute(
                    wih.Handle,
                    NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE,
                    ref preference,
                    sizeof(int));
            }
            catch { }
        }
    }
}

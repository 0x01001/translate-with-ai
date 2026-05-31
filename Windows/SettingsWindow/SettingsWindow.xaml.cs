using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;

namespace ReWrite
{
    /// <summary>
    /// Standalone settings window (WebView2-hosted). Handles settings-only
    /// web messages. WebView2 initialisation delegated to WebViewHost.
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private readonly MainWindow _parent;
        private bool _isInitialized = false;
        private bool _pendingSettings = false;

        public SettingsWindow(MainWindow parent)
        {
            InitializeComponent();
            Icon = MainWindow.LoadWindowIcon();
            _parent = parent;

            // Localize UI
            UpdateLocalization();
            Localization.LocaleChanged += OnLocaleChanged;

            if (LoadingOverlay != null)
                LoadingOverlay.Visibility = Visibility.Visible;
            webView.Visibility = Visibility.Hidden;

            this.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Escape)
                    this.Close();
            };

            InitializeWebViewAsync();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            try
            {
                var wih = new WindowInteropHelper(this);

                int roundCorners = NativeMethods.DWMWCP_ROUND;
                NativeMethods.DwmSetWindowAttribute(
                    wih.Handle,
                    NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE,
                    ref roundCorners,
                    sizeof(int));

                int darkMode = 1;
                NativeMethods.DwmSetWindowAttribute(
                    wih.Handle,
                    NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE,
                    ref darkMode,
                    sizeof(int));
            }
            catch { }
        }

        private void OnLocaleChanged(string locale)
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateLocalization();
                SendLocaleToWebView(locale);
            }));
        }

        private void UpdateLocalization()
        {
            try { this.Title = Localization.Get("title.settings"); } catch { }
            try { LoadingText.Text = Localization.Get("loading"); } catch { }
            try { LoadingSubText.Text = Localization.Get("loading.sub"); } catch { }
        }

        // ── WebView2 initialisation ───────────────────────────────────────────────

        private async void InitializeWebViewAsync()
        {
            try
            {
                var env = await WebViewHost.CreateEnvironmentAsync();
                await webView.EnsureCoreWebView2Async(env);
                webView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(15, 17, 21); // #0F1115

                EmbeddedUiContent.ConfigureWebView(webView.CoreWebView2);
                webView.WebMessageReceived += WebView_WebMessageReceived;

                if (LoadingOverlay != null)
                    LoadingOverlay.Visibility = Visibility.Visible;

                webView.Source = new Uri("https://rewrite.local/settings.html");
                try
                {
                    var localePayload = new { @event = "set_locale", locale = Localization.CurrentLocale };
                    webView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(localePayload));
                }
                catch { }

                _isInitialized = true;

                if (_pendingSettings)
                {
                    PushShowMessage();
                    _pendingSettings = false;
                }
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

        // ── Show ──────────────────────────────────────────────────────────────────

        public void ShowSettings()
        {
            if (_isInitialized) PushShowMessage();
            else                _pendingSettings = true;

            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
            this.Focus();
        }

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
                    HideLoadingOverlay();
                    SendLocaleToWebView(Localization.CurrentLocale);
                    return;
                }
                if (action == "get_startup")       { SendStartupStatus(); }
                else if (action == "get_hotkey")   { SendHotkeyStatus(); }
                else if (action == "set_hotkey")
                {
                    string hotkey = doc.RootElement.GetProperty("hotkey").GetString() ?? "";
                    if (!_parent.TryUpdateHotkey(hotkey, out string error))
                        SendHotkeyError(error);
                    SendHotkeyStatus();
                }
                else if (action == "set_locale")
                {
                    string locale = doc.RootElement.GetProperty("locale").GetString() ?? "";
                    if (!string.IsNullOrEmpty(locale))
                    {
                        try
                        {
                            // Persist setting
                            Directory.CreateDirectory(AppPaths.SettingsDirectory);
                            var settingsPath = Path.Combine(AppPaths.SettingsDirectory, "appsettings.json");
                            File.WriteAllText(settingsPath, JsonSerializer.Serialize(new { locale }));

                            // Apply immediately
                            Localization.SetLocale(locale);
                        }
                        catch { }
                    }
                    return;
                }
                else if (action == "set_startup")
                {
                    bool enabled = doc.RootElement.GetProperty("enabled").GetBoolean();
                    if (enabled) StartupManager.EnableAutostart();
                    else         StartupManager.DisableAutostart();
                    SendStartupStatus();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing web message: {ex.Message}");
            }
        }

        // ── Messages to front-end ─────────────────────────────────────────────────

        private void PushShowMessage()
        {
            try
            {
                var payload = new { @event = "show", text = "", settingsDirectly = true, settingsOnly = true };
                webView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(payload));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error pushing show message: {ex.Message}");
            }
        }

        public void SendLocaleToWebView(string locale)
        {
            try
            {
                if (webView?.CoreWebView2 != null)
                {
                    var payload = new { @event = "set_locale", locale };
                    webView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(payload));
                }
            }
            catch { }
        }

        protected override void OnClosed(EventArgs e)
        {
            Localization.LocaleChanged -= OnLocaleChanged;
            base.OnClosed(e);
        }

        private void SendStartupStatus()
        {
            if (!_isInitialized) return;
            try
            {
                var payload = new { @event = "startup_status", enabled = StartupManager.IsAutostartEnabled() };
                webView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(payload));
            }
            catch { }
        }

        private void SendHotkeyStatus()
        {
            if (!_isInitialized) return;
            try
            {
                var payload = new { @event = "hotkey_status", hotkey = _parent.GetCurrentHotkeyText() };
                webView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(payload));
            }
            catch { }
        }

        private void SendHotkeyError(string message)
        {
            if (!_isInitialized) return;
            try
            {
                var payload = new { @event = "hotkey_error", message };
                webView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(payload));
            }
            catch { }
        }
    }
}

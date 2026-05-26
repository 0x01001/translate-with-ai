using System;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace ReWrite
{
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

            if (LoadingOverlay != null)
            {
                LoadingOverlay.Visibility = Visibility.Visible;
            }
            webView.Visibility = Visibility.Hidden;

            this.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Escape)
                {
                    this.Close();
                }
            };

            InitializeWebViewAsync();
        }

        private async void InitializeWebViewAsync()
        {
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string userDir = Path.Combine(localAppData, "ReWrite");

                var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, userDir);
                await webView.EnsureCoreWebView2Async(env);
                webView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(15, 17, 21);

                string contentFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ui");
                if (!Directory.Exists(contentFolder))
                {
                    Directory.CreateDirectory(contentFolder);
                }

                webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "rewrite.local",
                    contentFolder,
                    Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow
                );

                if (LoadingOverlay != null)
                {
                    LoadingOverlay.Visibility = Visibility.Visible;
                }

                webView.Source = new Uri("https://rewrite.local/index.html?mode=settings");
                webView.WebMessageReceived += WebView_WebMessageReceived;

                _isInitialized = true;

                if (_pendingSettings)
                {
                    PushShowMessage();
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

        public void ShowSettings()
        {
            if (_isInitialized)
            {
                PushShowMessage();
            }
            else
            {
                _pendingSettings = true;
            }

            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
            this.Focus();
        }

        private void PushShowMessage()
        {
            try
            {
                var payload = new
                {
                    @event = "show",
                    text = "",
                    settingsDirectly = true,
                    settingsOnly = true
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
                if (action == "get_startup")
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
    }
}

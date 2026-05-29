using System;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace ReWrite
{
    /// <summary>
    /// Tutorial video window (WebView2-hosted).
    /// WebView2 initialisation delegated to WebViewHost.
    /// </summary>
    public partial class TutorialWindow : Window
    {
        public TutorialWindow()
        {
            InitializeComponent();
            Icon = MainWindow.LoadWindowIcon();

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

            this.Closing += TutorialWindow_Closing;

            InitializeWebViewAsync();
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
            try { this.Title = Localization.Get("title.tutorial"); } catch { }
            try { LoadingText.Text = Localization.Get("loading"); } catch { }
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

                webView.Source = new Uri("https://rewrite.local/tutorial.html");
                try
                {
                    var localePayload = new { @event = "set_locale", locale = Localization.CurrentLocale };
                    webView.CoreWebView2.PostWebMessageAsJson(System.Text.Json.JsonSerializer.Serialize(localePayload));
                }
                catch { }
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

        public void SendLocaleToWebView(string locale)
        {
            try
            {
                if (webView?.CoreWebView2 != null)
                {
                    var payload = new { @event = "set_locale", locale };
                    webView.CoreWebView2.PostWebMessageAsJson(System.Text.Json.JsonSerializer.Serialize(payload));
                }
            }
            catch { }
        }

        protected override void OnClosed(EventArgs e)
        {
            Localization.LocaleChanged -= OnLocaleChanged;
            base.OnClosed(e);
        }

        // ── Cleanup on close ──────────────────────────────────────────────────────

        private async void TutorialWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                if (webView?.CoreWebView2 is CoreWebView2 core)
                {
                    await core.ExecuteScriptAsync(@"
                        document.querySelectorAll('video, audio').forEach(m => {
                            m.pause();
                            m.src = '';
                            m.load();
                        });
                    ");
                    core.Stop();
                }
                webView?.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebView cleanup error: {ex.Message}");
            }
        }

        // ── Web message routing ───────────────────────────────────────────────────

        private void WebView_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string json = e.WebMessageAsJson;
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                string action = doc.RootElement.GetProperty("action").GetString() ?? "";

                if (action == "ui_ready")
                {
                    HideLoadingOverlay();
                    SendLocaleToWebView(Localization.CurrentLocale);
                    return;
                }
                if (action == "close")    { this.Close(); }
                if (action == "request_locale")
                {
                    try
                    {
                        var payload = new { @event = "set_locale", locale = Localization.CurrentLocale };
                        webView.CoreWebView2.PostWebMessageAsJson(System.Text.Json.JsonSerializer.Serialize(payload));
                    }
                    catch { }
                    return;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing web message: {ex.Message}");
            }
        }
    }
}
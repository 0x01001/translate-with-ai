using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
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
        private static readonly HttpClient AiHttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };

        private readonly MainWindow _parent;
        private IntPtr _targetHwnd = IntPtr.Zero;
        private bool _uiReady = false;       // Front-end has sent ui_ready
        private bool _hasPendingShow = false;
        private string _pendingText = "";
        private bool _pendingSettings = false;
        private string _lastShowText = "";
        private bool _lastShowSettings = false;
        private QuickTranslateStatusWindow? _quickStatusWindow;
        private bool _hasPendingQuickTranslate = false;
        private string _pendingQuickTranslateText = "";
        private IntPtr _pendingQuickTranslateTargetHwnd = IntPtr.Zero;

        public PopupWindow(MainWindow parent)
        {
            InitializeComponent();
            Icon = MainWindow.LoadWindowIcon();
            webView = (Microsoft.Web.WebView2.Wpf.WebView2)FindName("webViewControl");
            _parent = parent;

            // Keep host chrome in sync with the current locale.
            UpdateLocalization();
            Localization.LocaleChanged += OnLocaleChanged;

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
            try { this.Title = Localization.Get("title.popup"); } catch { }
            try { LoadingText.Text = Localization.Get("loading"); } catch { }
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
                webView.WebMessageReceived += WebView_WebMessageReceived;

                if (LoadingOverlay != null)
                    LoadingOverlay.Visibility = Visibility.Visible;

                webView.Source = new Uri("https://rewrite.local/popup.html");
                // Send current locale to frontend so it can load translations
                try
                {
                    var localePayload = new { @event = "set_locale", locale = Localization.CurrentLocale };
                    webView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(localePayload));
                }
                catch { }
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
            _pendingText = selectedText;
            _pendingSettings = openSettingsDirectly;
            _hasPendingShow = true;

            if (_uiReady)
            {
                // UI is fully ready — send immediately
                PushShowMessage(selectedText, openSettingsDirectly);
                _hasPendingShow = false;
                _pendingText = "";
                _pendingSettings = false;
                _ = ResendLastShowMessageAsync();
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
                    SendLocaleToWebView(Localization.CurrentLocale);

                    // Flush any text / settings queued before the UI was ready
                    if (_hasPendingShow)
                    {
                        PushShowMessage(_pendingText, _pendingSettings);
                        _hasPendingShow = false;
                        _pendingText     = "";
                        _pendingSettings = false;
                    }

                    if (_hasPendingQuickTranslate)
                    {
                        _targetHwnd = _pendingQuickTranslateTargetHwnd;
                        PushQuickTranslateMessage(_pendingQuickTranslateText);
                        _hasPendingQuickTranslate = false;
                        _pendingQuickTranslateText = "";
                        _pendingQuickTranslateTargetHwnd = IntPtr.Zero;
                    }

                    _ = ResendLastShowMessageAsync();
                    return;
                }

                if (action == "request_locale")
                {
                    try
                    {
                        var payload = new { @event = "set_locale", locale = Localization.CurrentLocale };
                        webView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(payload));
                    }
                    catch { }
                    return;
                }

                if (action == "paste")
                {
                    string text = doc.RootElement.GetProperty("text").GetString() ?? "";
                    PasteAndHide(text);
                }
                else if (action == "quick_translate_result")
                {
                    string text = doc.RootElement.GetProperty("text").GetString() ?? "";
                    _ = CompleteQuickTranslateAsync(text);
                }
                else if (action == "quick_translate_error")
                {
                    string message = doc.RootElement.TryGetProperty("message", out var prop) ? prop.GetString() ?? "" : "";
                    ShowQuickTranslateError(string.IsNullOrWhiteSpace(message) ? Localization.Get("quick_translate.error.failed") : message);
                }
                else if (action == "quick_translate_show_popup")
                {
                    HideQuickTranslateStatus();
                    ShowPreparedQuickTranslatePopup();
                }
                else if (action == "ai_proxy_request")
                {
                    var request = AiProxyRequest.FromJson(doc.RootElement);
                    _ = RunAiProxyRequestAsync(request);
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


        // ── Native AI proxy ───────────────────────────────────────────────────────

        private sealed class AiProxyRequest
        {
            public string RequestId { get; init; } = "";
            public string Provider { get; init; } = "";
            public string ApiKey { get; init; } = "";
            public string Model { get; init; } = "";
            public string BaseUrl { get; init; } = "";
            public string Prompt { get; init; } = "";
            public Dictionary<string, string> ExtraHeaders { get; init; } = new();

            public static AiProxyRequest FromJson(JsonElement root)
            {
                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (root.TryGetProperty("extraHeaders", out var extraHeaders) && extraHeaders.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in extraHeaders.EnumerateObject())
                    {
                        if (property.Value.ValueKind == JsonValueKind.String)
                        {
                            headers[property.Name] = property.Value.GetString() ?? "";
                        }
                    }
                }

                return new AiProxyRequest
                {
                    RequestId = root.TryGetProperty("requestId", out var requestId) ? requestId.GetString() ?? "" : "",
                    Provider = root.TryGetProperty("provider", out var provider) ? provider.GetString() ?? "" : "",
                    ApiKey = root.TryGetProperty("apiKey", out var apiKey) ? apiKey.GetString() ?? "" : "",
                    Model = root.TryGetProperty("model", out var model) ? model.GetString() ?? "" : "",
                    BaseUrl = root.TryGetProperty("baseUrl", out var baseUrl) ? baseUrl.GetString() ?? "" : "",
                    Prompt = root.TryGetProperty("prompt", out var prompt) ? prompt.GetString() ?? "" : "",
                    ExtraHeaders = headers
                };
            }
        }

        private async Task RunAiProxyRequestAsync(AiProxyRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.RequestId))
                return;

            try
            {
                PostWebEvent(new { @event = "ai_proxy_started", requestId = request.RequestId });

                if (string.IsNullOrWhiteSpace(request.ApiKey))
                    throw new InvalidOperationException(Localization.Get("quick_translate.error.failed") + " Missing API key.");
                if (string.IsNullOrWhiteSpace(request.Model))
                    throw new InvalidOperationException("Missing model name.");
                if (string.IsNullOrWhiteSpace(request.Prompt))
                    throw new InvalidOperationException("Missing prompt.");

                if (string.Equals(request.Provider, "gemini", StringComparison.OrdinalIgnoreCase))
                {
                    await RunGeminiProxyRequestAsync(request);
                    return;
                }

                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildChatCompletionsUrl(request.BaseUrl));
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.ApiKey);
                foreach (var header in request.ExtraHeaders)
                {
                    if (!string.IsNullOrWhiteSpace(header.Key) && !string.IsNullOrWhiteSpace(header.Value))
                    {
                        httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }

                var body = new
                {
                    model = request.Model,
                    messages = new[]
                    {
                        new { role = "system", content = "You are a professional writing assistant. You MUST always write in the same language as the user's input text. Never translate or switch to another language unless explicitly asked to translate." },
                        new { role = "user", content = request.Prompt }
                    },
                    stream = true,
                    temperature = 0.3
                };

                httpRequest.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

                using var response = await AiHttpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode)
                {
                    var errorText = await response.Content.ReadAsStringAsync();
                    throw new InvalidOperationException(ExtractOpenAiErrorMessage(errorText, $"HTTP {(int)response.StatusCode}"));
                }

                await using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(stream, Encoding.UTF8);
                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (line == null) break;
                    var trimmed = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed) || trimmed == "data: [DONE]") continue;
                    if (!trimmed.StartsWith("data: ", StringComparison.Ordinal)) continue;

                    var chunk = ExtractOpenAiChunkText(trimmed.Substring(6));
                    if (!string.IsNullOrEmpty(chunk))
                    {
                        PostWebEvent(new { @event = "ai_proxy_chunk", requestId = request.RequestId, text = chunk });
                    }
                }

                PostWebEvent(new { @event = "ai_proxy_done", requestId = request.RequestId });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AI proxy request failed: {ex.Message}");
                PostWebEvent(new { @event = "ai_proxy_error", requestId = request.RequestId, message = ex.Message });
            }
        }


        private async Task RunGeminiProxyRequestAsync(AiProxyRequest request)
        {
            var model = request.Model.Trim();
            if (model.StartsWith("models/", StringComparison.OrdinalIgnoreCase))
                model = model.Substring("models/".Length);

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent?key={Uri.EscapeDataString(request.ApiKey)}";
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);

            var body = new
            {
                system_instruction = new
                {
                    parts = new[]
                    {
                        new { text = "You are a professional writing assistant. You MUST always write in the same language as the user's input text. Never translate or switch to another language unless explicitly asked to translate." }
                    }
                },
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = request.Prompt }
                        }
                    }
                },
                generationConfig = new { temperature = 0.3 }
            };

            httpRequest.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            using var response = await AiHttpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseContentRead);
            var responseText = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(ExtractOpenAiErrorMessage(responseText, $"HTTP {(int)response.StatusCode}"));

            var text = ExtractGeminiText(responseText);
            if (!string.IsNullOrEmpty(text))
                PostWebEvent(new { @event = "ai_proxy_chunk", requestId = request.RequestId, text });
            PostWebEvent(new { @event = "ai_proxy_done", requestId = request.RequestId });
        }

        private static string ExtractGeminiText(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array)
                    return "";

                var builder = new StringBuilder();
                foreach (var candidate in candidates.EnumerateArray())
                {
                    if (!candidate.TryGetProperty("content", out var content)) continue;
                    if (!content.TryGetProperty("parts", out var parts) || parts.ValueKind != JsonValueKind.Array) continue;
                    foreach (var part in parts.EnumerateArray())
                    {
                        if (part.TryGetProperty("text", out var text)) builder.Append(text.GetString() ?? "");
                    }
                }
                return builder.ToString();
            }
            catch { return ""; }
        }

        private void PostWebEvent(object payload)
        {
            try
            {
                var json = JsonSerializer.Serialize(payload);
                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try { webView.CoreWebView2.PostWebMessageAsJson(json); } catch { }
                }));
            }
            catch { }
        }

        private static string BuildChatCompletionsUrl(string baseUrl)
        {
            var clean = (baseUrl ?? "").Trim().TrimEnd('/');
            if (string.IsNullOrWhiteSpace(clean)) clean = "http://localhost:20128/v1";
            if (clean.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)) return clean;
            return clean + "/chat/completions";
        }

        private static string ExtractOpenAiErrorMessage(string json, string fallback)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("error", out var error))
                {
                    if (error.ValueKind == JsonValueKind.Object && error.TryGetProperty("message", out var message))
                        return message.GetString() ?? fallback;
                    if (error.ValueKind == JsonValueKind.String)
                        return error.GetString() ?? fallback;
                }
            }
            catch { }
            return string.IsNullOrWhiteSpace(json) ? fallback : json;
        }

        private static string ExtractOpenAiChunkText(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
                    return "";
                var choice = choices[0];
                if (!choice.TryGetProperty("delta", out var delta))
                    return "";
                if (delta.TryGetProperty("content", out var content))
                    return content.GetString() ?? "";
            }
            catch { }
            return "";
        }

        // ── Messages to front-end ─────────────────────────────────────────────────

        public void BeginQuickTranslate(string selectedText, IntPtr targetHwnd)
        {
            _targetHwnd = targetHwnd;
            PositionNearCursor();

            string text = (selectedText ?? "").Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                ShowQuickTranslateError(Localization.Get("quick_translate.error.no_selection"));
                return;
            }

            ShowQuickTranslateStatus(Localization.Get("quick_translate.loading"));

            if (_uiReady)
            {
                PushQuickTranslateMessage(text);
            }
            else
            {
                _hasPendingQuickTranslate = true;
                _pendingQuickTranslateText = text;
                _pendingQuickTranslateTargetHwnd = targetHwnd;
            }
        }

        private void PushShowMessage(string selectedText, bool openSettingsDirectly)
        {
            try
            {
                _lastShowText = selectedText;
                _lastShowSettings = openSettingsDirectly;

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

        private void PushQuickTranslateMessage(string selectedText)
        {
            try
            {
                var payload = new
                {
                    @event = "quick_translate",
                    text = selectedText
                };
                webView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(payload));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error pushing quick translate message: {ex.Message}");
                ShowQuickTranslateError(Localization.Get("quick_translate.error.failed"));
            }
        }

        private async Task ResendLastShowMessageAsync()
        {
            await Task.Delay(120);

            if (!_uiReady || !IsVisible || !webView.IsVisible)
                return;

            if (_lastShowText == "" && !_lastShowSettings)
                return;

            PushShowMessage(_lastShowText, _lastShowSettings);
        }

        // ── Paste & hide ──────────────────────────────────────────────────────────

        private async void PasteAndHide(string text)
        {
            this.Hide(); // Hide immediately for snappy UX
            await PasteTextToTargetAsync(text);
        }

        private async Task CompleteQuickTranslateAsync(string text)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    ShowQuickTranslateError(Localization.Get("quick_translate.error.empty_result"));
                    return;
                }

                await PasteTextToTargetAsync(text);
                HideQuickTranslateStatus();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Quick translate paste failed: {ex.Message}");
                ShowQuickTranslateError(Localization.Get("quick_translate.error.failed"));
            }
        }

        private async Task PasteTextToTargetAsync(string text)
        {
            if (_targetHwnd == IntPtr.Zero) return;

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

        private void ShowPreparedQuickTranslatePopup()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
            Focus();
        }

        private void ShowQuickTranslateStatus(string message)
        {
            _quickStatusWindow ??= new QuickTranslateStatusWindow();
            _quickStatusWindow.ShowNearCursor(message);
        }

        private void HideQuickTranslateStatus()
        {
            try { _quickStatusWindow?.Hide(); } catch { }
        }

        private void ShowQuickTranslateError(string message)
        {
            _quickStatusWindow ??= new QuickTranslateStatusWindow();
            _quickStatusWindow.ShowErrorThenHide(message);
        }

        // ── Layout helpers ────────────────────────────────────────────────────────

        private void PositionNearCursor()
        {
            NativeMethods.GetCursorPos(out NativeMethods.POINT mousePos);

            double screenWidth  = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            double popupWidth   = Width > 0 ? Width : 460;
            double popupHeight  = Height > 0 ? Height : 360;

            double left = mousePos.X - (popupWidth / 2);
            double top  = mousePos.Y + 15;

            if (left < 10) left = 10;
            if (left + popupWidth > screenWidth) left = screenWidth - popupWidth - 10;
            if (top + popupHeight > screenHeight) top = mousePos.Y - popupHeight - 15;
            if (top < 10) top = 10;

            Left = left;
            Top = top;
        }

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

        // Allow host to push locale changes to the webview at runtime
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

        protected override void OnClosed(EventArgs e)
        {
            Localization.LocaleChanged -= OnLocaleChanged;
            try { _quickStatusWindow?.Close(); } catch { }
            base.OnClosed(e);
        }
    }
}

import Cocoa
import WebKit

/// Replicates the WebView2 message bridge so the existing web UI runs unmodified:
/// a user script polyfills `window.chrome.webview` on top of WKWebView's
/// `webkit.messageHandlers`, and native code dispatches events back through
/// `window.chrome.webview.__dispatchMessage(payload)`.
protocol WebViewBridgeDelegate: AnyObject {
    func bridge(_ bridge: WebViewBridge, didReceive action: String, payload: [String: Any])
}

final class WebViewBridge: NSObject, WKScriptMessageHandler {
    weak var delegate: WebViewBridgeDelegate?
    let webView: WKWebView

    private static let shimSource = """
    (function () {
        if (window.chrome && window.chrome.webview) { return; }
        var listeners = [];
        window.chrome = window.chrome || {};
        window.chrome.webview = {
            postMessage: function (message) {
                window.webkit.messageHandlers.bridge.postMessage(JSON.stringify(message));
            },
            addEventListener: function (type, handler) {
                if (type === "message") { listeners.push(handler); }
            },
            removeEventListener: function (type, handler) {
                var index = listeners.indexOf(handler);
                if (index >= 0) { listeners.splice(index, 1); }
            },
            __dispatchMessage: function (data) {
                listeners.forEach(function (handler) {
                    try { handler({ data: data }); } catch (error) {}
                });
            }
        };
    })();
    """

    override init() {
        let configuration = WKWebViewConfiguration()
        configuration.setURLSchemeHandler(WebAssetSchemeHandler(), forURLScheme: WebAssetSchemeHandler.scheme)
        configuration.websiteDataStore = .default()

        let contentController = WKUserContentController()
        // Main frame only — WebView2 does not expose chrome.webview to
        // cross-origin iframes either (e.g. the YouTube embed in tutorial.html
        // must not be able to post bridge messages).
        contentController.addUserScript(WKUserScript(
            source: Self.shimSource,
            injectionTime: .atDocumentStart,
            forMainFrameOnly: true
        ))
        configuration.userContentController = contentController

        webView = WKWebView(frame: .zero, configuration: configuration)
        super.init()

        contentController.add(WeakScriptMessageHandler(self), name: "bridge")
        if #available(macOS 12.0, *) {
            webView.underPageBackgroundColor = NSColor(srgbRed: 15 / 255, green: 17 / 255, blue: 21 / 255, alpha: 1)
        }
    }

    func load(_ page: String) {
        guard let url = URL(string: "\(WebAssetSchemeHandler.origin)/\(page)") else { return }
        webView.load(URLRequest(url: url))
    }

    /// Equivalent of CoreWebView2.PostWebMessageAsJson.
    func postEvent(_ payload: [String: Any]) {
        guard JSONSerialization.isValidJSONObject(payload),
              let data = try? JSONSerialization.data(withJSONObject: payload),
              let json = String(data: data, encoding: .utf8)
        else { return }
        webView.evaluateJavaScript("window.chrome.webview.__dispatchMessage(\(json));", completionHandler: nil)
    }

    func postLocale() {
        postEvent(["event": "set_locale", "locale": Localization.currentLocale])
    }

    func userContentController(_ userContentController: WKUserContentController,
                               didReceive message: WKScriptMessage) {
        guard message.frameInfo.isMainFrame,
              let body = message.body as? String,
              let data = body.data(using: .utf8),
              let object = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
              let action = object["action"] as? String
        else { return }
        delegate?.bridge(self, didReceive: action, payload: object)
    }
}

/// WKUserContentController retains its message handlers; this proxy
/// breaks the resulting bridge ↔ webView retain cycle.
private final class WeakScriptMessageHandler: NSObject, WKScriptMessageHandler {
    private weak var target: WKScriptMessageHandler?

    init(_ target: WKScriptMessageHandler) {
        self.target = target
    }

    func userContentController(_ userContentController: WKUserContentController,
                               didReceive message: WKScriptMessage) {
        target?.userContentController(userContentController, didReceive: message)
    }
}

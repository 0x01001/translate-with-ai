import Cocoa
import WebKit

/// Port of Windows/TutorialWindow/TutorialWindow.xaml.cs.
/// Resizable window hosting tutorial.html (shown on first launch).
final class TutorialWindowController: NSObject, WebViewBridgeDelegate {
    private let bridge = WebViewBridge()
    private let window: NSWindow

    override init() {
        window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 860, height: 520),
            styleMask: [.titled, .closable, .miniaturizable, .resizable],
            backing: .buffered,
            defer: false
        )
        super.init()

        window.title = Localization.get("title.tutorial")
        window.isReleasedWhenClosed = false
        window.backgroundColor = NSColor(srgbRed: 15 / 255, green: 17 / 255, blue: 21 / 255, alpha: 1)
        window.center()

        bridge.delegate = self
        bridge.webView.frame = window.contentView?.bounds ?? .zero
        bridge.webView.autoresizingMask = [.width, .height]
        window.contentView?.addSubview(bridge.webView)
        bridge.load("tutorial.html")

        NotificationCenter.default.addObserver(
            forName: Localization.localeChanged, object: nil, queue: .main
        ) { [weak self] _ in
            self?.window.title = Localization.get("title.tutorial")
            self?.bridge.postLocale()
        }
    }

    func show() {
        window.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)
    }

    func bridge(_ bridge: WebViewBridge, didReceive action: String, payload: [String: Any]) {
        switch action {
        case "ui_ready", "request_locale":
            bridge.postLocale()
        case "close":
            window.orderOut(nil)
        default:
            break
        }
    }
}

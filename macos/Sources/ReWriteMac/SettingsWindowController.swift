import Cocoa
import WebKit

/// Port of Windows/SettingsWindow/SettingsWindow.xaml.cs.
/// Standard titled window hosting settings.html; bridges hotkey,
/// autostart and locale messages to the native layer.
final class SettingsWindowController: NSObject, WebViewBridgeDelegate, NSWindowDelegate {
    private let bridge = WebViewBridge()
    private let window: NSWindow

    /// Supplied by AppDelegate (which owns hotkey registration state).
    var currentHotkeyText: () -> String = { "" }
    var updateHotkey: (String) -> String? = { _ in nil } // returns error message or nil

    override init() {
        window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 760, height: 705),
            styleMask: [.titled, .closable, .miniaturizable],
            backing: .buffered,
            defer: false
        )
        super.init()

        window.title = Localization.get("title.settings")
        window.isReleasedWhenClosed = false
        window.backgroundColor = NSColor(srgbRed: 15 / 255, green: 17 / 255, blue: 21 / 255, alpha: 1)
        window.center()
        window.delegate = self

        bridge.delegate = self
        bridge.webView.frame = window.contentView?.bounds ?? .zero
        bridge.webView.autoresizingMask = [.width, .height]
        window.contentView?.addSubview(bridge.webView)
        bridge.load("settings.html")

        NotificationCenter.default.addObserver(
            forName: Localization.localeChanged, object: nil, queue: .main
        ) { [weak self] _ in
            self?.window.title = Localization.get("title.settings")
            self?.bridge.postLocale()
        }
    }

    func showSettings() {
        bridge.postEvent(["event": "show", "text": "", "settingsDirectly": true, "settingsOnly": true])
        // The window (and its webview) is reused across opens — push fresh
        // native state so toggles made elsewhere (e.g. status-bar autostart)
        // are reflected.
        sendStartupStatus()
        sendHotkeyStatus()
        window.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)
    }

    /// Keep the webview (and its state) alive across closes — just hide.
    func windowShouldClose(_ sender: NSWindow) -> Bool {
        window.orderOut(nil)
        return false
    }

    // ── Bridge messages from settings.js ──────────────────────────────────────

    func bridge(_ bridge: WebViewBridge, didReceive action: String, payload: [String: Any]) {
        switch action {
        case "ui_ready", "request_locale":
            bridge.postLocale()
        case "get_startup":
            sendStartupStatus()
        case "get_hotkey":
            sendHotkeyStatus()
        case "set_hotkey":
            let hotkey = payload["hotkey"] as? String ?? ""
            if let error = updateHotkey(hotkey) {
                bridge.postEvent(["event": "hotkey_error", "message": error])
            }
            sendHotkeyStatus()
        case "set_locale":
            if let locale = payload["locale"] as? String, !locale.isEmpty {
                AppSettingsStore.saveLocale(locale)
                Localization.setLocale(locale)
            }
        case "set_startup":
            StartupManager.setAutostart(payload["enabled"] as? Bool ?? false)
            sendStartupStatus()
        // "start_drag" deliberately ignored (as on Windows): the settings
        // window is titled and natively draggable, and honoring it makes
        // label clicks start a drag.
        case "close":
            window.orderOut(nil)
        default:
            break
        }
    }

    private func sendStartupStatus() {
        bridge.postEvent(["event": "startup_status", "enabled": StartupManager.isAutostartEnabled])
    }

    private func sendHotkeyStatus() {
        bridge.postEvent(["event": "hotkey_status", "hotkey": currentHotkeyText()])
    }
}

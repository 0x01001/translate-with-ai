import Cocoa
import WebKit

/// Port of Windows/PopupWindow/PopupWindow.xaml.cs.
/// Borderless floating panel hosting popup.html; appears at the cursor,
/// hides when it loses key status, pastes the AI result back into the
/// app that was active when the hotkey fired.
final class PopupWindowController: NSObject, WebViewBridgeDelegate, NSWindowDelegate {
    private static let defaultSize = NSSize(width: 460, height: 360)
    private static let minHeight: CGFloat = 240
    private static let screenMargin: CGFloat = 10

    private let bridge = WebViewBridge()
    private let panel: KeyablePanel

    private var targetApp: NSRunningApplication?
    private var uiReady = false
    private var pendingText: String?
    private var lastShowText = ""

    override init() {
        panel = KeyablePanel(
            contentRect: NSRect(origin: .zero, size: Self.defaultSize),
            styleMask: [.borderless, .nonactivatingPanel],
            backing: .buffered,
            defer: false
        )
        super.init()

        panel.level = .floating
        panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
        panel.isOpaque = false
        panel.backgroundColor = .clear
        panel.hidesOnDeactivate = false
        panel.isReleasedWhenClosed = false
        panel.delegate = self
        panel.onCancel = { [weak self] in self?.hide() }

        let container = NSView(frame: NSRect(origin: .zero, size: Self.defaultSize))
        container.wantsLayer = true
        container.layer?.cornerRadius = 12
        container.layer?.masksToBounds = true
        container.layer?.backgroundColor = NSColor(srgbRed: 15 / 255, green: 17 / 255, blue: 21 / 255, alpha: 1).cgColor

        bridge.delegate = self
        bridge.webView.frame = container.bounds
        bridge.webView.autoresizingMask = [.width, .height]
        container.addSubview(bridge.webView)
        panel.contentView = container

        bridge.load("popup.html")

        NotificationCenter.default.addObserver(
            forName: Localization.localeChanged, object: nil, queue: .main
        ) { [weak self] _ in self?.bridge.postLocale() }
    }

    // ── Show / hide ───────────────────────────────────────────────────────────

    func show(text: String, target: NSRunningApplication?) {
        targetApp = target
        positionNearMouse()

        if uiReady {
            pushShowMessage(text)
            pendingText = nil
            resendLastShowMessageSoon()
        } else {
            pendingText = text
        }

        panel.makeKeyAndOrderFront(nil)
    }

    private func hide() {
        panel.orderOut(nil)
    }

    func windowDidResignKey(_ notification: Notification) {
        hide() // mirrors Window_Deactivated → Hide on Windows
    }

    // ── Bridge messages from popup.js ─────────────────────────────────────────

    func bridge(_ bridge: WebViewBridge, didReceive action: String, payload: [String: Any]) {
        switch action {
        case "ui_ready":
            uiReady = true
            bridge.postLocale()
            if let text = pendingText {
                pushShowMessage(text)
                pendingText = nil
            }
            resendLastShowMessageSoon()
        case "request_locale":
            bridge.postLocale()
        case "paste":
            pasteAndHide(payload["text"] as? String ?? "")
        case "start_drag":
            if let event = NSApp.currentEvent {
                panel.performDrag(with: event)
                clampToScreen()
            }
        case "close":
            hide()
        case "resize_popup":
            if let height = payload["height"] as? Double {
                resize(toHeight: CGFloat(height))
            }
        default:
            break
        }
    }

    private func pushShowMessage(_ text: String) {
        lastShowText = text
        bridge.postEvent(["event": "show", "text": text, "settingsDirectly": false, "settingsOnly": false])
    }

    /// WebView2 occasionally drops the first message after ui_ready; the
    /// Windows build re-sends after 120 ms. Keep the same belt-and-braces.
    private func resendLastShowMessageSoon() {
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.12) { [weak self] in
            guard let self, self.uiReady, self.panel.isVisible, !self.lastShowText.isEmpty else { return }
            self.pushShowMessage(self.lastShowText)
        }
    }

    // ── Paste back into the target app ────────────────────────────────────────

    private func pasteAndHide(_ text: String) {
        hide() // hide immediately for snappy UX
        guard let target = targetApp else { return }

        Task { @MainActor in
            target.activate(options: [])
            await KeyboardSimulator.sleep(ms: 80) // wait for focus to settle

            let originalClipboard = Pasteboard.readString()
            Pasteboard.write(text)
            await KeyboardSimulator.simulatePaste()
            await KeyboardSimulator.sleep(ms: 150) // wait for paste to complete

            if let originalClipboard {
                Pasteboard.write(originalClipboard)
            }
        }
    }

    // ── Layout ────────────────────────────────────────────────────────────────

    private func positionNearMouse() {
        let mouse = NSEvent.mouseLocation
        let screen = NSScreen.screens.first { NSMouseInRect(mouse, $0.frame, false) } ?? NSScreen.main
        guard let visible = screen?.visibleFrame else { return }

        let size = panel.frame.size
        var x = mouse.x - size.width / 2
        var y = mouse.y - 15 - size.height // below the cursor (Cocoa origin is bottom-left)

        if y < visible.minY + Self.screenMargin {
            y = mouse.y + 15 // flip above the cursor
        }
        x = min(max(x, visible.minX + Self.screenMargin), visible.maxX - size.width - Self.screenMargin)
        y = min(max(y, visible.minY + Self.screenMargin), visible.maxY - size.height - Self.screenMargin)

        panel.setFrameOrigin(NSPoint(x: x, y: y))
    }

    private func resize(toHeight height: CGFloat) {
        let newHeight = max(height, Self.minHeight)
        var frame = panel.frame
        guard abs(frame.height - newHeight) >= 1 else { return }
        frame.origin.y += frame.height - newHeight // keep the top edge anchored
        frame.size.height = newHeight
        panel.setFrame(frame, display: true)
        clampToScreen()
    }

    /// Mirrors ClampToScreen on Windows: keep the whole popup inside the
    /// visible frame after drags and content-driven resizes.
    private func clampToScreen() {
        guard let visible = (panel.screen ?? NSScreen.main)?.visibleFrame else { return }
        var frame = panel.frame
        frame.origin.x = min(max(frame.origin.x, visible.minX + Self.screenMargin),
                             visible.maxX - frame.width - Self.screenMargin)
        frame.origin.y = min(max(frame.origin.y, visible.minY + Self.screenMargin),
                             visible.maxY - frame.height - Self.screenMargin)
        if frame.origin != panel.frame.origin {
            panel.setFrameOrigin(frame.origin)
        }
    }
}

/// Borderless panels refuse key status by default; the popup needs it so the
/// user can type in the compose field. Non-activating keeps the target app's
/// menu bar ownership, Spotlight-style.
final class KeyablePanel: NSPanel {
    var onCancel: (() -> Void)?

    override var canBecomeKey: Bool { true }

    override func cancelOperation(_ sender: Any?) {
        onCancel?() // Escape hides the popup
    }
}

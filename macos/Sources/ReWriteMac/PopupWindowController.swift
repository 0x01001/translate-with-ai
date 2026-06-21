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
    private let quickStatusPanel = QuickTranslateStatusPanel()

    private var targetApp: NSRunningApplication?
    private var uiReady = false
    private var pendingText: String?
    private var pendingQuickTranslateText: String?
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

    func quickTranslate(text: String, target: NSRunningApplication?) {
        targetApp = target
        let trimmed = text.trimmingCharacters(in: .whitespacesAndNewlines)

        guard !trimmed.isEmpty else {
            showQuickTranslateError(Localization.get("quick_translate.error.no_selection"))
            return
        }

        quickStatusPanel.show(message: Localization.get("quick_translate.loading"))

        if uiReady {
            pushQuickTranslateMessage(trimmed)
        } else {
            pendingQuickTranslateText = trimmed
        }
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
            if let text = pendingQuickTranslateText {
                pushQuickTranslateMessage(text)
                pendingQuickTranslateText = nil
            }
            resendLastShowMessageSoon()
        case "request_locale":
            bridge.postLocale()
        case "paste":
            pasteAndHide(payload["text"] as? String ?? "")
        case "quick_translate_result":
            completeQuickTranslate(payload["text"] as? String ?? "")
        case "quick_translate_error":
            showQuickTranslateError(payload["message"] as? String ?? Localization.get("quick_translate.error.failed"))
        case "quick_translate_show_popup":
            quickStatusPanel.hide()
            positionNearMouse()
            panel.makeKeyAndOrderFront(nil)
        case "ai_proxy_request":
            runAiProxyRequest(payload)
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

    private func pushQuickTranslateMessage(_ text: String) {
        bridge.postEvent(["event": "quick_translate", "text": text])
    }

    /// WebView2 occasionally drops the first message after ui_ready; the
    /// Windows build re-sends after 120 ms. Keep the same belt-and-braces.
    private func resendLastShowMessageSoon() {
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.12) { [weak self] in
            guard let self, self.uiReady, self.panel.isVisible, !self.lastShowText.isEmpty else { return }
            self.pushShowMessage(self.lastShowText)
        }
    }


    // ── Native AI proxy ───────────────────────────────────────────────────────

    private struct AiProxyRequest {
        let requestId: String
        let provider: String
        let apiKey: String
        let model: String
        let baseUrl: String
        let prompt: String
        let extraHeaders: [String: String]

        init?(_ payload: [String: Any]) {
            requestId = payload["requestId"] as? String ?? ""
            provider = payload["provider"] as? String ?? ""
            apiKey = payload["apiKey"] as? String ?? ""
            model = payload["model"] as? String ?? ""
            baseUrl = payload["baseUrl"] as? String ?? ""
            prompt = payload["prompt"] as? String ?? ""
            extraHeaders = payload["extraHeaders"] as? [String: String] ?? [:]
            if requestId.isEmpty { return nil }
        }
    }

    private func runAiProxyRequest(_ payload: [String: Any]) {
        guard let request = AiProxyRequest(payload) else { return }

        Task { [weak self] in
            await self?.performAiProxyRequest(request)
        }
    }

    private func performAiProxyRequest(_ request: AiProxyRequest) async {
        await postAiProxyEvent(["event": "ai_proxy_started", "requestId": request.requestId])

        do {
            if request.apiKey.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                throw AiProxyError.message(Localization.get("quick_translate.error.failed") + " Missing API key.")
            }
            if request.model.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                throw AiProxyError.message("Missing model name.")
            }
            if request.prompt.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                throw AiProxyError.message("Missing prompt.")
            }

            if request.provider.lowercased() == "gemini" {
                try await performGeminiProxyRequest(request)
                return
            }

            guard let url = URL(string: buildChatCompletionsUrl(request.baseUrl)) else {
                throw AiProxyError.message("Invalid Base URL.")
            }

            var urlRequest = URLRequest(url: url)
            urlRequest.httpMethod = "POST"
            urlRequest.setValue("Bearer \(request.apiKey)", forHTTPHeaderField: "Authorization")
            urlRequest.setValue("application/json", forHTTPHeaderField: "Content-Type")
            for (key, value) in request.extraHeaders where !key.isEmpty && !value.isEmpty {
                urlRequest.setValue(value, forHTTPHeaderField: key)
            }

            let body: [String: Any] = [
                "model": request.model,
                "messages": [
                    [
                        "role": "system",
                        "content": "You are a professional writing assistant. You MUST always write in the same language as the user's input text. Never translate or switch to another language unless explicitly asked to translate."
                    ],
                    ["role": "user", "content": request.prompt]
                ],
                "stream": true,
                "temperature": 0.3
            ]
            urlRequest.httpBody = try JSONSerialization.data(withJSONObject: body)

            let (bytes, response) = try await URLSession.shared.bytes(for: urlRequest)
            if let http = response as? HTTPURLResponse, !(200...299).contains(http.statusCode) {
                var errorBody = ""
                for try await line in bytes.lines {
                    errorBody += line
                    if errorBody.count > 16_384 { break }
                }
                throw AiProxyError.message(extractOpenAiErrorMessage(errorBody, fallback: "HTTP \(http.statusCode)"))
            }

            for try await line in bytes.lines {
                let trimmed = line.trimmingCharacters(in: .whitespacesAndNewlines)
                if trimmed.isEmpty || trimmed == "data: [DONE]" { continue }
                guard trimmed.hasPrefix("data: ") else { continue }
                let json = String(trimmed.dropFirst(6))
                let chunk = extractOpenAiChunkText(json)
                if !chunk.isEmpty {
                    await postAiProxyEvent(["event": "ai_proxy_chunk", "requestId": request.requestId, "text": chunk])
                }
            }

            await postAiProxyEvent(["event": "ai_proxy_done", "requestId": request.requestId])
        } catch {
            NSLog("ReWrite: AI proxy request failed: \(error.localizedDescription)")
            await postAiProxyEvent(["event": "ai_proxy_error", "requestId": request.requestId, "message": error.localizedDescription])
        }
    }


    private func performGeminiProxyRequest(_ request: AiProxyRequest) async throws {
        var model = request.model.trimmingCharacters(in: .whitespacesAndNewlines)
        if model.lowercased().hasPrefix("models/") {
            model = String(model.dropFirst("models/".count))
        }

        let escapedModel = model.addingPercentEncoding(withAllowedCharacters: .urlPathAllowed) ?? model
        let escapedKey = request.apiKey.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed) ?? request.apiKey
        guard let url = URL(string: "https://generativelanguage.googleapis.com/v1beta/models/\(escapedModel):generateContent?key=\(escapedKey)") else {
            throw AiProxyError.message("Invalid Gemini model or API key.")
        }

        var urlRequest = URLRequest(url: url)
        urlRequest.httpMethod = "POST"
        urlRequest.setValue("application/json", forHTTPHeaderField: "Content-Type")
        let body: [String: Any] = [
            "system_instruction": [
                "parts": [["text": "You are a professional writing assistant. You MUST always write in the same language as the user's input text. Never translate or switch to another language unless explicitly asked to translate."]]
            ],
            "contents": [["parts": [["text": request.prompt]]]],
            "generationConfig": ["temperature": 0.3]
        ]
        urlRequest.httpBody = try JSONSerialization.data(withJSONObject: body)

        let (data, response) = try await URLSession.shared.data(for: urlRequest)
        if let http = response as? HTTPURLResponse, !(200...299).contains(http.statusCode) {
            let errorBody = String(data: data, encoding: .utf8) ?? ""
            throw AiProxyError.message(extractOpenAiErrorMessage(errorBody, fallback: "HTTP \(http.statusCode)"))
        }

        let text = extractGeminiText(data)
        if !text.isEmpty {
            await postAiProxyEvent(["event": "ai_proxy_chunk", "requestId": request.requestId, "text": text])
        }
        await postAiProxyEvent(["event": "ai_proxy_done", "requestId": request.requestId])
    }

    private func extractGeminiText(_ data: Data) -> String {
        guard let object = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
              let candidates = object["candidates"] as? [[String: Any]]
        else { return "" }

        var result = ""
        for candidate in candidates {
            guard let content = candidate["content"] as? [String: Any],
                  let parts = content["parts"] as? [[String: Any]]
            else { continue }
            for part in parts {
                result += part["text"] as? String ?? ""
            }
        }
        return result
    }

    @MainActor
    private func postAiProxyEvent(_ payload: [String: Any]) {
        bridge.postEvent(payload)
    }

    private enum AiProxyError: LocalizedError {
        case message(String)
        var errorDescription: String? {
            switch self {
            case .message(let text): return text
            }
        }
    }

    private func buildChatCompletionsUrl(_ baseUrl: String) -> String {
        var clean = baseUrl.trimmingCharacters(in: .whitespacesAndNewlines)
        while clean.hasSuffix("/") { clean.removeLast() }
        if clean.isEmpty { clean = "http://localhost:20128/v1" }
        if clean.lowercased().hasSuffix("/chat/completions") { return clean }
        return clean + "/chat/completions"
    }

    private func extractOpenAiErrorMessage(_ json: String, fallback: String) -> String {
        guard let data = json.data(using: .utf8),
              let object = try? JSONSerialization.jsonObject(with: data) as? [String: Any]
        else { return json.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ? fallback : json }

        if let error = object["error"] as? [String: Any], let message = error["message"] as? String {
            return message
        }
        if let error = object["error"] as? String {
            return error
        }
        return fallback
    }

    private func extractOpenAiChunkText(_ json: String) -> String {
        guard let data = json.data(using: .utf8),
              let object = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
              let choices = object["choices"] as? [[String: Any]],
              let first = choices.first,
              let delta = first["delta"] as? [String: Any]
        else { return "" }
        return delta["content"] as? String ?? ""
    }

    // ── Paste back into the target app ────────────────────────────────────────

    private func pasteAndHide(_ text: String) {
        hide() // hide immediately for snappy UX
        pasteTextToTarget(text)
    }

    private func completeQuickTranslate(_ text: String) {
        let trimmed = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else {
            showQuickTranslateError(Localization.get("quick_translate.error.empty_result"))
            return
        }

        pasteTextToTarget(trimmed) { [weak self] in
            self?.quickStatusPanel.hide()
        }
    }

    private func pasteTextToTarget(_ text: String, completion: (() -> Void)? = nil) {
        guard let target = targetApp else {
            completion?()
            return
        }

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
            completion?()
        }
    }

    func showQuickTranslateError(_ message: String) {
        quickStatusPanel.showError(message: message.isEmpty ? Localization.get("quick_translate.error.failed") : message)
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

private final class QuickTranslateStatusPanel {
    private let panel: NSPanel
    private let dot = NSView(frame: NSRect(x: 0, y: 0, width: 9, height: 9))
    private let label = NSTextField(labelWithString: "Translating...")

    init() {
        panel = NSPanel(
            contentRect: NSRect(x: 0, y: 0, width: 260, height: 46),
            styleMask: [.borderless, .nonactivatingPanel],
            backing: .buffered,
            defer: false
        )
        panel.level = .floating
        panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
        panel.isOpaque = false
        panel.backgroundColor = .clear
        panel.hidesOnDeactivate = false
        panel.isReleasedWhenClosed = false

        let container = NSView(frame: panel.contentView?.bounds ?? NSRect(x: 0, y: 0, width: 260, height: 46))
        container.wantsLayer = true
        container.layer?.cornerRadius = 14
        container.layer?.backgroundColor = NSColor(srgbRed: 12 / 255, green: 15 / 255, blue: 20 / 255, alpha: 0.96).cgColor
        container.layer?.borderWidth = 1
        container.layer?.borderColor = NSColor.white.withAlphaComponent(0.10).cgColor
        container.autoresizingMask = [.width, .height]

        dot.wantsLayer = true
        dot.layer?.cornerRadius = 4.5
        dot.layer?.backgroundColor = NSColor.systemPurple.cgColor
        dot.translatesAutoresizingMaskIntoConstraints = false

        label.textColor = NSColor(calibratedRed: 245 / 255, green: 243 / 255, blue: 255 / 255, alpha: 1)
        label.font = NSFont.systemFont(ofSize: 13, weight: .semibold)
        label.lineBreakMode = .byTruncatingTail
        label.translatesAutoresizingMaskIntoConstraints = false

        container.addSubview(dot)
        container.addSubview(label)
        panel.contentView = container

        NSLayoutConstraint.activate([
            dot.leadingAnchor.constraint(equalTo: container.leadingAnchor, constant: 14),
            dot.centerYAnchor.constraint(equalTo: container.centerYAnchor),
            dot.widthAnchor.constraint(equalToConstant: 9),
            dot.heightAnchor.constraint(equalToConstant: 9),

            label.leadingAnchor.constraint(equalTo: dot.trailingAnchor, constant: 9),
            label.trailingAnchor.constraint(equalTo: container.trailingAnchor, constant: -14),
            label.centerYAnchor.constraint(equalTo: container.centerYAnchor)
        ])
    }

    func show(message: String) {
        setMessage(message, isError: false)
        positionNearMouse()
        panel.orderFrontRegardless()
    }

    func showError(message: String) {
        setMessage(message, isError: true)
        positionNearMouse()
        panel.orderFrontRegardless()
        DispatchQueue.main.asyncAfter(deadline: .now() + 2.6) { [weak self] in
            self?.hide()
        }
    }

    func hide() {
        panel.orderOut(nil)
    }

    private func setMessage(_ message: String, isError: Bool) {
        label.stringValue = message.isEmpty ? "Translating..." : message
        label.textColor = isError ? NSColor.systemRed : NSColor(calibratedRed: 245 / 255, green: 243 / 255, blue: 255 / 255, alpha: 1)
        dot.layer?.backgroundColor = (isError ? NSColor.systemRed : NSColor.systemPurple).cgColor
    }

    private func positionNearMouse() {
        let mouse = NSEvent.mouseLocation
        let screen = NSScreen.screens.first { NSMouseInRect(mouse, $0.frame, false) } ?? NSScreen.main
        guard let visible = screen?.visibleFrame else { return }

        var frame = panel.frame
        frame.origin.x = mouse.x + 14
        frame.origin.y = mouse.y - frame.height - 14

        if frame.origin.x + frame.width > visible.maxX - 10 {
            frame.origin.x = visible.maxX - frame.width - 10
        }
        if frame.origin.y < visible.minY + 10 {
            frame.origin.y = mouse.y + 14
        }
        frame.origin.x = min(max(frame.origin.x, visible.minX + 10), visible.maxX - frame.width - 10)
        frame.origin.y = min(max(frame.origin.y, visible.minY + 10), visible.maxY - frame.height - 10)
        panel.setFrameOrigin(frame.origin)
    }
}

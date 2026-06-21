import Cocoa

/// Port of App.xaml.cs + the controller logic of MainWindow.xaml.cs:
/// single-instance guard, locale bootstrap, global hotkey, status bar item,
/// and the hotkey → copy selection → quick translate flow.
final class AppDelegate: NSObject, NSApplicationDelegate {
    private var popupController: PopupWindowController!
    private var settingsController: SettingsWindowController!
    private var tutorialController: TutorialWindowController?
    private var statusBarController: StatusBarController!

    private var currentHotkey: ParsedHotkey!

    func applicationDidFinishLaunching(_ notification: Notification) {
        AppPaths.ensureSettingsDirectory()
        terminateIfAlreadyRunning()

        Localization.initialize(AppSettingsStore.loadLocale())
        AccessibilityPermission.promptIfNeeded()

        // Warm up the popup so the webview is loaded when the hotkey fires.
        popupController = PopupWindowController()

        settingsController = SettingsWindowController()
        settingsController.currentHotkeyText = { [weak self] in self?.currentHotkey.normalized ?? "" }
        settingsController.updateHotkey = { [weak self] text in self?.tryUpdateHotkey(text) }

        statusBarController = StatusBarController()
        statusBarController.openSettings = { [weak self] in self?.settingsController.showSettings() }

        registerInitialHotkey()
        showTutorialOnFirstRun()
    }

    func applicationWillTerminate(_ notification: Notification) {
        HotKeyManager.shared.unregister()
    }

    // ── Single instance ───────────────────────────────────────────────────────

    private func terminateIfAlreadyRunning() {
        guard let bundleID = Bundle.main.bundleIdentifier else { return } // bare binary in dev
        let running = NSRunningApplication.runningApplications(withBundleIdentifier: bundleID)
        guard running.count > 1 else { return }

        let alert = NSAlert()
        alert.alertStyle = .warning
        alert.messageText = "ReWrite"
        alert.informativeText = "A previous instance of ReWrite is already running. Look for the ReWrite icon in the menu bar."
        alert.runModal()
        NSApp.terminate(nil)
    }

    // ── Hotkey ────────────────────────────────────────────────────────────────

    private func registerInitialHotkey() {
        HotKeyManager.shared.handler = { [weak self] in self?.hotKeyPressed() }

        var parseError = ""
        let persisted = HotkeyPersistence.load()
        currentHotkey = persisted.flatMap { HotkeyParser.parse($0, error: &parseError) }
            ?? HotkeyParser.parse("Option+X", error: &parseError)!

        let registered = HotKeyManager.shared.register(currentHotkey)
        NSLog("ReWrite: hotkey \(currentHotkey.normalized) registered=\(registered) accessibilityTrusted=\(AccessibilityPermission.isTrusted)")
        if !registered {
            let alert = NSAlert()
            alert.alertStyle = .warning
            alert.messageText = "ReWrite - Hotkey Register Failed"
            alert.informativeText = "Could not register the global hotkey \(currentHotkey.normalized). "
                + "Please ensure no other application is using it."
            alert.runModal()
        }
    }

    /// Returns an error message, or nil on success. Used by the settings UI.
    private func tryUpdateHotkey(_ hotkeyText: String) -> String? {
        var error = ""
        guard let parsed = HotkeyParser.parse(hotkeyText, error: &error) else { return error }

        guard HotKeyManager.shared.register(parsed) else {
            HotKeyManager.shared.register(currentHotkey) // restore the previous hotkey
            return "Could not register the hotkey. It may be in use by another application."
        }

        currentHotkey = parsed
        HotkeyPersistence.save(parsed.normalized)
        return nil
    }

    // ── Hotkey handler: capture selection, quick translate ─────────────────────

    private struct SelectionCaptureResult {
        let text: String
        let errorMessage: String?
    }

    private func hotKeyPressed() {
        // Remember the app that has focus before our panel appears.
        let targetApp = NSWorkspace.shared.frontmostApplication
        let bundlePath = Bundle.main.bundleURL.path
        let isTranslocated = bundlePath.contains("/AppTranslocation/")
        NSLog(
            "ReWrite: hotkey fired, target=\(describe(targetApp)) accessibilityTrusted=\(AccessibilityPermission.isTrusted) translocated=\(isTranslocated) bundle=\(bundlePath)"
        )

        Task { @MainActor in
            let result = await self.captureSelectedText(from: targetApp, appWasTranslocated: isTranslocated)
            if let errorMessage = result.errorMessage {
                self.popupController.showQuickTranslateError(errorMessage)
                return
            }

            self.popupController.quickTranslate(text: result.text, target: targetApp)
        }
    }

    @MainActor
    private func captureSelectedText(from targetApp: NSRunningApplication?, appWasTranslocated: Bool) async -> SelectionCaptureResult {
        guard AccessibilityPermission.isTrusted else {
            NSLog("ReWrite: selection capture blocked because Accessibility permission is not trusted")
            AccessibilityPermission.promptIfNeeded()
            return SelectionCaptureResult(
                text: "",
                errorMessage: Localization.get("quick_translate.error.accessibility_required")
            )
        }

        // The hotkey fires while the physical modifier keys may still be down.
        // Wait before touching the pasteboard so a slow key release does not
        // leave the user's clipboard empty while we wait.
        let modifiersReleased = await KeyboardSimulator.waitForModifierRelease()
        if !modifiersReleased {
            NSLog("ReWrite: selection capture continuing before all hotkey modifiers were released")
        }

        // Carbon global hotkeys normally leave focus in the target app, but make
        // that explicit before sending Cmd+C. This mirrors the paste-back path.
        targetApp?.activate(options: [])
        await KeyboardSimulator.sleep(ms: 60)

        let originalClipboard = Pasteboard.snapshot()
        Pasteboard.clear()
        let baselineChangeCount = Pasteboard.changeCount
        _ = await KeyboardSimulator.simulateCopy(waitForModifiers: false)

        var selectedText = ""
        var latestChangeCount = Pasteboard.changeCount
        var sawPasteboardChange = latestChangeCount > baselineChangeCount

        // Wait up to 250 ms for the target app to populate the pasteboard.
        for _ in 0..<10 {
            latestChangeCount = Pasteboard.changeCount
            if latestChangeCount > baselineChangeCount {
                sawPasteboardChange = true
                if let text = Pasteboard.readString(), !text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                    selectedText = text
                    break
                }
            }
            await KeyboardSimulator.sleep(ms: 25)
        }

        // Restore the user's clipboard exactly as best as NSPasteboard allows.
        Pasteboard.restore(originalClipboard)

        let trimmed = selectedText.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else {
            NSLog(
                "ReWrite: selection capture failed, target=\(describe(targetApp)) modifiersReleased=\(modifiersReleased) baseline=\(baselineChangeCount) latest=\(latestChangeCount) sawPasteboardChange=\(sawPasteboardChange) translocated=\(appWasTranslocated)"
            )
            return SelectionCaptureResult(
                text: "",
                errorMessage: Localization.get("quick_translate.error.copy_failed")
            )
        }

        NSLog(
            "ReWrite: selection capture succeeded, target=\(describe(targetApp)) length=\(trimmed.count) baseline=\(baselineChangeCount) latest=\(latestChangeCount)"
        )
        return SelectionCaptureResult(text: selectedText, errorMessage: nil)
    }

    private func describe(_ app: NSRunningApplication?) -> String {
        guard let app else { return "none" }
        let name = app.localizedName ?? "unknown"
        let bundleID = app.bundleIdentifier ?? "no-bundle-id"
        return "\(name) [\(bundleID)]"
    }

    // ── First run ─────────────────────────────────────────────────────────────

    private func showTutorialOnFirstRun() {
        guard !TutorialStateStore.hasSeenTutorial() else { return }
        tutorialController = TutorialWindowController()
        tutorialController?.show()
        TutorialStateStore.markSeen()
    }
}

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

    private func hotKeyPressed() {
        // Remember the app that has focus before our panel appears.
        let targetApp = NSWorkspace.shared.frontmostApplication
        NSLog("ReWrite: hotkey fired, target=\(targetApp?.localizedName ?? "none") accessibilityTrusted=\(AccessibilityPermission.isTrusted)")

        Task { @MainActor in
            let originalClipboard = Pasteboard.readString()

            Pasteboard.clear()
            let baseline = Pasteboard.changeCount
            await KeyboardSimulator.simulateCopy()

            // Wait up to 250 ms for the target app to populate the pasteboard.
            var selectedText = ""
            for _ in 0..<10 {
                if Pasteboard.changeCount > baseline, let text = Pasteboard.readString() {
                    selectedText = text
                    break
                }
                await KeyboardSimulator.sleep(ms: 25)
            }

            // Restore the user's clipboard so we don't pollute their history.
            if let originalClipboard {
                Pasteboard.write(originalClipboard)
            }

            self.popupController.quickTranslate(text: selectedText, target: targetApp)
        }
    }

    // ── First run ─────────────────────────────────────────────────────────────

    private func showTutorialOnFirstRun() {
        guard !TutorialStateStore.hasSeenTutorial() else { return }
        tutorialController = TutorialWindowController()
        tutorialController?.show()
        TutorialStateStore.markSeen()
    }
}

import Cocoa

/// Port of Core/KeyboardSimulator.cs.
/// Posts synthetic Cmd+C / Cmd+V key events into the active app.
/// Requires the Accessibility permission (System Settings → Privacy & Security).
enum KeyboardSimulator {
    private static let keyC: CGKeyCode = 8
    private static let keyV: CGKeyCode = 9

    /// Simulate Cmd+C and give the target app time to populate the pasteboard.
    static func simulateCopy() async {
        await waitForModifierRelease()
        postCommandKey(keyC)
        await sleep(ms: 55)
    }

    /// Simulate Cmd+V into whatever app currently has focus.
    static func simulatePaste() async {
        await waitForModifierRelease()
        postCommandKey(keyV)
        await sleep(ms: 50)
    }

    /// The user is still physically holding the hotkey modifiers when the event
    /// fires; a synthetic Cmd+C sent now would combine with them. Wait up to
    /// 400 ms for all modifiers to be released (mirrors the Windows build).
    private static func waitForModifierRelease() async {
        let interesting: NSEvent.ModifierFlags = [.command, .shift, .control, .option]
        for _ in 0..<27 {
            if NSEvent.modifierFlags.intersection(interesting).isEmpty { return }
            await sleep(ms: 15)
        }
    }

    private static func postCommandKey(_ key: CGKeyCode) {
        let source = CGEventSource(stateID: .combinedSessionState)
        let down = CGEvent(keyboardEventSource: source, virtualKey: key, keyDown: true)
        let up = CGEvent(keyboardEventSource: source, virtualKey: key, keyDown: false)
        down?.flags = .maskCommand
        up?.flags = .maskCommand
        down?.post(tap: .cghidEventTap)
        up?.post(tap: .cghidEventTap)
    }

    static func sleep(ms: UInt64) async {
        try? await Task.sleep(nanoseconds: ms * 1_000_000)
    }
}

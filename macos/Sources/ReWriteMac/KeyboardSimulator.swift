import Cocoa

/// Port of Core/KeyboardSimulator.cs.
/// Posts synthetic Cmd+C / Cmd+V key events into the active app.
/// Requires the Accessibility permission (System Settings → Privacy & Security).
enum KeyboardSimulator {
    private static let keyC: CGKeyCode = 8
    private static let keyV: CGKeyCode = 9

    /// Simulate Cmd+C and give the target app time to populate the pasteboard.
    @discardableResult
    static func simulateCopy(waitForModifiers: Bool = true) async -> Bool {
        let modifiersReleased = waitForModifiers ? await waitForModifierRelease() : true
        postCommandKey(keyC)
        await sleep(ms: 55)
        return modifiersReleased
    }

    /// Simulate Cmd+V into whatever app currently has focus.
    @discardableResult
    static func simulatePaste(waitForModifiers: Bool = true) async -> Bool {
        let modifiersReleased = waitForModifiers ? await waitForModifierRelease() : true
        postCommandKey(keyV)
        await sleep(ms: 50)
        return modifiersReleased
    }

    /// The user is still physically holding the hotkey modifiers when the event
    /// fires; a synthetic Cmd+C sent now would combine with them. Wait up to
    /// 400 ms for all modifiers to be released (mirrors the Windows build).
    @discardableResult
    static func waitForModifierRelease() async -> Bool {
        let interesting: NSEvent.ModifierFlags = [.command, .shift, .control, .option]
        for _ in 0..<27 {
            if NSEvent.modifierFlags.intersection(interesting).isEmpty { return true }
            await sleep(ms: 15)
        }
        return NSEvent.modifierFlags.intersection(interesting).isEmpty
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

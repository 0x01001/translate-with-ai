import Carbon.HIToolbox
import Foundation

/// Port of Core/HotKeyManager.cs.
/// Registers a system-wide hotkey via Carbon RegisterEventHotKey —
/// works without the Accessibility permission, unlike CGEventTap.
final class HotKeyManager {
    static let shared = HotKeyManager()

    /// Invoked on the main queue whenever the registered hotkey fires.
    var handler: (() -> Void)?

    private var hotKeyRef: EventHotKeyRef?
    private var eventHandlerInstalled = false

    private init() {}

    @discardableResult
    func register(_ hotkey: ParsedHotkey) -> Bool {
        unregister()
        installEventHandlerIfNeeded()

        let hotKeyID = EventHotKeyID(signature: Self.fourCC("RWRT"), id: 1)
        var ref: EventHotKeyRef?
        let status = RegisterEventHotKey(
            hotkey.keyCode,
            hotkey.carbonModifiers,
            hotKeyID,
            GetApplicationEventTarget(),
            0,
            &ref
        )
        guard status == noErr, let ref else { return false }
        hotKeyRef = ref
        return true
    }

    func unregister() {
        if let ref = hotKeyRef {
            UnregisterEventHotKey(ref)
            hotKeyRef = nil
        }
    }

    private func installEventHandlerIfNeeded() {
        guard !eventHandlerInstalled else { return }

        var eventType = EventTypeSpec(
            eventClass: OSType(kEventClassKeyboard),
            eventKind: UInt32(kEventHotKeyPressed)
        )
        // Non-capturing closure bridges to the required C function pointer.
        InstallEventHandler(GetApplicationEventTarget(), { _, _, _ in
            DispatchQueue.main.async { HotKeyManager.shared.handler?() }
            return noErr
        }, 1, &eventType, nil, nil)

        eventHandlerInstalled = true
    }

    private static func fourCC(_ string: String) -> OSType {
        string.utf8.reduce(0) { ($0 << 8) + OSType($1) }
    }
}

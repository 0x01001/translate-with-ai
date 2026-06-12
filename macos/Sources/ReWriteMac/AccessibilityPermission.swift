import ApplicationServices
import Cocoa

/// CGEvent posting (the synthetic Cmd+C / Cmd+V) only works once the user
/// grants the app Accessibility access. Prompt on first launch.
enum AccessibilityPermission {
    static var isTrusted: Bool { AXIsProcessTrusted() }

    static func promptIfNeeded() {
        guard !isTrusted else { return }
        let options = [kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String: true] as CFDictionary
        AXIsProcessTrustedWithOptions(options)
    }
}

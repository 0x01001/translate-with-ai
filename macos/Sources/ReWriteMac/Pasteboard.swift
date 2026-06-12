import Cocoa

/// Thin NSPasteboard wrapper, counterpart of System.Windows.Clipboard usage.
enum Pasteboard {
    static func readString() -> String? {
        NSPasteboard.general.string(forType: .string)
    }

    static func write(_ string: String) {
        let pb = NSPasteboard.general
        pb.clearContents()
        pb.setString(string, forType: .string)
    }

    static func clear() {
        NSPasteboard.general.clearContents()
    }

    static var changeCount: Int {
        NSPasteboard.general.changeCount
    }
}

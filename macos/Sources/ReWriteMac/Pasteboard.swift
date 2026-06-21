import Cocoa

/// Thin NSPasteboard wrapper, counterpart of System.Windows.Clipboard usage.
enum Pasteboard {
    struct Snapshot {
        let string: String?
    }

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

    static func snapshot() -> Snapshot {
        Snapshot(string: readString())
    }

    static func restore(_ snapshot: Snapshot) {
        if let string = snapshot.string {
            write(string)
        } else {
            clear()
        }
    }

    static var changeCount: Int {
        NSPasteboard.general.changeCount
    }
}

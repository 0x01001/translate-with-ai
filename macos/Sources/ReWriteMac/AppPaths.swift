import Foundation

/// macOS counterpart of Core/AppPaths.cs.
/// Settings live in ~/Library/Application Support/ReWrite.
enum AppPaths {
    static var settingsDirectory: URL {
        let base = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first!
        return base.appendingPathComponent("ReWrite", isDirectory: true)
    }

    static func ensureSettingsDirectory() {
        try? FileManager.default.createDirectory(at: settingsDirectory, withIntermediateDirectories: true)
    }

    static var appSettingsFile: URL { settingsDirectory.appendingPathComponent("appsettings.json") }
    static var hotkeyFile: URL { settingsDirectory.appendingPathComponent("hotkey.json") }
    static var tutorialFile: URL { settingsDirectory.appendingPathComponent("tutorial.json") }

    /// Root directory of the embedded web UI (popup.html, settings.js, locales/, …).
    /// In the assembled bundle this is Contents/Resources/web; when running the
    /// bare SPM binary it falls back to a `web` directory next to the executable.
    static var webRoot: URL {
        if let res = Bundle.main.resourceURL {
            let bundled = res.appendingPathComponent("web", isDirectory: true)
            if FileManager.default.fileExists(atPath: bundled.path) { return bundled }
        }
        let exeDir = URL(fileURLWithPath: CommandLine.arguments[0]).deletingLastPathComponent()
        return exeDir.appendingPathComponent("web", isDirectory: true)
    }
}

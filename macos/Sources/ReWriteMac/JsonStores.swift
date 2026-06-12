import Foundation

/// Ports of Core/HotkeyPersistence.cs, Core/TutorialStateStore.cs and the
/// appsettings.json locale persistence — small JSON files in Application Support.

enum HotkeyPersistence {
    static func load() -> String? {
        guard let data = try? Data(contentsOf: AppPaths.hotkeyFile),
              let object = try? JSONSerialization.jsonObject(with: data) as? [String: Any]
        else { return nil }
        return object["hotkey"] as? String
    }

    static func save(_ hotkeyText: String) {
        AppPaths.ensureSettingsDirectory()
        let object = ["hotkey": hotkeyText]
        if let data = try? JSONSerialization.data(withJSONObject: object) {
            try? data.write(to: AppPaths.hotkeyFile)
        }
    }
}

enum TutorialStateStore {
    static func hasSeenTutorial() -> Bool {
        guard let data = try? Data(contentsOf: AppPaths.tutorialFile),
              let object = try? JSONSerialization.jsonObject(with: data) as? [String: Any]
        else { return false }
        return object["seen"] as? Bool ?? false
    }

    static func markSeen() {
        AppPaths.ensureSettingsDirectory()
        if let data = try? JSONSerialization.data(withJSONObject: ["seen": true]) {
            try? data.write(to: AppPaths.tutorialFile)
        }
    }
}

enum AppSettingsStore {
    static func loadLocale() -> String? {
        guard let data = try? Data(contentsOf: AppPaths.appSettingsFile),
              let object = try? JSONSerialization.jsonObject(with: data) as? [String: Any]
        else { return nil }
        return object["locale"] as? String
    }

    static func saveLocale(_ locale: String) {
        AppPaths.ensureSettingsDirectory()
        if let data = try? JSONSerialization.data(withJSONObject: ["locale": locale]) {
            try? data.write(to: AppPaths.appSettingsFile)
        }
    }
}

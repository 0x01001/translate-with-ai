import Foundation

/// Port of Core/Localization/Localization.cs.
/// Loads flat key/value JSON from web/locales/{lang}.json and notifies on change.
enum Localization {
    private(set) static var currentLocale = "en"
    private static var strings: [String: String] = [:]

    static let localeChanged = Notification.Name("ReWriteLocaleChanged")

    static func initialize(_ locale: String?) {
        let lang = locale ?? Locale.preferredLanguages.first.map { String($0.prefix(2)) } ?? "en"
        if !tryLoad(lang), lang != "en" {
            _ = tryLoad("en")
        }
    }

    static func setLocale(_ locale: String) {
        initialize(locale)
        NotificationCenter.default.post(name: localeChanged, object: currentLocale)
    }

    static func get(_ key: String) -> String {
        if let value = strings[key], !value.isEmpty { return value }
        return key // make missing keys obvious in UI, same as the Windows build
    }

    private static func tryLoad(_ lang: String) -> Bool {
        let file = AppPaths.webRoot
            .appendingPathComponent("locales", isDirectory: true)
            .appendingPathComponent(lang + ".json")
        guard let data = try? Data(contentsOf: file),
              let object = try? JSONSerialization.jsonObject(with: data) as? [String: Any]
        else { return false }

        var dict: [String: String] = [:]
        for (key, value) in object {
            dict[key] = value as? String ?? ""
        }
        strings = dict
        currentLocale = lang
        return true
    }
}

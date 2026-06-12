import Cocoa

/// Port of the tray icon in Windows/MainWindow/MainWindow.xaml.cs,
/// rebuilt on NSStatusBar with a localized menu.
final class StatusBarController: NSObject, NSMenuDelegate {
    private let statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)
    private let menu = NSMenu()

    private let settingsItem = NSMenuItem()
    private let autostartItem = NSMenuItem()
    private let quitItem = NSMenuItem()

    var openSettings: (() -> Void)?

    override init() {
        super.init()

        statusItem.button?.image = Self.loadIcon()
        menu.delegate = self

        settingsItem.target = self
        settingsItem.action = #selector(openSettingsAction)
        autostartItem.target = self
        autostartItem.action = #selector(toggleAutostartAction)
        quitItem.target = self
        quitItem.action = #selector(quitAction)

        menu.addItem(settingsItem)
        menu.addItem(autostartItem)
        menu.addItem(.separator())
        menu.addItem(quitItem)
        statusItem.menu = menu

        applyLocalization()
        NotificationCenter.default.addObserver(
            forName: Localization.localeChanged, object: nil, queue: .main
        ) { [weak self] _ in self?.applyLocalization() }
    }

    func menuWillOpen(_ menu: NSMenu) {
        autostartItem.state = StartupManager.isAutostartEnabled ? .on : .off
    }

    private func applyLocalization() {
        statusItem.button?.toolTip = Localization.get("tray.tooltip")
        settingsItem.title = Localization.get("tray.open_settings")
        autostartItem.title = Localization.get("tray.autostart")
        quitItem.title = Localization.get("tray.exit")
    }

    @objc private func openSettingsAction() {
        openSettings?()
    }

    @objc private func toggleAutostartAction() {
        StartupManager.setAutostart(!StartupManager.isAutostartEnabled)
    }

    @objc private func quitAction() {
        NSApp.terminate(nil)
    }

    private static func loadIcon() -> NSImage? {
        let logoURL = AppPaths.webRoot.appendingPathComponent("logo.png")
        guard let image = NSImage(contentsOf: logoURL) else {
            return NSImage(systemSymbolName: "pencil.and.outline", accessibilityDescription: "ReWrite")
        }
        image.size = NSSize(width: 18, height: 18)
        return image
    }
}

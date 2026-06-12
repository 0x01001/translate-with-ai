import Foundation
import ServiceManagement

/// Port of Core/StartupManager.cs.
/// Uses SMAppService (macOS 13+) instead of the Windows registry Run key.
/// Only effective when running from an assembled .app bundle.
enum StartupManager {
    static var isAutostartEnabled: Bool {
        SMAppService.mainApp.status == .enabled
    }

    @discardableResult
    static func setAutostart(_ enabled: Bool) -> Bool {
        do {
            if enabled {
                try SMAppService.mainApp.register()
            } else {
                try SMAppService.mainApp.unregister()
            }
            return true
        } catch {
            NSLog("ReWrite: failed to update login item: \(error.localizedDescription)")
            return false
        }
    }
}

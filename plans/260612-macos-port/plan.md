# macOS Port Plan

## Status: Implemented (pending manual end-to-end test)

## Context
- Windows app: WPF shell + WebView2, web UI (HTML/JS) holds ~70% of logic (AI calls, settings, history).
- Goal: native macOS version reusing web UI unmodified.

## Architecture
- `macos/` Swift Package, AppKit + WKWebView, menu bar agent app (LSUIElement).
- Web assets copied at build time from `Windows/**` + `ui/` + `Core/Localization/locales`; `https://rewrite.local/` rewritten to `rewrite://local/` (custom WKURLSchemeHandler).
- JS bridge shim injected at document start: `window.chrome.webview.postMessage` -> `webkit.messageHandlers.bridge`; native -> JS via `window.chrome.webview.__dispatchMessage(payload)`.

## Platform mapping
| Windows | macOS |
|---|---|
| RegisterHotKey (Win32) | Carbon RegisterEventHotKey |
| keybd_event Ctrl+C/V | CGEvent Cmd+C/V (Accessibility permission) |
| NotifyIcon tray | NSStatusItem menu |
| Registry Run key / StartupTask | SMAppService.mainApp (macOS 13+) |
| %LocalAppData%\ReWrite | ~/Library/Application Support/ReWrite |
| GetForegroundWindow/SetForegroundWindow | NSWorkspace.frontmostApplication / NSRunningApplication.activate |
| WebView2 | WKWebView |
| DWM rounded corners | layer cornerRadius on borderless NSPanel |

## Message protocol (unchanged, both directions)
JS->native: ui_ready, request_locale, paste{text}, start_drag, close, resize_popup{height}, get_startup, get_hotkey, set_hotkey{hotkey}, set_locale{locale}, set_startup{enabled}
native->JS: set_locale{locale}, show{text,settingsDirectly,settingsOnly}, startup_status{enabled}, hotkey_status{hotkey}, hotkey_error{message}

## Simplifications (documented)
- WelcomeWindow splash (XAML-drawn) not ported; first run opens Tutorial window directly.
- Default hotkey Cmd+Shift+A (Ctrl -> Cmd mapping).

## Todo
- [x] Investigate codebase
- [x] Swift sources (Core, Infrastructure, Windows equivalents) — 17 files in macos/Sources/ReWriteMac
- [x] Build script: SPM build + .app bundle assembly + asset patch + icns (+ swiftc fallback for broken CLT)
- [x] Compile check + smoke launch (app runs, no crash)
- [x] macos/README.md (build, permissions, usage, troubleshooting)
- [x] Root README.md updated (platform badge, macOS install + hotkeys)
- [ ] Manual end-to-end test (hotkey capture, AI stream, paste-back) — needs user with API key + Accessibility grant

## Build environment note
Local Command Line Tools install broken two ways: SwiftPM manifest link failure AND duplicated SwiftBridging modulemap (breaks ALL swiftc compiles). build-app.sh works around both without sudo (direct swiftc + -vfsoverlay masking module.modulemap). Permanent fix: reinstall CLT or `sudo mv /Library/Developer/CommandLineTools/usr/include/swift/module.modulemap{,.bak}`.

## Unresolved questions
- OpenAI/Gemini CORS from custom scheme origin: expected OK (same cross-origin pattern as Windows rewrite.local origin); verify on first manual run.
- localStorage persistence under custom scheme in WKWebView: Tauri uses same pattern successfully; verify settings survive relaunch.

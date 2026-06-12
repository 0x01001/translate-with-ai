# ReWrite for macOS

Native macOS port of [ReWrite AI](../README.md). Same web UI as the Windows
build (popup, settings, tutorial — HTML/JS shared verbatim from `../Windows`
and `../ui`), wrapped in a Swift/AppKit shell:

| Concern | Windows | macOS |
|---|---|---|
| UI host | WPF + WebView2 | AppKit + WKWebView |
| Global hotkey | `RegisterHotKey` (Win32) | Carbon `RegisterEventHotKey` |
| Copy/paste simulation | `keybd_event` Ctrl+C/V | `CGEvent` Cmd+C/V |
| Tray | NotifyIcon | `NSStatusItem` (menu bar) |
| Launch at login | Registry Run key / StartupTask | `SMAppService` |
| Settings dir | `%LocalAppData%\ReWrite` | `~/Library/Application Support/ReWrite` |

The web UI talks to the shell through the same `window.chrome.webview`
message protocol; a small user script (see `WebViewBridge.swift`) polyfills it
on top of `webkit.messageHandlers`. Assets are copied at build time and the
`https://rewrite.local/` virtual origin is rewritten to `rewrite://local/`
(served by `WebAssetSchemeHandler`), because WKWebView cannot intercept https.

## Requirements

- macOS 13 Ventura or newer (SMAppService)
- Swift toolchain (Xcode or Command Line Tools) to build

## Build

```bash
cd macos
./build-app.sh          # produces dist/ReWrite.app
open dist/ReWrite.app
```

The script prefers `swift build`; if the SwiftPM manifest toolchain is broken
(happens with some Command Line Tools installs) it falls back to direct
`swiftc`, including a VFS-overlay workaround for the duplicated
`SwiftBridging` modulemap bug — no `sudo` needed.

For the login-item toggle to work reliably, move `ReWrite.app` to
`/Applications` first.

## First-run setup

1. **Launch** `ReWrite.app`. It is a menu bar app — look for the ReWrite logo
   in the top-right of the screen, next to the clock. There is **no Dock icon
   and no main window**.
2. **Grant Accessibility access** when macOS prompts
   (System Settings → Privacy & Security → Accessibility → enable ReWrite).
   This is required for the synthetic Cmd+C/Cmd+V that captures your
   selection and pastes the result back. Without it the popup still opens,
   but with empty source text and no 1-click replace.
3. **Enter an API key**: menu bar icon → *Open Settings* → paste your
   Google AI Studio or OpenAI key and pick a model. Keys are stored locally
   in the webview's localStorage, exactly like the Windows build.

## Usage

1. Select (highlight) text in any app — browser, Mail, Notes, Slack…
2. Press **Cmd+Shift+A** (note: **Cmd**, not Ctrl — the Windows default
   `Ctrl+Shift+A` maps to `Cmd+Shift+A` on macOS).
3. The popup appears at your cursor with the selected text. Choose
   Rewrite / Translate / Compose, then **Replace** to paste the result
   back into the original app (your clipboard is restored afterwards).
4. Press **Esc** or click elsewhere to dismiss the popup.

The hotkey is customizable in Settings — accepts e.g. `Cmd+Shift+Space`,
`Ctrl+Option+R`; `Win` is treated as `Cmd`.

## Troubleshooting: hotkey does nothing

Check in this order:

1. **Is the app running?** Look for the ReWrite icon in the menu bar.
   If missing, launch the app again.
2. **Are you pressing Cmd+Shift+A?** The Windows docs say `Ctrl+Shift+A`;
   on macOS the default is **Cmd**+Shift+A.
3. **Hotkey conflict.** If another app already owns the combination, ReWrite
   shows a "Hotkey Register Failed" alert at launch. Pick a different
   combination in Settings (e.g. `Cmd+Shift+Space`).
4. **Popup opens but the source text is empty / Replace does nothing →
   Accessibility access is missing.** System Settings → Privacy & Security →
   Accessibility → enable ReWrite, then quit (menu bar icon → Exit) and
   relaunch.
5. **Rebuilt the app?** The build is ad-hoc signed, so the signature changes
   on every rebuild and macOS silently invalidates the previous Accessibility
   grant. Remove ReWrite from the Accessibility list (− button) and re-add
   the new build (or re-enable it after the prompt re-appears).
6. **Generate does nothing →** missing/invalid API key in Settings.

Still stuck? The app logs registration and hotkey events; inspect with:

```bash
log show --process ReWrite --last 5m | grep "ReWrite:"
```

`registered=false` → hotkey conflict (see 3). `accessibilityTrusted=false` →
permission missing (see 4/5). No "hotkey fired" line on keypress → wrong
combination or app not running.

## Differences vs Windows build (intentional)

- No welcome splash window; first launch opens the tutorial directly.
- Hotkey modifier `Ctrl` means the macOS Control key; `Cmd` is the default
  primary modifier.
- Single-instance guard uses the bundle identifier (only active when running
  from the assembled .app).

## Source layout

```
macos/
├── Package.swift                  # SPM manifest (swiftc fallback in build script)
├── Info.plist.template            # LSUIElement agent app
├── build-app.sh                   # build + bundle + asset copy/patch + icns + sign
└── Sources/ReWriteMac/
    ├── main.swift                 # entry point (accessory activation policy)
    ├── AppDelegate.swift          # hotkey flow, single instance, first run
    ├── PopupWindowController.swift    # floating panel ⇄ popup.html
    ├── SettingsWindowController.swift # settings.html bridge
    ├── TutorialWindowController.swift # tutorial.html
    ├── StatusBarController.swift  # menu bar item
    ├── WebViewBridge.swift        # chrome.webview polyfill + message routing
    ├── WebAssetSchemeHandler.swift# rewrite://local asset server
    ├── HotKeyManager.swift        # Carbon hotkey registration
    ├── HotkeyParser.swift         # "Cmd+Shift+A" → keycode+modifiers
    ├── KeyboardSimulator.swift    # CGEvent Cmd+C / Cmd+V
    ├── Pasteboard.swift           # NSPasteboard helpers
    ├── StartupManager.swift       # SMAppService login item
    ├── Localization.swift         # locale JSON loader (shared locales/)
    ├── JsonStores.swift           # hotkey.json / tutorial.json / appsettings.json
    ├── AccessibilityPermission.swift
    └── AppPaths.swift
```

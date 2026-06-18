# Quick Translate Hotkey Design

Date: 2026-06-18

## Goal

Change the global hotkey flow so a user can select text in any app, press the default hotkey, and have ReWrite translate it automatically. The default behavior should replace the selected text with the translated result after the AI response completes.

The new default hotkey is `Alt+X` on Windows and the closest platform equivalent on macOS is also `Alt+X` / `Option+X`.

## User-facing behavior

### Default flow

1. User highlights text in another application.
2. User presses `Alt+X`.
3. ReWrite copies the selected text using the existing clipboard-based selection capture.
4. ReWrite shows a small non-intrusive native loading indicator such as `Translating...` near the cursor.
5. ReWrite calls the configured AI provider to translate the selected text into the configured default target language.
6. When the AI response finishes, ReWrite pastes the translated text back into the original application, replacing the highlighted text.
7. ReWrite hides the loading indicator and restores the user's previous clipboard content.

The large popup is not shown in this default auto-paste mode.

### Optional popup flow

Users can disable auto-paste in Settings. When disabled, pressing the hotkey should:

1. Copy the selected text.
2. Open the existing popup directly on the Translate tab.
3. Automatically start translation without requiring the user to press Generate.
4. Show the translated result in the popup.
5. Let the user manually press Replace if they want to paste the result back.

This preserves a safer review-before-replace workflow for users who do not want automatic replacement.

## Settings

Extend the existing `rewrite_settings` localStorage object with:

```js
quickTranslateAutoPaste: true,
quickTranslateTargetLang: "Tiếng Việt"
```

Settings UI changes:

- Add a `Quick Translate` area in the Workspace settings section.
- Add a target-language dropdown named conceptually `quickTranslateTargetLang`.
- Add an auto-paste toggle named conceptually `quickTranslateAutoPaste`.
- Default target language is `Tiếng Việt`.
- Default auto-paste is enabled.
- The saved values remain in localStorage with the rest of the web settings.

The Translate popup's existing `param-lang` dropdown should use `quickTranslateTargetLang` as its default value so manual and hotkey translation use the same default target language.

## Hotkey behavior

Change default Windows hotkey state from:

```csharp
Ctrl+Shift+A
```

to:

```csharp
Alt+X
```

Change default macOS hotkey state from:

```swift
Cmd+Shift+A
```

to:

```swift
Option+X
```

Persisted user hotkey config continues to take precedence. Existing users who already saved a hotkey should keep their saved hotkey. New installs or users without `hotkey.json` should get the new default.

## Architecture

Use the existing web AI engine in `Windows/PopupWindow/popup.js` instead of duplicating provider calls in C# and Swift.

Reasoning:

- Current Gemini, OpenAI, and OpenRouter-compatible streaming logic already lives in shared popup JavaScript.
- Settings are already stored in WebView localStorage.
- macOS copies and reuses the same web UI assets.
- Reusing the existing JS path avoids duplicating API logic across Windows and macOS.

## Message flow

### Native to web

Add a native-to-web message for quick translation:

```json
{
  "event": "quick_translate",
  "text": "selected text",
  "targetLang": "Tiếng Việt",
  "autoPaste": true
}
```

The popup WebView may remain hidden. It is already warmed up during app startup, so it can process this message without displaying the full popup window.

### Web to native

On success, popup JS sends:

```json
{
  "action": "quick_translate_result",
  "text": "translated text"
}
```

On failure, popup JS sends:

```json
{
  "action": "quick_translate_error",
  "message": "error text"
}
```

Native code handles paste/error display depending on mode.

## Popup JavaScript behavior

Add a quick-translate path that:

1. Refreshes app settings from localStorage.
2. Builds the existing translate prompt using `promptTemplates.translate`.
3. Calls the currently active provider with the existing streaming implementation.
4. Accumulates the final result without requiring a visible result panel.
5. Sends either `quick_translate_result` or `quick_translate_error` back to native.

Refactor only as much as needed to share prompt construction and provider streaming between normal popup generation and quick translation. Avoid duplicating provider-specific logic.

When `quickTranslateAutoPaste` is disabled, the normal visible popup should switch to Translate and call the same generation flow automatically.

## Native Windows behavior

After hotkey capture in `MainWindow.OnHotKeyPressed()`:

1. Load quick-translate preferences from WebView/localStorage via popup JS or from the existing settings message flow.
2. If selected text is empty, show the popup or a small error indicator explaining that no text was selected.
3. If auto-paste is enabled:
   - show a lightweight loading window near the cursor;
   - send `quick_translate` to the hidden popup WebView;
   - on `quick_translate_result`, focus the original target window and paste the translated text;
   - restore the clipboard and hide loading;
   - on error, hide loading and show an error indicator.
4. If auto-paste is disabled:
   - show the normal popup;
   - open the Translate tab;
   - auto-start generation;
   - do not paste until the user clicks Replace.

## Native macOS behavior

Mirror the Windows behavior in the existing Swift controllers:

- `AppDelegate` continues to capture selected text through pasteboard copy.
- `PopupWindowController` gains a hidden quick-translate request path.
- A small non-activating `NSPanel` or equivalent indicator shows `Translating...` during auto-paste mode.
- On success, reactivate the original target app, paste the translated text, restore the pasteboard, and hide the indicator.

## Error handling

- No selected text: do not call AI. Show a short error message.
- Missing API key or invalid provider settings: show a short error in loading/toast mode, or the existing popup error in popup mode.
- AI request failure: do not paste anything. Restore clipboard/pasteboard.
- Empty AI result: treat as failure and do not paste.
- WebView not ready: queue the quick-translate request until the popup WebView reports `ui_ready`, matching the existing pending-show behavior.

## Localization

Add locale keys for:

- Quick Translate section title.
- Target language label.
- Auto-paste label/help text.
- Loading text such as `Translating...`.
- Quick-translate errors such as no selected text and translation failed.

Update all existing locale JSON files so `scripts/check_locale_keys.js` stays clean.

## Testing plan

Run available static checks:

```bash
node --check Windows/SettingsWindow/settings.js
node --check Windows/PopupWindow/popup.js
node scripts/check_locale_keys.js
npm run build
git diff --check
```

If .NET and macOS tooling are available, also run:

```bash
dotnet build ReWrite.csproj
cd macos && ./build-app.sh
```

Manual behavior checks:

1. Fresh settings: confirm default hotkey is `Alt+X` / `Option+X`.
2. Select text in another app, press hotkey, confirm loading appears and translated text replaces selection.
3. Disable auto-paste, press hotkey, confirm popup opens on Translate and starts generating automatically.
4. Change default target language and confirm quick translation uses it.
5. Test provider errors and missing API key behavior.

## Out of scope

- Adding multiple separate hotkeys for rewrite versus translate.
- Native C#/Swift implementations of Gemini/OpenAI/OpenRouter API calls.
- Cloud sync of settings.
- Notarized macOS signing.
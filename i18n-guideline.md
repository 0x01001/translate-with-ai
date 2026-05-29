# i18n — Agent Guide (ReWrite)

Purpose: precise, machine-readable instructions for an AI agent to add, update, and validate JSON-based localization in this repo.

Quick rules
- Source of truth: `Core/Localization/locales/en.json`.
- All new keys MUST be added to `en.json` first.
- Other locale files must include the same keys. Use `"__MISSING__"` if translation not available.
- Key format: dot-separated, lowercase (e.g. `popup.no_selection`, `button.close`).

Files & locations
- Locale files: `Core/Localization/locales/{lang}.json`.
- Host code: C# `Core/Localization/Localization.cs` and Windows `*.xaml.cs` files.
- Frontend: `Windows/*Window/*.html` and `*.js` served from `https://rewrite.local`.

Host ↔ Frontend contract
- Messages are JSON with `action` field.
- Required actions:
  - Host → Frontend: `{ "action":"set_locale", "locale":"<xx>" }`
  - Frontend → Host: `{ "action":"request_locale" }`, `{ "action":"ui_ready" }`, and `{ "action":"set_locale","locale":"<xx>" }` (from Settings page)

Code changes checklist (agent steps)
1. Find UI strings (search XAML `Title=`/`Content=`, HTML text nodes, JS UI literals).
2. Create key name using component prefix: `{component}.{sub}.{name}`.
3. Edit `en.json`: add key => English text.
4. Edit other `{lang}.json`: add same key => `"__MISSING__"` or translated text.
5. Update host:
   - Replace code-behind literals with `Localization.Get("key")`.
   - For XAML static text, set values in code-behind on load and subscribe to `Localization.LocaleChanged` to update runtime.
   - Every window that owns a WebView2 must update its own host chrome and rebroadcast `set_locale` to its own WebView inside the same `LocaleChanged` path. Do not rely on a one-off caller loop in another window.
6. Update frontend:
   - Add `data-i18n="key"` or `data-i18n-placeholder="key"` to HTML elements.
   - Ensure JS fetches `https://rewrite.local/locales/{locale}.json`, assigns to `i18n`, and calls `applyTranslations()`.
   - `applyTranslations()` must map keys to element text, attributes, placeholders. Use `i18n[key] || key` as fallback for debugging.
7. Persist & broadcast:
   - When user changes locale in Settings, write to `appsettings.json` in `AppPaths.SettingsDirectory`, call `Localization.SetLocale(locale)`, and let each subscribed window rebroadcast `{action:set_locale}` to its own WebView.
   - Avoid ad hoc per-window loops in Settings; the global locale event should be the only source of truth for runtime propagation.

8. Settings UI & persistence (explicit checklist when adding a new language):
   - Add the new language option to every frontend language selector (`Windows/*Window/*settings.html`, `Windows/*Window/*popup.html`) using the `value` equal to the locale code (e.g. `fr`) and `data-i18n="lang.xx"` attribute.
   - Ensure each frontend JS that manages settings (`Windows/*Window/settings.js`, `Windows/*Window/popup.js`) loads `rewrite_settings` from `localStorage`, exposes a `locale` property, and sets the selector value on load.
   - When saving settings, update the saved JSON in `localStorage` to include `locale`, and send a `{ action: 'set_locale', locale }` web message to the host so the host can persist and broadcast the change.
   - Host side: when receiving `set_locale`, persist it to `appsettings.json` and call `Localization.SetLocale(locale)` so `LocaleChanged` events fire for all windows.
   - Update any UI lists or labels that enumerate available languages (`lang.xx` entries) in all locale files to include the new language name and native name.


csproj inclusion (must exist)
```xml
<ItemGroup>
  <EmbeddedResource Include="Core\Localization\locales\*.json" />
  <None Include="Core\Localization\locales\*.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

Validation (automated checks the agent must run)
1. `dotnet build` succeeds.
2. Key consistency: load `en.json` keys and compare every other `{lang}.json`. Report missing keys and `__MISSING__` values.
3. Repo scan: search for leftover hardcoded UI strings and produce a list of locations.
4. Runtime test: open app, change locale in Settings, verify:
   - `Localization.CurrentLocale` updated.
   - `LocaleChanged` fired and WPF texts updated.
   - WebView2 frontends received `set_locale` or completed `request_locale` flow and applied translations.
5. Run the provided locale checker script to validate scaffolds and keys:

```bash
node scripts/check_locale_keys.js
# or use the helper npm script if available
npm run check:i18n:scaffold
```

6. When adding a new language scaffold (example: `fr`), run the checker and verify:
   - `fr.json` contains the same keys as `en.json` (values may be `__MISSING__`).
   - Frontend selectors include the new `fr` option and display correctly after loading the locale.

Optional script
- Create `scripts/check_locales.js` to compare keys across locales and print a JSON report.

Rules & constraints
- Do not rename or remove keys without simultaneous code+frontend+locales update in same PR.
- Keep JSON structure flat (dot-keys). If changing structure, update loader code first.
- Use numbered placeholders `{0}`, `{1}` for interpolation and implement replacement helpers in C# and JS.

Commit & PR
- Commit message format: `i18n: <verb> <key> (<component>) [<langs>]` (e.g. `i18n: add popup.no_selection (popup) [en,vi]`).
- PR description: list keys added/changed, files modified, and verification steps.

Example: scaffold French
1. Copy `Core/Localization/locales/en.json` → `Core/Localization/locales/fr.json` (values `__MISSING__`).
2. Ensure csproj includes pattern.
3. Commit: `i18n: add fr scaffold [fr]`.

End.

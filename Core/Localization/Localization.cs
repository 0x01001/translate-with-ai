using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace ReWrite
{
    // Very small and dependency-free localization helper.
    // Loads JSON files from "locales/{lang}.json" next to the app executable.
    public static class Localization
    {
        private static Dictionary<string, string> _strings = new();
        private static string _locale = "en";
        public static event Action<string>? LocaleChanged;

        public static void Initialize(string? locale = null)
        {
            if (!string.IsNullOrEmpty(locale)) _locale = locale!;
            else _locale = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

            // Try exact lang, fallback to en
            var baseDir = AppContext.BaseDirectory ?? Environment.CurrentDirectory;
            var localesDir = Path.Combine(baseDir, "locales");

            TryLoadLocale(localesDir, _locale);
            if (_strings.Count == 0 && _locale != "en")
                TryLoadLocale(localesDir, "en");
        }

        public static string CurrentLocale => _locale;

        public static void SetLocale(string locale)
        {
            Initialize(locale);
            try { LocaleChanged?.Invoke(_locale); } catch { }
        }

        private static void TryLoadLocale(string localesDir, string lang)
        {
            try
            {
                var file = Path.Combine(localesDir, lang + ".json");
                string? json = null;
                if (File.Exists(file))
                {
                    json = File.ReadAllText(file);
                }
                else
                {
                    // Try embedded resource fallback
                    try
                    {
                        var asm = System.Reflection.Assembly.GetExecutingAssembly();
                        // Resource name pattern: ReWrite.Core.Localization.locales.{lang}.json
                        var resourceName = asm.GetName().Name + ".Core.Localization.locales." + lang + ".json";
                        using var rs = asm.GetManifestResourceStream(resourceName);
                        if (rs != null)
                        {
                            using var sr = new StreamReader(rs);
                                json = sr.ReadToEnd();
                        }
                        else
                        {
                            // Try to find any matching resource suffix
                            foreach (var name in asm.GetManifestResourceNames())
                            {
                                if (name.EndsWith($".locales.{lang}.json", StringComparison.OrdinalIgnoreCase))
                                {
                                    using var rs2 = asm.GetManifestResourceStream(name);
                                    if (rs2 != null)
                                    {
                                        using var sr2 = new StreamReader(rs2);
                                        json = sr2.ReadToEnd();
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }

                if (string.IsNullOrEmpty(json)) return;
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var dict = new Dictionary<string, string>();
                foreach (var prop in root.EnumerateObject())
                {
                    dict[prop.Name] = prop.Value.GetString() ?? "";
                }
                _strings = dict;
                _locale = lang;
            }
            catch { }
        }

        public static string Get(string key)
        {
            if (_strings.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v)) return v;
            // fallback to key to make missing keys obvious in UI
            return key;
        }
    }
}

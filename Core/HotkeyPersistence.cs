using System;
using System.IO;
using System.Text.Json;

namespace ReWrite
{
    /// <summary>
    /// Persists and loads the user's chosen global hotkey to/from disk.
    /// Extracted from MainWindow to isolate I/O from window logic.
    /// </summary>
    internal static class HotkeyPersistence
    {
        private const string FileName = "hotkey.json";

        private static string FilePath => Path.Combine(AppPaths.SettingsDirectory, FileName);

        private sealed class HotkeyConfig
        {
            public string Hotkey { get; set; } = "";
        }

        /// <summary>
        /// Loads saved hotkey settings. Returns null if no config exists or it is invalid.
        /// </summary>
        public static (uint modifiers, uint vk, string normalized)? Load()
        {
            try
            {
                string path = FilePath;
                if (!File.Exists(path)) return null;

                string json = File.ReadAllText(path);
                var config = JsonSerializer.Deserialize<HotkeyConfig>(json);
                if (config == null || string.IsNullOrWhiteSpace(config.Hotkey)) return null;

                if (HotkeyParser.TryParse(config.Hotkey, out uint modifiers, out uint vk, out string normalized, out _))
                {
                    return (modifiers, vk, normalized);
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Saves the active hotkey string to disk (e.g. "Ctrl+Shift+A").
        /// </summary>
        public static void Save(string hotkeyText)
        {
            try
            {
                Directory.CreateDirectory(AppPaths.SettingsDirectory);
                var config = new HotkeyConfig { Hotkey = hotkeyText };
                string json = JsonSerializer.Serialize(config);
                File.WriteAllText(FilePath, json);
            }
            catch { }
        }
    }
}

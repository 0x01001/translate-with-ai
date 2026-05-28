using System;
using System.IO;
using System.Text.Json;

namespace ReWrite
{
    /// <summary>
    /// Persists whether the user has already seen the tutorial video.
    /// Extracted from WelcomeWindow to isolate I/O from window logic.
    /// </summary>
    internal static class TutorialStateStore
    {
        private const string FileName = "tutorial.json";

        private static string FilePath => Path.Combine(AppPaths.SettingsDirectory, FileName);

        private sealed class TutorialState
        {
            public bool HasSeenVideo { get; set; }
        }

        /// <summary>Returns true if the user has previously seen the tutorial.</summary>
        public static bool HasSeenTutorial()
        {
            try
            {
                string path = FilePath;
                if (!File.Exists(path)) return false;

                string json = File.ReadAllText(path);
                var state = JsonSerializer.Deserialize<TutorialState>(json);
                return state?.HasSeenVideo ?? false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Marks the tutorial as seen and persists the state.</summary>
        public static void MarkSeen()
        {
            try
            {
                Directory.CreateDirectory(AppPaths.SettingsDirectory);
                var state = new TutorialState { HasSeenVideo = true };
                string json = JsonSerializer.Serialize(state);
                File.WriteAllText(FilePath, json);
            }
            catch { }
        }
    }
}

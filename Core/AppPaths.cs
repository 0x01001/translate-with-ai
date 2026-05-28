using System;
using System.IO;

namespace ReWrite
{
    /// <summary>
    /// Provides common filesystem paths used across the application.
    /// </summary>
    internal static class AppPaths
    {
        private const string AppFolderName = "ReWrite";

        /// <summary>
        /// %LocalAppData%\ReWrite — where all settings files are stored.
        /// </summary>
        public static string SettingsDirectory
        {
            get
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(localAppData, AppFolderName);
            }
        }
    }
}

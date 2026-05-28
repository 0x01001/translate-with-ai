using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;

namespace ReWrite
{
    /// <summary>
    /// Factory for creating a shared CoreWebView2Environment.
    /// Eliminates the identical initialisation block that was copy-pasted
    /// into PopupWindow, SettingsWindow, and TutorialWindow.
    /// </summary>
    internal static class WebViewHost
    {
        /// <summary>
        /// Creates a WebView2 environment using the app's dedicated user-data
        /// folder (%LocalAppData%\ReWrite) to ensure write permissions and
        /// profile isolation.
        /// </summary>
        public static async Task<CoreWebView2Environment> CreateEnvironmentAsync()
        {
            string userDir = AppPaths.SettingsDirectory;
            return await CoreWebView2Environment.CreateAsync(null, userDir);
        }
    }
}

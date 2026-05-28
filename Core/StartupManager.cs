using System;
using Microsoft.Win32;

namespace ReWrite
{
    public static class StartupManager
    {
        private const string AppName = "ReWrite";

        public static bool EnableAutostart()
        {
            try
            {
                string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (string.IsNullOrEmpty(exePath)) return false;

                // Path must be quoted to handle paths with spaces correctly
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", true);
                if (key != null)
                {
                    key.SetValue(AppName, $"\"{exePath}\"");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error enabling auto-start: {ex.Message}");
                return false;
            }
        }

        public static bool DisableAutostart()
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", true);
                if (key != null)
                {
                    key.DeleteValue(AppName, false);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disabling auto-start: {ex.Message}");
                return false;
            }
        }

        public static bool IsAutostartEnabled()
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", false);
                return key?.GetValue(AppName) != null;
            }
            catch
            {
                return false;
            }
        }
    }
}

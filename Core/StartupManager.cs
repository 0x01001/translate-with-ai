using System;
using System.Threading.Tasks;
using Microsoft.Win32;
using Windows.ApplicationModel;

namespace ReWrite
{
    public static class StartupManager
    {
        private const string AppName = "ReWrite";
        private const string StartupTaskId = "ReWriteStartupTask";

        public static async Task<bool> EnableAutostartAsync()
        {
            var startupTask = await GetStartupTaskAsync();
            if (startupTask != null)
            {
                try
                {
                    var newState = await startupTask.RequestEnableAsync();
                    System.Diagnostics.Debug.WriteLine($"[StartupManager] RequestEnableAsync → {newState}");
                    return newState == StartupTaskState.Enabled;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[StartupManager] StartupTask enable failed: {ex.Message}");
                }
            }

            return await SetRegistryRunAsync(true);
        }

        public static async Task<bool> DisableAutostartAsync()
        {
            var startupTask = await GetStartupTaskAsync();
            if (startupTask != null)
            {
                try
                {
                    startupTask.Disable();
                    System.Diagnostics.Debug.WriteLine("[StartupManager] StartupTask disabled");
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[StartupManager] StartupTask disable failed: {ex.Message}");
                }
            }

            return await SetRegistryRunAsync(false);
        }

        public static async Task<bool> IsAutostartEnabledAsync()
        {
            var startupTask = await GetStartupTaskAsync();
            if (startupTask != null)
            {
                try
                {
                    var state = startupTask.State;
                    System.Diagnostics.Debug.WriteLine($"[StartupManager] StartupTask state: {state}");
                    return state == StartupTaskState.Enabled;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[StartupManager] StartupTask read failed: {ex.Message}");
                }
            }

            return await Task.Run(() =>
            {
                try
                {
                    using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                        @"Software\Microsoft\Windows\CurrentVersion\Run", false);
                    var exists = key?.GetValue(AppName) != null;
                    System.Diagnostics.Debug.WriteLine($"[StartupManager] Registry Run key exists: {exists}");
                    return exists;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[StartupManager] Registry read failed: {ex.Message}");
                    return false;
                }
            });
        }

        private static async Task<StartupTask?> GetStartupTaskAsync()
        {
            try
            {
                var task = await StartupTask.GetAsync(StartupTaskId).AsTask().ConfigureAwait(false);
                System.Diagnostics.Debug.WriteLine("[StartupManager] Using StartupTask API (packaged)");
                return task;
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine("[StartupManager] Not packaged, falling back to registry");
                return null;
            }
        }

        private static async Task<bool> SetRegistryRunAsync(bool enable)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                        @"Software\Microsoft\Windows\CurrentVersion\Run", true);
                    if (key == null) return false;

                    if (enable)
                    {
                        string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
                        if (string.IsNullOrEmpty(exePath)) return false;
                        key.SetValue(AppName, $"\"{exePath}\"");
                    }
                    else
                    {
                        key.DeleteValue(AppName, false);
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[StartupManager] Registry write failed: {ex.Message}");
                    return false;
                }
            });
        }
    }
}

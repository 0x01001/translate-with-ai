using System;
using System.Runtime.InteropServices;

namespace ReWrite
{
    /// <summary>
    /// Centralises all P/Invoke declarations (user32, dwmapi) that were previously
    /// scattered across multiple window code-behind files.
    /// </summary>
    internal static class NativeMethods
    {
        internal enum PreferredAppMode
        {
            Default = 0,
            AllowDark = 1,
            ForceDark = 2,
            ForceLight = 3,
            Max = 4
        }

        // ── Structures ────────────────────────────────────────────────────────────

        [StructLayout(LayoutKind.Sequential)]
        internal struct POINT
        {
            public int X;
            public int Y;
        }

        // ── Constants ─────────────────────────────────────────────────────────────

        // Window dragging
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HTCAPTION = 0x2;
        public const int WM_NULL = 0x0000;

        // Popup menu
        public const uint TPM_RIGHTBUTTON = 0x0002;
        public const uint TPM_RETURNCMD = 0x0100;
        public const uint MF_STRING = 0x0000;
        public const uint MF_SEPARATOR = 0x0800;
        public const uint MF_CHECKED = 0x0008;
        public const uint MF_UNCHECKED = 0x0000;

        // DWM window attributes
        public const int DWMWA_USE_IMMERSIVE_DARK_MODE       = 20;
        public const int DWMWA_WINDOW_CORNER_PREFERENCE      = 33;
        public const int DWMWCP_ROUND                        = 2;  // standard rounded corners

        // ── user32.dll ────────────────────────────────────────────────────────────

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern uint RegisterWindowMessage(string lpString);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool AppendMenu(
            IntPtr hMenu,
            uint uFlags,
            uint uIDNewItem,
            string? lpNewItem);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool AppendMenu(
            IntPtr hMenu,
            uint uFlags,
            uint uIDNewItem,
            IntPtr lpNewItem);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetMenuDefaultItem(IntPtr hMenu, uint uItem, bool fByPos);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint TrackPopupMenuEx(
            IntPtr hmenu,
            uint fuFlags,
            int x,
            int y,
            IntPtr hwnd,
            IntPtr lptpm);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyMenu(IntPtr hMenu);

        public static readonly IntPtr HWND_BROADCAST = new IntPtr(0xffff);

        // ── dwmapi.dll ────────────────────────────────────────────────────────────

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(
            IntPtr hwnd, int attr, ref int pvAttribute, int cbAttribute);

        // UxTheme dark-mode helpers (undocumented, but supported on Windows 11)
        [DllImport("uxtheme.dll", EntryPoint = "#135", SetLastError = true)]
        internal static extern PreferredAppMode SetPreferredAppMode(PreferredAppMode appMode);

        [DllImport("uxtheme.dll", EntryPoint = "#136", SetLastError = true)]
        internal static extern bool AllowDarkModeForWindow(IntPtr hWnd, bool allow);

        [DllImport("uxtheme.dll", EntryPoint = "#137", SetLastError = true)]
        internal static extern void FlushMenuThemes();
    }
}

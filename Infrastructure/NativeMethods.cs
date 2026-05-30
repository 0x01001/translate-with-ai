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

        public static readonly IntPtr HWND_BROADCAST = new IntPtr(0xffff);

        // ── dwmapi.dll ────────────────────────────────────────────────────────────

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(
            IntPtr hwnd, int attr, ref int pvAttribute, int cbAttribute);
    }
}

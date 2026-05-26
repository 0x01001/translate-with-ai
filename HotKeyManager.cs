using System;
using System.Runtime.InteropServices;

namespace ReWrite
{
    public static class HotKeyManager
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        // Modifiers
        public const uint MOD_NONE = 0x0000;
        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;
        public const uint MOD_NOREPEAT = 0x4000;

        public const int WM_HOTKEY = 0x0312;

        public static bool Register(IntPtr hwnd, int id, uint modifiers, uint vk)
        {
            // By default, prevent repeating the hotkey if held down
            return RegisterHotKey(hwnd, id, modifiers | MOD_NOREPEAT, vk);
        }

        public static bool Unregister(IntPtr hwnd, int id)
        {
            return UnregisterHotKey(hwnd, id);
        }
    }
}

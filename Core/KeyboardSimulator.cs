using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ReWrite
{
    public static class KeyboardSimulator
    {
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private const int KEYEVENTF_KEYUP = 0x0002;
        private const byte VK_CONTROL = 0x11;
        private const byte VK_C = 0x43;
        private const byte VK_V = 0x56;

        /// <summary>
        /// Simulates pressing Ctrl+C to copy selected text to clipboard.
        /// </summary>
        public static async Task SimulateCopyAsync()
        {
            // Wait up to 400ms until the user physically releases all modifier keys
            // This prevents hotkey keys from interfering and avoids the Alt-menu focus bug!
            for (int i = 0; i < 20; i++)
            {
                bool ctrlDown  = (GetAsyncKeyState(0x11) & 0x8000) != 0;
                bool shiftDown = (GetAsyncKeyState(0x10) & 0x8000) != 0;
                bool altDown   = (GetAsyncKeyState(0x12) & 0x8000) != 0;
                bool winDown   = (GetAsyncKeyState(0x5B) & 0x8000) != 0 ||
                                 (GetAsyncKeyState(0x5C) & 0x8000) != 0;

                if (!ctrlDown && !shiftDown && !altDown && !winDown) break;
                await Task.Delay(20);
            }

            // Settle delay
            await Task.Delay(15);

            // Press Ctrl+C
            keybd_event(VK_CONTROL, 0, 0, 0);           // Ctrl down
            keybd_event(VK_C, 0, 0, 0);                 // C down
            await Task.Delay(55);                        // Wait for registration
            keybd_event(VK_C, 0, KEYEVENTF_KEYUP, 0);   // C up
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, 0); // Ctrl up
            await Task.Delay(55);                        // Give system time to populate clipboard
        }

        /// <summary>
        /// Simulates pressing Ctrl+V to paste text from clipboard.
        /// </summary>
        public static async Task SimulatePasteAsync()
        {
            // Press Ctrl+V
            keybd_event(VK_CONTROL, 0, 0, 0);           // Ctrl down
            keybd_event(VK_V, 0, 0, 0);                 // V down
            await Task.Delay(50);                        // Wait for registration
            keybd_event(VK_V, 0, KEYEVENTF_KEYUP, 0);   // V up
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, 0); // Ctrl up
            await Task.Delay(50);                        // Wait for paste completion
        }
    }
}

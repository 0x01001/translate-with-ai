using System.Windows.Input;

namespace ReWrite
{
    /// <summary>
    /// Parses and formats global hotkey strings (e.g. "Ctrl+Shift+A").
    /// Extracted from MainWindow to isolate parsing logic.
    /// </summary>
    internal static class HotkeyParser
    {
        /// <summary>
        /// Tries to parse a human-readable hotkey string into Win32 modifiers + virtual key.
        /// </summary>
        public static bool TryParse(
            string hotkeyText,
            out uint modifiers,
            out uint vk,
            out string normalized,
            out string error)
        {
            modifiers = HotKeyManager.MOD_NONE;
            vk = 0;
            normalized = "";
            error = "";

            if (string.IsNullOrWhiteSpace(hotkeyText))
            {
                error = "Phím tắt không được để trống.";
                return false;
            }

            string[] parts = hotkeyText.Split('+',
                System.StringSplitOptions.RemoveEmptyEntries |
                System.StringSplitOptions.TrimEntries);

            string? keyToken = null;

            foreach (string raw in parts)
            {
                string token = raw.Trim();
                string lower = token.ToLowerInvariant();

                if (lower == "ctrl" || lower == "control") { modifiers |= HotKeyManager.MOD_CONTROL; continue; }
                if (lower == "shift")                       { modifiers |= HotKeyManager.MOD_SHIFT;   continue; }
                if (lower == "alt")                         { modifiers |= HotKeyManager.MOD_ALT;     continue; }
                if (lower == "win" || lower == "windows")   { modifiers |= HotKeyManager.MOD_WIN;     continue; }

                if (keyToken != null)
                {
                    error = "Chỉ được phép một phím chính.";
                    return false;
                }
                keyToken = token;
            }

            if (keyToken == null)
            {
                error = "Thiếu phím chính.";
                return false;
            }

            if (modifiers == HotKeyManager.MOD_NONE)
            {
                error = "Cần ít nhất một phím bổ trợ (Ctrl/Shift/Alt/Win).";
                return false;
            }

            if (!TryParseKeyToken(keyToken, out Key key, out string keyDisplay))
            {
                error = "Phím chính không hợp lệ.";
                return false;
            }

            vk = (uint)KeyInterop.VirtualKeyFromKey(key);
            normalized = BuildText(modifiers, keyDisplay);
            return true;
        }

        /// <summary>
        /// Builds the canonical display string for a hotkey (e.g. "Ctrl+Shift+A").
        /// </summary>
        public static string BuildText(uint modifiers, string keyDisplay)
        {
            string text = "";
            if ((modifiers & HotKeyManager.MOD_CONTROL) != 0) text += "Ctrl+";
            if ((modifiers & HotKeyManager.MOD_SHIFT)   != 0) text += "Shift+";
            if ((modifiers & HotKeyManager.MOD_ALT)     != 0) text += "Alt+";
            if ((modifiers & HotKeyManager.MOD_WIN)     != 0) text += "Win+";
            return text + keyDisplay;
        }

        private static bool TryParseKeyToken(string token, out Key key, out string keyDisplay)
        {
            key = Key.None;
            keyDisplay = "";

            string upper = token.Trim().ToUpperInvariant();

            if (upper.Length == 1)
            {
                char c = upper[0];
                if (c >= 'A' && c <= 'Z')
                {
                    key = (Key)System.Enum.Parse(typeof(Key), c.ToString());
                    keyDisplay = c.ToString();
                    return true;
                }
                if (c >= '0' && c <= '9')
                {
                    key = (Key)System.Enum.Parse(typeof(Key), "D" + c);
                    keyDisplay = c.ToString();
                    return true;
                }
            }

            if (upper.StartsWith("F") &&
                int.TryParse(upper.Substring(1), out int f) &&
                f >= 1 && f <= 24)
            {
                key = (Key)((int)Key.F1 + (f - 1));
                keyDisplay = "F" + f;
                return true;
            }

            switch (upper)
            {
                case "SPACE": case "SPACEBAR":
                    key = Key.Space; keyDisplay = "Space"; return true;
                case "ENTER": case "RETURN":
                    key = Key.Enter; keyDisplay = "Enter"; return true;
                case "TAB":
                    key = Key.Tab; keyDisplay = "Tab"; return true;
                case "ESC": case "ESCAPE":
                    key = Key.Escape; keyDisplay = "Esc"; return true;
                case "BACK": case "BACKSPACE":
                    key = Key.Back; keyDisplay = "Backspace"; return true;
                case "DEL": case "DELETE":
                    key = Key.Delete; keyDisplay = "Delete"; return true;
                case "INS": case "INSERT":
                    key = Key.Insert; keyDisplay = "Insert"; return true;
                case "HOME":
                    key = Key.Home; keyDisplay = "Home"; return true;
                case "END":
                    key = Key.End; keyDisplay = "End"; return true;
                case "PGUP": case "PAGEUP":
                    key = Key.PageUp; keyDisplay = "PageUp"; return true;
                case "PGDN": case "PAGEDOWN":
                    key = Key.PageDown; keyDisplay = "PageDown"; return true;
                case "UP":
                    key = Key.Up; keyDisplay = "Up"; return true;
                case "DOWN":
                    key = Key.Down; keyDisplay = "Down"; return true;
                case "LEFT":
                    key = Key.Left; keyDisplay = "Left"; return true;
                case "RIGHT":
                    key = Key.Right; keyDisplay = "Right"; return true;
            }

            return false;
        }
    }
}

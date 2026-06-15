import Carbon.HIToolbox

/// Port of Core/HotkeyParser.cs for macOS.
/// Parses strings like "Cmd+Shift+A" into Carbon key code + modifiers.
/// Windows-style tokens are accepted: "Ctrl" stays the macOS Control key,
/// "Win"/"Windows" maps to Command, "Alt" maps to Option.
struct ParsedHotkey {
    let keyCode: UInt32
    let carbonModifiers: UInt32
    let normalized: String
}

enum HotkeyParser {
    static func parse(_ hotkeyText: String, error: inout String) -> ParsedHotkey? {
        error = ""
        let trimmed = hotkeyText.trimmingCharacters(in: .whitespaces)
        guard !trimmed.isEmpty else {
            error = "Hotkey must not be empty."
            return nil
        }

        var modifiers: UInt32 = 0
        var keyToken: String?

        for raw in trimmed.split(separator: "+") {
            let token = raw.trimmingCharacters(in: .whitespaces)
            if token.isEmpty { continue }
            switch token.lowercased() {
            case "cmd", "command", "win", "windows": modifiers |= UInt32(cmdKey)
            case "ctrl", "control":                  modifiers |= UInt32(controlKey)
            case "shift":                            modifiers |= UInt32(shiftKey)
            case "alt", "option", "opt":             modifiers |= UInt32(optionKey)
            default:
                if keyToken != nil {
                    error = "Only one main key is allowed."
                    return nil
                }
                keyToken = token
            }
        }

        guard let keyToken else {
            error = "Missing main key."
            return nil
        }
        guard modifiers != 0 else {
            error = "At least one modifier is required (Cmd/Ctrl/Shift/Option)."
            return nil
        }
        guard let (keyCode, keyDisplay) = parseKeyToken(keyToken) else {
            error = "Invalid main key."
            return nil
        }

        return ParsedHotkey(
            keyCode: keyCode,
            carbonModifiers: modifiers,
            normalized: buildText(modifiers: modifiers, keyDisplay: keyDisplay)
        )
    }

    /// Canonical display string, macOS modifier order: Ctrl, Option, Shift, Cmd.
    static func buildText(modifiers: UInt32, keyDisplay: String) -> String {
        var text = ""
        if modifiers & UInt32(controlKey) != 0 { text += "Ctrl+" }
        if modifiers & UInt32(optionKey)  != 0 { text += "Option+" }
        if modifiers & UInt32(shiftKey)   != 0 { text += "Shift+" }
        if modifiers & UInt32(cmdKey)     != 0 { text += "Cmd+" }
        return text + keyDisplay
    }

    // ── Key token → Carbon virtual key code (ANSI layout) ─────────────────────

    private static let letterCodes: [Character: UInt32] = [
        "A": 0, "B": 11, "C": 8, "D": 2, "E": 14, "F": 3, "G": 5, "H": 4,
        "I": 34, "J": 38, "K": 40, "L": 37, "M": 46, "N": 45, "O": 31, "P": 35,
        "Q": 12, "R": 15, "S": 1, "T": 17, "U": 32, "V": 9, "W": 13, "X": 7,
        "Y": 16, "Z": 6,
    ]

    private static let digitCodes: [Character: UInt32] = [
        "0": 29, "1": 18, "2": 19, "3": 20, "4": 21,
        "5": 23, "6": 22, "7": 26, "8": 28, "9": 25,
    ]

    private static let functionKeyCodes: [Int: UInt32] = [
        1: 122, 2: 120, 3: 99, 4: 118, 5: 96, 6: 97, 7: 98, 8: 100, 9: 101,
        10: 109, 11: 103, 12: 111, 13: 105, 14: 107, 15: 113, 16: 106,
        17: 64, 18: 79, 19: 80, 20: 90,
    ]

    private static let namedKeys: [String: (UInt32, String)] = [
        "SPACE": (49, "Space"), "SPACEBAR": (49, "Space"),
        "ENTER": (36, "Enter"), "RETURN": (36, "Enter"),
        "TAB": (48, "Tab"),
        "ESC": (53, "Esc"), "ESCAPE": (53, "Esc"),
        "BACK": (51, "Backspace"), "BACKSPACE": (51, "Backspace"),
        "DEL": (117, "Delete"), "DELETE": (117, "Delete"),
        "INS": (114, "Insert"), "INSERT": (114, "Insert"),
        "HOME": (115, "Home"), "END": (119, "End"),
        "PGUP": (116, "PageUp"), "PAGEUP": (116, "PageUp"),
        "PGDN": (121, "PageDown"), "PAGEDOWN": (121, "PageDown"),
        "UP": (126, "Up"), "DOWN": (125, "Down"),
        "LEFT": (123, "Left"), "RIGHT": (124, "Right"),
    ]

    private static func parseKeyToken(_ token: String) -> (UInt32, String)? {
        let upper = token.trimmingCharacters(in: .whitespaces).uppercased()

        if upper.count == 1, let c = upper.first {
            if let code = letterCodes[c] { return (code, String(c)) }
            if let code = digitCodes[c] { return (code, String(c)) }
        }

        if upper.hasPrefix("F"), let f = Int(upper.dropFirst()), let code = functionKeyCodes[f] {
            return (code, "F\(f)")
        }

        return namedKeys[upper]
    }
}

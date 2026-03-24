using Type4Me.NativeMethods;

namespace Type4Me.Input;

/// <summary>
/// Maps Win32 virtual key codes to human-readable display names.
/// </summary>
public static class KeyMapping
{
    public static string GetKeyName(int vk) => vk switch
    {
        // Modifiers
        Win32.VK_LCONTROL => "Left Ctrl",
        Win32.VK_RCONTROL => "Right Ctrl",
        Win32.VK_LSHIFT   => "Left Shift",
        Win32.VK_RSHIFT   => "Right Shift",
        Win32.VK_LMENU    => "Left Alt",
        Win32.VK_RMENU    => "Right Alt",
        Win32.VK_LWIN     => "Left Win",
        Win32.VK_RWIN     => "Right Win",
        Win32.VK_CONTROL  => "Ctrl",
        Win32.VK_SHIFT    => "Shift",
        Win32.VK_MENU     => "Alt",

        // Special keys
        Win32.VK_RETURN  => "Enter",
        Win32.VK_ESCAPE  => "Escape",
        Win32.VK_SPACE   => "Space",
        Win32.VK_TAB     => "Tab",
        Win32.VK_BACK    => "Backspace",
        Win32.VK_DELETE  => "Delete",
        Win32.VK_CAPITAL => "Caps Lock",

        // Navigation
        0x21 => "Page Up",
        0x22 => "Page Down",
        0x23 => "End",
        0x24 => "Home",
        0x25 => "←",
        0x26 => "↑",
        0x27 => "→",
        0x28 => "↓",
        0x2D => "Insert",

        // F keys
        0x70 => "F1",  0x71 => "F2",  0x72 => "F3",  0x73 => "F4",
        0x74 => "F5",  0x75 => "F6",  0x76 => "F7",  0x77 => "F8",
        0x78 => "F9",  0x79 => "F10", 0x7A => "F11", 0x7B => "F12",

        // Numbers
        >= 0x30 and <= 0x39 => ((char)vk).ToString(),

        // Letters
        >= 0x41 and <= 0x5A => ((char)vk).ToString(),

        // Numpad
        >= 0x60 and <= 0x69 => $"Numpad {vk - 0x60}",
        0x6A => "Numpad *",
        0x6B => "Numpad +",
        0x6D => "Numpad -",
        0x6E => "Numpad .",
        0x6F => "Numpad /",

        // OEM keys
        0xBA => ";",
        0xBB => "=",
        0xBC => ",",
        0xBD => "-",
        0xBE => ".",
        0xBF => "/",
        0xC0 => "`",
        0xDB => "[",
        0xDC => "\\",
        0xDD => "]",
        0xDE => "'",

        _ => $"Key 0x{vk:X2}",
    };

    /// <summary>Build a display string for a hotkey combination.</summary>
    public static string FormatHotkey(int? keyCode, uint? modifiers)
    {
        if (keyCode == null) return "";

        var parts = new List<string>();

        if (modifiers.HasValue)
        {
            if ((modifiers.Value & Win32.MOD_CONTROL) != 0) parts.Add("Ctrl");
            if ((modifiers.Value & Win32.MOD_SHIFT) != 0) parts.Add("Shift");
            if ((modifiers.Value & Win32.MOD_ALT) != 0) parts.Add("Alt");
            if ((modifiers.Value & Win32.MOD_WIN) != 0) parts.Add("Win");
        }

        parts.Add(GetKeyName(keyCode.Value));
        return string.Join("+", parts);
    }
}

namespace ScreenShare;

/// <summary>A global keyboard shortcut, stored in settings as text like "Ctrl+Alt+S".</summary>
internal readonly record struct Hotkey(bool Ctrl, bool Alt, bool Shift, bool Win, Keys Key)
{
    public uint Modifiers =>
        (Ctrl ? NativeMethods.MOD_CONTROL : 0) |
        (Alt ? NativeMethods.MOD_ALT : 0) |
        (Shift ? NativeMethods.MOD_SHIFT : 0) |
        (Win ? NativeMethods.MOD_WIN : 0);

    public bool IsValid => Key != Keys.None && (Ctrl || Alt || Win || IsFunctionKey(Key));

    public static bool IsFunctionKey(Keys key) => key is >= Keys.F1 and <= Keys.F24;

    public static bool IsModifierKey(Keys key) => key is
        Keys.ControlKey or Keys.LControlKey or Keys.RControlKey or
        Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey or
        Keys.Menu or Keys.LMenu or Keys.RMenu or
        Keys.LWin or Keys.RWin;

    /// <summary>Text for settings and the UI, e.g. "Ctrl+Alt+S".</summary>
    public override string ToString()
    {
        var parts = new List<string>();
        if (Ctrl) parts.Add("Ctrl");
        if (Alt) parts.Add("Alt");
        if (Shift) parts.Add("Shift");
        if (Win) parts.Add("Win");
        if (Key != Keys.None) parts.Add(KeyName(Key));
        return string.Join("+", parts);
    }

    private static string KeyName(Keys key) => key switch
    {
        >= Keys.D0 and <= Keys.D9 => ((char)('0' + (key - Keys.D0))).ToString(),
        Keys.Oemcomma => ",",
        Keys.OemPeriod => ".",
        Keys.OemMinus => "-",
        Keys.Oemplus => "=",
        _ => key.ToString(),
    };

    public static Hotkey? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        bool ctrl = false, alt = false, shift = false, win = false;
        Keys key = Keys.None;
        foreach (var raw in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl" or "control": ctrl = true; break;
                case "alt": alt = true; break;
                case "shift": shift = true; break;
                case "win" or "windows": win = true; break;
                case "," : key = Keys.Oemcomma; break;
                case ".": key = Keys.OemPeriod; break;
                case "-": key = Keys.OemMinus; break;
                case "=": key = Keys.Oemplus; break;
                default:
                    if (raw.Length == 1 && char.IsDigit(raw[0]))
                        key = Keys.D0 + (raw[0] - '0');
                    else if (Enum.TryParse(raw, ignoreCase: true, out Keys parsed))
                        key = parsed;
                    else
                        return null;
                    break;
            }
        }

        var hotkey = new Hotkey(ctrl, alt, shift, win, key);
        return hotkey.IsValid ? hotkey : null;
    }
}

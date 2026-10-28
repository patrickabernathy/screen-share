using System.ComponentModel;

namespace ScreenShare;

/// <summary>A text box that records the shortcut the user presses instead of accepting typed text.</summary>
internal sealed class HotkeyBox : TextBox
{
    private Hotkey? _hotkey;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Hotkey? Value
    {
        get => _hotkey;
        set
        {
            _hotkey = value;
            Text = value?.ToString() ?? string.Empty;
        }
    }

    public HotkeyBox()
    {
        ReadOnly = true;
        ShortcutsEnabled = false;
        BackColor = SystemColors.Window;
        Cursor = Cursors.Hand;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        Keys key = keyData & Keys.KeyCode;
        bool ctrl = keyData.HasFlag(Keys.Control);
        bool alt = keyData.HasFlag(Keys.Alt);
        bool shift = keyData.HasFlag(Keys.Shift);
        bool win = (NativeMethods.GetKeyState(NativeMethods.VK_LWIN) & 0x8000) != 0 ||
                   (NativeMethods.GetKeyState(NativeMethods.VK_RWIN) & 0x8000) != 0;

        // Tab still moves between fields; everything else is captured.
        if (key == Keys.Tab && !ctrl && !alt && !win)
            return base.ProcessCmdKey(ref msg, keyData);

        if (Hotkey.IsModifierKey(key))
        {
            // Show the modifiers held so far while waiting for the main key.
            Text = new Hotkey(ctrl, alt, shift, win, Keys.None) + "+…";
            return true;
        }

        var candidate = new Hotkey(ctrl, alt, shift, win, key);
        if (candidate.IsValid)
            Value = candidate;
        else
            Text = _hotkey?.ToString() ?? string.Empty;
        return true;
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        // Letting go of the modifiers without a main key restores the last valid shortcut.
        if (Hotkey.IsModifierKey(e.KeyCode) && Text.EndsWith('…'))
            Text = _hotkey?.ToString() ?? string.Empty;
        base.OnKeyUp(e);
    }

    protected override void OnLeave(EventArgs e)
    {
        Text = _hotkey?.ToString() ?? string.Empty;
        base.OnLeave(e);
    }
}

namespace ScreenShare;

internal sealed class SettingsForm : Form
{
    private readonly HotkeyBox _shareBox = new() { Width = 220 };
    private readonly HotkeyBox _pauseBox = new() { Width = 220 };
    private readonly CheckBox _cursorBox = new() { Text = "Show the mouse pointer to viewers", AutoSize = true };

    public Hotkey ShareHotkey => _shareBox.Value!.Value;
    public Hotkey PauseHotkey => _pauseBox.Value!.Value;
    public bool CaptureCursor => _cursorBox.Checked;

    public SettingsForm(Hotkey share, Hotkey pause, bool captureCursor)
    {
        Text = "Screen Share Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = SystemFonts.MessageBoxFont;
        ShowInTaskbar = true;
        TopMost = true;

        _shareBox.Value = share;
        _pauseBox.Value = pause;
        _cursorBox.Checked = captureCursor;

        var layout = new TableLayoutPanel
        {
            ColumnCount = 2,
            AutoSize = true,
            Padding = new Padding(16),
            Dock = DockStyle.Fill,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var help = new Label
        {
            Text = "Click a box, then press the new shortcut.",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(3, 0, 3, 12),
        };
        layout.Controls.Add(help, 0, 0);
        layout.SetColumnSpan(help, 2);

        layout.Controls.Add(MakeLabel("Share the active window:"), 0, 1);
        layout.Controls.Add(_shareBox, 1, 1);
        layout.Controls.Add(MakeLabel("Pause / resume sharing:"), 0, 2);
        layout.Controls.Add(_pauseBox, 1, 2);

        _cursorBox.Margin = new Padding(3, 12, 3, 3);
        layout.Controls.Add(_cursorBox, 0, 3);
        layout.SetColumnSpan(_cursorBox, 2);

        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 16, 0, 0),
        };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        layout.Controls.Add(buttons, 0, 4);
        layout.SetColumnSpan(buttons, 2);

        Controls.Add(layout);
        AcceptButton = ok;
        CancelButton = cancel;
        FormClosing += OnFormClosing;
    }

    private static Label MakeLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Anchor = AnchorStyles.Left,
        Margin = new Padding(3, 6, 12, 6),
    };

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (DialogResult != DialogResult.OK)
            return;

        if (_shareBox.Value is null || _pauseBox.Value is null)
        {
            MessageBox.Show(this, "Both shortcuts need a value.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            e.Cancel = true;
        }
        else if (_shareBox.Value == _pauseBox.Value)
        {
            MessageBox.Show(this, "The two shortcuts must be different.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            e.Cancel = true;
        }
    }
}

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ScreenShare;

/// <summary>
/// The window you share in Meet/Zoom/etc. It mirrors whichever window is currently shared,
/// so switching what viewers see never touches the meeting app's share controls.
/// </summary>
internal sealed class ProjectorForm : Form
{
    public const string WindowTitle = "Screen Share Projector";

    private static readonly int TaskbarButtonCreated = NativeMethods.RegisterWindowMessage("TaskbarButtonCreated");

    private int _renderPending;
    private ITaskbarList3? _taskbar;
    private Icon? _badge;
    private string? _badgeDescription;

    public Renderer? Renderer { get; private set; }

    /// <summary>Raised with the hotkey id when a registered global hotkey is pressed.</summary>
    public event Action<int>? HotkeyPressed;

    /// <summary>Set when the app is exiting, so closing really closes instead of hiding.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool AllowClose { get; set; }

    public event Action? HiddenByUser;

    /// <summary>
    /// Drops the title bar so viewers see only the shared content. Meet captures the whole window, frame included.
    /// The window handle survives the switch, so the meeting's capture keeps running.
    /// </summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Borderless
    {
        get => FormBorderStyle == FormBorderStyle.None;
        set
        {
            if (value == Borderless)
                return;
            var clientSize = ClientSize;
            FormBorderStyle = value ? FormBorderStyle.None : FormBorderStyle.Sizable;
            if (WindowState == FormWindowState.Normal)
                ClientSize = clientSize;
            if (!value)
                ReapplyIcons();
        }
    }

    /// <summary>When true, the close button hides the projector to the tray; otherwise it exits the app.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool CloseToTray { get; set; }

    /// <summary>Raised when the user closes the projector and <see cref="CloseToTray"/> is off.</summary>
    public event Action? ExitRequested;

    /// <summary>
    /// A restored title bar comes back without its icon if the icon changed while borderless, even though the
    /// window still holds it. Clearing and re-setting the same icons makes Windows draw it again.
    /// </summary>
    private void ReapplyIcons()
    {
        foreach (int which in new[] { NativeMethods.ICON_SMALL, NativeMethods.ICON_BIG })
        {
            IntPtr icon = NativeMethods.SendMessage(Handle, NativeMethods.WM_GETICON, which, IntPtr.Zero);
            NativeMethods.SendMessage(Handle, NativeMethods.WM_SETICON, which, IntPtr.Zero);
            NativeMethods.SendMessage(Handle, NativeMethods.WM_SETICON, which, icon);
        }
    }

    public ProjectorForm(int width, int height)
    {
        Text = WindowTitle;
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(width, height);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.Black;
        MinimumSize = new Size(320, 200);

        // D3D owns every pixel; stop WinForms from painting over it.
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Opaque, true);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Renderer = new Renderer(Handle, ClientSize.Width, ClientSize.Height);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (Renderer is null || WindowState == FormWindowState.Minimized)
            return;
        Renderer.Resize(ClientSize.Width, ClientSize.Height);
        Renderer.Render();
    }

    protected override void OnPaint(PaintEventArgs e) => Renderer?.Render();

    protected override void OnPaintBackground(PaintEventArgs e)
    {
    }

    /// <summary>Asks for a redraw from any thread; bursts of frames collapse into one draw.</summary>
    public void RequestRender()
    {
        if (Interlocked.Exchange(ref _renderPending, 1) == 1 || !IsHandleCreated)
            return;
        try
        {
            BeginInvoke(() =>
            {
                Volatile.Write(ref _renderPending, 0);
                if (WindowState != FormWindowState.Minimized)
                    Renderer?.Render();
            });
        }
        catch (InvalidOperationException)
        {
            // The window is closing.
        }
    }

    /// <summary>Shows (or with null, clears) a small badge over the projector's taskbar button.</summary>
    public void SetTaskbarBadge(Icon? badge, string? description)
    {
        _badge = badge;
        _badgeDescription = description;
        ApplyTaskbarBadge();
    }

    private void ApplyTaskbarBadge()
    {
        // Before the taskbar has created our button the call fails; TaskbarButtonCreated reapplies it.
        if (_taskbar is null || !IsHandleCreated)
            return;
        try
        {
            _taskbar.SetOverlayIcon(Handle, _badge?.Handle ?? IntPtr.Zero, _badgeDescription);
        }
        catch (COMException)
        {
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == TaskbarButtonCreated)
        {
            // Sent when our button appears, including after Explorer restarts.
            try
            {
                _taskbar ??= (ITaskbarList3)new TaskbarListClass();
                _taskbar.HrInit();
            }
            catch (COMException)
            {
                _taskbar = null;
            }
            ApplyTaskbarBadge();
        }
        if (m.Msg == NativeMethods.WM_HOTKEY)
        {
            HotkeyPressed?.Invoke((int)m.WParam);
            return;
        }
        if (Borderless)
        {
            if (m.Msg == NativeMethods.WM_NCHITTEST)
            {
                m.Result = BorderlessHitTest(m.LParam);
                return;
            }
            // Double-clicking a "caption" would maximize the borderless window over the taskbar.
            if (m.Msg == NativeMethods.WM_NCLBUTTONDBLCLK)
                return;
        }
        base.WndProc(ref m);
    }

    /// <summary>Without a frame, drag anywhere to move and drag the edges to resize.</summary>
    private IntPtr BorderlessHitTest(IntPtr lParam)
    {
        long packed = lParam.ToInt64();
        var pt = PointToClient(new Point((short)(packed & 0xFFFF), (short)((packed >> 16) & 0xFFFF)));
        if (WindowState != FormWindowState.Normal)
            return NativeMethods.HTCAPTION;

        int grip = LogicalToDeviceUnits(8);
        bool left = pt.X < grip, right = pt.X >= ClientSize.Width - grip;
        bool top = pt.Y < grip, bottom = pt.Y >= ClientSize.Height - grip;
        int hit = (top, bottom, left, right) switch
        {
            (true, _, true, _) => NativeMethods.HTTOPLEFT,
            (true, _, _, true) => NativeMethods.HTTOPRIGHT,
            (_, true, true, _) => NativeMethods.HTBOTTOMLEFT,
            (_, true, _, true) => NativeMethods.HTBOTTOMRIGHT,
            (true, _, _, _) => NativeMethods.HTTOP,
            (_, true, _, _) => NativeMethods.HTBOTTOM,
            (_, _, true, _) => NativeMethods.HTLEFT,
            (_, _, _, true) => NativeMethods.HTRIGHT,
            _ => NativeMethods.HTCAPTION,
        };
        return hit;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // A bare WM_CLOSE (e.g. Task Manager's End task) arrives as TaskManagerClosing, not UserClosing; it still
        // has to shut the app down properly, or the tray would keep running with no projector behind it.
        if (!AllowClose && e.CloseReason != CloseReason.WindowsShutDown)
        {
            e.Cancel = true;
            if (CloseToTray && e.CloseReason == CloseReason.UserClosing)
            {
                // Hide instead of closing, so the app keeps running in the tray.
                Hide();
                HiddenByUser?.Invoke();
            }
            else
            {
                // Let the app shut down properly (save settings, release hotkeys) rather than closing just this window.
                ExitRequested?.Invoke();
            }
            return;
        }
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Renderer?.Dispose();
            Renderer = null;
        }
        base.Dispose(disposing);
    }
}

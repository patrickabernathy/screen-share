using System.ComponentModel;

namespace ScreenShare;

/// <summary>
/// The window you share in Meet/Zoom/etc. It mirrors whichever window is currently shared,
/// so switching what viewers see never touches the meeting app's share controls.
/// </summary>
internal sealed class ProjectorForm : Form
{
    public const string WindowTitle = "Screen Share Projector";

    private int _renderPending;

    public Renderer? Renderer { get; private set; }

    /// <summary>Raised with the hotkey id when a registered global hotkey is pressed.</summary>
    public event Action<int>? HotkeyPressed;

    /// <summary>Set when the app is exiting, so closing really closes instead of hiding.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool AllowClose { get; set; }

    public event Action? HiddenByUser;

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

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_HOTKEY)
        {
            HotkeyPressed?.Invoke((int)m.WParam);
            return;
        }
        base.WndProc(ref m);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!AllowClose && e.CloseReason == CloseReason.UserClosing)
        {
            // Closing the projector would end the meeting share; hide it and keep running in the tray.
            e.Cancel = true;
            Hide();
            HiddenByUser?.Invoke();
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

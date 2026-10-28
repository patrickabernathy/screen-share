namespace ScreenShare;

/// <summary>Owns the tray icon, the global hotkeys, the projector window, and the current capture.</summary>
internal sealed class TrayApp : ApplicationContext
{
    private const int ShareHotkeyId = 1;
    private const int PauseHotkeyId = 2;

    private readonly AppSettings _settings;
    private readonly ProjectorForm _projector;
    private readonly WindowCapture _capture;
    private readonly NotifyIcon _tray;
    private readonly ContextMenuStrip _menu = new();
    private readonly Icon _idleIcon = AppIcon.Create(live: false);
    private readonly Icon _liveIcon = AppIcon.Create(live: true);

    private Hotkey _shareHotkey;
    private Hotkey _pauseHotkey;
    private IntPtr _sharedWindow;
    private string _sharedTitle = string.Empty;
    private bool _paused;
    private bool _toldAboutTray;
    private SettingsForm? _settingsForm;

    public TrayApp()
    {
        _settings = AppSettings.Load();
        _shareHotkey = Hotkey.Parse(_settings.ShareHotkey) ?? Hotkey.Parse("Ctrl+Alt+S")!.Value;
        _pauseHotkey = Hotkey.Parse(_settings.PauseHotkey) ?? Hotkey.Parse("Ctrl+Alt+P")!.Value;

        _projector = new ProjectorForm(_settings.ProjectorWidth, _settings.ProjectorHeight) { Icon = _idleIcon };
        _projector.HotkeyPressed += OnHotkey;
        _projector.HiddenByUser += OnProjectorHidden;
        _projector.Show();

        _capture = new WindowCapture(_projector.Renderer!);
        _capture.FrameReady += _projector.RequestRender;
        _capture.SourceClosed += () => _projector.BeginInvoke(StopSharing);

        _tray = new NotifyIcon
        {
            Icon = _idleIcon,
            Visible = true,
            ContextMenuStrip = _menu,
        };
        _tray.DoubleClick += (_, _) => ShowProjector(activate: true);
        _menu.Opening += (_, e) =>
        {
            BuildMenu();
            // WinForms pre-cancels Opening when the menu is empty, which it is before the first build.
            e.Cancel = false;
        };

        UpdateStatus();
        RegisterHotkeys(showErrors: true);
    }

    private void OnHotkey(int id)
    {
        switch (id)
        {
            case ShareHotkeyId:
                // The share key toggles: pressing it in the window that's already shared stops sharing.
                var hwnd = NativeMethods.GetAncestor(NativeMethods.GetForegroundWindow(), NativeMethods.GA_ROOT);
                if (hwnd == _sharedWindow && hwnd != IntPtr.Zero)
                    StopSharing();
                else
                    ShareWindow(hwnd);
                break;
            case PauseHotkeyId:
                TogglePause();
                break;
        }
    }

    private void ShareWindow(IntPtr hwnd)
    {
        if (hwnd == _projector.Handle || !WindowList.IsShareable(hwnd))
        {
            System.Media.SystemSounds.Beep.Play();
            return;
        }

        try
        {
            _capture.Start(hwnd, _settings.CaptureCursor);
        }
        catch (Exception ex)
        {
            _tray.ShowBalloonTip(4000, "Can't share that window", ex.Message, ToolTipIcon.Warning);
            return;
        }

        _sharedWindow = hwnd;
        _sharedTitle = NativeMethods.GetWindowTitle(hwnd);
        _paused = false;
        UpdateStatus();
        ShowProjector(activate: false);
    }

    private void StopSharing()
    {
        _capture.Stop();
        _projector.Renderer?.ClearFrame();
        _sharedWindow = IntPtr.Zero;
        _sharedTitle = string.Empty;
        _paused = false;
        UpdateStatus();
    }

    private void TogglePause()
    {
        if (_sharedWindow == IntPtr.Zero)
            return;
        _paused = !_paused;
        UpdateStatus();
    }

    /// <summary>Syncs the projector placeholder, tray tooltip, and icons with the current state.</summary>
    private void UpdateStatus()
    {
        var renderer = _projector.Renderer!;
        renderer.Paused = _paused;
        if (_sharedWindow == IntPtr.Zero)
        {
            renderer.Title = "Nothing is being shared";
            renderer.Hint = $"Click a window and press {_shareHotkey}";
        }
        else
        {
            renderer.Title = "Sharing is paused";
            renderer.Hint = string.Empty;
        }
        renderer.Render();

        bool live = _sharedWindow != IntPtr.Zero && !_paused;
        _tray.Icon = live ? _liveIcon : _idleIcon;
        _projector.Icon = _tray.Icon;

        string status = _sharedWindow == IntPtr.Zero ? "nothing shared"
            : _paused ? "paused"
            : $"sharing {_sharedTitle}";
        string tip = $"Screen Share: {status}";
        _tray.Text = tip.Length > 127 ? tip[..124] + "..." : tip;
    }

    private void ShowProjector(bool activate)
    {
        if (!_projector.Visible)
        {
            if (activate)
                _projector.Show();
            else
                NativeMethods.ShowWindow(_projector.Handle, NativeMethods.SW_SHOWNOACTIVATE);
        }
        if (_projector.WindowState == FormWindowState.Minimized)
            _projector.WindowState = FormWindowState.Normal;
        if (activate)
            _projector.Activate();
    }

    private void OnProjectorHidden()
    {
        if (_toldAboutTray)
            return;
        _toldAboutTray = true;
        _tray.ShowBalloonTip(4000, "Screen Share is still running",
            "Double-click the tray icon to bring the projector back. Use Exit in the tray menu to quit.", ToolTipIcon.Info);
    }

    private void BuildMenu()
    {
        _menu.Items.Clear();

        var shareMenu = new ToolStripMenuItem("Share window");
        var exclude = new HashSet<IntPtr> { _projector.Handle };
        foreach (var window in WindowList.Enumerate(exclude))
        {
            string text = window.Title.Length > 70 ? window.Title[..67] + "..." : window.Title;
            var item = new ToolStripMenuItem(text.Replace("&", "&&")) { Checked = window.Handle == _sharedWindow };
            var hwnd = window.Handle;
            item.Click += (_, _) => ShareWindow(hwnd);
            shareMenu.DropDownItems.Add(item);
        }
        if (shareMenu.DropDownItems.Count == 0)
            shareMenu.DropDownItems.Add(new ToolStripMenuItem("(no windows)") { Enabled = false });
        _menu.Items.Add(shareMenu);

        var pause = new ToolStripMenuItem(_paused ? "Resume sharing" : "Pause sharing")
        {
            ShortcutKeyDisplayString = _pauseHotkey.ToString(),
            Enabled = _sharedWindow != IntPtr.Zero,
        };
        pause.Click += (_, _) => TogglePause();
        _menu.Items.Add(pause);

        var stop = new ToolStripMenuItem("Stop sharing") { Enabled = _sharedWindow != IntPtr.Zero };
        stop.Click += (_, _) => StopSharing();
        _menu.Items.Add(stop);

        _menu.Items.Add(new ToolStripSeparator());

        var size = new ToolStripMenuItem("Projector size");
        foreach (var (w, h) in new[] { (1280, 720), (1920, 1080) })
        {
            var item = new ToolStripMenuItem($"{w} × {h}") { Checked = _projector.ClientSize == new Size(w, h) };
            item.Click += (_, _) => SetProjectorSize(w, h);
            size.DropDownItems.Add(item);
        }
        _menu.Items.Add(size);

        var show = new ToolStripMenuItem("Show projector");
        show.Click += (_, _) => ShowProjector(activate: true);
        _menu.Items.Add(show);

        var settings = new ToolStripMenuItem($"Settings… (share key: {_shareHotkey})");
        settings.Click += (_, _) => OpenSettings();
        _menu.Items.Add(settings);

        _menu.Items.Add(new ToolStripSeparator());

        var exit = new ToolStripMenuItem("Exit");
        exit.Click += (_, _) => ExitThread();
        _menu.Items.Add(exit);
    }

    private void SetProjectorSize(int width, int height)
    {
        _projector.WindowState = FormWindowState.Normal;
        _projector.ClientSize = new Size(width, height);
        ShowProjector(activate: true);
    }

    private void OpenSettings()
    {
        if (_settingsForm is not null)
        {
            _settingsForm.Activate();
            return;
        }

        // Release the shortcuts so pressing them in the dialog records them instead of firing them.
        UnregisterHotkeys();
        try
        {
            while (true)
            {
                using var form = new SettingsForm(_shareHotkey, _pauseHotkey, _settings.CaptureCursor);
                _settingsForm = form;
                if (form.ShowDialog() != DialogResult.OK)
                    break;

                _shareHotkey = form.ShareHotkey;
                _pauseHotkey = form.PauseHotkey;
                _settings.ShareHotkey = _shareHotkey.ToString();
                _settings.PauseHotkey = _pauseHotkey.ToString();
                _settings.CaptureCursor = form.CaptureCursor;
                _settings.Save();
                UpdateStatus();

                // Reopen the dialog if another app already owns one of the shortcuts.
                if (RegisterHotkeys(showErrors: true))
                    return;
                UnregisterHotkeys();
            }
        }
        finally
        {
            _settingsForm = null;
        }
        RegisterHotkeys(showErrors: true);
    }

    /// <returns>True when both shortcuts registered.</returns>
    private bool RegisterHotkeys(bool showErrors)
    {
        UnregisterHotkeys();
        var failed = new List<string>();
        if (!NativeMethods.RegisterHotKey(_projector.Handle, ShareHotkeyId, _shareHotkey.Modifiers | NativeMethods.MOD_NOREPEAT, (uint)_shareHotkey.Key))
            failed.Add($"{_shareHotkey} (share window)");
        if (!NativeMethods.RegisterHotKey(_projector.Handle, PauseHotkeyId, _pauseHotkey.Modifiers | NativeMethods.MOD_NOREPEAT, (uint)_pauseHotkey.Key))
            failed.Add($"{_pauseHotkey} (pause)");

        if (failed.Count > 0 && showErrors)
        {
            MessageBox.Show(
                $"These shortcuts are already used by another app:\n\n{string.Join("\n", failed)}\n\nPick different ones in Settings (right-click the tray icon).",
                "Screen Share", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        return failed.Count == 0;
    }

    private void UnregisterHotkeys()
    {
        NativeMethods.UnregisterHotKey(_projector.Handle, ShareHotkeyId);
        NativeMethods.UnregisterHotKey(_projector.Handle, PauseHotkeyId);
    }

    protected override void ExitThreadCore()
    {
        UnregisterHotkeys();
        _capture.Dispose();

        if (_projector.WindowState == FormWindowState.Normal)
        {
            _settings.ProjectorWidth = _projector.ClientSize.Width;
            _settings.ProjectorHeight = _projector.ClientSize.Height;
        }
        _settings.Save();

        _tray.Visible = false;
        _tray.Dispose();
        _menu.Dispose();
        _projector.AllowClose = true;
        _projector.Close();
        _projector.Dispose();
        _idleIcon.Dispose();
        _liveIcon.Dispose();
        base.ExitThreadCore();
    }
}

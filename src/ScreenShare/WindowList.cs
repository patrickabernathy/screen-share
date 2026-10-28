namespace ScreenShare;

internal readonly record struct ShareableWindow(IntPtr Handle, string Title);

internal static class WindowList
{
    private static readonly HashSet<string> ShellClasses = new(StringComparer.Ordinal)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd",
    };

    /// <summary>True for normal top-level app windows; false for the desktop, taskbar, and hidden/tool windows.</summary>
    public static bool IsShareable(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !NativeMethods.IsWindowVisible(hwnd) || NativeMethods.IsCloaked(hwnd))
            return false;
        if (NativeMethods.GetWindow(hwnd, NativeMethods.GW_OWNER) != IntPtr.Zero)
            return false;
        if ((NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE) & NativeMethods.WS_EX_TOOLWINDOW) != 0)
            return false;
        return !ShellClasses.Contains(NativeMethods.GetWindowClass(hwnd));
    }

    /// <summary>Open app windows in Alt+Tab order, excluding the given ones (our own windows).</summary>
    public static List<ShareableWindow> Enumerate(IReadOnlySet<IntPtr> exclude)
    {
        var windows = new List<ShareableWindow>();
        NativeMethods.EnumWindows((hwnd, _) =>
        {
            if (!exclude.Contains(hwnd) && IsShareable(hwnd))
            {
                string title = NativeMethods.GetWindowTitle(hwnd);
                if (title.Length > 0)
                    windows.Add(new ShareableWindow(hwnd, title));
            }
            return true;
        }, IntPtr.Zero);
        return windows;
    }
}

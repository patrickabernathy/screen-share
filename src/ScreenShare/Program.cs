using Windows.Graphics.Capture;

namespace ScreenShare;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, @"Local\ScreenShare-6b1f6c2e", out bool firstInstance);
        if (!firstInstance)
        {
            MessageBox.Show("Screen Share is already running. Look for its icon in the system tray.",
                "Screen Share", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();

        if (!GraphicsCaptureSession.IsSupported())
        {
            MessageBox.Show("Window capture isn't supported on this version of Windows.",
                "Screen Share", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Application.Run(new TrayApp());
    }
}

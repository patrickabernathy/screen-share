using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;

namespace ScreenShare;

/// <summary>Captures one window with Windows.Graphics.Capture and feeds its frames to the renderer.</summary>
internal sealed class WindowCapture : IDisposable
{
    private readonly Renderer _renderer;
    private GraphicsCaptureItem? _item;
    private Direct3D11CaptureFramePool? _framePool;
    private GraphicsCaptureSession? _session;
    private SizeInt32 _poolSize;

    /// <summary>Raised on a capture thread after a new frame has been handed to the renderer.</summary>
    public event Action? FrameReady;

    /// <summary>Raised on a capture thread when the shared window closes.</summary>
    public event Action? SourceClosed;

    /// <summary>Raised with the shared window's size when capture starts and whenever that window is resized (may be on a capture thread).</summary>
    public event Action<SizeInt32>? SourceSizeChanged;

    public WindowCapture(Renderer renderer) => _renderer = renderer;

    public void Start(IntPtr hwnd, bool captureCursor)
    {
        Stop();

        var item = CaptureInterop.CreateItemForWindow(hwnd);
        _poolSize = item.Size;
        var framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
            _renderer.WinRtDevice,
            DirectXPixelFormat.B8G8R8A8UIntNormalized,
            2,
            _poolSize);
        var session = framePool.CreateCaptureSession(item);
        session.IsCursorCaptureEnabled = captureCursor;

        framePool.FrameArrived += OnFrameArrived;
        item.Closed += OnItemClosed;

        _item = item;
        _framePool = framePool;
        _session = session;
        session.StartCapture();
        SourceSizeChanged?.Invoke(_poolSize);
    }

    public void Stop()
    {
        if (_framePool is not null)
            _framePool.FrameArrived -= OnFrameArrived;
        if (_item is not null)
            _item.Closed -= OnItemClosed;

        _session?.Dispose();
        _framePool?.Dispose();
        _session = null;
        _framePool = null;
        _item = null;
    }

    private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        using var frame = sender.TryGetNextFrame();
        if (frame is null)
            return;

        // The shared window was resized: grow/shrink the pool so the next frames aren't cropped.
        if (frame.ContentSize.Width != _poolSize.Width || frame.ContentSize.Height != _poolSize.Height)
        {
            _poolSize = frame.ContentSize;
            sender.Recreate(_renderer.WinRtDevice, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, _poolSize);
            SourceSizeChanged?.Invoke(_poolSize);
        }

        using var texture = CaptureInterop.GetTexture(frame.Surface);
        _renderer.SubmitFrame(texture, frame.ContentSize);
        FrameReady?.Invoke();
    }

    private void OnItemClosed(GraphicsCaptureItem sender, object args) => SourceClosed?.Invoke();

    public void Dispose() => Stop();
}

using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DirectWrite;
using Vortice.DXGI;
using Vortice.Mathematics;
using Windows.Graphics;
using Windows.Graphics.DirectX.Direct3D11;
using D2DAlphaMode = Vortice.DCommon.AlphaMode;
using D2DPixelFormat = Vortice.DCommon.PixelFormat;
using FactoryType = Vortice.Direct2D1.FactoryType;
using FeatureLevel = Vortice.Direct3D.FeatureLevel;
using FontStyle = Vortice.DirectWrite.FontStyle;
using InterpolationMode = Vortice.Direct2D1.InterpolationMode;

namespace ScreenShare;

/// <summary>
/// Draws the most recent captured frame (or a placeholder message) into the projector window.
/// Frames arrive on a capture thread; drawing happens on the UI thread. One lock guards the device.
/// </summary>
internal sealed class Renderer : IDisposable
{
    private static readonly Color4 Background = new(0.07f, 0.07f, 0.08f, 1f);

    private readonly object _gate = new();
    private readonly ID3D11Device _device;
    private readonly ID3D11DeviceContext _context;
    private readonly IDXGISwapChain1 _swapChain;
    private readonly ID2D1Factory1 _d2dFactory;
    private readonly ID2D1Device _d2dDevice;
    private readonly ID2D1DeviceContext _dc;
    private readonly IDWriteFactory _dwrite;
    private readonly IDWriteTextFormat _titleFormat;
    private readonly IDWriteTextFormat _hintFormat;
    private readonly ID2D1SolidColorBrush _titleBrush;
    private readonly ID2D1SolidColorBrush _hintBrush;

    private ID2D1Bitmap1? _target;
    private ID3D11Texture2D? _frame;
    private ID2D1Bitmap1? _frameBitmap;
    private SizeInt32 _contentSize;

    public IDirect3DDevice WinRtDevice { get; }

    /// <summary>When true the placeholder is drawn even if a frame is available.</summary>
    public bool Paused { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Hint { get; set; } = string.Empty;

    public Renderer(IntPtr hwnd, int width, int height)
    {
        D3D11.D3D11CreateDevice(
            null,
            DriverType.Hardware,
            DeviceCreationFlags.BgraSupport,
            [FeatureLevel.Level_11_1, FeatureLevel.Level_11_0],
            out ID3D11Device? device,
            out ID3D11DeviceContext? context).CheckError();
        _device = device!;
        _context = context!;

        using (var multithread = _device.QueryInterface<ID3D11Multithread>())
            multithread.SetMultithreadProtected(true);

        using var dxgiDevice = _device.QueryInterface<IDXGIDevice>();
        WinRtDevice = CaptureInterop.CreateWinRtDevice(dxgiDevice);

        using var adapter = dxgiDevice.GetAdapter();
        using var factory = adapter.GetParent<IDXGIFactory2>();

        // Blt-model (Discard) on purpose: it presents into the window's redirection surface,
        // which every screen-share capturer can read, including older GDI-based window capture.
        var desc = new SwapChainDescription1
        {
            Width = (uint)Math.Max(width, 1),
            Height = (uint)Math.Max(height, 1),
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            BufferUsage = Usage.RenderTargetOutput,
            BufferCount = 1,
            Scaling = Scaling.Stretch,
            SwapEffect = SwapEffect.Discard,
            AlphaMode = Vortice.DXGI.AlphaMode.Ignore,
        };
        _swapChain = factory.CreateSwapChainForHwnd(_device, hwnd, desc);
        factory.MakeWindowAssociation(hwnd, WindowAssociationFlags.IgnoreAll);

        _d2dFactory = D2D1.D2D1CreateFactory<ID2D1Factory1>(FactoryType.MultiThreaded);
        _d2dDevice = _d2dFactory.CreateDevice(dxgiDevice);
        _dc = _d2dDevice.CreateDeviceContext(DeviceContextOptions.None);
        _dc.SetDpi(96, 96);

        _dwrite = DWrite.DWriteCreateFactory<IDWriteFactory>();
        _titleFormat = CreateCenteredFormat(40, FontWeight.SemiBold);
        _hintFormat = CreateCenteredFormat(22, FontWeight.Normal);
        _titleBrush = _dc.CreateSolidColorBrush(new Color4(0.92f, 0.92f, 0.94f, 1f));
        _hintBrush = _dc.CreateSolidColorBrush(new Color4(0.6f, 0.6f, 0.65f, 1f));

        CreateTarget();
    }

    private IDWriteTextFormat CreateCenteredFormat(float size, FontWeight weight)
    {
        var format = _dwrite.CreateTextFormat("Segoe UI", weight, FontStyle.Normal, FontStretch.Normal, size);
        format.TextAlignment = TextAlignment.Center;
        format.ParagraphAlignment = ParagraphAlignment.Center;
        return format;
    }

    private void CreateTarget()
    {
        using var backBuffer = _swapChain.GetBuffer<IDXGISurface>(0);
        _target = _dc.CreateBitmapFromDxgiSurface(backBuffer, new BitmapProperties1(
            new D2DPixelFormat(Format.B8G8R8A8_UNorm, D2DAlphaMode.Ignore),
            96, 96,
            BitmapOptions.Target | BitmapOptions.CannotDraw));
        _dc.Target = _target;
    }

    public void Resize(int width, int height)
    {
        lock (_gate)
        {
            _dc.Target = null;
            _target?.Dispose();
            _target = null;
            _swapChain.ResizeBuffers(0, (uint)Math.Max(width, 1), (uint)Math.Max(height, 1), Format.Unknown, SwapChainFlags.None).CheckError();
            CreateTarget();
        }
    }

    /// <summary>Copies a captured frame so it can be redrawn later (resize, pause, minimized source).</summary>
    public void SubmitFrame(ID3D11Texture2D texture, SizeInt32 contentSize)
    {
        lock (_gate)
        {
            var desc = texture.Description;
            if (_frame is null || _frame.Description.Width != desc.Width || _frame.Description.Height != desc.Height)
            {
                ReleaseFrame();
                _frame = _device.CreateTexture2D(new Texture2DDescription
                {
                    Width = desc.Width,
                    Height = desc.Height,
                    MipLevels = 1,
                    ArraySize = 1,
                    Format = desc.Format,
                    SampleDescription = new SampleDescription(1, 0),
                    Usage = ResourceUsage.Default,
                    BindFlags = BindFlags.ShaderResource,
                });
                using var surface = _frame.QueryInterface<IDXGISurface>();
                _frameBitmap = _dc.CreateBitmapFromDxgiSurface(surface, new BitmapProperties1(
                    new D2DPixelFormat(desc.Format, D2DAlphaMode.Ignore)));
            }

            _context.CopyResource(_frame, texture);
            _contentSize = new SizeInt32
            {
                Width = Math.Min(contentSize.Width, (int)desc.Width),
                Height = Math.Min(contentSize.Height, (int)desc.Height),
            };
        }
    }

    public void ClearFrame()
    {
        lock (_gate)
            ReleaseFrame();
    }

    private void ReleaseFrame()
    {
        _frameBitmap?.Dispose();
        _frameBitmap = null;
        _frame?.Dispose();
        _frame = null;
    }

    public void Render()
    {
        lock (_gate)
        {
            if (_target is null)
                return;

            var size = _target.PixelSize;
            _dc.BeginDraw();
            _dc.Clear(Background);

            if (!Paused && _frameBitmap is not null && _contentSize.Width > 0 && _contentSize.Height > 0)
            {
                // Fit the source into the window, keeping its aspect ratio (letterbox/pillarbox).
                float scale = Math.Min((float)size.Width / _contentSize.Width, (float)size.Height / _contentSize.Height);
                float w = _contentSize.Width * scale;
                float h = _contentSize.Height * scale;
                float x = (size.Width - w) / 2;
                float y = (size.Height - h) / 2;
                _dc.DrawBitmap(
                    _frameBitmap,
                    new Rect(x, y, w, h),
                    1f,
                    InterpolationMode.HighQualityCubic,
                    new Rect(0, 0, _contentSize.Width, _contentSize.Height),
                    null);
            }
            else
            {
                float mid = size.Height / 2f;
                _dc.DrawText(Title, _titleFormat, new Rect(0, mid - 70, size.Width, 60), _titleBrush);
                _dc.DrawText(Hint, _hintFormat, new Rect(0, mid + 5, size.Width, 40), _hintBrush);
            }

            _dc.EndDraw();
            _swapChain.Present(1, PresentFlags.None);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            ReleaseFrame();
            _dc.Target = null;
            _target?.Dispose();
            _hintBrush.Dispose();
            _titleBrush.Dispose();
            _hintFormat.Dispose();
            _titleFormat.Dispose();
            _dwrite.Dispose();
            _dc.Dispose();
            _d2dDevice.Dispose();
            _d2dFactory.Dispose();
            _swapChain.Dispose();
            (WinRtDevice as IDisposable)?.Dispose();
            _context.Dispose();
            _device.Dispose();
        }
    }
}

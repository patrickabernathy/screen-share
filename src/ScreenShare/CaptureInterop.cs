using System.Runtime.InteropServices;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;

namespace ScreenShare;

/// <summary>
/// The COM glue between Windows.Graphics.Capture (WinRT) and Direct3D 11 (Vortice).
/// Interfaces are called through their vtables so nothing depends on built-in COM interop.
/// </summary>
internal static unsafe class CaptureInterop
{
    private static readonly Guid IID_IGraphicsCaptureItemInterop = new("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356");
    private static readonly Guid IID_IGraphicsCaptureItem = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");
    private static readonly Guid IID_IDirect3DDxgiInterfaceAccess = new("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1");
    private static readonly Guid IID_ID3D11Texture2D = new("6F15AAF2-D208-4E89-9AB4-489535D34F9C");

    [DllImport("combase.dll", CharSet = CharSet.Unicode)]
    private static extern int WindowsCreateString(string sourceString, int length, out IntPtr hstring);

    [DllImport("combase.dll")]
    private static extern int WindowsDeleteString(IntPtr hstring);

    [DllImport("combase.dll")]
    private static extern int RoGetActivationFactory(IntPtr activatableClassId, in Guid iid, out IntPtr factory);

    [DllImport("d3d11.dll")]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

    public static GraphicsCaptureItem CreateItemForWindow(IntPtr hwnd)
    {
        const string className = "Windows.Graphics.Capture.GraphicsCaptureItem";
        Marshal.ThrowExceptionForHR(WindowsCreateString(className, className.Length, out IntPtr hstring));
        IntPtr factory;
        try
        {
            Marshal.ThrowExceptionForHR(RoGetActivationFactory(hstring, IID_IGraphicsCaptureItemInterop, out factory));
        }
        finally
        {
            WindowsDeleteString(hstring);
        }

        IntPtr itemPtr = IntPtr.Zero;
        try
        {
            // IGraphicsCaptureItemInterop::CreateForWindow is vtable slot 3 (after IUnknown).
            Guid iid = IID_IGraphicsCaptureItem;
            var createForWindow = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, Guid*, IntPtr*, int>)(*(void***)factory)[3];
            Marshal.ThrowExceptionForHR(createForWindow(factory, hwnd, &iid, &itemPtr));
            return GraphicsCaptureItem.FromAbi(itemPtr);
        }
        finally
        {
            if (itemPtr != IntPtr.Zero)
                Marshal.Release(itemPtr);
            Marshal.Release(factory);
        }
    }

    public static IDirect3DDevice CreateWinRtDevice(IDXGIDevice dxgiDevice)
    {
        Marshal.ThrowExceptionForHR(CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer, out IntPtr devicePtr));
        try
        {
            return MarshalInterface<IDirect3DDevice>.FromAbi(devicePtr);
        }
        finally
        {
            Marshal.Release(devicePtr);
        }
    }

    public static ID3D11Texture2D GetTexture(IDirect3DSurface surface)
    {
        IntPtr surfacePtr = MarshalInterface<IDirect3DSurface>.FromManaged(surface);
        IntPtr accessPtr = IntPtr.Zero;
        try
        {
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(surfacePtr, IID_IDirect3DDxgiInterfaceAccess, out accessPtr));

            // IDirect3DDxgiInterfaceAccess::GetInterface is vtable slot 3.
            Guid iid = IID_ID3D11Texture2D;
            IntPtr texturePtr;
            var getInterface = (delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int>)(*(void***)accessPtr)[3];
            Marshal.ThrowExceptionForHR(getInterface(accessPtr, &iid, &texturePtr));
            return new ID3D11Texture2D(texturePtr);
        }
        finally
        {
            if (accessPtr != IntPtr.Zero)
                Marshal.Release(accessPtr);
            Marshal.Release(surfacePtr);
        }
    }
}

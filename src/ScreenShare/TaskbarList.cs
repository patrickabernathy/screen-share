using System.Runtime.InteropServices;

namespace ScreenShare;

/// <summary>
/// The shell's ITaskbarList3, used only for the overlay badge. Changing the window icon alone doesn't reliably
/// repaint the taskbar button, but an overlay is applied by the taskbar directly.
/// </summary>
[ComImport]
[Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITaskbarList3
{
    // ITaskbarList
    void HrInit();
    void AddTab(IntPtr hwnd);
    void DeleteTab(IntPtr hwnd);
    void ActivateTab(IntPtr hwnd);
    void SetActiveAlt(IntPtr hwnd);

    // ITaskbarList2
    void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fullscreen);

    // ITaskbarList3 (declared in vtable order up to the one method we call)
    void SetProgressValue(IntPtr hwnd, ulong completed, ulong total);
    void SetProgressState(IntPtr hwnd, int flags);
    void RegisterTab(IntPtr tab, IntPtr mdi);
    void UnregisterTab(IntPtr tab);
    void SetTabOrder(IntPtr tab, IntPtr insertBefore);
    void SetTabActive(IntPtr tab, IntPtr mdi, uint reserved);
    void ThumbBarAddButtons(IntPtr hwnd, uint count, IntPtr buttons);
    void ThumbBarUpdateButtons(IntPtr hwnd, uint count, IntPtr buttons);
    void ThumbBarSetImageList(IntPtr hwnd, IntPtr imageList);
    void SetOverlayIcon(IntPtr hwnd, IntPtr icon, [MarshalAs(UnmanagedType.LPWStr)] string? description);
}

[ComImport]
[Guid("56FDF344-FD6D-11d0-958A-006097C9A090")]
[ClassInterface(ClassInterfaceType.None)]
internal class TaskbarListClass
{
}

# Screen Share

![.NET](https://img.shields.io/badge/.NET-10-512bd4?logo=dotnet)

Teams-style "share this window" switching for Google Meet (or Zoom, or anything else that can share a window) on Windows 11.

Meet makes you stop presenting and go back through its picker every time you want to show a different window. Screen Share avoids that: you share one window, the **Screen Share Projector**, once at the start of class. After that, the projector mirrors whichever window you point it at, and you switch with a global keyboard shortcut. The meeting never knows anything changed.

## Table of Contents

- [Features](#features)
- [Using it](#using-it)
- [Tray menu](#tray-menu)
- [Settings](#settings)
- [Not included](#not-included)
- [For developers](#for-developers)

## Features

- **One share, any window** — share the projector once, then switch what viewers see with **Ctrl+Alt+S** without touching Meet.
- **Pause and stop from the keyboard** — **Ctrl+Alt+P** shows a "Sharing is paused" card; **Ctrl+Alt+S** in the shared window stops sharing.
- **Clean output** — while sharing, the projector drops its title bar and moves off screen, so viewers see only the content and no stray mouse pointers.
- **Auto size** — the projector can follow the shared window's size, even when you resize that window mid-share.
- **Always know what's live** — Windows draws a yellow border around the shared window, and the tray icon and taskbar button get a green dot.
- **Configurable** — change both shortcuts, whether viewers see your pointer, what the close button does, and whether the projector moves off screen.

## Using it

1. Run `ScreenShare.exe`. A projector window opens, and an icon appears in the system tray. Only one copy runs at a time.
2. In Meet: **Present → A window → Screen Share Projector**.
3. Click any window and press **Ctrl+Alt+S**. That window is now what viewers see.
4. Press **Ctrl+Alt+S** in another window to switch, or press it again in the shared window to stop sharing. Press **Ctrl+Alt+P** to pause, which shows a "Sharing is paused" card, and press it again to resume.

While something is shared:

- Windows draws a yellow border around the shared window, and the tray icon and taskbar button get a green dot.
- The projector drops its title bar so viewers see only the content.
- The projector moves off screen, so your pointer can't land on it (Meet draws your real pointer whenever it's over the shared window). It keeps streaming from there and comes back when you stop sharing.
- To see the projector mid-share, double-click the tray icon or choose **Show projector**. Drag anywhere in it to move it, and drag its edges to resize. Choose **Hide projector** to send it back off screen.

If you turn off the off-screen move in **Settings**, keep the projector somewhere out of the way, such as a second monitor. It keeps working when other windows cover it, but not when it's minimized.

To quit, close the projector, or right-click the tray icon and choose **Exit**. If you'd rather the close button hide the projector and keep the app running in the tray, turn that on in **Settings**. Double-click the tray icon to bring a hidden projector back.

## Tray menu

- **Share window**: pick from the list of open windows with the mouse instead of the shortcut.
- **Pause sharing / Resume sharing** and **Stop sharing**
- **Projector size**: 1280×720, 1920×1080, or **Auto (match shared window)**, which keeps the projector the same size as the shared window, even as you resize that window. A window bigger than the projector's screen is scaled down to fit. Meet sends the projector at roughly its on-screen size, so 1080p reads best for code and slides. You can also resize it freely.
- **Show projector / Hide projector**: bring the projector back on screen, or, mid-share, send it off screen again.
- **Settings**
- **Exit**

## Settings

- **Shortcuts**: change the share and pause shortcuts. Click a box, then press the new shortcut.
- **Show the mouse pointer to viewers**
- **Closing the projector hides it to the tray instead of exiting** (off by default, so the close button exits)
- **Move the projector off screen while sharing** (on by default)

Settings are saved to `%APPDATA%\ScreenShare\settings.json`.

## Not included

The real taskbar hover button ("Share this window") is a Windows API (`ShareWindowCommandSource`) that Microsoft gates as a Limited Access Feature, so a personal app can't turn it on. The keyboard shortcut and the tray menu stand in for it.

## For developers

### Building

Requires Windows 11 (22H2 or later) and the .NET 10 SDK.

```bash
dotnet publish src/ScreenShare -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

Or run `publish.bat` from the repo root, which runs the same command. That produces `publish/ScreenShare.exe`. It needs the .NET 10 Desktop Runtime installed. Add `--self-contained true` for a larger exe that runs without it. Exit Screen Share before publishing, or the running exe can't be replaced.

The exe's icon is `src/ScreenShare/app.ico`, generated from the same monitor design the app draws at runtime. If you change the design in `AppIcon.cs`, regenerate it with `powershell -File tools/New-AppIcon.ps1`.

### How it works

- **Capture:** Windows.Graphics.Capture records the chosen window on the GPU. It works while the window is covered or on another monitor, and it handles resizes.
- **Projector:** Direct3D 11 and Direct2D draw each frame into the projector, scaled to fit with the aspect ratio kept. The swap chain uses the blt model on purpose, so every capturer can read the projector, including older GDI-based window capture.
- **Off screen:** Windows keeps drawing a shown window even when it's off every monitor, so the meeting's capture of the projector keeps running while it's parked past the desktop edge.
- **Shortcuts:** global hotkeys come from `RegisterHotKey`. The app is a normal desktop program, not a service.

### Project structure

All source is in `src/ScreenShare/`:

- `Program.cs` — entry point, the one-copy check, and the capture-support check.
- `TrayApp.cs` — tray icon and menu, hotkeys, sharing state, and projector sizing and parking.
- `ProjectorForm.cs` — the projector window: borderless mode, hotkey messages, taskbar badge, and close behavior.
- `WindowCapture.cs` — the Windows.Graphics.Capture session for the shared window.
- `Renderer.cs` — draws the latest frame, or a placeholder card, into the projector.
- `CaptureInterop.cs` — COM glue between Windows.Graphics.Capture and Direct3D 11.
- `WindowList.cs` — which windows can be shared, for the tray list.
- `Hotkey.cs`, `HotkeyBox.cs` — shortcut parsing and the box that records a new shortcut.
- `SettingsForm.cs`, `AppSettings.cs` — the settings dialog and `settings.json`.
- `AppIcon.cs`, `TaskbarList.cs` — the runtime icons and the taskbar badge.
- `NativeMethods.cs` — Win32 calls.

---

<sub>Last updated: 2026-10-29 · [commit history](https://github.com/patrickabernathy/screen-share/commits/main)</sub>

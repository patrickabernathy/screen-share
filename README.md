# Screen Share

Teams-style "share this window" switching for Google Meet (or Zoom, or anything else that can share a window) on Windows 11.

Meet makes you stop presenting and go back through its picker every time you want to show a different window. Screen Share avoids that: you share one window, the **Screen Share Projector**, once at the start of class. After that, the projector mirrors whichever window you point it at, and you switch with a global keyboard shortcut. The meeting never knows anything changed.

## Using it

1. Run `ScreenShare.exe`. A projector window opens, and an icon appears in the system tray.
2. Put the projector somewhere out of the way, such as a second monitor. It keeps working when other windows cover it, but not when it's minimized.
3. In Meet: **Present → A window → Screen Share Projector**.
4. Click any window and press **Ctrl+Alt+S**. That window is now what viewers see. Windows draws a yellow border around it, so you always know what's live.
5. Press **Ctrl+Alt+S** in another window to switch. Press **Ctrl+Alt+P** to pause, which shows a "Sharing is paused" card, and press it again to resume.

To quit, right-click the tray icon and choose **Exit**. Closing the projector only hides it, so you can't end your meeting share by accident. Double-click the tray icon to bring it back.

### Tray menu

- **Share window**: pick from the list of open windows with the mouse instead of the shortcut.
- **Pause / Stop sharing**
- **Projector size**: 1280×720 or 1920×1080. Meet sends the projector at roughly its on-screen size, so 1080p reads best for code and slides. You can also resize it freely.
- **Settings**: change both shortcuts and choose whether viewers see your mouse pointer.

Settings are saved to `%APPDATA%\ScreenShare\settings.json`.

## Building

Requires Windows 11 (22H2 or later) and the .NET 10 SDK.

```bash
dotnet publish src/ScreenShare -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

That produces `publish/ScreenShare.exe`. It needs the .NET 10 Desktop Runtime installed. Add `--self-contained true` for a larger exe that runs without it.

## How it works

- **Capture:** Windows.Graphics.Capture records the chosen window on the GPU. It works while the window is covered or on another monitor, and it handles resizes.
- **Projector:** Direct3D 11 and Direct2D draw each frame into the projector, scaled to fit with the aspect ratio kept. The swap chain uses the blt model on purpose, so every capturer can read the projector, including older GDI-based window capture.
- **Shortcuts:** global hotkeys come from `RegisterHotKey`. The app is a normal desktop program, not a service.

## Not included

The real taskbar hover button ("Share this window") is a Windows API (`ShareWindowCommandSource`) that Microsoft gates as a Limited Access Feature, so a personal app can't turn it on. The keyboard shortcut and the tray menu stand in for it.

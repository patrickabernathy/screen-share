# Screen Share

Teams-style "share this window" switching for Google Meet (or Zoom, or anything else that can share a window) on Windows 11.

Meet makes you stop presenting and go back through its picker every time you want to show a different window. Screen Share avoids that: you share one window, the **Screen Share Projector**, once at the start of class. After that, the projector mirrors whichever window you point it at, and you switch with a global keyboard shortcut. The meeting never knows anything changed.

## Using it

1. Run `ScreenShare.exe`. A projector window opens, and an icon appears in the system tray.
2. Put the projector somewhere out of the way, such as a second monitor. It keeps working when other windows cover it, but not when it's minimized.
3. In Meet: **Present → A window → Screen Share Projector**.
4. Click any window and press **Ctrl+Alt+S**. That window is now what viewers see. Windows draws a yellow border around it, so you always know what's live. While sharing is live, the tray icon and the taskbar button get a green dot. While something is shared, the projector drops its title bar so viewers see only the content, and it moves off screen so your pointer can't land on it (Meet draws the real pointer whenever it's over the shared window). It keeps streaming from there, and it comes back when you stop sharing. To see it mid-share, double-click the tray icon or choose **Show projector**; then drag anywhere in it to move it, and drag its edges to resize. You can turn off the off-screen move in **Settings**.
5. Press **Ctrl+Alt+S** in another window to switch, or press it again in the shared window to stop sharing. Press **Ctrl+Alt+P** to pause, which shows a "Sharing is paused" card, and press it again to resume.

To quit, close the projector, or right-click the tray icon and choose **Exit**. If you'd rather the close button hide the projector and keep the app running in the tray, turn that on in **Settings**. Double-click the tray icon to bring a hidden projector back.

### Tray menu

- **Share window**: pick from the list of open windows with the mouse instead of the shortcut.
- **Pause / Stop sharing**
- **Projector size**: 1280×720, 1920×1080, or **Auto**, which keeps the projector the same size as the shared window, even as you resize that window. A window bigger than the projector's screen is scaled down to fit. Meet sends the projector at roughly its on-screen size, so 1080p reads best for code and slides. You can also resize it freely.
- **Settings**: change both shortcuts, choose whether viewers see your mouse pointer, choose whether closing the projector exits or hides it to the tray, and choose whether the projector moves off screen while sharing.

Settings are saved to `%APPDATA%\ScreenShare\settings.json`.

## Building

Requires Windows 11 (22H2 or later) and the .NET 10 SDK.

```bash
dotnet publish src/ScreenShare -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

Or run `publish.bat` from the repo root, which runs the same command. That produces `publish/ScreenShare.exe`. It needs the .NET 10 Desktop Runtime installed. Add `--self-contained true` for a larger exe that runs without it.

The exe's icon is `src/ScreenShare/app.ico`, generated from the same monitor design the app draws at runtime. If you change the design in `AppIcon.cs`, regenerate it with `powershell -File tools/New-AppIcon.ps1`.

## How it works

- **Capture:** Windows.Graphics.Capture records the chosen window on the GPU. It works while the window is covered or on another monitor, and it handles resizes.
- **Projector:** Direct3D 11 and Direct2D draw each frame into the projector, scaled to fit with the aspect ratio kept. The swap chain uses the blt model on purpose, so every capturer can read the projector, including older GDI-based window capture.
- **Shortcuts:** global hotkeys come from `RegisterHotKey`. The app is a normal desktop program, not a service.

## Not included

The real taskbar hover button ("Share this window") is a Windows API (`ShareWindowCommandSource`) that Microsoft gates as a Limited Access Feature, so a personal app can't turn it on. The keyboard shortcut and the tray menu stand in for it.

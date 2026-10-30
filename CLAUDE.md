# Screen Share

Windows tray app that mirrors a chosen window into a "projector" window, so a meeting app (Google Meet, Zoom) shares the projector once and the user switches what viewers see with a global hotkey. See [README.md](README.md) for what it does from the user's side.

## Stack

- C# on .NET 10, WinForms (`net10.0-windows10.0.22621.0`, so Windows 11 22H2 or later), PerMonitorV2 DPI.
- Windows.Graphics.Capture for capture; Direct3D 11 and Direct2D through Vortice (`Vortice.Direct3D11`, `Vortice.Direct2D1`) for drawing.
- One project: `src/ScreenShare/ScreenShare.csproj`. No tests.

## Commands

- Build: `dotnet build src/ScreenShare -c Release`
- Publish the exe: `publish.bat` (same as `dotnet publish src/ScreenShare -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish`). It fails with "Access to the path ... is denied" while `publish/ScreenShare.exe` is running, so exit the app first.
- Regenerate the exe icon: `powershell -File tools/New-AppIcon.ps1` (writes `src/ScreenShare/app.ico`).

## Layout

`TrayApp.cs` owns the state (shared window, paused, source size) and wires everything together. `ProjectorForm.cs` is the window the meeting captures. `WindowCapture.cs` feeds frames to `Renderer.cs`. The README's "Project structure" section lists every file.

macOS support is planned, not started: [docs/mac-support.md](docs/mac-support.md) has the approach (one multi-targeted project, platform folders, shared controller). Read it before restructuring the project or adding cross-platform code.

## Things that are easy to get wrong

- **The projector's window handle must never change.** The meeting app captures it by handle, and hotkeys and the swap chain are bound to it too. Toggling `FormBorderStyle` (the `Borderless` property) keeps the handle; some other WinForms properties recreate it, so check before changing projector properties at runtime.
- **The swap chain is blt model (`SwapEffect.Discard`) on purpose**, so GDI-based capturers can read the projector too. Don't switch it to a flip model.
- **Minimizing or `Hide()`ing the projector stops the meeting's stream.** Moving it off screen also stopped the drawing in practice, which is why the off-screen "parking" feature was removed; don't bring it back.
- **The icon design lives in two places:** `AppIcon.cs` (runtime tray, window, and badge icons) and `tools/New-AppIcon.ps1` (the embedded `app.ico`). Change both and regenerate `app.ico`.
- **The taskbar button doesn't reliably repaint when the window icon changes**, so "sharing" is shown with an `ITaskbarList3` overlay badge (`TaskbarList.cs`), reapplied whenever `TaskbarButtonCreated` arrives.
- **Closing:** the X sends `SC_CLOSE` (`CloseReason.UserClosing`); a bare `WM_CLOSE` (Task Manager's End task) arrives as `TaskManagerClosing`. Both must shut the app down through `ExitThread` so settings save and hotkeys unregister; only a Windows shutdown is let through untouched.
- **Capture events arrive on a capture thread.** Marshal to the UI thread with `_projector.BeginInvoke` before touching forms or app state.
- Settings are JSON at `%APPDATA%\ScreenShare\settings.json` (`AppSettings.cs`). New settings need a sensible default, since older files won't have the key.

---

<sub>Last updated: 2026-10-29 · [commit history](https://github.com/patrickabernathy/screen-share/commits/main)</sub>

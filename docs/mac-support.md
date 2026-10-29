# Mac support

**Status: planned, not started.** Nothing in this doc is built yet. It's the plan for adding a macOS version, written so whoever picks it up (and whoever tests it on a Mac) doesn't have to rediscover the reasoning.

## Table of Contents

- [Goal](#goal)
- [Why it isn't a recompile](#why-it-isnt-a-recompile)
- [Approach: one project, two builds](#approach-one-project-two-builds)
- [Piece by piece](#piece-by-piece)
- [Risks to test first](#risks-to-test-first)
- [Build and release requirements](#build-and-release-requirements)
- [Phases](#phases)
- [Tester checklist](#tester-checklist)

## Goal

The same app on macOS: share the Screen Share Projector once in Meet (or Zoom), then switch which window it mirrors with a global shortcut, pause with another, and never touch the meeting's picker again. The Windows version must keep working exactly as it does now.

## Why it isn't a recompile

.NET 10 runs on macOS, but every layer of the current app is Windows-only:

- **Target and UI:** `net10.0-windows10.0.22621.0` with WinForms (projector, tray icon, settings dialog).
- **Capture:** Windows.Graphics.Capture via `combase.dll` and `d3d11.dll` (`WindowCapture.cs`, `CaptureInterop.cs`).
- **Drawing:** Direct3D 11, Direct2D, and DirectWrite through Vortice (`Renderer.cs`).
- **Win32:** `RegisterHotKey`, `EnumWindows`, `GetForegroundWindow`, DWM cloaking, and the `ITaskbarList3` badge (`NativeMethods.cs`, `WindowList.cs`, `TaskbarList.cs`).

`dotnet publish -r osx-arm64` fails at build time because of the `-windows` target. What does carry over is the logic: settings, shortcut parsing, and the share / pause / switch / stop state.

## Approach: one project, two builds

Keep a single project that multi-targets, with shared code compiled into both builds and each platform's code in its own folder. The output is still two builds: `ScreenShare.exe` on Windows and `ScreenShare.app` on macOS.

Runtime checks like `OperatingSystem.IsMacOS()` can't do the switching on their own: the Windows capture APIs only exist under the `-windows` target and the ScreenCaptureKit/AppKit bindings only under `-macos`, so no single target sees both. The split happens at build time.

### Project file

Pick the target from the machine doing the build: the Windows target on Windows, the macOS target on a Mac. If the macOS target were always listed, restore on Windows would fail because the macOS workload can't be installed there, and a plain `dotnet build` / `publish.bat` would break.

```xml
<PropertyGroup>
  <TargetFrameworks>net10.0-windows10.0.22621.0</TargetFrameworks>
  <TargetFrameworks Condition="$([MSBuild]::IsOSPlatform('OSX'))">net10.0-macos</TargetFrameworks>
</PropertyGroup>

<PropertyGroup Condition="$(TargetFramework.Contains('-windows'))">
  <OutputType>WinExe</OutputType>
  <UseWindowsForms>true</UseWindowsForms>
  <ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>
  <ApplicationIcon>app.ico</ApplicationIcon>
</PropertyGroup>

<PropertyGroup Condition="$(TargetFramework.Contains('-macos'))">
  <OutputType>Exe</OutputType>
  <SupportedOSPlatformVersion>13.0</SupportedOSPlatformVersion>
</PropertyGroup>

<ItemGroup Condition="!$(TargetFramework.Contains('-windows'))">
  <Compile Remove="Platforms\Windows\**" />
</ItemGroup>
<ItemGroup Condition="!$(TargetFramework.Contains('-macos'))">
  <Compile Remove="Platforms\Mac\**" />
</ItemGroup>

<ItemGroup Condition="$(TargetFramework.Contains('-windows'))">
  <PackageReference Include="Vortice.Direct2D1" Version="3.8.3" />
  <PackageReference Include="Vortice.Direct3D11" Version="3.8.3" />
</ItemGroup>
```

The SDK defines `WINDOWS` and `MACOS` automatically for each target, so `#if WINDOWS` / `#elif MACOS` works where a file genuinely has to differ, but prefer whole files in the platform folders over `#if` blocks.

### Layout

```
src/ScreenShare/
  AppSettings.cs, Hotkey.cs          ← shared (platform-neutral parts)
  SharingController.cs               ← shared: share / pause / switch / stop / park state, pulled out of TrayApp.cs
  IWindowCapture.cs, IProjector.cs,  ← shared interfaces the controller talks to
  IGlobalHotkeys.cs, ITrayIcon.cs,
  IWindowList.cs
  Platforms/Windows/                 ← today's code, moved behind the interfaces
  Platforms/Mac/                     ← new
```

Each platform keeps its own entry point, because WinForms (`Application.Run`) and AppKit (`NSApplication.Main`) start up differently. Both build the platform pieces and hand them to the same `SharingController`.

Native UI on each side, not Avalonia: a shared Avalonia UI would mean copying every captured frame from the GPU into an Avalonia bitmap, and on Windows it would need re-proving that every capturer can read the projector, which today depends on the blt-model swap chain (see `CLAUDE.md`).

## Piece by piece

| Today (Windows) | macOS equivalent | Notes |
|---|---|---|
| `WindowCapture.cs` + `CaptureInterop.cs` (Windows.Graphics.Capture) | ScreenCaptureKit: `SCStream` with `SCContentFilter` for a single window (`desktopIndependentWindow`) | macOS 12.3+. Needs the Screen Recording permission. `SCStreamConfiguration.ShowsCursor` maps to the "show the mouse pointer" setting. Window resize: update the stream configuration's width/height when the window's frame changes. |
| `Renderer.cs` (D3D11 + D2D swap chain) | A layer-backed `NSView`; set `CALayer.Contents` to each frame's `IOSurface` with `ContentsGravity = ResizeAspect` | Zero-copy and gives letterboxing for free. The "Sharing is paused" / idle card can be ordinary AppKit text views over a dark background. Metal only if this proves too slow. |
| `ProjectorForm.cs` (WinForms form, borderless toggle) | `NSWindow`; toggle `StyleMask` between titled and borderless | Same rule as Windows: the meeting captures this window, so it must stay the same window for the app's lifetime. Never close and recreate it to change its style. |
| `TrayApp.cs` tray icon and menu | `NSStatusItem` with an `NSMenu` (menu-bar icon) | Set `LSUIElement` in `Info.plist` if the app shouldn't also show in the Dock; decide this alongside the badge below. |
| `TaskbarList.cs` green-dot badge | `NSApplication.SharedApplication.DockTile.BadgeLabel`, or a different menu-bar icon while sharing | A Dock badge only makes sense if the app shows in the Dock. |
| `Hotkey.cs` `RegisterHotKey` | Carbon `RegisterEventHotKey` (P/Invoke into Carbon) | Doesn't need the Accessibility permission. Defaults: Ctrl+Option+S and Ctrl+Option+P mirror the Windows ones; `HotkeyBox.cs` needs Mac modifier names (⌃⌥⇧⌘). |
| `GetForegroundWindow` (which window to share on the hotkey) | `NSWorkspace.SharedWorkspace.FrontmostApplication`, then the first layer-0 window for that PID from `CGWindowListCopyWindowInfo` (front-to-back order) | Match it to an `SCWindow` by window ID. |
| `WindowList.cs` (`EnumWindows`, cloaking, tool windows) | `SCShareableContent.GetShareableContent`, filtered to on-screen, layer-0, titled windows that aren't ours | |
| `SettingsForm.cs` | An AppKit settings window | Same four settings. |
| `AppSettings.cs` path `%APPDATA%\ScreenShare\settings.json` | `~/Library/Application Support/ScreenShare/settings.json` | Make the path platform-aware; the JSON stays the same. |
| One-copy check in `Program.cs` | A named lock file or `NSRunningApplication` check by bundle ID | |
| Windows' yellow capture border on the shared window | None. macOS shows its own screen-recording indicator in the menu bar | The menu-bar icon change has to carry "what's live" on its own. |

## Risks to test first

Prove these with a throwaway test app on a real Mac before building anything else. Either one can change the design.

1. **Parking the projector off screen.** On Windows the projector moves past the desktop edge while sharing so the presenter's pointer can't land on it, and Windows keeps drawing it. On macOS, check both: can a borderless window be placed fully off every screen (titled windows get pulled back on screen), and does Meet in Chrome keep receiving frames from it while it's there? If either fails, the fallback is keeping the projector on a second display or in a corner, and the "Move the projector off screen" setting becomes Windows-only.
2. **Screen Recording permission prompts.** ScreenCaptureKit needs the permission, and recent macOS versions re-ask periodically for apps that capture without going through the system's own picker. Check how often a tester actually gets re-prompted and whether that's tolerable mid-class. The system picker (`SCContentSharingPicker`) avoids it but brings back a picker step, which is what the app exists to remove.
3. **Capturing a covered or other-display window.** Windows.Graphics.Capture keeps capturing a window that's covered or on another monitor. Confirm a single-window `SCContentFilter` does the same, and note what happens when the shared window is minimized.

## Build and release requirements

- **A Mac with Xcode** to build, run, and sign. The macOS workload (`dotnet workload install macos`) doesn't build on Windows. A GitHub Actions `macos-latest` runner can produce builds, but someone still has to run them on a Mac.
- **Code signing and notarization** so macOS opens the app without "unidentified developer" warnings. That needs an Apple Developer account. Unsigned builds work for a tester who right-clicks → Open, but not for general release.
- **`Info.plist`:** bundle ID, `LSUIElement` (if menu-bar only), and `NSScreenCaptureUsageDescription` text for the permission prompt.
- **Windows build commands don't change.** With the macOS target only chosen on a Mac, `publish.bat` and `dotnet build src/ScreenShare` keep working as-is.

## Phases

1. **Spike on a Mac (no repo changes):** the three risks above, with the smallest possible test app.
2. **Refactor on Windows (no behavior change):** add the interfaces and `SharingController`, move today's code into `Platforms/Windows/`, and switch to `TargetFrameworks` with the per-OS condition. Ship it as a normal Windows release to prove nothing changed.
3. **Mac capture + projector:** `Platforms/Mac/` capture and projector window, sharing a fixed window picked from a list.
4. **Mac hotkeys, menu-bar icon, settings, one-copy check.**
5. **Signing, notarization, and a release `.app`/`.dmg`**, plus README updates for the Mac download and permissions.

Phases 1 and 2 are independent: 2 can be done on Windows any time without a Mac.

## Tester checklist

For whoever tests on a Mac:

- [ ] macOS version and chip (Apple silicon or Intel)
- [ ] First launch: Screen Recording prompt appears, granting it works, and the app recovers after the restart macOS asks for
- [ ] In Meet (Chrome): Present → A window → Screen Share Projector shows the mirrored window
- [ ] Ctrl+Option+S in another app switches what viewers see; again in the shared app stops sharing
- [ ] Ctrl+Option+P shows the "Sharing is paused" card and resumes
- [ ] The shared window covered by another window, and moved to a second display: viewers still see it
- [ ] The shared window resized mid-share: no cropping or stretching
- [ ] Projector parked off screen: viewers still see frames, and the presenter's pointer never appears on it
- [ ] "Show the mouse pointer to viewers" on and off
- [ ] Menu-bar icon shows when something is shared
- [ ] Quitting from the menu-bar icon and from the projector's close button
- [ ] How often macOS re-asks for Screen Recording over a week of use

---

<sub>Last updated: 2026-10-29 · [commit history](https://github.com/patrickabernathy/screen-share/commits/main)</sub>

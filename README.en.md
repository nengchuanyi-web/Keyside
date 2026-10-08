<p align="center"><img src="docs/images/banner.svg" alt="Keyside — shortcuts, always at your side" width="100%"></p>

<p align="center">
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-7a8fb8" alt="MIT License"></a>
  <a href="https://github.com/nengchuanyi-web/Keyside/releases/latest"><img src="https://img.shields.io/github/v/release/nengchuanyi-web/Keyside?color=8c9fc2" alt="Latest release"></a>
  <img src="https://img.shields.io/badge/Windows-10%20%2F%2011-9bb4de" alt="Windows 10 / 11">
  <img src="https://img.shields.io/badge/local-offline-a8bea0" alt="Local and offline">
</p>

<p align="center"><b>Your everyday shortcuts, always at your side.</b></p>
<p align="center"><a href="https://github.com/nengchuanyi-web/Keyside/releases/latest"><b>Download for Windows</b></a> · <a href="README.md">简体中文</a></p>

Keyside is a local Windows panel for organizing and looking up keyboard shortcuts. Dock it to any screen edge, hover the narrow edge strip to reveal it, and move away or click outside to hide it. Keep a named tab for each app, with shortcuts, actions and your own notes in one place.

> Download `Keyside-v0.6-win-x64.zip`, **extract the entire archive and double-click `Keyside.exe`**. No installer or .NET SDK is required.

## Preview

<table>
  <tr><td align="center"><b>Glass over a dark backdrop</b></td><td align="center"><b>Glass over a light backdrop</b></td></tr>
  <tr>
    <td><img src="docs/preview-v0.6.png" alt="Glass panel with opaque text and emoji" width="380"></td>
    <td><img src="docs/preview-v0.6-light.png" alt="Glass panel adapting to a light backdrop" width="380"></td>
  </tr>
</table>

Actual v0.6 window captures over dedicated test backgrounds. Transparency affects the background only; text and emoji stay opaque. The glass layer is blurred independently and text contrast adapts to backdrop brightness.

## Features

| | What it does |
| --- | --- |
| Edge docking | Dock to the left, right, top or bottom; hover to reveal, leave to hide, click outside to hide immediately. Floating mode is also available. |
| App tabs | Create and name software tabs; search shortcuts, actions and notes; pin frequently used entries to the top. |
| Adjustable columns | Drag dividers for all three columns: shortcut / action / notes. Width proportions persist. Collapse notes without losing their contents. |
| Emoji notes | Double-click to edit, including empty cells. Multiline notes and hover previews, with offline color rendering for Unicode Emoji 17.0. |
| Appearance | Glass, light, dark and wallpaper-brightness themes; soft blue, pink or green accents; 0–65% background transparency and 80–150% text size. |
| Resize and lock | Resize from any edge or corner. Pin the panel to keep it open and lock editing; search and navigation remain available. |
| Import and backup | Preview TXT imports, create or append to tabs, skip duplicates; save a TXT template; export and restore complete JSON backups. |
| Languages | Chinese and English UI. Editable Photoshop and VS Code examples are included. |

Entries are reference text. Clicking a shortcut does not send keyboard input to another app.

## Quick start

1. Download the Windows x64 portable ZIP from [Releases](https://github.com/nengchuanyi-web/Keyside/releases/latest), extract it and run `Keyside.exe`.
2. Drag the title bar near a screen edge. Hover the thin edge strip to reveal the panel; click outside to hide it.
3. Use `+` to create app tabs. Double-click cells to edit and right-click entries to manage or pin them. Change appearance in Settings.

The pin button keeps the panel expanded and locks editing. Click it again to unlock. The system tray can show the panel or exit the app.

### TXT imports

Use `···` → **Import shortcuts TXT**. Each file represents one app's shortcuts, with one entry per line:

```text
Ctrl + N@New document
Ctrl + O@Open file
Ctrl + S@Save
```

UTF-8 is recommended; UTF-16 with BOM and GB18030 are also supported. Preview the file before creating a tab or appending to an existing one. TXT does not contain notes or pins; use JSON backups for complete migration. The menu's **Save TXT template** action saves an example; [Template.txt](examples/Template.txt) is also included.

## Requirements and privacy

- Windows 10 / 11 x64 and .NET Framework 4.8. Captured glass requires Windows 10 version 2004 or newer; older systems use a compatibility appearance.
- Local and offline. Data lives in `%LOCALAPPDATA%\Keyside`, with atomic saves and a previous valid backup.
- No global keyboard recording or automatic startup installation. Glass mode samples the area behind the panel in memory to draw the backdrop; sampled images are not saved to disk.
- This is an unsigned MVP built and verified on Windows 11. Windows 10 and physical mixed-DPI setups still need further manual testing.

See the [full usage guide](docs/USAGE.zh-CN.md), which includes an English quick start, data and compatibility details.

## Build

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1

# Optional isolated verification (displays test windows)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Verify

# Package portable and source ZIPs
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\package.ps1
```

Alternatively, double-click `Build.cmd`. Run `dist\Keyside.exe` or `Start.cmd` after building. The build uses the system .NET Framework compiler; no .NET SDK, Node, Python or NuGet is required.

All **200 v0.6 checks passed**, covering docking, outside clicks, persistence, editing locks, column resizing, emoji and native glass composition. See the [verification report](docs/verification-v0.6.txt), [architecture](docs/Architecture.md) and [changelog](CHANGELOG.md). This is not a guarantee of compatibility on every device.

## Credits and license

Edge interaction was inspired by [Afterhours](https://github.com/Tokaku7/Afterhours). The separated glass background and project presentation were inspired by [Glance](https://github.com/lulu-loopp/glance). Keyside is independently implemented in C# / WPF; its screenshots and banner show this project.

Application source code is licensed under [MIT](LICENSE). [Twemoji 17.0.3](https://github.com/jdecked/twemoji/tree/v17.0.3) artwork is CC BY 4.0, and Unicode data uses Unicode License v3. Third-party artwork and trademarks retain their respective rights and licenses; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

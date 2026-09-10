<div align="center">

<img src="docs/icon-portrait.png" width="72" alt="">

# Portable Monitor Rotate

**One click in the tray flips your portable monitor between landscape and portrait — and it stays exactly where you put it.**

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%2F%2011-0078D4)](#)
[![Download](https://img.shields.io/github/v/release/Imchiajui/portable-monitor-rotate?label=download)](https://github.com/Imchiajui/portable-monitor-rotate/releases/latest)
[![Size](https://img.shields.io/badge/size-~28%20KB-brightgreen)](#)

[繁體中文說明](README.zh-TW.md) · [Download the .exe](https://github.com/Imchiajui/portable-monitor-rotate/releases/latest)

</div>

---

## The problem

Portable USB-C monitors are great in portrait mode — code, documents, chat, logs. But most of them have **no orientation sensor**, so Windows will never auto-rotate them. You are stuck opening **Settings → System → Display**, picking the monitor, changing the orientation dropdown, and confirming. Every single time.

And then Windows moves it. Rotate a 1920×1080 monitor to 1080×1920 and its position in the virtual desktop changes shape, so Windows reflows the layout and drops your monitor somewhere new. You set it to sit on the left, level with your laptop; after one rotation it is floating half a screen too high.

This is a 28 KB tray utility that fixes both halves of that.

![How the layout is preserved](docs/layout.svg)

## What it does

- **Click the tray icon** to toggle landscape ↔ portrait. That's it.
- **The icon shows the current state** — a wide monitor or a tall one.
- **Position is preserved.** The monitor keeps the side you put it on (left / right / above / below) and stays flush against the shared edge, through every rotation, unplug and reconnect.
- **Remembers across disconnects.** Unplug the monitor, the icon disappears; plug it back in, the icon returns *to the same slot in the tray* and the monitor returns to the same place on your desk.
- **The pointer stays put.** Changing display mode normally warps your cursor to the centre of the primary screen. It doesn't here.
- **English or Traditional Chinese interface**, following Windows by default.
- **Scriptable** via CLI and a named pipe, so you can drive it from AutoHotkey, a Stream Deck, or your own hardware.
- **No installer, no dependencies, no telemetry, no background network access.** A single .exe on .NET Framework 4.x, which ships with Windows.

## Install

1. Download `MonitorRotateTray.exe` from the [latest release](https://github.com/Imchiajui/portable-monitor-rotate/releases/latest).
2. Put it somewhere permanent — for example `C:\Users\<you>\Tools\MonitorRotateTray\`.
3. Run it. Right-click the tray icon → **Start with Windows** if you want it always on.

> **Pick the folder before you enable autostart.** Two things bind to the executable's full path: the Windows autostart entry, and the tray icon's remembered position. Moving the .exe later breaks both — autostart silently stops working, and the icon resets to the overflow area once.

> **SmartScreen.** The binary is not code-signed, so Windows shows a blue "Windows protected your PC" screen on first run. Click **More info → Run anyway**, or [build it yourself](#build-from-source) — it's one file and one command. Every release lists its SHA-256 so you can verify what you downloaded.

## Using it

| Action | Result |
|---|---|
| **Left click** the tray icon | Toggle between the two orientations |
| **Right click** | Menu: all four angles, target monitor, position & alignment, language, autostart |
| Hover | Monitor name and current orientation |

The interface follows the Windows display language — English elsewhere, Traditional Chinese on a Chinese system. Override it under **Language** in the right-click menu.

### Choosing the monitor

It finds the right screen on its own, trying three rules in order:

1. **EDID hardware id** (e.g. `CND30FE`) — this identifies the *model*, not one particular unit, so any monitor of the same model matches, on any port, whatever number Windows assigns it.
2. **Friendly-name substring** (default `MSI`) — any non-primary display whose EDID name contains it.
3. **Any external display** — last resort.

Rule 1 is filled in automatically the first time you run it. Pick a different screen any time from the right-click menu.

### Position and alignment

Two monitors always share an edge exactly — no gaps, no diagonal offsets.

- **Side**: left, right, directly above, directly below.
- **Alignment** along that edge. The default is automatic: **side by side → bottom edges level** (they sit on the same desk), **stacked → horizontally centred**. You can override it.

Drag the monitor somewhere else in Windows Settings and the app adopts your new arrangement — it won't fight you.

### Scripting

The tray app listens on a named pipe, so anything can drive it:

```
\\.\pipe\MonitorRotateTray
```

Send one line: `toggle`, `landscape`, `portrait`, `0`–`3`, `layout`, `show`, `diag`, `quit`.

The executable is also its own client — run it with an argument and it forwards the command:

```bat
MonitorRotateTray.exe toggle
MonitorRotateTray.exe portrait
MonitorRotateTray.exe layout    :: force the monitor back to its remembered position
MonitorRotateTray.exe diag      :: write a full state snapshot to the log
```

<details>
<summary>PowerShell example</summary>

```powershell
$pipe = New-Object System.IO.Pipes.NamedPipeClientStream('.', 'MonitorRotateTray', 'Out')
$pipe.Connect(2000)
$writer = New-Object System.IO.StreamWriter($pipe)
$writer.WriteLine('toggle')
$writer.Flush()
$pipe.Dispose()
```

</details>

This is deliberately the whole integration surface. If you ever bolt a real orientation sensor onto the monitor — a small BLE accelerometer puck, say — it only has to write one word into that pipe.

### Configuration

`%APPDATA%\MonitorRotateTray\config.ini`, written by the menu — edit by hand if you prefer.

| Key | Meaning |
|---|---|
| `TargetHardwareId` | EDID vendor+product code of the monitor to rotate |
| `TargetNameMatch` | Fallback: substring of the EDID friendly name |
| `FallbackToExternal` | `1` = last resort, use any non-primary display |
| `OrientA` / `OrientB` | The two orientations the left click toggles between (`0`=0°, `1`=90°, `2`=180°, `3`=270°) |
| `Placement` | `0`=left `1`=right `2`=above `3`=below |
| `Align` | `-1`=auto, `0`=start, `1`=centre, `2`=end |
| `KeepLayout` | Re-apply the position on rotate and reconnect |
| `LearnPlacement` | Adopt the arrangement you set in Windows Settings |
| `HideWhenDisconnected` | Hide the tray icon while the monitor is away |
| `Language` | `auto` (follow Windows), `en`, or `zh-TW` |

A state snapshot is written to `%APPDATA%\MonitorRotateTray\last-run.log` on every start and on `diag` — attach it if you file an issue.

## Build from source

No SDK, no NuGet, no project file. The C# compiler that ships with Windows is enough:

```bat
build.cmd
```

Or directly:

```bat
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /optimize+ ^
  /out:MonitorRotateTray.exe ^
  /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Core.dll ^
  src\MonitorRotateTray.cs
```

> The source file is UTF-8 **with BOM** on purpose. Without it `csc` decodes non-ASCII text using the system ANSI code page, and the interface strings come out as mojibake on non-English Windows.

## How it works

Three Win32 details do most of the work. Each one is a trap that is easy to fall into and confusing to debug, so they're worth writing down.

<details>
<summary><b>A stable tray position needs a GUID, not a window handle</b></summary>

Windows remembers where a tray icon lives — pinned on the taskbar or tucked in the overflow — keyed by the icon's identity. `NOTIFYICONDATA` offers two identities: `(hWnd, uID)`, or a `GUID` via the `NIF_GUID` flag.

WinForms' `NotifyIcon` only ever uses `(hWnd, uID)`. Both change every time the process starts, so Windows sees a brand-new icon on each launch and dumps it back in the overflow area. That's why so many tray apps refuse to stay where you drag them.

Calling `Shell_NotifyIcon` directly with a fixed `GUID` fixes it. The position then survives hiding the icon, showing it again, and restarting the app.

The catch: Windows binds that GUID to the executable's **full path**. Move the .exe and `NIM_ADD` fails. The code detects that, clears the stale registration, retries, and falls back to `uID` mode rather than showing no icon at all.

</details>

<details>
<summary><b>Version 4 tray icons deliver every click twice</b></summary>

After `NIM_SETVERSION` with `NOTIFYICON_VERSION_4`, a single left click on the icon sends **four** messages:

```
WM_MOUSEMOVE → WM_LBUTTONDOWN → WM_LBUTTONUP → NIN_SELECT
```

Handle both `WM_LBUTTONUP` and `NIN_SELECT` — which looks like reasonable defensive coding — and every click fires your action twice. For a toggle that means the screen rotates and instantly rotates back, so the app looks completely dead while actually doing its job perfectly, twice.

Under version 4 only the `NIN_*` events count. The raw mouse messages are the fallback for when `NIM_SETVERSION` fails. Same story on the right: `WM_CONTEXTMENU` **and** `WM_RBUTTONUP` both arrive, and acting on both pops the menu twice.

</details>

<details>
<summary><b>Orientation and position must be one atomic change</b></summary>

Rotating a display changes its width and height. Apply that alone and Windows reflows the desktop to remove the overlap it just created — putting your monitor wherever it likes.

So orientation *and* position go into the same `DEVMODE`, staged with `CDS_UPDATEREGISTRY | CDS_NORESET`, then committed with a single `ChangeDisplaySettingsEx(NULL, NULL, NULL, 0, NULL)`. Without staging, the new position can be rejected as an overlap while the old size is still in effect.

Hotplug needs more care still. Reconnecting a monitor is not atomic: Windows parks it somewhere arbitrary, fires several `WM_DISPLAYCHANGE` events, and may reflow *again* after you have acted. An app that reads the geometry during that window and treats it as the user's intent will slowly corrupt its own memory of the layout. So geometry is only trusted after it has held still for 1.5 s, and the layout is re-asserted for 12 s after a reconnect in case Windows moves it back.

</details>

<details>
<summary><b>Bonus: keeping the mouse pointer still</b></summary>

Any display mode change warps the cursor to the centre of the primary display. Save it with `GetCursorPos` before, restore with `SetCursorPos` after — then restore it once more after the message queue drains, because Windows moves it again as the desktop finishes reconfiguring. Only restore if the old point still lands inside some display.

</details>

## Does my monitor really have no sensor?

This started as a hardware question: the [MSI PRO MP161 E6T](https://www.msi.com/) is a touch panel, so it has a USB data path to the PC — maybe there's an accelerometer in there that Windows simply isn't using?

Three checks answered it, and you can run the same ones on your own monitor:

1. **Windows sensor stack.** `Get-PnpDevice -Class Sensor` — empty, including hidden and disabled devices, and the WinRT `Accelerometer` / `Gyrometer` / `SimpleOrientationSensor` / `Inclinometer` classes all return nothing.
2. **HID report descriptors.** Enumerate every HID collection the monitor exposes and read its usage page via `HidP_GetCaps`. On this panel: a digitizer (touch), a mouse, two device-configuration collections and one vendor-defined channel. **No usage page `0x20` (Sensors)** — that page is the only standard way a monitor could report orientation.
3. **DDC/CI.** The MCCS capability string lists every VCP code the monitor implements. `0xAA` *Screen Orientation* is absent, and reading it directly returns unsupported.

A quick zero-risk version of the same test, no code required: turn the monitor to portrait and open its **OSD menu**. If the on-screen menu rotates to stay upright, there is a G-sensor driving the scaler. If the menu lies on its side, there isn't.

So the sensor genuinely isn't there — and a tray toggle is the honest fix.

## Tested on

Built and used daily on **Windows 11 Pro** with a **ThinkPad X1 Carbon Gen 8** driving an **MSI PRO MP161 E6T** over USB-C DisplayPort alt mode.

Nothing in it is specific to that hardware — the monitor is found by EDID with generic fallbacks, and it should work with any external display on Windows 10 or 11. Reports from other setups are very welcome; please include `last-run.log`.

## Contributing

Issues and pull requests are welcome. Useful things to report:

- Monitors or laptops where the target isn't detected (include `last-run.log`)
- Layouts the position logic gets wrong
- Multi-monitor setups with three or more displays — currently the target is placed relative to the primary only

## License

[MIT](LICENSE)

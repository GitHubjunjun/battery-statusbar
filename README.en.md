<div align="right">

**简体中文** | [English](README.en.md)

</div>

# Battery Statusbar · Desktop Battery Bar

A floating battery status bar for the Windows desktop. Frosted-glass rounded corners with a low-saturation palette, implemented as a single-process C# / WinForms (.NET 8) app — built to be **extremely lightweight**: a framework-dependent single file of ~180KB with near-zero idle CPU, and working set trimmed periodically via `EmptyWorkingSet`.

![screenshot](docs/screenshot.png?v=2)

## Features

### Display
- **Laptop / HID peripherals / Bluetooth headset** battery on one bar, sorted by charge
- **Solves the pain point that 2.4G wireless keyboards/mice show no battery anywhere** — vendor HID protocols reverse-engineered so even dongle devices invisible to the OS can be displayed (see below)
- Devices disappear from the list when disconnected and reappear when connected
- Window height adapts to the number of device rows (top edge fixed, bottom edge expands)
- Name column width adapts (widest name wins; progress bars start and end perfectly aligned)
- Battery **<30%** turns that row's bar and percentage **dark red** (subtle, not flashy)

### Background
- Frosted-glass background taken **live from the desktop wallpaper** (`PrintWindow` on the Progman desktop + box blur with edge expansion to kill border artifacts)
- Dark scrim adapts to wallpaper luminance: strong scrim on bright wallpapers, nearly none on dark ones, keeping text readable
- 32bpp pre-multiplied alpha layered window (`UpdateLayeredWindow`) — borderless, per-pixel transparency

### Interaction
- **Drag** to move; live center snapping while dragging (no need to finish dragging first)
- **Bottom-right resize grip** (0.7× ~ 1.8×); transparent pixels never block the mouse
- **Win+D does not hide it** (250ms watchdog restores visibility)
- Tray icon menu:
  - Show / Hide (double-click the tray icon does the same)
  - **Refresh** (rescan all devices immediately, no waiting for the interval)
  - Auto-hide when windows are maximized
  - Launch at startup (writes to the user startup folder)
  - Exit

### Refresh policy (power friendly)
| Laptop battery | Interval |
|---|---|
| ≥90% | 10 min |
| 60% ~ 90% | 3 min |
| <60% | 1 min |

## Build & Run

### Requirements
- Windows 10 19041+ / Windows 11
- .NET 8 Desktop Runtime (framework-dependent; switch to self-contained for fully offline use)

### Build
```powershell
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

Output: `bin/Release/net8.0-windows10.0.19041.0/win-x64/publish/StatusBar.exe`

Just double-click to run — no installer, no background service. Enable auto-start from the tray menu.

> Note: the TFM must be `net8.0-windows10.0.19041.0` (with the Windows version). Otherwise WinRT projection is unavailable and namespaces like `Windows.Devices.Enumeration` cannot be referenced.

## Where the battery data comes from

For each device type the bar picks "the best data source the OS already knows", with layered fallbacks:

| Device type | Source | Notes |
|---|---|---|
| Laptop battery | `GetSystemPowerStatus` (Win32) | Always available |
| MCHOSE K87S keyboard | Vendor HID Feature protocol (SetupAPI + hid.dll) | Reverse-engineered from the vendor's software |
| Rapoo VT3 MAX mouse | Vendor HID report protocol (write col05 / read col09) | Same |
| Huawei FreeBuds series | **PnP-level HFP battery property** | See below — same data source as the Settings page |
| Other BLE devices | GATT Battery Service (0x180F / 0x2A19) | Standard Bluetooth battery service |

### Why is headset battery "unreadable"?

Most guides tell you to read `System.Devices.BatteryLife` — but that property is usually empty at the AEP / interface layer. In practice, for classic Bluetooth audio devices like Huawei headsets, the battery actually lives at the **PnP device layer** (`DeviceInformationKind.Device`):

- Device node: `BTHENUM\{0000111e-...}` (the Hands-Free service interface)
- Property key: `{104EA319-6EE2-4701-BD47-8DDBF425BBE5},2` (DEVPKEY_Bluetooth namespace, pid=2)
- How to read: `DeviceInformation.FindAllAsync("", props, DeviceInformationKind.Device)`, then merge into the Bluetooth list by name prefix

This is exactly the same data the Windows "Settings → Bluetooth & devices" page shows. This project is a complete implementation of that approach.

## The pain point: 2.4G wireless keyboards/mice have nowhere to show battery

Probably the trickiest part of building a desktop battery display — and the most valuable part of this project:

**Bluetooth devices are the easy case** — since Windows 10 1809, the Settings page shows battery for Bluetooth devices (standard HFP / GATT protocols), and third-party apps can read the same data at the PnP layer.

**But keyboards/mice on a 2.4G USB dongle are a different world entirely**:

1. **Zero OS-level display** — Windows shows no battery at all for 2.4G dongles: not in Settings, not in the taskbar. The only way is launching the vendor's bloatware
2. **No standard interface** — these dongles generally don't implement the standard HID battery Usages (Usage Page 0x85/0x86); the descriptor simply has no "battery" field. Measured in this project: all 6 HID collections of the MCHOSE K87S report `feat_len=0` (nothing readable via feature reports); all 9 collections of the Rapoo VT3 MAX are Consumer/System/Vendor (0xFF00), with battery data buried in the private Vendor page
3. **Proprietary, undocumented protocols** — every vendor (MCHOSE / Rapoo / Dareu / Logitech…) uses a different dongle wire format; packet-capture reverse engineering is the only way. For Rapoo, the only open-source implementation is the Linux kernel module `hid_rapoo` (13-byte input report, byte[8] = battery %), which depends on a keyboard interface that doesn't exist on Windows — not portable
4. **Vendor software is closed and offers no API** — the only place to see the battery is the vendor's own Windows app (e.g. MCHOSE HUB); no API whatsoever, and you must keep a whole suite resident just to see a percentage

**What this project does**: for the MCHOSE K87S and Rapoo VT3 MAX, it reverse-engineers the vendor software / web-driver HID protocols and implements them natively — including pitfalls like unnumbered 64-byte reports and Windows `WriteFile` requiring 65 bytes (first byte as the Report ID placeholder) — so 2.4G devices appear on the same bar as laptops and Bluetooth devices, with zero vendor software required.

Other 2.4G models need their own protocol implemented in `Program.cs` (`ReadK87` / `ReadRapoo`). PRs welcome.

## Configuration files

Generated automatically next to the exe; editable by hand:

| File | Purpose |
|---|---|
| `statusbar_cs.json` | Position (`x`/`y`) and scale — saved automatically after drag/resize |
| `statusbar_cs_last.json` | Last valid peripheral battery cache, reused while a device is briefly offline |

`debug.log` records every scan (Bluetooth AQS results, PnP merges, interval decisions) — handy when troubleshooting a device.

## Project layout

```
StatusBarCs/
├── Program.cs          # Main: layered-window rendering, HID reads, interaction, tray, watchdog
├── Bt.cs               # Bluetooth battery: AEP / PnP / GATT three-layer reads and merging
├── StatusBarCs.csproj  # net8.0-windows10.0.19041.0 / WinForms / x64
└── app.manifest        # PerMonitorV2 DPI awareness
```

## FAQ

**Q: Why doesn't my Bluetooth headset show up?**
A: Three checks — ① make sure it's connected (disconnected devices are hidden); ② check whether the Windows Settings page shows a battery percentage for it (if even Settings doesn't, the device doesn't speak a standard protocol and this project cannot read it); ③ hit "Refresh" in the tray menu to force a rescan.

**Q: Can it show the charging case / individual earbud batteries?**
A: No. Standard Windows interfaces (HFP/GATT) only provide a single aggregated level. Case/bud levels use vendor-proprietary protocols (Huawei PC Manager uses a private channel) — out of scope unless the vendor protocol is reverse-engineered.

**Q: How much resources does it use?**
A: Framework-dependent single file ~180KB; idle CPU ≈ 0 (shortest refresh interval is 1 minute, working set trimmed after every scan); memory stays in the few-MB range after `EmptyWorkingSet`.

**Q: Does it conflict with Wallpaper Engine or similar?**
A: No. The background is sampled from the rendered desktop (Progman surface); after changing wallpapers it follows automatically within 60 seconds.

## License

For learning and personal use.

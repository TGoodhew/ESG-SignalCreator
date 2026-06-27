# Packaging — Windows installer

ESG-SignalCreator ships as an **MSI** built with the **[WiX Toolset](https://wixtoolset.org/) v5**.

## Why WiX v5

- **Free & open-source** (MS-RL), the de-facto standard for authoring Windows Installer (MSI) packages.
- **First-class .NET Framework / WinForms support** and clean MSI semantics: stable `UpgradeCode`,
  major-upgrade handling, proper Add/Remove Programs (ARP) entry, advertised shortcuts.
- **No machine setup required to build**: the WiX v5 SDK is restored from NuGet by `dotnet build`
  (no global tool install). The installer project is deliberately **not** part of
  `ESG-SignalCreator.sln`, so a developer without WiX can still build and run the app.

Alternatives considered: MSIX (sandboxing/identity friction with a desktop app that loads an
externally-installed VISA provider), Inno Setup (great, but not MSI/ARP-native). WiX won on
quality + zero-cost + CI-friendliness.

## What gets installed

| Item | Notes |
|------|-------|
| `ESG-SignalCreator.exe` | The WinForms app; Start-menu shortcut (+ optional desktop shortcut) |
| `ESG-SignalCreator.Core.dll` | Core library |
| `ESG-SignalCreator.exe.config` | App config / binding redirects |
| ARP entry | Name, version, publisher (Tony Goodhew), icon, help/about links |

Installs **per-machine** to `C:\Program Files\ESG-SignalCreator\`. Major-upgrade replaces any prior
version; uninstall removes all files, shortcuts, and the Start-menu folder.

The **NI/IVI VISA assemblies are intentionally not redistributed** — they belong to the installed
VISA runtime (see prerequisites).

## Prerequisites

- **.NET Framework 4.7.2+** — a blocking launch condition (release key ≥ 461808) with a clear message.
- **A VISA runtime** (for live instrument I/O) — detected (vendor-neutral, via the IVI Foundation
  VISA shared-components registry key) and recorded, but **not** blocking: the app builds/exports
  waveforms offline and reports a clear error if you try to connect without VISA. Any IVI-compliant
  VISA provider works — **Keysight IO Libraries Suite, NI-VISA, Rohde & Schwarz, Rigol, …** — once
  the app's transport uses the vendor-neutral IVI VISA.NET shared components (see *Known limitation*).

## Building the installer

```powershell
# From the repo root. Builds the solution in Release, then the MSI.
./build-installer.ps1 -Version 1.0.0.0
```

Or directly (after building the app in Release):

```powershell
dotnet build ESG-SignalCreator.Installer\ESG-SignalCreator.Installer.wixproj -c Release `
    -p:ProductVersion=1.0.0.0 -p:HarvestPath=ESG-SignalCreator.App\bin\Release
```

The artifact lands in `ESG-SignalCreator.Installer\bin\…\ESG-SignalCreator-<version>.msi`.

### Options

- `INSTALLDESKTOPSHORTCUT=0` (msiexec property) skips the desktop shortcut.
- The MSI is x64 (per-machine, Program Files).

## Code signing (optional)

Authenticode signing is supported out-of-band: sign `ESG-SignalCreator.exe` before harvesting and
sign the produced `.msi` with `signtool` (certificate supplied separately). A signing hook can be
added to `build-installer.ps1` when a cert is available.

## Known limitation — vendor-neutral VISA

The app currently binds VISA I/O to **NI-VISA** (`NationalInstruments.Visa`). Making it work with any
IVI-compliant VISA provider (Keysight/NI/R&S/Rigol/…) via the IVI `GlobalResourceManager` is tracked
as a separate code issue. The installer is already vendor-neutral in how it *detects* VISA.

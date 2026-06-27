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
  VISA provider works — **Keysight IO Libraries Suite, NI-VISA, Rohde & Schwarz, Rigol, …** — the
  app's transport uses the vendor-neutral IVI VISA.NET shared components (`GlobalResourceManager`).

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

## Continuous release (GitHub Actions)

`.github/workflows/release.yml` builds the MSI and publishes a GitHub Release automatically:

| Trigger | Result |
|---------|--------|
| Push to `main` (or manual *Run workflow*) | **Prerelease** tagged `v1.0.<run_number>.0` — every update produces a build |
| Push a tag `vX.Y.Z` | **Stable release** using that tag |
| Manual run with a `version` input | Stable release at that version |

Each run: builds the solution in Release, runs the unit tests, builds the MSI (WiX restored from
NuGet), and uploads the `.msi` to the release with its SHA256.

### Build runner

Since #102, `Core` references only the **IVI VISA.NET Shared Components** (`Ivi.Visa`) by `HintPath` —
no NI-specific assemblies. So the build runs on any Windows runner (MSBuild + .NET SDK) that has the
**IVI VISA.NET Shared Components** installed; those ship with *any* VISA provider (Keysight IO
Libraries, NI-VISA, R&S, Rigol, …), so a lab machine that already talks to instruments works as a
**self-hosted** runner with no extra setup:

1. GitHub → repo **Settings → Actions → Runners → New self-hosted runner** (Windows).
2. Follow the steps; give it the labels **`self-hosted`** and **`windows`**.
3. Run it as a service so releases build unattended.

> A GitHub-hosted runner can also work if a step installs the IVI VISA.NET Shared Components (or the
> reference is switched to the `Ivi.Visa` NuGet package); the runtime still needs an installed VISA
> provider, which is why a VISA-equipped self-hosted runner is the simplest choice.

## Code signing (optional)

Authenticode signing is supported out-of-band: sign `ESG-SignalCreator.exe` before harvesting and
sign the produced `.msi` with `signtool` (certificate supplied separately). A signing hook can be
added to `build-installer.ps1` when a cert is available.

## Vendor-neutral VISA

The app talks to instruments through the **IVI `GlobalResourceManager`**, so it works with any
IVI-compliant VISA provider (Keysight IO Libraries, NI-VISA, R&S, Rigol, …) for TCPIP/LAN, GPIB, USB
and serial — no vendor-specific assemblies are referenced (#102, bench-verified on an E4438C including
the byte-exact ARB download). The installer detects VISA vendor-neutrally too.

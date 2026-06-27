# Packaging — Windows installer

ESG-SignalCreator ships in two forms, both built with the **[WiX Toolset](https://wixtoolset.org/) v5**:

| Artifact | Use |
|----------|-----|
| **`ESG-SignalCreator-Setup-<version>.exe`** | **Recommended.** A Burn *bootstrapper* that chains the **.NET Framework 4.7.2** redistributable ahead of the MSI — it installs the framework automatically if it's missing, then the app. Self-contained (the MSI is embedded); the .NET redist downloads from Microsoft on demand. |
| **`ESG-SignalCreator-<version>.msi`** | The raw package, for machines that already have .NET Framework 4.7.2 (or deploy it via SCCM/Intune/GPO). |

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

- **.NET Framework 4.7.2+** — the **bootstrapper installs this for you** when it's missing (chained
  redistributable, detected via release key ≥ 461808). The bare MSI does **not** install it: it keeps a
  blocking launch condition with a clear message, so a direct MSI install on a machine without the
  framework fails fast rather than installing a broken app.
- **A VISA runtime** (for live instrument I/O) — detected (vendor-neutral, via the IVI Foundation
  VISA shared-components registry key) and recorded, but **not** blocking: the app builds/exports
  waveforms offline and reports a clear error if you try to connect without VISA. Any IVI-compliant
  VISA provider works — **Keysight IO Libraries Suite, NI-VISA, Rohde & Schwarz, Rigol, …** — the
  app's transport uses the vendor-neutral IVI VISA.NET shared components (`GlobalResourceManager`).

## Building the installer

```powershell
# From the repo root. Builds the solution in Release, then the MSI, then the .exe bootstrapper.
./build-installer.ps1 -Version 1.0.0.0
#   -NoBundle   build only the MSI, skip the bootstrapper
```

Or the projects directly (after building the app in Release; build the bundle **after** the MSI — it
embeds the `.msi`):

```powershell
dotnet build ESG-SignalCreator.Installer\ESG-SignalCreator.Installer.wixproj -c Release `
    -p:ProductVersion=1.0.0.0 -p:HarvestPath=ESG-SignalCreator.App\bin\Release

dotnet build ESG-SignalCreator.Bundle\ESG-SignalCreator.Bundle.wixproj -c Release `
    -p:ProductVersion=1.0.0.0 -p:MsiPath=ESG-SignalCreator.Installer\bin\x64\Release\ESG-SignalCreator-1.0.0.0.msi
```

The artifacts land in `ESG-SignalCreator.Installer\bin\…\ESG-SignalCreator-<version>.msi` and
`ESG-SignalCreator.Bundle\bin\…\ESG-SignalCreator-Setup-<version>.exe`. Like the installer project, the
**bundle project is intentionally not in `ESG-SignalCreator.sln`** so the app still builds without WiX.

### The bootstrapper chain

`ESG-SignalCreator.Bundle\Bundle.wxs` is a WiX **Burn** bundle. Its `<Chain>` is:

1. `<PackageGroupRef Id="NetFx472Redist" />` — from `WixToolset.NetFx.wixext`. Detects the installed
   .NET Framework release key and runs the full 4.7.2 redistributable **only when the framework is
   absent** (downloaded from Microsoft on demand).
2. `<MsiPackage>` — the app MSI, embedded in `setup.exe`.

It carries the **MIT license** page (`WixStandardBootstrapperApplication`, `rtfLicense` theme) and its
own `UpgradeCode` for bundle-level upgrades.

### Options

- `INSTALLDESKTOPSHORTCUT=0` (msiexec property) skips the desktop shortcut. Pass it through the
  bootstrapper with `ESG-SignalCreator-Setup-<v>.exe INSTALLDESKTOPSHORTCUT=0`.
- Both the MSI and bootstrapper are x64 (per-machine, Program Files).
- Silent install: `ESG-SignalCreator-Setup-<v>.exe /quiet` (or `/passive` for progress-only).

## Application icon

The app icon ([ESG-SignalCreator.App/ESG-SignalCreator.ico](../ESG-SignalCreator.App/ESG-SignalCreator.ico))
— an abstracted E4438C front panel (green I/Q display + RPG knob) — is a multi-resolution `.ico`
(16–256 px). It is set as the project `ApplicationIcon`, so it is embedded in `ESG-SignalCreator.exe`
(Explorer, taskbar, and the form title bar via `Icon.ExtractAssociatedIcon`). The MSI's Add/Remove-Programs
icon is extracted from the exe, and the bootstrapper's ARP icon points at the same `.ico` — one icon,
everywhere. Regenerate it with `tools/make_icon.py` (Pillow) if the artwork changes.

## Continuous release (GitHub Actions)

`.github/workflows/release.yml` builds the MSI and publishes a GitHub Release automatically:

| Trigger | Result |
|---------|--------|
| Push to `main` (or manual *Run workflow*) | **Prerelease** tagged `v1.0.<run_number>.0` — every update produces a build |
| Push a tag `vX.Y.Z` | **Stable release** using that tag |
| Manual run with a `version` input | Stable release at that version |

Each run: builds the solution in Release, runs the unit tests, builds the MSI then the `setup.exe`
bootstrapper (WiX restored from NuGet), and uploads **both** to the release (the bootstrapper as the
recommended download) with their SHA256s.

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

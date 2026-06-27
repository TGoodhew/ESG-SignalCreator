# ESG-SignalCreator

A Windows desktop application — **ESG Signal Studio** — for driving Agilent/Keysight
**ESG-series RF signal generators** (e.g. the **E4438C**, E4400-series). It builds
baseband I/Q waveforms on the PC, previews and validates them, and downloads them to
the instrument's dual ARB player over a deliberate **Calculate → Download → Play**
pipeline. It is a modern reimplementation of the legacy *Signal Studio for E4438C*
(see the requirement docs in [docs/](docs/)).

Built with C# / WinForms targeting **.NET Framework 4.7.2**, split into a UI-free
core library, the WinForms app, and an xUnit test project.

## Features

- **Signal-flow shell** — a top action bar, a left project tree, a signal-flow
  block canvas (`Source → … → Output`), a personality picker with a live
  configuration panel, and a right dock of up to three verification plots.
- **Signal personalities** — pluggable sources that produce normalized I/Q:
  - **CW / single tone** (frequency-offset, seamless-looping)
  - **Multitone** (tone table, auto-spacing, random/equal/Newman phasing, live PAPR)
  - **Custom digital modulation** (BPSK/QPSK/8PSK/16–256-QAM/MSK, PN9/15/23 data,
    RRC/RC/Gaussian pulse shaping)
  - **AWGN** (band-limited Gaussian noise with crest-factor clipping)
  - **Import I/Q** (CSV/TSV, raw interleaved int16, WAV)
- **Verification plots** — I/Q vs time, FFT spectrum, constellation, and CCDF, each
  with a view dropdown and rubber-band zoom.
- **Deliberate pipeline** — **Calculate** generates I/Q off the UI thread with a
  progress bar, plots, and a live dependency check; **Download** turns the ARB off,
  encodes to interleaved 16-bit big-endian two's-complement, frames an IEEE-488.2
  definite-length block and writes it to volatile `WFM1`; **Play/Stop** arms the ARB
  and RF with a four-state play indicator. A one-click **Calc → DL → Play** runs all
  three.
- **Validation** — a live checker (minimum samples, even/granularity, memory cap vs
  the connected baseband option, sample-clock and carrier limits, DAC over-range
  heuristic) surfaced in a Notifications dock.
- **Instrument control** — connect over **NI-VISA** or **NI-488.2 (GPIB)** with
  discovery and `*IDN?`/`*OPT?`; an instrument-settings panel (frequency, amplitude,
  RF/modulation, ARB sample clock, runtime scaling) with read-back; and a raw-SCPI
  console with a timestamped log.
- **In-app closed-loop verification (E4406A)** — connect an E4406A VSA, then **Verify**
  measures the played signal (channel power, PAPR, and — for a tone — frequency) and shows
  it against the expected values (from the generated I/Q) in an Expected-vs-Measured
  **Verification** view with pass/fail. A guided **Path cal…** wizard captures cable loss +
  analyzer offset as a path-loss correction; a **Reference** menu locks both instruments to a
  common 10 MHz timebase; a **VSA Mode** menu (gated off `:INSTrument:CATalog?`) selects any
  installed standard personality (GSM / W-CDMA / cdma2000 / …).
- **Projects** — save/open the active source + settings as a `*.ssproj` JSON file.
- Pass **`--classic`** on the command line to launch the original single-window UI.

## Requirements

- Windows with .NET Framework 4.7.2
- Visual Studio 2017+ (or MSBuild) to build
- For live instrument control, one or both vendor stacks installed:
  - **NI-VISA** — IVI VISA.NET Shared Components + `NationalInstruments.Visa`
  - **NI-488.2** — `NationalInstruments.NI4882` (MeasurementStudio)

The core library references these assemblies via `HintPath` entries in
[ESG-SignalCreator.Core.csproj](ESG-SignalCreator.Core/ESG-SignalCreator.Core.csproj);
adjust the paths if your installation differs. Authoring, preview, validation and
project save/load all work without any instrument connected.

## Building

```powershell
# From a Developer Command Prompt / PowerShell with MSBuild on PATH
msbuild ESG-SignalCreator.sln -t:Restore,Build /p:Configuration=Release
```

Or open `ESG-SignalCreator.sln` in Visual Studio and build. Run the tests with
`dotnet test` or VS Test Explorer (`-t:Restore` matters — the test project uses
NuGet `PackageReference`s).

## Usage

1. Click **Connect…**, pick **NI-VISA** or **NI-488.2**, discover/select the
   instrument and connect (`*IDN?`/`*OPT?` are shown).
2. In the **Source** view, choose a personality from the picker and edit its
   parameters (sample rate, length as time/samples/symbols, and the personality's
   own settings).
3. Click **Calculate** to generate and preview the waveform; check the
   **Notifications** for any validation warnings.
4. Click **Download** then **Play** (or the combined **Calc → DL → Play**) to load
   and run the waveform; **Stop** turns the ARB off. The instrument-settings and
   SCPI-console views are available from the left tree.

## Hardware-in-the-loop testing

The 208 unit tests run with **no instrument** (block framing, encoder, DSP, validation,
VSA SCPI parsing, …). To validate the real instruments, run the headless harness
([ESG-SignalCreator.HilHarness](ESG-SignalCreator.HilHarness/)):

```powershell
# ESG-only: RF stays OFF (power -30 dBm) unless --rf-on briefly enables it.
ESG-SignalCreator.HilHarness.exe "TCPIP0::192.168.1.82::inst1::INSTR"

# Comprehensive closed-loop battery (ESG -> E4406A) across the frequency range:
# verifies EVERY signal type on the analyzer, with a machine-readable report.
ESG-SignalCreator.HilHarness.exe --vsa GPIB0::17::INSTR --all --dwell-seconds 3 --json report.json

# A single signal type, or the amplitude-flatness power sweep:
ESG-SignalCreator.HilHarness.exe --vsa --signal multitone
ESG-SignalCreator.HilHarness.exe --vsa --flatness
#   options: --points N --start-hz --stop-hz --carrier-hz --offset-hz
#            --verify-power-dbm --max-input-dbm --path-loss-db --dwell-seconds --json
```

ESG-only mode checks `*IDN?`/`*OPT?`, downloads a CW to `WFM1`, arms the ARB, and reads
back frequency/amplitude. The **closed-loop battery** (`--all`) connects the E4406A (refusing
a non-E4406A), enforces the **input-damage safety gate** (E4406A rated +35 dBm; gate default
+30 dBm), and for each signal type — **CW, multitone, AWGN, custom-mod (QAM), multi-carrier,
I/Q-impairment, import-I/Q** — drives the ESG at a safe level across a frequency sweep and
verifies on the analyzer:

- **channel power** vs the commanded level, and **PAPR** (CCDF) vs the value computed from the
  generated I/Q, for every signal;
- **tone frequency** (CW / import-I/Q), **ACPR** (custom-mod), and the **gain-imbalance image**
  (I/Q-impairment) where applicable.

The analyzer runs in continuous mode during the per-point dwell so the front panel tracks live;
the run ends RF-off with the analyzer still sweeping. Per-step PASS/FAIL, optional JSON report,
non-zero exit on failure. A separate console project, kept out of the unit-test run so CI stays
hardware-free.

> Bench-validated (2026-06, E4406A FW A.08.10) across 50 MHz–3 GHz: all signal types PASS —
> e.g. multitone PAPR ≈3.8 dB (exp 2.9), AWGN crest ≈10.2 dB, 16-QAM ACPR ≈−48 dBc, a 3 dB
> I/Q gain imbalance → image at −15.4 dBc (matches theory), and amplitude accuracy within a
> consistent ~0.76 dB cable roll-off at 3 GHz.

## Installer

A Windows **MSI** is built with the free **WiX Toolset v5** (restored from NuGet by `dotnet build`,
no toolset install needed). From the repo root:

```powershell
./build-installer.ps1 -Version 1.0.0.0
```

It installs the app per-machine to `Program Files`, adds Start-menu/desktop shortcuts and a proper
Add/Remove-Programs entry, requires .NET Framework 4.7.2, and detects an installed **VISA** runtime
(vendor-neutral — Keysight, NI, R&S, Rigol, …). The installer project is kept out of the solution so
a machine without WiX still builds the app. See [docs/Packaging.md](docs/Packaging.md) for details.

Prebuilt installers are published on the [Releases](https://github.com/TGoodhew/ESG-SignalCreator/releases)
page. A GitHub Actions workflow builds the MSI and publishes a release on every push to `main`
(prerelease) and on every `vX.Y.Z` tag (stable) — see [docs/Packaging.md](docs/Packaging.md#continuous-release-github-actions)
(requires a self-hosted Windows runner with the VISA stack).

## Project layout

The solution is split into a UI-free core library, the WinForms app, and a test project:

| Path | Purpose |
|------|---------|
| [ESG-SignalCreator.Core/](ESG-SignalCreator.Core/) | Class library — no UI dependency. Transport, ARB encoding, DSP, personalities, validation. |
| [Core/EsgController.cs](ESG-SignalCreator.Core/EsgController.cs) | High-level SCPI helpers (frequency, power, ARB download/playback) |
| [Core/Instruments/](ESG-SignalCreator.Core/Instruments/) | `IInstrument` transport abstraction; VISA and GPIB (488.2) implementations |
| [Core/Visa/](ESG-SignalCreator.Core/Visa/) | `EsgInstrument` facade + `*IDN?`/`*OPT?` parsing |
| [Core/Arb/](ESG-SignalCreator.Core/Arb/) | IEEE-488.2 block framing and the int16/interleave/big-endian ARB encoder |
| [Core/Model/](ESG-SignalCreator.Core/Model/) | `WaveformModel` — the neutral I/Q output of every signal personality |
| [Core/Personalities/](ESG-SignalCreator.Core/Personalities/) | `IWaveformPersonality` contract + CW, Multitone, CustomMod, AWGN, Import-IQ |
| [Core/Dsp/](ESG-SignalCreator.Core/Dsp/) | FFT, FIR (RRC/RC/Gaussian), windows, CCDF/PAPR, resampling |
| [Core/Validation/](ESG-SignalCreator.Core/Validation/) | `WaveformValidator` dependency checker |
| [Core/Capability/](ESG-SignalCreator.Core/Capability/) | Per-target capability profiles (embedded JSON) |
| [Core/Timing/](ESG-SignalCreator.Core/Timing/) | `SampleCountSolver` (time/samples/symbols → sample count) |
| [Core/Project/](ESG-SignalCreator.Core/Project/) | `SsProject` + `ProjectStore` (`.ssproj` save/load) |
| [App/Ui/](ESG-SignalCreator.App/Ui/) | `StudioForm` shell, signal-flow canvas, source panels, plot panes, instrument UI |
| [ESG-SignalCreator.App/](ESG-SignalCreator.App/) | WinForms application — references Core (entry point `Program.cs`) |
| [Core/Measure/](ESG-SignalCreator.Core/Measure/) | E4406A Basic-mode measurements: Channel Power, ACP, CCDF, Spectrum, Waveform, Power-vs-Time + mask |
| [Core/Verify/](ESG-SignalCreator.Core/Verify/) | Closed-loop verification harness/profile/result, RF-path safety gate, path calibration |
| [ESG-SignalCreator.Tests/](ESG-SignalCreator.Tests/) | xUnit tests (256: framing, encoder, DSP, personalities, validation, sequencing, measurements, verification, …) |
| [ESG-SignalCreator.HilHarness/](ESG-SignalCreator.HilHarness/) | Headless hardware-in-the-loop test runner for a real E4438C |

Run the tests with `dotnet test` or VS Test Explorer.

## Disclaimer

Not affiliated with or endorsed by Keysight Technologies, Agilent, or National
Instruments. "ESG", "E4438C", VISA, and GPIB are referenced for interoperability
only. Use at your own risk when driving real hardware.

## License

Released under the [MIT License](LICENSE).

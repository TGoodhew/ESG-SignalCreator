# ESG-SignalCreator

A Windows desktop application — **ESG Signal Studio** — for driving Agilent/Keysight
**ESG-series RF signal generators** (e.g. the **E4438C**, E4400-series). It builds
baseband I/Q waveforms on the PC, previews and validates them, and downloads them to
the instrument's dual ARB player over a deliberate **Calculate → Download → Play**
pipeline. It is a modern reimplementation of the legacy *Signal Studio for E4438C*
(see the requirement docs in [docs/](docs/)).

Built with C# / WinForms targeting **.NET Framework 4.7.2**, split into a UI-free
core library, the WinForms app, and an xUnit test project.

**Documentation:** [User Guide](docs/UserGuide.md) (a complete reference for every feature) ·
[Tutorials](docs/Tutorials.md) (21 hands-on walkthroughs, simple → complex) ·
[Manual Verification](docs/ManualVerification.md) (step-by-step bench check with a VSA settings checklist) ·
[Packaging](docs/Packaging.md) (installer + release build).
🌐 Dansk: [Brugervejledning](docs/da/UserGuide.md) · [Tutorials](docs/da/Tutorials.md) · [Manuel verifikation](docs/da/ManualVerification.md).

> 📝 **Docs parity:** English is authoritative; the Danish set under [docs/da/](docs/da/) is a machine
> translation kept in parity. When you change an English document, update its Danish counterpart (and add
> one if the English doc is new).

> 🔄 **Tutorial images are generated, not hand-grabbed.** The signal-tutorial analyzer screenshots
> (`docs/images/tutorials/*-n9010a.png`) are real N9010A captures from `HilHarness --tutorial-captures <dir>`
> (#150); the two views the analyzer can't draw (QPSK constellation/eye) come from the app renderer
> `ESG-SignalCreator.exe --tutorial-images <dir>`; app-UI screenshots for the workflow tutorials come from
> `ESG-SignalCreator.exe --tutorial-ui-images <dir>`; verification screenshots (`docs/images/vsa/`) come
> from `HilHarness --install-verify --capture-dir` (#143). **When a tutorial changes, update the relevant
> harness and regenerate the affected images in the same change.** (E4406A captures coming soon.)

## Features

- **Signal-flow shell** — a top action bar, a left project tree, a signal-flow
  block canvas (`Source → … → Output`), a personality picker with a live
  configuration panel, and a right dock of up to three verification plots.
- **Signal personalities** — pluggable sources that produce normalized I/Q:
  - **CW / single tone** (frequency-offset, seamless-looping)
  - **Multitone** (tone table, auto-spacing, random/equal/Newman phasing, live PAPR)
  - **Custom digital modulation** (BPSK/QPSK/8PSK/16–256-QAM/MSK, PN9/15/23 data,
    RRC/RC/Gaussian pulse shaping)
  - **Pulse Building** (radar-style pulse train: PRI/width, raised-cosine edges,
    intra-pulse LFM chirp or Barker phase codes, per-pulse markers — N7620A v1)
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
- **Instrument control** — connect over **VISA** through any installed provider (Keysight IO
  Libraries, NI-VISA, R&S, Rigol, …), for TCPIP/LAN, GPIB, USB or serial resources, with discovery
  and `*IDN?`/`*OPT?`; an instrument-settings panel (frequency, amplitude, RF/modulation, ARB sample
  clock, runtime scaling) with read-back; and a raw-SCPI console with a timestamped log.
- **In-app closed-loop verification (E4406A / N9010A)** — connect a VSA, then **Verify**
  measures the played signal (channel power, PAPR, and — for a tone — frequency) and shows
  it against the expected values (from the generated I/Q) in an Expected-vs-Measured
  **Verification** view with pass/fail. A guided **Path cal…** wizard captures cable loss +
  analyzer offset as a path-loss correction; a **Reference** menu locks both instruments to a
  common 10 MHz timebase; a **VSA Mode** menu (gated off `:INSTrument:CATalog?`) selects any
  installed standard personality (GSM / W-CDMA / cdma2000 / …). A **VSA model** toggle selects
  which analyzer the app targets — the E4406A or a **Keysight N9010A (EXA)**; connect verifies
  the instrument matches the selection. _(N9010A support is landing in stages — analyzer
  selection and the X-Series control plane are in; the SA-mode measurement mapping is in progress.)_
- **In-app Claude assistant** (opt-in) — a natural-language pane that drives the app through a
  versioned, function-calling tool surface rather than synthetic clicks: **read** tools (app state,
  config, validation, readout, personalities), **configure** tools (pick/configure a source, select a
  plot, project save/load, calculate), and **hardware** tools (connect, download, play/stop, set
  instrument settings). Guardrails are enforced in the dispatcher, not the prompt: read/configure run
  freely, but anything that touches the instrument requires an inline Approve/Decline card (RF and bus
  takeover always confirm), and a pre-execution validation gate refuses hardware actions on a hard
  validation failure — even if approved. It can also **measure + verify** on the connected analyzer (channel power,
  ACP, CCDF/PAPR, spectrum peak, waveform, and a closed-loop `verify_signal`), and exposes an opt-in,
  always-confirmed **raw-SCPI** escape hatch (off by default). Read tool_uses run concurrently while
  configure/hardware stay serialized; long chats are compacted. Tool output is treated as data, never
  commands; the API key is stored encrypted (Windows DPAPI); the feature is off until enabled. Covered
  by an end-to-end acceptance suite (schema validity, gate/confirmation, injection resistance, SCPI
  parity, secret hygiene).
- **Projects** — save/open the active source + settings as a `*.ssproj` JSON file.
- Pass **`--classic`** on the command line to launch the original single-window UI.

## Requirements

- Windows with .NET Framework 4.7.2
- Visual Studio 2017+ (or MSBuild) to build
- For live instrument control, **any IVI-compliant VISA runtime** installed (Keysight IO Libraries
  Suite, NI-VISA, R&S, Rigol, …). The app uses the vendor-neutral **IVI VISA.NET Shared Components**
  (`Ivi.Visa` / `GlobalResourceManager`) and dispatches to whichever provider is installed — GPIB,
  TCPIP/LAN, USB and serial all go through VISA. No vendor-specific assemblies are referenced.

The core library references `Ivi.Visa` via a `HintPath` entry in
[ESG-SignalCreator.Core.csproj](ESG-SignalCreator.Core/ESG-SignalCreator.Core.csproj) (the IVI VISA.NET
Shared Components, installed by any VISA provider); adjust the path if your installation differs.
Authoring, preview, validation and project save/load all work without any instrument connected.

## Building

```powershell
# From a Developer Command Prompt / PowerShell with MSBuild on PATH
msbuild ESG-SignalCreator.sln -t:Restore,Build /p:Configuration=Release
```

Or open `ESG-SignalCreator.sln` in Visual Studio and build. Run the tests with
`dotnet test` or VS Test Explorer (`-t:Restore` matters — the test project uses
NuGet `PackageReference`s).

## Usage

1. Click **Connect…**, enter or discover a **VISA resource** (e.g.
   `TCPIP0::192.168.1.82::inst1::INSTR` or `GPIB0::19::INSTR`) and connect (`*IDN?`/`*OPT?` are shown).
2. In the **Source** view, choose a personality from the picker and edit its
   parameters (sample rate, length as time/samples/symbols, and the personality's
   own settings).
3. Click **Calculate** to generate and preview the waveform; check the
   **Notifications** for any validation warnings.
4. Click **Download** then **Play** (or the combined **Calc → DL → Play**) to load
   and run the waveform; **Stop** turns the ARB off. The instrument-settings and
   SCPI-console views are available from the left tree.

## Hardware-in-the-loop testing

The unit tests run with **no instrument** (block framing, encoder, DSP, validation,
VSA SCPI parsing, screen-capture block decoding, …). To validate the real instruments, run the headless
harness ([ESG-SignalCreator.HilHarness](ESG-SignalCreator.HilHarness/)):

```powershell
# ESG-only: RF stays OFF (power -30 dBm) unless --rf-on briefly enables it.
ESG-SignalCreator.HilHarness.exe "TCPIP0::192.168.1.82::inst1::INSTR"

# Comprehensive closed-loop battery (ESG -> VSA) across the frequency range:
# verifies EVERY signal type on the analyzer, with a machine-readable report.
ESG-SignalCreator.HilHarness.exe --vsa GPIB0::17::INSTR --all --dwell-seconds 3 --json report.json

# Target a Keysight N9010A instead of the E4406A (LAN address, --vsa-model):
ESG-SignalCreator.HilHarness.exe --vsa TCPIP0::192.168.1.90::hislip0::INSTR --vsa-model n9010a --all

# Install self-test: the CW/AM/FM/IQ battery on the one selected analyzer (JSON + exit code):
ESG-SignalCreator.HilHarness.exe --install-verify --vsa GPIB0::17::INSTR --vsa-model e4406a --json verify.json

# A single signal type, or the amplitude-flatness power sweep:
ESG-SignalCreator.HilHarness.exe --vsa --signal multitone
ESG-SignalCreator.HilHarness.exe --vsa --flatness
#   options: --vsa-model e4406a|n9010a --points N --start-hz --stop-hz --carrier-hz --offset-hz
#            --verify-power-dbm --max-input-dbm --path-loss-db --dwell-seconds --json

# AUTOMATED screenshots: drive CW/AM/FM/IQ, measure each, and capture the analyzer screen per step —
# one command, no manual setup. Writes cw/am/fm/iq-multitone images + an index.md into the folder:
ESG-SignalCreator.HilHarness.exe --install-verify --vsa GPIB0::17::INSTR --vsa-model e4406a --capture-dir docs/images/vsa

# Or capture just the analyzer's CURRENT display (analyzer-only, no ESG/RF), for an ad-hoc shot:
ESG-SignalCreator.HilHarness.exe --capture-screen docs/images/vsa/cw-result.png --vsa GPIB0::17::INSTR --vsa-model e4406a
#   SCPI overrides for either mode (confirm/adjust per firmware):
#     --capture-data-query ":MMEMory:DATA? \"{0}\"" --capture-save-cmd ":MMEMory:STORe:SCReen \"{0}\""
#     --capture-cleanup-cmd ":MMEMory:DELete \"{0}\"" --capture-temp-path "C:\Temp\ESGCAP.png"
```

ESG-only mode checks `*IDN?`/`*OPT?`, downloads a CW to `WFM1`, arms the ARB, and reads
back frequency/amplitude. The **closed-loop battery** (`--all`) connects the analyzer selected by
`--vsa-model` (E4406A default, or N9010A; refusing a mismatched model), enforces the **input-damage
safety gate** (per-model default — E4406A +30 dBm below its +35 dBm rating; N9010A +30 dBm / 1 W per
its data sheet), and for each signal type — **CW, multitone, AWGN, custom-mod (QAM), multi-carrier,
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

**Screen capture** produces the per-step VSA screenshots for the tutorials and the
[Manual Verification](docs/ManualVerification.md) doc (images written as PNG on the X-Series, GIF on the
E4406A). Two modes:

- **Automated** — `--install-verify --capture-dir <dir>` drives the ESG through the CW/AM/FM/I-Q battery,
  measures each on the analyzer, and captures the analyzer's display after each signal, all in **one
  command with no manual setup**. It writes `cw`, `am`, `fm`, `iq-multitone` images plus an `index.md`
  that embeds them (ready to paste into the docs).
- **Ad-hoc** — `--capture-screen <file>` is analyzer-only (no ESG/RF): it captures whatever the VSA is
  currently showing, for a one-off shot after you've set a signal up yourself.

Both read the display back over VISA as an IEEE-488.2 block. The default capture SCPI is manual-derived
and **needs bench confirmation** — the `--capture-*` overrides let you tune it per firmware without a
rebuild.

> Bench-validated (2026-06, E4406A FW A.08.10) across 50 MHz–3 GHz: all signal types PASS —
> e.g. multitone PAPR ≈3.8 dB (exp 2.9), AWGN crest ≈10.2 dB, 16-QAM ACPR ≈−48 dBc, a 3 dB
> I/Q gain imbalance → image at −15.4 dBc (matches theory), and amplitude accuracy within a
> consistent ~0.76 dB cable roll-off at 3 GHz.
>
> N9010A support is derived from the Keysight X-Series manuals and unit-tested for the SCPI dialect
> (mode routing, measurement roots, and result-scalar orderings), but is **not yet bench-validated** —
> confirm the ACP result layout and the max-safe-input limit against your unit.

## Installer

Two artifacts are built with the free **WiX Toolset v5** (restored from NuGet by `dotnet build`, no
toolset install needed) — a **`setup.exe`** bootstrapper and a raw **MSI**. From the repo root:

```powershell
./build-installer.ps1 -Version 1.0.0.0   # builds the app, the MSI, then the setup.exe
```

- **`ESG-SignalCreator-Setup-<version>.exe`** (recommended) — a bootstrapper that **chains the .NET
  Framework 4.7.2 installer**: it installs the framework automatically if it's missing, then the app.
- **`ESG-SignalCreator-<version>.msi`** — the raw package, for machines that already have .NET 4.7.2.

Both install the app per-machine to `Program Files`, add Start-menu/desktop shortcuts and a proper
Add/Remove-Programs entry (with the app icon), and detect an installed **VISA** runtime (vendor-neutral
— Keysight, NI, R&S, Rigol, …). The installer/bootstrapper projects are kept out of the solution so a
machine without WiX still builds the app. See [docs/Packaging.md](docs/Packaging.md) for details.

Prebuilt installers are published on the [Releases](https://github.com/TGoodhew/ESG-SignalCreator/releases)
page. A GitHub Actions workflow builds the MSI + bootstrapper and publishes a release on every push to
`main` (prerelease) and on every `vX.Y.Z` tag (stable) — see [docs/Packaging.md](docs/Packaging.md#continuous-release-github-actions)
(builds on a Windows runner with the IVI VISA.NET Shared Components — any VISA provider — installed).

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
| [Core/Personalities/](ESG-SignalCreator.Core/Personalities/) | `IWaveformPersonality` contract + CW, Multitone, Multi-Carrier, CustomMod, Pulse, AWGN, Import-IQ |
| [Core/Dsp/](ESG-SignalCreator.Core/Dsp/) | FFT, FIR (RRC/RC/Gaussian), windows, CCDF/PAPR, resampling |
| [Core/Validation/](ESG-SignalCreator.Core/Validation/) | `WaveformValidator` dependency checker |
| [Core/Capability/](ESG-SignalCreator.Core/Capability/) | Per-target capability profiles (embedded JSON) |
| [Core/Timing/](ESG-SignalCreator.Core/Timing/) | `SampleCountSolver` (time/samples/symbols → sample count) |
| [Core/Project/](ESG-SignalCreator.Core/Project/) | `SsProject` + `ProjectStore` (`.ssproj` save/load) |
| [App/Ui/](ESG-SignalCreator.App/Ui/) | `StudioForm` shell, signal-flow canvas, source panels, plot panes, instrument UI |
| [ESG-SignalCreator.App/](ESG-SignalCreator.App/) | WinForms application — references Core (entry point `Program.cs`) |
| [Core/Measure/](ESG-SignalCreator.Core/Measure/) | VSA measurements (E4406A Basic-mode / N9010A SA + IQ Analyzer, via a per-model SCPI dialect): Channel Power, ACP, CCDF, Spectrum, Waveform, Power-vs-Time + mask |
| [Core/Verify/](ESG-SignalCreator.Core/Verify/) | Closed-loop verification harness/profile/result, RF-path safety gate, path calibration |
| [ESG-SignalCreator.Assistant/](ESG-SignalCreator.Assistant/) | In-app Claude assistant: Messages API client, agent loop, tool surface (read/configure/hardware), guardrails, DPAPI secrets |
| [ESG-SignalCreator.Tests/](ESG-SignalCreator.Tests/) | xUnit tests (356: framing, encoder, DSP, personalities, validation, sequencing, measurements, verification, assistant tools + guardrails + acceptance, …) |
| [ESG-SignalCreator.HilHarness/](ESG-SignalCreator.HilHarness/) | Headless hardware-in-the-loop test runner for a real E4438C |

Run the tests with `dotnet test` or VS Test Explorer.

## Disclaimer

Not affiliated with or endorsed by Keysight Technologies, Agilent, or National
Instruments. "ESG", "E4438C", VISA, and GPIB are referenced for interoperability
only. Use at your own risk when driving real hardware.

## License

Released under the [MIT License](LICENSE).

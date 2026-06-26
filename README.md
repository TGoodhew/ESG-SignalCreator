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

The 184 unit tests run with **no instrument** (block framing, encoder, DSP, validation, …).
To validate the real ARB download/play path against an actual E4438C, run the headless
harness ([ESG-SignalCreator.HilHarness](ESG-SignalCreator.HilHarness/)):

```powershell
# RF stays OFF and power LOW (-30 dBm) by default; --rf-on briefly enables RF.
ESG-SignalCreator.HilHarness.exe "TCPIP0::192.168.1.82::inst1::INSTR"
# or set ESG_VISA_RESOURCE and omit the argument.
```

It connects over VISA, checks `*IDN?`/`*OPT?`, downloads a CW waveform to `WFM1`, arms the
ARB, sets/reads back frequency and amplitude, and polls `:SYSTem:ERRor?` after each step —
printing a per-step PASS/FAIL summary and exiting non-zero on any failure. It is a separate
console project, kept out of the normal unit-test run so CI stays hardware-free.

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
| [ESG-SignalCreator.Tests/](ESG-SignalCreator.Tests/) | xUnit tests (184: framing, encoder, DSP, personalities, validation, sequencing, …) |
| [ESG-SignalCreator.HilHarness/](ESG-SignalCreator.HilHarness/) | Headless hardware-in-the-loop test runner for a real E4438C |

Run the tests with `dotnet test` or VS Test Explorer.

## Disclaimer

Not affiliated with or endorsed by Keysight Technologies, Agilent, or National
Instruments. "ESG", "E4438C", VISA, and GPIB are referenced for interoperability
only. Use at your own risk when driving real hardware.

## License

Released under the [MIT License](LICENSE).

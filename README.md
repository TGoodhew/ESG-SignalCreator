# ESG-SignalCreator

A Windows desktop application for driving Agilent/Keysight **ESG-series RF signal
generators** (e.g. the **E4438C**, E4400-series). It provides direct SCPI control
of the instrument and a built-in I/Q waveform builder that generates baseband
signals, previews them, and downloads them to the instrument's dual ARB player.

Built with C# / WinForms targeting **.NET Framework 4.7.2**.

## Features

- **Two transport options** — connect over **NI-VISA** (resource string) or
  **NI-488.2 (GPIB)** (board + primary address), with instrument discovery.
- **Basic RF control** — set carrier frequency (Hz/kHz/MHz/GHz) and amplitude
  (dBm), toggle modulation and RF output, read back settings, `*RST`/`*CLS`,
  `*IDN?`, and a free-form SCPI send/query console.
- **I/Q ARB signal builder** — generate one of several baseband signals:
  - Single tone (frequency-offset CW)
  - AM (rate + depth %)
  - FM (rate + deviation Hz)
  - PM (rate + deviation degrees)
- **Live preview** — time-domain I/Q plot plus a baseband magnitude spectrum
  (via an internal FFT) before anything is sent to the instrument.
- **Download & play** — streams the waveform into volatile ARB memory (`WFM1`)
  as interleaved 16-bit big-endian two's-complement samples, selects it, sets the
  sample clock and runtime scaling, and starts the ARB on the chosen carrier.

## Requirements

- Windows with .NET Framework 4.7.2
- Visual Studio 2017+ (or MSBuild) to build
- For live instrument control, one or both vendor stacks installed:
  - **NI-VISA** — IVI VISA.NET Shared Components + `NationalInstruments.Visa`
  - **NI-488.2** — `NationalInstruments.NI4882` (MeasurementStudio)

The project references these assemblies via `HintPath` entries in
[ESG-SignalCreator.csproj](ESG-SignalCreator/ESG-SignalCreator.csproj); adjust the
paths if your installation differs. The waveform builder and preview work without
any instrument connected.

## Building

```powershell
# From a Developer Command Prompt / PowerShell with MSBuild on PATH
msbuild ESG-SignalCreator.sln /p:Configuration=Release
```

Or open `ESG-SignalCreator.sln` in Visual Studio and build.

## Usage

1. Pick an interface (**NI-VISA** or **NI-488.2**), click **Refresh** to discover
   instruments, select one, and **Connect**. The `*IDN?` response is logged.
2. Use the RF controls to set frequency/amplitude and toggle output, or send raw
   SCPI from the console.
3. In the **ARB Signal Builder**, choose a signal type, set its parameters and the
   sample clock (≤ 100 MHz for the E4438C), and click **Generate** to preview.
4. With an instrument connected, click **Download & Play** to load and run the
   waveform; **ARB Off** stops it.

## Project layout

The solution is split into a UI-free core library, the WinForms app, and a test project:

| Path | Purpose |
|------|---------|
| [ESG-SignalCreator.Core/](ESG-SignalCreator.Core/) | Class library — no UI dependency. Transport, ARB encoding, DSP, and signal models. |
| [Core/EsgController.cs](ESG-SignalCreator.Core/EsgController.cs) | High-level SCPI helpers (frequency, power, ARB download/playback) |
| [Core/Instruments/](ESG-SignalCreator.Core/Instruments/) | `IInstrument` transport abstraction; VISA and GPIB (488.2) implementations |
| [Core/Arb/](ESG-SignalCreator.Core/Arb/) | IEEE-488.2 block framing and the int16/interleave/big-endian ARB encoder |
| [Core/Model/](ESG-SignalCreator.Core/Model/) | `WaveformModel` — the neutral I/Q output of every signal personality |
| [Core/Personalities/](ESG-SignalCreator.Core/Personalities/) | `IWaveformPersonality` plug-in contract |
| [Core/Waveform/](ESG-SignalCreator.Core/Waveform/) | I/Q waveform helper and the (legacy) signal generator |
| [Core/Capability/](ESG-SignalCreator.Core/Capability/) | Per-target capability profiles (embedded JSON) for validation / offline mode |
| [Core/Dsp/Fft.cs](ESG-SignalCreator.Core/Dsp/Fft.cs) | FFT used for the spectrum preview |
| [ESG-SignalCreator.App/](ESG-SignalCreator.App/) | WinForms application (`MainForm`, entry point) — references Core |
| [ESG-SignalCreator.Tests/](ESG-SignalCreator.Tests/) | xUnit tests (block framing, encoder, capability profiles, generator) |

Run the tests with `dotnet test` or VS Test Explorer.

## Disclaimer

Not affiliated with or endorsed by Keysight Technologies, Agilent, or National
Instruments. "ESG", "E4438C", VISA, and GPIB are referenced for interoperability
only. Use at your own risk when driving real hardware.

## License

Released under the [MIT License](LICENSE).

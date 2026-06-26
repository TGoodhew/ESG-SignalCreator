# Signal Studio (Reborn) — Product Brief & Requirements

**Target instrument:** Keysight / Agilent **E4438C ESG** Vector Signal Generator
**Implementation stack:** C# · Windows Forms · .NET Framework 4.7.2 · NI-VISA (NI-VISA.NET / `Ivi.Visa` or `NationalInstruments.Visa`)
**Document purpose:** Hand-off spec for Claude Code (VS Code) to scaffold and build a modern reimplementation of the legacy Agilent *Signal Studio for E4438C* PC application.

> Status: v1 draft assembled from public Agilent/Keysight documentation. Section 11 lists open items to confirm against your own instrument before coding the binary/SCPI layer.

---

## 1. Background — What the original Signal Studio was

Signal Studio was a **PC-hosted application** that ran on a Windows host and talked to the E4438C over **GPIB or 10BaseT LAN**. Its job was to take a high-level signal description (a standard's framing, channel config, payload, impairments), **compute a baseband I/Q ARB waveform on the PC**, and **download that waveform plus instrument settings** into the ESG's baseband generator, then trigger playback. The instrument needed the matching **option license** installed for a given personality to play the resulting waveform.

The legacy workflow had four conceptual steps, which we preserve:

1. **Configure** the signal (format-specific parameters, payload, framing).
2. **Calculate** — generate the I/Q waveform file on the PC from the config. (Plots of I/Q, spectrum, and CCDF were offered.)
3. **Download** — send the I/Q waveform + marker/header + instrument settings to the ESG.
4. **Play** — instrument begins producing the modulated RF; local front-panel control is re-enabled.

Config could be saved/recalled as project files (legacy stored under `Agilent\Signal Studio\E4438C\<personality>\`).

### Personalities (option-gated) the original family covered
The original product was actually a *family* of personality modules, each tied to an E4438C option number. Useful as a feature map even though our v1 will only implement a subset:

| Option | Personality |
|---|---|
| 401 | cdma2000 / IS-95-A |
| 402 | TDMA (GSM, GPRS, EDGE, EGPRS, NADC, PDC, PHS, DECT, TETRA) |
| 403 | Calibrated Noise (AWGN) |
| 404 | 1xEV-DO |
| 405 | (CDMA variants) |
| 406 | (TD-SCDMA family) |
| 407 | Signal Studio for S-DMB |
| 408 | Signal Studio for Enhanced Multitone |
| 409 | Multi-satellite GPS |
| 410/411 | TD-SCDMA (incl. TSM) |
| 417/N7617B | 802.11 a/b/g WLAN |
| 419 | 3GPP W-CDMA HSPA |
| N7606B | Bluetooth |
| N7620A | Pulse Building |
| N7621B | Multitone Distortion |

> **Scope guidance for v1:** the *waveform engine + transport + instrument control* is identical across all personalities — only the **bit/symbol/IQ generation** differs. Build the platform first, then add personalities as plug-ins. Recommended v1 personalities (no external IP, fully synthesizable from first principles): **Custom IQ import**, **Multitone**, **Single-tone/CW IQ**, **AWGN**, **Custom digital modulation (ASK/FSK/MSK/PSK/QAM)**. Standards-based personalities (W-CDMA, GSM, etc.) are later phases.

---

## 2. Goals & Non-Goals

**Goals**
- Recreate the *configure → calculate → download → play* loop for the E4438C over NI-VISA (GPIB **and** TCPIP/LAN).
- Correct, byte-accurate ARB waveform download (this is the make-or-break detail — see §5).
- A clean WinForms UI: connection manager, personality workspace, plot views (I/Q vs time, spectrum, CCDF), instrument settings panel, project save/recall.
- Plug-in architecture so new signal personalities can be added without touching the transport core.

**Non-Goals (v1)**
- No attempt to reproduce Agilent's encrypted/licensed waveform extraction format.
- No Baseband Studio / N5102A digital I/O, fading, or real-time personalities.
- No front-panel emulation of the instrument.
- We do **not** need the instrument option license to *download and play a plain ARB waveform* — option licenses gated the *personalities/real-time formats*, not generic ARB playback (Option 001/601 or 002/602 baseband generator hardware is what's actually required on the box).

---

## 3. System Architecture

```
+-----------------------------------------------+
|  WinForms UI (.NET 4.7.2)                      |
|  - ConnectionManagerForm                       |
|  - PersonalityWorkspace (docked panels)        |
|  - Plot panels (IQ / Spectrum / CCDF)          |
|  - InstrumentSettingsPanel                     |
|  - Project open/save                           |
+----------------------+-------------------------+
                       | (MVP / MVVM-ish; keep UI thin)
+----------------------v-------------------------+
|  Core (class library)                          |
|   +-------------+    +------------------------+ |
|   | IWaveformGen|    | WaveformModel (IQ+mark)| |
|   |  plug-ins   |--->|  float[] I, float[] Q, | |
|   | (per format)|    |  byte[] markers, fs    | |
|   +-------------+    +-----------+------------+ |
|                                  | encode       |
|                      +-----------v------------+ |
|                      | EsgArbEncoder          | |
|                      |  scale->int16->        | |
|                      |  interleave->BE block  | |
|                      +-----------+------------+ |
|                      +-----------v------------+ |
|                      | EsgInstrument          | |
|                      |  NI-VISA session, SCPI,| |
|                      |  IEEE-488.2 block write| |
|                      +------------------------+ |
+----------------------+-------------------------+
                       | GPIB / TCPIP via NI-VISA
              +--------v--------+
              |   E4438C ESG    |
              +-----------------+
```

**Key principle:** the personality plug-in only ever produces a `WaveformModel` (normalized float I/Q in range roughly [-1, +1] plus optional per-sample marker bits and a sample rate). Everything downstream — scaling, integer conversion, byte order, block framing, transport — is shared and lives in `EsgArbEncoder` + `EsgInstrument`.

---

## 4. Transport & Connection (NI-VISA)

- Use NI-VISA .NET. Resource strings:
  - GPIB: `GPIB0::19::INSTR` (ESG default GPIB address is typically 19; make it configurable).
  - LAN: `TCPIP0::<host-or-ip>::INSTR` (VXI-11) — the ESG supports 10BaseT LAN + SCPI raw sockets (port 5025) and FTP. Prefer VXI-11/`INSTR` through VISA for simplicity; optionally support raw socket `TCPIP0::<ip>::5025::SOCKET`.
- Identify with `*IDN?` — expect e.g. `Agilent Technologies, E4438C, <serial>, C.0x.xx`. Parse firmware revision (some behaviors are firmware-gated; see §11).
- Always set a generous timeout for the block download (large waveforms over GPIB are slow). Make timeout configurable; default ~30 s for transfers.
- Recommended session hygiene: `*CLS`, check `:SYSTem:ERRor?` after each logical operation, surface errors in UI.
- For big-endian block writes, use the VISA formatted/binary write that does **not** reorder bytes — we control byte order ourselves (see §5). Send the raw `byte[]`.

---

## 5. ⭐ The ARB Waveform Format & Download (most important section)

This is the part that legacy Signal Studio got exactly right and is the thing most likely to break a reimplementation. Get this byte-perfect.

### 5.1 Sample format
Per Agilent's *Creating and Downloading Waveform Files* and the published MATLAB downloader for the E4438C:

- I and Q are each **16-bit signed two's-complement** integers.
- DAC scale: `+32767` = positive full scale, `0` = 0 V, `-32768` = negative full scale. (In practice many tools clamp the negative to `-32767` to keep symmetric scaling.)
- **Interleaved** as I, Q, I, Q, … (Q immediately follows its I).
- **Big-endian** byte order (MSB first / lower address sent first).
- **Minimum 60 samples** per waveform (i.e. 60 I + 60 Q). Use an **even** sample count to minimize imperfections.
- A separate **marker** stream exists; 1 marker byte per sample conceptually (the instrument's internal sample is 5 bytes: 2 I + 2 Q + 1 marker). For the `MMEM:DATA "WFM1:..."` path you download the **interleaved I/Q block**; markers go to a separate marker file (see 5.3). For basic playback you can omit a custom marker file and the instrument supplies defaults.

### 5.2 Scaling to avoid DAC over-range
Because of interpolation in the signal path, a DAC over-range can occur **even when all I/Q samples are individually within range**. Two-part strategy, matching the documented approach:

1. **Pre-scale the I/Q data** so peak excursion leaves headroom (don't drive to full scale). Normalize the personality's float output, then map to int16 with a safety factor.
2. Set the instrument's **runtime ARB scaling** with `:RADio:ARB:RSCaling <percent>` (factory preset is **70%**). Pick the largest value that does not produce a DAC over-range for your waveform.

Expose both: an encoder-side "digital backoff" and the instrument-side `RSCaling`. Default to encoder normalization to ~0.95 of int16 full scale **and** `RSCaling 70`.

### 5.3 SCPI download commands (volatile WFM1 path — recommended)
Before downloading, turn the ARB off to avoid overwriting the playing waveform:

```
:SOURce:RADio:ARB:STATe OFF
```

Then write the interleaved I/Q block to volatile waveform memory:

```
:MMEMory:DATA "WFM1:<file_name>", #<block>
```

where `#<block>` is an **IEEE-488.2 definite-length arbitrary block**:
`#` + (one digit giving the number of length digits) + (length digits = byte count) + (raw big-endian interleaved I/Q bytes). Example header for 2400 bytes: `#42400` followed by the 2400 bytes.

Optional marker file (separate path):
```
:MMEMory:DATA "MKR1:<file_name>", #<block>     ' marker bytes, one per sample
```
(Legacy FTP/`put` form used `/USER/MARKERS/<file_name>`; via SCPI the `MKR1:` logical prefix is the equivalent.)

**Alternative legacy E443xB-compatible path** (separate I and Q files, also accepted by the E4438C and auto-converted):
```
:MMEM:DATA "ARBI:<file_name>", #<I block>
:MMEM:DATA "ARBQ:<file_name>", #<Q block>
' non-volatile equivalents:
:MMEM:DATA "NVARBI:<file_name>", #<I block>
:MMEM:DATA "NVARBQ:<file_name>", #<Q block>
```
For v1, implement the **`WFM1:` interleaved path** as primary; keep `ARBI/ARBQ` as a fallback option.

> If you ever want waveforms to be **re-extractable** later, the docs require `:MEMory:DATA:UNPRotected "<filename>", <datablock>` — out of scope for v1 but note it exists.

### 5.4 Memory limits (waveform sample capacity)
| Baseband option | Samples available | Memory |
|---|---|---|
| 001 / 601 | 8,377,088 | 40 MB |
| 002 | 33,509,120 | 160 MB |
| 602 | 67,018,496 | 320 MB |

Validate the generated sample count against the connected unit's option before download; fail early with a clear message.

### 5.5 Select & play the downloaded waveform
After download, point the dual-ARB at the file, set sample clock, enable ARB and RF:

```
:SOURce:RADio:ARB:WAVeform "WFM1:<file_name>"
:SOURce:RADio:ARB:SCLock:RATE <sample_rate_Hz>     ' I/Q sample readout rate
:SOURce:RADio:ARB:STATe ON
:OUTPut:STATe ON                                   ' RF on
```
(For non-volatile storage, copy WFM1 → NVWFM, or load NVWFM → WFM1 before playback.)

---

## 6. Instrument Settings the app must control

Mirror the legacy "ESG Configuration" / "Advanced" menus. At minimum:

- **Frequency** — `:SOURce:FREQuency:FIXed <Hz>` (also support channel-number → frequency mapping per personality if relevant).
- **Output power / amplitude** — `:SOURce:POWer[:LEVel] <dBm>`.
- **RF output on/off** — `:OUTPut:STATe ON|OFF`.
- **Modulation on/off** — `:OUTPut:MODulation:STATe ON|OFF`.
- **ARB sample clock rate** — `:SOURce:RADio:ARB:SCLock:RATE`.
- **ARB runtime scaling** — `:SOURce:RADio:ARB:RSCaling`.
- **Reference** — external/internal 10 MHz (`:ROSCillator:SOURce`), warn user to connect ext ref *before* download if used.
- **ALC** state/bandwidth where relevant.
- "Advanced" passthrough: an editable raw-SCPI escape hatch so power users can send arbitrary commands (mirrors the original "Advanced" menu and is invaluable for debugging).

Always read back/verify after writing critical values and poll `:SYSTem:ERRor?`.

---

## 7. Waveform Engine (personality plug-in contract)

```csharp
public sealed class WaveformModel {
    public float[] I;          // normalized, ~[-1, +1]
    public float[] Q;
    public byte[]  Markers;    // optional, length == I.Length, or null
    public double  SampleRateHz;
    public string  Name;       // becomes WFM1:<Name>
}

public interface IWaveformPersonality {
    string   Id { get; }                 // e.g. "multitone"
    string   DisplayName { get; }
    UserControl CreateConfigPanel();     // WinForms config UI
    object   GetConfig();                // serializable settings (project save)
    void     LoadConfig(object cfg);
    WaveformModel Calculate(IProgress<int> progress);  // the "Calculate" button
    int?     RequiredOption { get; }     // null if generic ARB is enough
}
```

**v1 personalities to implement:**
1. **Custom IQ Import** — load I/Q from CSV/WAV/MAT/binary, resample if needed.
2. **Multitone** — N tones, spacing, per-tone power & phase (random/Newman phasing to control crest factor); mirrors instrument's multitone table.
3. **CW / Single tone** — trivial, good smoke test.
4. **AWGN** — band-limited Gaussian noise, configurable BW & crest-factor clipping.
5. **Custom digital modulation** — symbol mapper (ASK/FSK/MSK/PSK/QAM) + pulse-shaping FIR (RRC/RC/Gaussian), configurable symbol rate, oversampling, filter alpha.

Each plug-in is responsible **only** for producing normalized I/Q + sample rate. Crest-factor / CCDF is computed centrally for display.

---

## 8. UI Requirements (WinForms)

- **Connection Manager:** pick VISA resource (enumerate via VISA), test with `*IDN?`, show firmware + installed options (`*OPT?`), connection state indicator.
- **Personality workspace:** left = config panel from the active plug-in; right = plot tabs.
- **Plots (3 tabs):**
  - I & Q vs time (overlay or stacked).
  - Spectrum (FFT of complex I+jQ; windowed; dBc relative).
  - CCDF (complementary CDF of instantaneous power; report PAPR / crest factor).
  - Include zoom (rubber-band select) like the original's Tools→Zoom.
- **Action bar:** **Calculate**, **Download**, **Play/Stop RF**, plus a combined "Download & Play."
- **Instrument settings panel:** §6 fields, with read-back.
- **Project files:** save/recall full config as JSON (`*.ssproj`). Replaces legacy per-personality file dirs. Also support exporting the generated raw ARB block to disk for offline inspection.
- **Status/error log dock:** every SCPI write/read + `:SYSTem:ERRor?` results, timestamped. Crucial for bring-up.
- Long operations (Calculate, Download) run off the UI thread with progress + cancel.

---

## 9. Validation / Acceptance Tests

1. **`*IDN?` round-trip** over both GPIB and TCPIP.
2. **Block framing unit test** (no instrument): feed a known I/Q array → assert exact bytes of `#<header>` + big-endian interleaved payload against a golden vector.
3. **Round-trip CW:** generate a single tone offset +1 MHz from carrier; download; on a spectrum analyzer confirm the tone at `Fc + 1 MHz` at the set power.
4. **Multitone crest factor:** verify CCDF/PAPR matches expectation for Newman vs random phasing.
5. **Over-range guard:** intentionally over-scale → confirm we catch DAC over-range (instrument error queue) and surface it, and that 70% `RSCaling` clears it.
6. **Memory guard:** request a waveform larger than the unit's option capacity → blocked pre-download with a clear message.
7. **Min-sample guard:** <60 samples rejected; odd-sample warning.

---

## 10. Suggested project layout (for Claude Code)

```
SignalStudioReborn.sln
 |- SignalStudio.Core/                 (.NET 4.7.2 class lib)
 |   |- Visa/EsgInstrument.cs          // NI-VISA session + SCPI
 |   |- Visa/IInstrumentTransport.cs
 |   |- Arb/EsgArbEncoder.cs           // float->int16->interleave->BE->block
 |   |- Arb/Ieee4882Block.cs           // #<digits><len><bytes>
 |   |- Model/WaveformModel.cs
 |   |- Personalities/IWaveformPersonality.cs
 |   |- Personalities/Multitone/...
 |   |- Personalities/Awgn/...
 |   |- Personalities/CustomMod/...
 |   |- Personalities/CustomIq/...
 |   |- Dsp/ (FFT, FIR, CCDF, resampling)
 |- SignalStudio.Ui/                   (WinForms app)
 |   |- ConnectionManagerForm.cs
 |   |- MainForm.cs
 |   |- Plots/ (IQ, Spectrum, CCDF controls)
 |   |- Settings/InstrumentSettingsPanel.cs
 |- SignalStudio.Tests/                (block-framing & DSP unit tests)
```

NuGet/refs: NI-VISA .NET assemblies (`NationalInstruments.Visa` + `Ivi.Visa`, install NI-VISA runtime), a FFT lib (e.g. MathNet.Numerics) for spectrum/CCDF, Newtonsoft.Json for project files. WinForms charting: built-in `System.Windows.Forms.DataVisualization` or OxyPlot.

---

## 11. Open items to confirm on your own E4438C before coding §5

These are the points where public docs are slightly version-dependent — verify against your unit (and note your firmware from `*IDN?`):

- Exact accepted form of the marker file logical name (`MKR1:` vs `/USER/MARKERS/`) on your firmware.
- Whether your unit prefers `:RADio:ARB:WAVeform` vs `:RADio:ARB:SEQuence` selection for a single segment.
- `*OPT?` string format so the option/capacity gate in §5.4 parses correctly.
- Confirm `SCLock:RATE` upper bound for your hardware option.
- Confirm default GPIB address and that LAN VXI-11 is enabled (vs raw socket only).

---

## 12. Reference links

**Core download/format (build §5 from these):**
- Creating and Downloading Waveform Files (N5162A/N5182A/E4438C/E8267D), Keysight lit. E4400-90627 / 9018-05434 — https://www.keysight.com/us/en/lib/software-detail/instrument-firmware-software/signal-generator--mxg-xseries-psg-esg-download-assistant-and-matlab-84805.html
- Creating and Downloading Waveform Files (older E4400-90505 programming guide, ManualsLib mirror with the `MMEM:DATA "WFM1:..."` and `/USER/MARKERS/` text) — https://www.manualslib.com/manual/3217498/Agilent-Technologies-E4438c.html
- "Commands for Downloading Waveform Data" (IEEE-488.2 `#ABC` block explanation) — https://rfmw.em.keysight.com/wireless/helpfiles/n5106a/commands_for_downloading_waveform_data.htm
- MATLAB `agt_download_wave` for E4438C (authoritative statement of int16 / interleaved / big-endian / 60-sample min / memory table) — https://www.mathworks.com/matlabcentral/fileexchange/29973-agilent-e4438c-dowload-wave-file
- Creating and Downloading User-Data Files (PRAM, bit/binary user files, FIR coefficients) — https://www.testunlimited.com/pdf/an/E4400-90651.pdf

**SCPI references:**
- E4428C/38C ESG SCPI Reference Vol 1 (9018-01502), Vol 2 (E4400-90535 / 9018-01503 — http://literature.cdn.keysight.com/litweb/pdf/E4400-90535.pdf), Vol 3 (9018-04205)
- Backward-compatible SCPI commands (E4400-90543) — https://www.testunlimited.com/pdf/an/E4400-90543.pdf

**Instrument platform / options / capability:**
- E4438C Data Sheet (5988-4039EN) — https://www.keysight.com/us/en/assets/7018-01039/data-sheets-archived/5988-4039.pdf
- E4438C Configuration Guide (option list incl. 401/402/403/407/408/409/419, baseband 601/602) — https://dfzk-www.oss-cn-beijing.aliyuncs.com/www-PRD/resources/files/AG_E4438C_ConfigurationGuide_en.pdf
- E4438C ESG technical overview / Signal Studio personality catalog (N76xx) — https://www.testunlimited.com/pdf/an/5988-3935en.pdf
- E4428C/38C User's Guide (instrument settings, multitone table, ARB ops) — http://www.chiantech.com.tw/upload/e4438c-UG_1.pdf
- Firmware release descriptions (firmware-gated behaviors, `:MEM:DATA:UNPR`, RSCaling history) — https://www.keysight.com/upload/cmc_upload/All/C_05_84.htm

**Personality examples (for later phases, show original UI/feature framing):**
- Signal Studio for Bluetooth app note AN-1421 (configure→calculate→download flow, plots, file save under `\E4438C\Bt`) — https://www.manualslib.com/manual/2854241/Agilent-Technologies-E4438c.html
- Signal Studio for 1xEV-DO (Option 404) app note — https://www.yumpu.com/en/document/view/19745572/agilent-signal-studio-for-1xev-do-e4438c-esg-vector-signal-
- Signal Studio for 802.11a WLAN product overview — https://keysight-docs.s3-us-west-2.amazonaws.com/keysight-pdfs/E4438C-410/E4438C+Signal+Studio+for+802.11a+WLAN+Product+O.pdf

**Firmware download (to know what you're running):**
- E4428C/E4438C ESG Signal Generator Firmware — https://www.keysight.com/us/en/lib/software-detail/instrument-firmware-software/e4428ce4438c-esg-signal-generator-firmware-1217286.html

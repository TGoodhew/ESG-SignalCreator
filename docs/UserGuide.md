# ESG-SignalCreator — User Guide

> 🌐 **English** (this page) · Dansk: [Brugervejledning](da/UserGuide.md)

A complete reference for **ESG-SignalCreator**, a Windows application that builds arbitrary I/Q
waveforms, plays them on an Agilent/Keysight **E4438C** ESG vector signal generator, and (optionally)
verifies the result on a **VSA** (an Agilent **E4406A** or a Keysight **N9010A**) — with an in-app **Claude assistant** that can drive the whole
flow in natural language.

This document explains *what the app does and how every feature works*. For step-by-step walkthroughs,
see [Tutorials.md](Tutorials.md).

---

## 1. What the app is

ESG-SignalCreator turns *intent* ("a 4-tone signal at 1 GHz, −10 dBm, lowest PAPR") into a real RF
signal coming out of the generator, and can prove the RF matches the intent by measuring it on the
analyzer. It is organised around four ideas:

- **Signal personalities** — pluggable *sources* that each produce a normalized baseband **I/Q
  waveform** (CW, Multitone, Multi-Carrier, Custom Digital Modulation, AWGN, Import I/Q).
- **A deliberate pipeline** — you **Calculate** the waveform on the PC, **Download** it to the
  generator's ARB memory, then **Play** it (arm the ARB + turn on RF). Each stage is explicit so you
  always know what is on the instrument.
- **Closed-loop verification** — with a VSA (E4406A or N9010A) connected to the generator's RF output, the app
  measures the played signal (channel power, PAPR, tone frequency, ACP, …) and compares **expected vs
  measured**.
- **An assistant** — an opt-in pane where Claude uses the *same* operations you do, through a guarded
  tool surface (nothing reaches the DAC or RF without your explicit approval).

Everything except live instrument I/O works **offline**: you can build, preview, validate, and save
waveforms with no instrument attached.

The app is C#/.NET Framework 4.7.2 / WinForms. Launch the modern shell (`StudioForm`) normally, or pass
`--classic` on the command line for the original single-window UI (`MainForm`).

---

## 2. Key concepts

- **I/Q waveform** — a complex baseband signal, a stream of (I, Q) sample pairs at a **sample clock**
  (sample rate). The ESG's ARB plays these samples to reconstruct the modulated RF carrier.
- **ARB (arbitrary waveform generator)** — the ESG option (001/601 or 002/602) that stores and replays
  I/Q samples from "WFM1" volatile memory. Requires a baseband generator option.
- **PAPR / crest factor** — peak-to-average power ratio (dB). High PAPR (e.g. noise, OFDM-like
  multitone) stresses amplifiers; low-PAPR phasing (Newman) packs the same tones with smaller peaks.
- **Runtime scaling** — the ARB plays samples at a fraction of full scale (default 70%) to leave DAC
  headroom and avoid over-range clipping.
- **Sample granularity / minimum samples** — the ESG requires the waveform length to meet a minimum
  (≈60 samples) and a granularity; the validator checks this.
- **Seamless loop** — a played waveform loops; if the end doesn't line up with the start there is a
  discontinuity ("seam"). Integer-cycle lengths loop cleanly.
- **VISA resource** — the address string for an instrument, e.g. `TCPIP0::192.168.1.82::inst1::INSTR`
  (LAN) or `GPIB0::17::INSTR` (GPIB). The app uses any installed VISA provider to open it.

---

## 3. Requirements & installation

- **Windows** with **.NET Framework 4.7.2** or later.
- For **live instrument control**, any **IVI-compliant VISA runtime** installed — Keysight IO
  Libraries Suite, NI-VISA, Rohde & Schwarz, Rigol, etc. The app talks to instruments through the
  vendor-neutral **IVI `GlobalResourceManager`**, so whichever provider is installed is used
  automatically (for TCPIP/LAN, GPIB, USB, and serial resources).
- **Install** from the MSI on the project's Releases page (per-machine, x64; Start-menu + optional
  desktop shortcuts; clean uninstall). The installer enforces .NET 4.7.2 and detects a VISA runtime.
  See [Packaging.md](Packaging.md) for build/CI details.
- **On-instrument playback** needs an E4438C with a baseband/ARB option (001/601 or 002/602).
  **Verification** needs a VSA (E4406A or N9010A) on the generator's RF output.

---

## 4. The main window

The shell is split into four regions:

### 4.1 Top toolbar (actions)

| Button | What it does |
|--------|--------------|
| **Connect…** | Open the connection manager and connect to the ESG (VISA resource or GPIB board/address). |
| **Connect VSA…** | Connect the analyzer (E4406A or N9010A, per the **VSA model** toggle), including the RF-path safety settings (§9). |
| **Calculate** | Generate the I/Q waveform from the current source + impairments (off the UI thread, with a progress bar). Updates the plots, validation and readout. No hardware. |
| **Download** | Encode the waveform and push it to the generator's ARB memory (WFM1). Requires a connection. |
| **Play** | Arm the ARB and turn RF **on**. |
| **Stop** | Disarm the ARB and turn RF **off**. |
| **Verify** | Closed-loop measure the played signal on the analyzer and show expected-vs-measured (§9). |
| **Path cal…** | Run the path-calibration wizard to capture cable loss + analyzer offset (§9). |
| **Verify install…** | Run the install self-test — a CW → AM → FM → I/Q battery measured on the analyzer (§9.7). |
| **Reference** | Lock the ESG and the analyzer to independent timebases or a common external 10 MHz. |
| **VSA model** | Toggle which analyzer the app targets — E4406A or N9010A (§9). |
| **VSA Mode** | Pick the analyzer measurement mode from the modes actually installed on the unit. |
| **Calc → DL → Play** | Run all three pipeline stages in sequence. |
| **Save… / Open…** | Save or load a project (`.ssproj`). |

The status strip at the bottom shows a status message, the **Online/Offline** indicator, and the
connected instrument model.

### 4.2 Left navigation tree (views)

Selecting a node shows its card in the centre:

- **Source** — pick a signal personality and edit its parameters.
- **Impairments** — optionally apply I/Q, AWGN, CFR and filter impairments.
- **Sequence** — build a sequence of waveform segments.
- **Instrument settings** — frequency, amplitude, RF/modulation, ARB sample clock, runtime scaling, with read-back.
- **SCPI console** — send raw SCPI and watch a timestamped log.
- **Notifications** — the validation/dependency-checker output.
- **Verification** — the Expected-vs-Measured table from the last Verify run.
- **Assistant** — the in-app Claude assistant pane.

### 4.3 Right dock (plots & state)

- **Three plot panes**, each with a view selector: **I/Q vs time**, **Spectrum** (FFT), **Constellation**, **CCDF**, and **Eye**. Default views are IQ / Spectrum / Constellation; rubber-band zoom is supported.
- A **results readout** strip: sample count, sample clock, duration, download size (bytes), peak, RMS, PAPR, and 99% occupied bandwidth.
- A **progress bar** for Calculate.
- A **play-state indicator** (Idle / Busy / Waiting-for-trigger / Playing).

---

## 5. Signal sources (personalities)

Open the **Source** view, choose a personality from the picker, and edit its parameters in the panel.
Each personality produces a `WaveformModel` (I/Q + sample rate) when you Calculate.

### 5.1 CW / Single tone
A single continuous-wave tone. Parameters: **frequency offset** from the carrier (Hz), **amplitude**
(dBFS, 0 = full scale, negative = backed off), and **starting phase** (degrees). Useful as a reference
and for frequency/level checks.

### 5.2 Multitone
N equally-spaced tones. Parameters: **number of tones**, **tone spacing** (Hz, or auto), and **phase
strategy** — **Newman** (minimizes PAPR), **Random**, or **Zero** (all phases aligned → high PAPR). A
classic test for amplifier linearity and PAPR handling.

### 5.3 Multi-Carrier
Several independently-placed carriers (each can be its own modulation), summed into one waveform — for
multi-channel / multi-standard scenarios.

### 5.4 Custom Digital Modulation
A digitally-modulated carrier. Parameters: **modulation format** (BPSK, QPSK, 8PSK, 16/64/256-QAM,
MSK…), **symbol rate** (Hz), a **pulse-shaping filter** (RRC, RC, Gaussian, or none) with **roll-off /
BT** (alpha), and a **payload** pattern (PN9/PN15/PN23, all-ones/zeros, random). Used for ACP/ACPR and
modulation-quality work.

### 5.5 Pulse Building
A radar-style pulse train (Signal Studio for Pulse Building, N7620A). Parameters:
**pulse width** (s), nominal **pulse repetition interval / PRI** (s, ≥ pulse width), an optional
raised-cosine **rise/fall** time (s, 0 = rectangular edges), a **start delay** (s), and the
**intra-pulse modulation**:
- **None** (gated CW burst);
- FM formats — **Linear FM chirp**, **Non-linear FM chirp** (with a **curvature** 0…<1), and
  **FM step** (stepped frequency) — all using a swept **bandwidth** in Hz; FM/AM step use a
  **step count**;
- **AM step** (rising amplitude staircase);
- phase codes — **BPSK** and **QPSK** (a seedable pseudo-random code with a **chip count** + **seed**)
  and a **Barker phase code** (length 2/3/4/5/7/11/13);
- polyphase codes — **Frank** (order N, length N²) and **P4** (configurable length).

**PRI patterning** selects how the gap between pulses is chosen: **Fixed** (the nominal PRI),
**Staggered** (a repeating pattern of intervals), or **Jittered** (the nominal PRI ± a seeded random
peak). **Per-pulse offset tables** (frequency in Hz, phase in degrees, power in dB) cycle across the
train so successive pulses can be frequency-agile, phase-stepped, or power-staircased. A single pulse
template is tiled to fill the waveform; a one-sample **marker** is emitted at each pulse start (handy as
an ARB trigger/scope sync). Used for radar/EW receiver test and pulse-compression work. The remaining
N7620A Option 205/206 features (custom envelope shapes, antenna-scan patterning, pattern nesting, CSV
import/export, and scenario impairments) are not yet implemented (see issue #179).

### 5.6 AWGN
Band-limited additive white Gaussian noise. Parameters: **noise bandwidth** (Hz), **carrier-to-noise
ratio** (dB), and optional **peak clipping**. AWGN has a high crest factor (~10 dB) — a good headroom
and CCDF test.

### 5.7 Import I/Q
Load I/Q from a file — the app's equivalent of the Signal Studio Toolkit (N7622A) "bring your own I/Q"
workflow. Parameters: **file path** (you supply it), **format** (**Auto**-detect by extension, delimited
text CSV/TSV, raw interleaved little-endian **Int16** `.bin`/`.iq`, Agilent/Keysight big-endian
**Int16** `.agt` — the ESG's native ARB byte order — Agilent/Keysight big-endian **14-bit**
(`AgilentInt14Be`; the 14-bit sample is left-justified in each 16-bit word, low 2 bits are markers),
16-bit PCM **WAV**, or **MATLAB Level-5** `.mat`), source **sample rate** (Hz), optional **I/Q swap**,
and a **scale** multiplier. Set the format explicitly to force a byte order when an extension is
ambiguous. Lets you replay externally-captured or externally-generated (MATLAB/C++) signals.

**MATLAB `.mat` (MAT File 5)** — both uncompressed (`save -v6`) and the default zlib-compressed (v7)
files are read. The first suitable numeric array is used: a **complex** vector maps to I/Q, a real
**2×N or N×2** array maps its two rows/columns to I and Q, and a real vector maps to I (Q = 0).

**Marker / trigger authoring** — attach a marker bit stream to the imported ARB segment: **None**,
**Start** (a single marker at sample 0 — a segment trigger), **Periodic** (a marker every *period*
samples), or **Range** (a marker block of *length* samples from *start*). The markers drive the ESG's
EVENT/marker output on playback.

### 5.8 Multitone Distortion
A dense multitone / noise-power-ratio (NPR) stimulus for amplifier and converter linearity testing
(Signal Studio for Multitone Distortion, N7621B). Parameters: **tone count** (2 up to 4097), **tone
spacing** (Hz), **centre offset** (Hz), a **phase preset** — **Parabolic** (Newman, low PAPR), **Random**,
or **Constant** (aligned, high PAPR) — and an optional **NPR notch** (enable, **width** Hz, **offset** Hz
from band centre) that clears a band of tones so you can measure intermodulation/noise falling into the
notch. Composite bandwidth ≈ tone count × spacing; live PAPR is reported.

**Per-tone tables.** Optional **per-tone magnitude** (dB) and **per-tone phase** (degrees) tables cycle
across the comb (tone *k* uses element *k* mod length), overriding the uniform power and the phase preset
— use them to shape the comb or hand-tune the crest factor. **Pre-distortion correction** (enable) takes
**measured per-tone magnitude/phase error** tables (from a signal analyzer, e.g. via the closed-loop
Verify path) and subtracts them from the base per-tone values, pre-inverting the measured channel response
so the emitted comb lands flat and IMD in the NPR notch is suppressed. Out-of-band notch-cancellation
(injecting correction tones inside the notch) is not yet implemented.

### 5.9 Jitter Injection
A jittered clock/tone for receiver jitter-tolerance testing (Signal Studio for Jitter Injection,
E4438C-SP1). Jitter is applied as timing (phase) modulation of a sinusoidal clock, so the envelope stays
constant. Parameters: **clock rate** (Hz, the signal being jittered); **periodic jitter** shape
(**Sinusoidal / Square / Triangle / SawTooth / Exponential / Custom**, or None), **rate** (Hz), and
**amplitude** (UI peak-to-peak); and optional **random** Gaussian jitter (**RMS** in UI + **seed**).
Periodic + random combine into a **composite** signal, and a given seed reproduces an identical
sequence (digital repeatability). Amplitudes are in unit intervals (1 UI = one clock period).

**Custom shape** takes a one-period profile table (normalized to ±1), interpolated across the jitter
period. **SJ frequency sweep** (enable) sweeps the sinusoidal-jitter frequency **linearly** or
**logarithmically** from a start to a stop frequency across the record — the classic way to trace a
jitter-tolerance mask. During a sweep the amplitude can **follow a tolerance mask**: a **Custom** mask
(your frequency/UI breakpoints) or a predefined **ITU-T G.8251** mask (**OC-48 / OC-192 / OC-768**).
*The predefined G.8251 corner values are approximate/representative — verify them against the standard
before conformance use.* **Achievable-range enforcement** rejects a clock or jitter frequency at/above
Nyquist and, if a **max jitter (UI pp)** cap is set, a periodic amplitude above it.

### 5.10 GSM/EDGE
A GSM-family carrier (Signal Studio for GSM/EDGE, N7602B) with two **modulations**:

- **GMSK** (GSM/GPRS) — the constant-envelope modulation: data bits drive an NRZ frequency pulse,
  Gaussian-filtered (**BT**, default 0.3) and integrated to continuous phase at modulation index 0.5.
- **EDGE 8-PSK** (v2, #186) — **3π/8-continuously-rotated 8-PSK** (3 bits/symbol), pulse-shaped and
  peak-normalized (non-constant envelope, unlike GMSK). The pulse is a representative RRC stand-in for
  the linearised GMSK pulse.

Parameters: **modulation**, **symbol rate** (Hz, 270.833 ksym/s), **samples per symbol**, **symbol
count**, **BT** (GMSK) / **EDGE RRC β**, Gaussian span, and **data source**. Sample rate = symbol rate ×
samples/symbol. Full burst/training-sequence framing and channel coding are not yet implemented.

### 5.11 Bluetooth
A Bluetooth carrier (Signal Studio for Bluetooth, N7606B) with two **modulations**:

- **GFSK** (Basic Rate / LE) — Gaussian-filtered FSK (BT 0.5) integrated to continuous phase at a
  configurable **modulation index** (BR ≈ 0.32, LE ≈ 0.5); constant envelope.
- **EDR** (v2, #190) — differential **π/4-DQPSK** (2 Mbps, `Edr2Mbps`) or **8-DPSK** (3 Mbps,
  `Edr3Mbps`), pulse-shaped (RRC β 0.4) and peak-normalized; non-constant envelope. EDR keeps the
  1 Msym/s symbol rate (2/3 bits per symbol).

Parameters: **modulation**, **symbol rate** (Hz; BR/LE-1M/EDR = 1 M, LE-2M = 2 M), **samples per
symbol**, **symbol count**, GFSK **modulation index** / **BT**, EDR **RRC β**, and **data source**. GFSK
shares the CPM engine with GMSK (GMSK is GFSK at index 0.5). The LE coded PHY, packet framing, and
hopping are not yet implemented.

### 5.12 3GPP W-CDMA FDD
A W-CDMA downlink-style signal (Signal Studio for 3GPP W-CDMA FDD, N7600B). Data
symbols are spread by an **OVSF** (Walsh) code, complex-**scrambled** by a PN sequence, and
root-raised-cosine shaped (**β** = 0.22) at the **3.84 Mcps** chip rate. Parameters: **chip rate**,
**samples per chip**, **symbol count**, **spreading factor** (power of two) and **OVSF index**,
**modulation** (QPSK…), **RRC β**, and **scramble** (enable + seed). Sample rate = chip rate × samples/
chip.

**Multi-code composite** (v2, #183): set **code channel count** > 1 to sum that many orthogonal OVSF code
channels (codes 0…N-1, equal power) into a multi-code downlink — the composite is scrambled and shaped as
one signal (raising the PAPR). Still representative — the named physical channels (CPICH/P-CCPCH/SCH),
slot/frame structure, TFCI, transmit diversity, and HSPA channels are not yet implemented.

### 5.13 3GPP W-CDMA HSPA
An HS-PDSCH-style HSPA signal (Signal Studio for 3GPP W-CDMA HSPA, E4438C-419). Same DSSS
structure as §5.12 but defaulting to **16QAM** on an **SF-16** code — the defining HSPA feature is
higher-order modulation on the high-speed shared channel. Parameters mirror W-CDMA FDD plus a
**modulation** choice (QPSK / 16QAM / 64QAM). **Multi-code** (v2, #184): set **code channel count** > 1
to sum multiple **HS-PDSCH multicodes** (as HSDPA does) into the composite. Representative — HS-SCCH/
HS-DPCCH, E-DCH channels, H-ARQ, and TTI structure are not yet implemented.

### 5.14 3GPP2 CDMA (cdma2000)
A cdma2000 forward-link-style signal (Signal Studio for 3GPP2 CDMA, N7601B). Same DSSS structure as
§5.12 at the **1.2288 Mcps** chip rate: QPSK data spread by a **Walsh** code, PN-scrambled, pulse-shaped
(RRC approximation of the cdma2000 filter). Parameters mirror W-CDMA FDD. **Multi-code** (v2, #185): set
**code channel count** > 1 to sum several **Walsh code channels** into a multi-channel forward-link
composite. Representative — pilot/sync/paging channels, radio configurations, and frame/PCG structure
are not yet implemented, and the exact cdma2000 baseband filter is approximated by RRC.

### 5.15 TD-SCDMA
A TD-SCDMA signal (Signal Studio for TD-SCDMA, N7612B) at the **1.28 Mcps**
low-chip-rate: QPSK/16QAM/64QAM data spread by an OVSF code, scrambled, RRC-shaped (β = 0.22).
Parameters mirror W-CDMA FDD. **Multi-code** (v2, #187): set **code channel count** > 1 to sum several
code channels within a timeslot into the composite. Representative — the 5 ms sub-frame / 7-timeslot
TDD burst structure (DwPTS/UpPTS/GP), midamble codes, switching points, and HSDPA channels are not yet
implemented.

### 5.16 S-DMB (CDM, approximate)
An **approximate** Satellite-DMB stimulus (a v1 of Signal Studio for S-DMB, E4438C-407). ⚠️ The S-DMB
(System E) air interface could not be confirmed from primary literature, so this generates a
representative **CDM** signal (QPSK spread by an OVSF code, scrambled, RRC-shaped) — **not a verified
S-DMB waveform.** Chip rate, spreading, FEC, and framing are placeholders; treat the output as "a QPSK
spread-spectrum signal." Parameters mirror the other CDMA-family personalities.

### 5.17 3GPP LTE FDD
A downlink OFDM signal with LTE numerology (Signal Studio for 3GPP LTE, N7624B). Uses a **15 kHz**
subcarrier spacing and the standard **FFT size / occupied-subcarrier count** for the selected
**channel bandwidth** (1.4 / 3 / 5 / 10 / 15 / 20 MHz → 128…2048-point FFT, RB×12 subcarriers).
Parameters: **bandwidth**, **symbol count**, and subcarrier/PDSCH **modulation** (QPSK / 16QAM / 64QAM /
256QAM). Two modes:

- **Generic** (default) — a plain OFDM data fill on all occupied subcarriers, for occupied-bandwidth /
  PAPR / spectral checks.
- **Frame-structured** (v2, #188) — enable **frame structured** to build a proper **E-UTRA downlink
  radio-frame**: a 10 ms frame of 0.5 ms slots with **per-symbol cyclic prefix** (**Normal** = 7
  symbols/slot, or **Extended** = 6), the **PSS** (Zadoff-Chu) and **SSS** synchronisation signals on
  their standard symbols (central 62 subcarriers of slots 0 and 10), **CRS** cell-specific reference
  signals (antenna port 0) at the standard positions, and a **PDSCH** data fill on the rest — all
  driven by the **physical cell ID** (0…503). Set the **subframe count** (10 = one radio frame).

The frame-structured mode follows 3GPP TS 36.211 for the PSS/SSS/CRS sequences and positions; it is a
representative frame, not fully conformant: single antenna port, no PBCH/PDCCH payloads or channel
coding, no PDSCH scrambling. Uplink, MIMO, HARQ, and carrier aggregation remain out of scope.

### 5.18 3GPP LTE TDD
The same LTE OFDM numerology and parameters as §5.17 — LTE's physical layer is common to FDD and TDD
(Signal Studio for 3GPP LTE TDD, N7625B). Two modes, like FDD:

- **Generic** (default) — a plain downlink OFDM data fill.
- **Frame-structured** (v2, #189) — enable **frame structured** to build a proper **E-UTRA TDD downlink
  frame** (frame structure type 2): the **D/S/U subframe pattern** of the selected **uplink-downlink
  configuration** (**0…6**), the **special subframe** split into DwPTS / GP / UpPTS by the
  **special-subframe configuration** (**0…9**), TDD-positioned **PSS** (in the DwPTS of subframes 1 & 6)
  and **SSS** (last symbol of subframes 0 & 5), **CRS**, and a **PDSCH** fill on the downlink symbols.
  Uplink subframes and the GP/UpPTS are transmitted silent (the app is a downlink generator). Also set
  the **cyclic prefix** (Normal/Extended), **physical cell ID** (0…503), and **subframe count**.

Same scope caveats as FDD (§5.17): representative frame, single antenna port, no channel coding, and
uplink physical channels / MIMO / HARQ / carrier aggregation remain deferred.

### 5.19 802.11 WLAN (OFDM)
An 802.11 OFDM signal (Signal Studio for 802.11 WLAN, N7617B) with 11a/g/n numerology: **312.5 kHz**
subcarrier spacing, a **64-point** (20 MHz) or **128-point** (40 MHz) FFT with the standard
used-subcarrier count and cyclic prefix. Parameters: **bandwidth**, **symbol count**, and subcarrier
**modulation** (BPSK…256QAM). Two modes:

- **Generic** (default) — a plain OFDM data fill, for occupied-bandwidth / PAPR / spectral checks.
- **Frame-structured** (v2, #191, 20 MHz only) — enable **frame structured** to build a representative
  **802.11a/g PPDU**: an optional **L-LTF** training preamble (two long training symbols + double guard
  interval) followed by data OFDM symbols that carry the four **pilot subcarriers** (±7, ±21) with the
  standard polarity, using a selectable **guard interval** (**Long** 0.8 µs / **Short** 0.4 µs, the
  802.11n short-GI option).

The L-LTF sequence and pilot positions/polarity follow IEEE 802.11. It's a representative PPDU — the
L-STF/L-SIG fields, channel coding/interleaving, MAC framing, MIMO, and 80/160 MHz (11ac/ax, which
exceed the ESG) remain deferred.

### 5.20 802.16-2004 WiMAX (OFDM)
A fixed-WiMAX (IEEE 802.16-2004) **256-FFT OFDM** signal (Signal Studio for 802.16-2004, N7613A) with
200 used subcarriers. Parameters: **channel bandwidth** (Hz; sample rate ≈ bandwidth × 8/7),
**cyclic-prefix ratio** (1/4, 1/8, 1/16, 1/32), **symbol count**, and **modulation** (BPSK…64QAM). Two
modes:

- **Generic** (default) — a plain OFDM fill of the 200 used subcarriers.
- **Frame-structured** (v2, #192) — enable **frame structured** for the exact 256-FFT map with the eight
  **pilot subcarriers** (±13, ±38, ±63, ±88; BPSK, modulated by the standard 802.16 pilot PRBS), and an
  optional **preamble** symbol (energy on every 4th subcarrier → four identical quarter-symbol
  repetitions in time, the WiMAX-preamble hallmark).

Pilot positions and the pilot PRBS follow IEEE 802.16-2004. Representative frame — the exact two-symbol
long preamble, FCH, DL/UL-MAP, DCD/UCD, RS-CC channel coding, and MAC framing remain deferred.

### 5.21 802.16e Mobile WiMAX (OFDMA)
A mobile-WiMAX (IEEE 802.16e) **scalable-OFDMA** signal (Signal Studio for 802.16 WiMAX, N7615B) at the
fixed **10.9375 kHz** subcarrier spacing, with a selectable **FFT size** (128 / 512 / 1024 / 2048 for
≈ 1.25 / 5 / 10 / 20 MHz) so the sample rate scales with FFT. Parameters: **FFT size**, **cyclic-prefix
ratio**, **symbol count**, and **modulation** (QPSK…64QAM). Two modes:

- **Generic** (default) — plain OFDM (no OFDMA subchannel permutation).
- **Frame-structured** (v2, #193) — enable **frame structured** for a DL-OFDMA-style frame: an optional
  **preamble** symbol (a BPSK PN carried on every 3rd used subcarrier — the OFDMA-preamble spacing) and
  data symbols with a **DL-PUSC pilot pattern** (two pilots per 14-subcarrier cluster, at cluster
  positions 4 and 8).

The preamble's every-3rd-subcarrier layout and the 14-subcarrier DL-PUSC cluster follow the 802.16e
structure, but this is a representative frame — the exact preamble PN per IDcell, the full
PUSC/FUSC/AMC subchannel permutation, FCH/DL-MAP/UL-MAP, MIMO, and CTC/CC coding remain deferred.

### 5.22 T-DMB (DAB COFDM)
The DAB COFDM signal underlying Terrestrial-DMB (Signal Studio for T-DMB, N7616B). Keeps a
**2.048 MHz** signal bandwidth across all four **transmission modes** (I/II/III/IV), which set the FFT
size (2048 / 512 / 256 / 1024), active carriers, and guard interval. Parameters: **mode**, **symbol
count**, **data source**. Two modes:

- **Generic** (default) — a plain-QPSK OFDM fill (DQPSK approximated by QPSK).
- **Frame-structured** (v2, #195) — enable **frame structured** for a DAB transmission frame: the
  **synchronisation channel** (a **null symbol** of silence for frame sync, then a **phase-reference
  symbol** carrying a known per-carrier phase) followed by **differentially-encoded DQPSK** data symbols
  (each carrier's phase accumulates the QPSK data delta from the previous symbol).

The null + phase-reference symbols and the differential (DQPSK) encoding follow the DAB frame structure
(ETSI EN 300 401). Representative — the exact phase-reference table, the FIC/MSC multiplex, the TII, and
convolutional coding remain deferred.

### 5.23 Digital Video (DVB-T COFDM)
A DVB-T COFDM signal for an 8 MHz channel (Signal Studio for Digital Video, N7623B). Parameters:
**transmission mode** (**2K** = 2048-FFT / **8K** = 8192-FFT), **guard-interval** ratio (1/4…1/32),
**symbol count**, and **modulation** (QPSK / 16QAM / 64QAM). Sample rate is the DVB-T elementary rate
(64/7 MHz). Two modes:

- **Generic** (default) — a plain OFDM data fill of the active carriers.
- **Frame-structured** (v2, #196) — enable **frame structured** to insert the standard DVB-T **scattered
  pilots**: on each symbol the carriers where `k mod 12 == 3·(l mod 4)` carry boosted (4/3) BPSK pilots,
  so the pilot pattern shifts by 3 subcarriers every symbol and repeats every 4 symbols. Pilot values
  come from the DVB-T reference PRBS (X¹¹ + X² + 1).

The scattered-pilot positions, 4/3 boosting, and reference PRBS follow ETSI EN 300 744. Representative —
the continual/TPS carriers, PRBS energy dispersal, RS/convolutional coding, and MPEG-TS framing remain
deferred, and the other digital-video standards (ISDB-T, ATSC 8VSB, DVB-C/S QAM, DTMB) are not implemented.

### 5.24 Broadcast Radio (FM)
An analog FM broadcast signal (Signal Studio for Broadcast Radio, N7611B). The baseband multiplex is an
**audio test tone** (mono), optionally with a **19 kHz stereo pilot** and a **38 kHz DSB-SC** stereo
subcarrier, frequency-modulated onto the carrier. Parameters: **audio-tone frequency**, **stereo** on/off,
**peak deviation** (75 kHz), **sample rate**, and **length**.

**RDS** (v2, #194): enable **RDS** to add the **57 kHz** data subcarrier — a **1187.5 bps** biphase
(Manchester) data stream (differentially-encoded PRBS), DSB-SC on 57 kHz (3× the 19 kHz pilot), with a
configurable **RDS deviation** (~2 kHz of the 75 kHz total). Representative — a single test tone rather
than program audio, real RDS group content (PS/PTY/RT/…), pre-emphasis, and SCA are not implemented, and
the digital broadcast formats (DAB/DAB+ → see §5.22 T-DMB; XM/HD Radio) are covered elsewhere or deferred.

---

## 6. Impairments

The **Impairments** view applies optional, independently-toggled effects to the calculated waveform (in
order) so you can model real-world imperfections or test correction:

- **I/Q impairments** — gain imbalance, quadrature (phase) error, and DC offset. (A gain imbalance
  produces a measurable image tone — see the verification tutorials.)
- **AWGN** — add noise to the signal at a set level.
- **CFR (crest-factor reduction)** — reduce PAPR (peak windowing/clipping) to test or emulate CFR.
- **Filter** — apply an additional FIR filter.

Each has a checkbox to enable it and a property grid to edit its settings. Impairments are applied
during **Calculate**, after the source produces its baseband I/Q.

---

## 7. The pipeline

The deliberate three-stage flow keeps you in control of what reaches the hardware:

1. **Calculate** — builds the I/Q off the UI thread (progress bar), applies any enabled impairments,
   then refreshes the three plots, runs **validation** (§8), and updates the **readout**. No hardware
   is touched, so this is always safe.
2. **Download** — turns the ARB off, encodes the I/Q to **interleaved 16-bit, two's-complement,
   big-endian** samples, frames them as an IEEE-488.2 definite-length block, and writes them to the
   generator's volatile **WFM1** memory. Requires a connection.
3. **Play / Stop** — **Play** selects + arms the ARB segment (at the sample clock, with runtime
   scaling) and turns RF on, showing the play-state indicator; **Stop** disarms and turns RF off.

**Calc → DL → Play** runs all three in one click. The download size and target are shown in the
readout / notifications.

---

## 8. Validation (Notifications)

After every Calculate the **dependency checker** reviews the waveform against the connected (or default)
target capability profile and lists findings in **Notifications** with severities (Info / Warning /
Error). It checks:

- **Minimum samples** and **granularity** (the ARB's length rules).
- **Memory cap** — does the waveform fit the installed baseband option's sample memory?
- **Sample-clock and carrier limits** — within the instrument's range?
- **DAC over-range** — would the samples clip at the chosen scaling?
- **Loop seam** — will the waveform loop seamlessly (integer-cycle length)? A discontinuity is a
  warning.

Errors should be resolved before downloading; the verification path and the assistant both re-run this
checker as a safety gate before any hardware action.

---

## 9. VSA verification (E4406A or N9010A)

With a **VSA** on the generator's RF output, the app becomes a closed-loop *generate → measure →
compare* system. The analyzer only ever **receives** RF. Two analyzers are supported: the Agilent
**E4406A** and the Keysight **N9010A (EXA)**.

**Choosing the analyzer.** The **VSA model** toggle on the action bar (next to **Connect VSA…**)
selects which analyzer the app targets — **E4406A** or **N9010A** — and is remembered between sessions.
The selection drives the connect dialog's title, its default interface and address hint (the E4406A
defaults to GPIB, e.g. `GPIB0::17::INSTR`; the N9010A defaults to LAN/USB, e.g.
`TCPIP0::<ip>::hislip0::INSTR`), the per-model input-damage default, and the identity check —
connecting an instrument that doesn't match the selected model is refused. The N9010A is validated
against the Keysight X-Series manuals; the E4406A path is additionally hardware-validated. Confirm the
N9010A's max safe input against its data sheet before driving power (see below).

> The N9010A runs periodic **auto-alignments** that can pause a measurement for seconds. The app waits
> these out using a Service-Request (SRQ) notification rather than a fixed timeout, so a measurement that
> coincides with an alignment completes normally instead of failing spuriously.

### 9.1 Connecting the analyzer (safety first)
**Connect VSA…** opens the VSA connection form, which includes the **RF-path safety** settings:

- **Armed** — turn this on when the analyzer is physically on the ESG output and must be protected.
- **Analyzer max safe input (dBm)** — the damage threshold, seeded from the selected model (E4406A
  type-N input ≈ +35 dBm, default gate +30 dBm; N9010A +30 dBm / 1 W max safe input per its data sheet,
  5989-6529EN). Override it for your unit.
- **Path loss (dB)** — any inline pad/attenuator between the ESG and the analyzer.

When armed, the **power safety gate** blocks any commanded ESG power that would put more than the safe
level at the analyzer input (accounting for path loss). This gate guards both the manual UI and the
assistant.

### 9.2 Verify (expected vs measured)
**Verify** measures the played signal and compares it to expectations, populating the **Verification**
table (metric, expected, measured, Δ, tolerance, pass/fail, with a summary):

- **Channel power** vs the commanded level minus path loss.
- **PAPR** vs the value computed from the generated I/Q.
- **Tone frequency** (for a single tone) vs carrier + offset.

### 9.3 Path-calibration wizard
**Path cal…** drives a clean unmodulated carrier at a known level, measures it on the analyzer, and
records *commanded − measured* as the inline **path loss** — applied to both the safety gate and Verify
so subsequent runs are self-consistent. RF is returned off when done.

### 9.4 Reference locking
The **Reference** menu sets the ESG and the analyzer to **independent** internal timebases or to a
**common 10 MHz external** reference (a house reference or the ESG's 10 MHz OUT cabled to the analyzer)
for clean frequency comparisons. It reports the resulting source of each instrument.

### 9.5 VSA measurement mode
The **VSA Mode** menu lists the measurement modes actually installed on the unit (read live from the
analyzer's `:INSTrument:CATalog?`): always **Basic**, plus any option-gated communications-standard
personalities (GSM, EDGE, cdmaOne, cdma2000, 1xEV-DO, W-CDMA, NADC, PDC, iDEN). Selecting one switches
the analyzer's mode; an uninstalled mode is refused with a message that lists what *is* installed
(see §9.8).

### 9.6 Measurements
Under the hood the app provides typed VSA measurements (also exposed to the
assistant, §10): **Channel Power**, **ACP/ACPR**, **CCDF / PAPR** (Power Statistics), **Spectrum**
marker (tone frequency/power, occupied BW), **Waveform** (time-domain peak/mean/peak-to-mean), and
**Power-vs-Time** with a configurable **power mask** (pass/fail over time windows) for bursted signals.

### 9.7 Install verification self-test
**Verify install…** runs a short, guided **generate → play → measure → compare** battery that proves the
whole install and configuration works end-to-end, across signal types rather than one. It synthesizes
four signals as ARB I/Q and plays each through the ESG, measuring each on the connected analyzer:

1. **CW** — an unmodulated tone; checks channel power, PAPR (≈ 0 dB), and tone frequency (carrier + offset).
2. **AM** — 50% at 100 kHz; the elevated PAPR fingerprints the amplitude path.
3. **FM** — 500 kHz deviation at 100 kHz; the constant-envelope PAPR (≈ 0 dB) fingerprints the frequency path.
4. **I/Q multitone** — a 4-tone Newman signal; channel power + PAPR for the full complex path.

Results appear in the **Verification** view (expected vs measured per step) with an overall **PASS/FAIL**.
It needs a baseband-capable ESG and a connected analyzer; the **input-damage safety gate** is enforced
before any RF, and RF is returned off when done. AM/FM are verified via power/PAPR (not analog demod).
On a **FAIL**, a **troubleshooting dialog** lists each failed check's likely cause and ordered fixes
(e.g. excessive AM → over-driven ESG or analyzer mis-read → lower level / check ARB scaling / re-run Path cal…).

To run the *same* battery **by hand** — reading the analyzer yourself, with every UI control, value and
expected reading spelled out and a standalone **VSA settings checklist** — follow the
[**Manual Verification Procedure**](ManualVerification.md).

### 9.8 Capability binding — Core vs Option-gated
The app binds to what the **connected unit actually reports**, not to a fixed model configuration, so it
never offers a personality the hardware can't run or accepts a setting the instrument would silently
reject. On connect it reads `*IDN?`, `*OPT?`, and the live `? MAX/MIN` limits and reconciles them against
the static profile (the *effective profile*). Two tiers:

- **Core (always present).** Functions available on any supported unit regardless of options:
  - *ESG (E4438C):* ARB waveform download & playback, frequency/amplitude/RF-output control, reference
    locking. (Baseband ARB itself requires an installed baseband generator option — see below.)
  - *Analyzer (E4406A):* **Basic** measurement mode. *(N9010A):* **SA** and **IQ Analyzer** modes.
    Channel Power, CCDF/PAPR, Spectrum marker, and Waveform measurements run in these core modes.
- **Option-gated (present only if the unit reports the option).**
  - *ESG:* baseband generator / ARB memory depth — the reconciled **sample count** and **sample-clock**
    ceilings reflect only the baseband options `*OPT?` actually reports, and the download path reads back
    `*OPC?` + `:SYSTem:ERRor?` so a rejected waveform is surfaced, not assumed loaded.
  - *Analyzer:* communications-standard personalities (GSM, EDGE, cdmaOne, cdma2000, 1xEV-DO, W-CDMA,
    NADC, PDC, iDEN). These appear in the **VSA Mode** menu only when installed.

If you select a mode that isn't installed, the app refuses it with a clear message (naming the installed
modes) rather than relying on a silent instrument-side rejection. Connecting a model the app doesn't
support (anything other than the selected E4406A/N9010A analyzer or the E4438C ESG) is refused at connect.

---

## 10. The Claude assistant

The **Assistant** view is an opt-in pane where Claude drives the app in natural language through a
*tool surface* — never synthetic clicks or hidden SCPI. It is **off until you enable it** and provide
an API key.

### 10.1 The pane
- A **transcript** of your messages and Claude's replies (streamed live).
- An **input box** with **Send** and **Stop** (Stop cancels the in-flight turn).
- A **settings strip**: **Enable assistant** (master switch), **Auto-approve hardware**, **Allow raw
  SCPI**, and **Set API key…**.
- **Inline confirmation cards** appear in the transcript whenever Claude wants to do something that
  touches the instrument — with the action, its parameters, and **Approve / Decline**.

### 10.2 What it can do (tools)
- **Read** (run freely): `get_app_state`, `list_personalities`, `get_current_config`,
  `get_validation_results`, `get_results_readout`.
- **Configure** (project/PC state only): `set_source_personality`, `configure_cw`,
  `configure_multitone`, `configure_custom_modulation`, `configure_awgn`, `configure_import_iq`,
  `select_plot_view`, `set_project`, `calculate_waveform`.
- **Hardware** (each behind confirmation): `connect_instrument`, `disconnect_instrument`,
  `download_waveform`, `play_rf`, `stop_rf`, `set_instrument_settings`.
- **Measure / verify** (read the analyzer): `get_vsa_state`, `measure_channel_power`, `measure_acp`,
  `measure_ccdf`, `measure_spectrum_peak`, `measure_waveform`, `verify_signal`.
- **Gated** (opt-in): `send_raw_scpi` — an advanced escape hatch, disabled until you tick **Allow raw
  SCPI**, and always confirmed per call.

### 10.3 Guardrails (safety)
Enforced in the dispatcher, not the prompt:

- **read / configure** run with no prompt; **hardware / destructive** require an **Approve/Decline**
  card. **Auto-approve hardware** can skip the prompt for ordinary hardware tools, but **`play_rf`,
  `connect_instrument`, and `send_raw_scpi` always confirm** (RF emission, bus takeover, raw commands).
- A **pre-execution validation gate** re-runs the dependency checker before `download_waveform` /
  `play_rf` and **refuses on a hard validation error — even if you approved**.
- Commanded power goes through the **input-damage safety gate** (§9.1).
- **Instruction-source boundary**: anything returned *from* a tool (a file's contents, an instrument
  response) is treated as **data, never commands** — Claude won't act on instructions hidden in tool
  output.
- The **API key** is stored encrypted with **Windows DPAPI** (per-user) and never written to projects,
  logs, or the request body. Bulk data (e.g. raw I/Q arrays) is minimized before being sent.

Reads issued in one turn run **concurrently**; configure/hardware steps stay **serialized** in order;
long conversations are **compacted** automatically.

---

## 11. Sequencing, projects, exports

- **Sequence** view — assemble multiple waveform **segments** into a sequence (table + script views),
  with subsequences and batch compile, for multi-segment playback.
- **Projects** — **Save… / Open…** persist the active source + settings as a `*.ssproj` JSON file.
- **Exports** — export the waveform as raw ARB bytes, CSV, or a SCPI script; and use built-in
  **test-model presets** as starting points.
- **Markers** — ARB markers are supported for triggering/segmentation.

---

## 12. SCPI console

The **SCPI console** sends raw SCPI to the connected instrument and shows a timestamped request/response
log — useful for ad-hoc commands and debugging. (The assistant's equivalent, `send_raw_scpi`, is gated
and confirmed; see §10.)

---

## 13. Headless hardware-in-the-loop harness

`ESG-SignalCreator.HilHarness.exe` is a console runner for automated hardware tests (CI / bench
regression), separate from the GUI:

```
# ESG-only: connect, *IDN?/*OPT?, download a CW, arm the ARB, read back (RF off, safe)
ESG-SignalCreator.HilHarness.exe "TCPIP0::192.168.1.82::inst1::INSTR"

# Closed-loop battery across signal types + a frequency sweep, with a JSON report
ESG-SignalCreator.HilHarness.exe --vsa GPIB0::17::INSTR --all --dwell-seconds 3 --json report.json

# A single signal type, or a flatness power sweep
ESG-SignalCreator.HilHarness.exe --vsa --signal multitone
ESG-SignalCreator.HilHarness.exe --vsa --flatness
```

It enforces the input-damage safety gate, keeps the analyzer running during dwell so the front panel
tracks live, exits non-zero on failure, and can emit a machine-readable JSON report.

---

## 14. Troubleshooting

- **"Connect the ESG first" / Offline** — connect via **Connect…** before Download/Play/Verify.
- **Open fails / resource not found** — confirm a VISA runtime is installed and the resource string is
  correct (try the SCPI console's discovery, or the connection manager's Find).
- **Download/Play disabled** — you need a calculated waveform *and* a connection; check Notifications
  for validation errors.
- **Validation errors (memory/min-samples/over-range)** — adjust length, sample clock, or scaling; the
  message names the limit. Hardware actions are refused while a hard error stands.
- **Verify fails on power** — run **Path cal…** so the path loss is captured, and confirm tolerances in
  the verification profile.
- **Assistant says it's disabled / no key** — tick **Enable assistant** and **Set API key…**.
- **Assistant won't touch hardware** — that's by design; approve the inline card. If a hardware action
  is *refused* despite approval, a validation error is blocking it — fix it (see Notifications).
- **CI release queued forever** — the release workflow needs a self-hosted Windows runner with a VISA
  provider installed (see [Packaging.md](Packaging.md)).

---

## 15. Safety notes

- The analyzer only ever receives RF. **Arm** the RF-path safety and set the **max safe input** + **path
  loss** before driving power into it; the gate then blocks unsafe levels.
- **Play turns RF on.** Use **Stop** to turn it off. The assistant always confirms `play_rf`.
- Treat **raw SCPI** (console or `send_raw_scpi`) as the advanced, fully-logged escape hatch — it can
  do anything the instrument allows.

---

*See [Tutorials.md](Tutorials.md) for hands-on, build-up walkthroughs of every feature.*

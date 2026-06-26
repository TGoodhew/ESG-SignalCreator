# Signal Creation UX — Product Brief & UI Requirements

**Scope:** The signal-creation user experience for the Signal Studio (Reborn) app targeting the E4438C (and extensible to other ARB/vector generators).
**Companion doc:** *SignalStudio-Rebuild-Requirements.md* (transport, ARB binary format, SCPI). This document covers **only the UI/UX of creating signals**, including I/Q.
**Implementation stack:** C# · Windows Forms · .NET Framework 4.7.2 · NI-VISA.
**Method:** Synthesized from the published manuals/UIs of the leading tools — Keysight **Signal Studio / PathWave Signal Generation (PWSG)** + **Advanced Waveform Utility (AWU)**, Rohde & Schwarz **WinIQSIM2**, Tektronix **RFXpress / SourceXpress / AWG70000-AWG5200**, NI **niFgen** scripting, and Zurich Instruments **LabOne AWG** / PicoScope AWG editor.

---

## 1. What the industry leaders do (competitive UX teardown)

Each major tool converged on a few strong patterns. We adopt the best of each.

### Keysight Signal Studio / PathWave (PWSG + AWU)
- **Tree-style navigation** down the left: format → carrier → channels → impairments, expanding into a parameter pane. ("Accelerate testing with graphical tree-style navigation.")
- **Unified app, many formats** as plug-ins sharing one engine and one look across Desktop and instrument-embedded.
- **Advanced Waveform Utility** adds: multi-segment sequence builder (per-segment repetition + power, insert idle/burst), **CFR (crest-factor reduction)** with a filter-mask spectrum plot, a **marker viewer/editor** (set start/stop points, Range-On/Range-Off, save/import marker files), a **calibration/correction wizard**, and direct sample-rate entry with no manual conversions.
- "Try before you buy" / premium features visible but gated at *generate* time with a clear license message.

### Rohde & Schwarz WinIQSIM2
- **Block-diagram signal flow** as the central canvas: *Baseband → AWGN → ARB/Vector Sig Gen* blocks, left-to-right, each block showing live state and opening its own settings dialog. This is the single most learnable mental model in the category.
- **Up to three simultaneously configurable graphics** for verification: I/Q vs time, magnitude/phase, FFT magnitude, constellation, eye, CCDF.
- Distinct generators for **Custom Digital Modulation**, **Multi-Carrier CW (MCCW)**, **Multi-Carrier**, **Multi-Segment**, and **Import I/Q**.
- **Parameter/dependency checker** that flags conflicts; a **notifications/history** panel; global **undo/redo**; "transmit to instrument **or** to file" as equal first-class outputs.
- **Test Models** — one-click standard presets that populate a whole config.

### Tektronix RFXpress / SourceXpress / AWG
- **Auto mode** computes the required **waveform length and sample rate** from the signal definition (entered in time, samples, *or* symbols) — the user states intent, the tool solves the sampling math. Major usability win.
- Explicit **Compile** step with a status bar (Online / Offline / Not Available) so the user always knows whether they're hitting hardware.
- **Offline / virtual-instrument** mode: build everything with no hardware present, recall when the instrument is available. ("No new application to learn" — same UI offline and on-box.)
- **Sequencer table** (the gold standard for waveform sequencing): rows = steps; columns = **Waveform, Repeat (1…∞), Wait (Off/TrigA/TrigB/Internal), Event-Jump (→destination), Go-To (Next/index), Flags (A/B/C/D)**. Subsequences, pattern jump, and **batch compile** (sweep a parameter to emit a family of waveforms).
- Seamless playback guarantee: **no wrap-around glitch** in time/frequency/phase across loop boundaries (a correctness property the UI must protect — e.g. warn on non-integer-cycle lengths).
- Per-channel **markers** (sample-aligned digital bits embedded in the waveform) **and** **flags** (separate full-resolution step indicators).

### NI niFgen scripting
- **Scripting language** for sequencing: `repeat until`, `wait until`, `if/else`, `clear`, with named waveforms referenced by name. Power-user path beneath the table UI.

### Zurich LabOne / PicoScope
- **C-like sequencer editor** with compile button + keyboard shortcuts, **CSV import** of float samples in [-1, +1] (one column per channel), waveform granularity guidance (use multiples of N samples; zero-padding warning).
- Built-in **waveform math**: add, multiply, scale, concatenate (join), cut/truncate; primitives like `gauss`, `rect`, `chirp`, `RRC`, PRBS/LFSR.

### Cross-cutting conventions worth stealing
- A persistent, obvious **online/offline + instrument-state** indicator.
- **Calculate/Compile** as a deliberate, visible action separate from **Download** and **Play**.
- **Save/recall the entire setup** (all parameters + generated data + markers) as one project artifact.
- Graphics are for **verification**, shown next to config, never blocking it.

---

## 2. Design principles for our app

1. **State intent, not arithmetic.** Like RFXpress Auto mode: let users specify duration in **time / samples / symbols** and solve sample-rate/length for them, with manual override always available.
2. **One mental model: the signal-flow canvas.** Adopt R&S's block diagram as the home screen: `Source block(s) → Impairments (AWGN/CFR/filter) → Output (ARB file / instrument)`. Every block shows state and opens a settings panel.
3. **Progressive disclosure.** Tree/accordion within each block: essential params first, "Advanced" expanders for the rest (mirrors Signal Studio tree + ESG "Advanced" menu).
4. **Verification is always one glance away.** Up to 3 docked plots, live-updating after Calculate.
5. **Deliberate pipeline.** Visually distinct **Calculate → Download → Play** stages with state and progress; never silently touch hardware.
6. **Offline-first.** Full authoring with no instrument (virtual instrument), validating against a chosen target's capability profile.
7. **Two depths for sequencing.** A spreadsheet-style **sequencer table** for everyone, plus a **script view** for power users — same model, two editors.
8. **Catch errors before the DAC.** A live dependency/parameter checker (over-range, memory cap, min-samples, granularity, non-integer-cycle wrap) with inline, fixable messages.

---

## 3. Information architecture / primary screens

```
├── Top bar: [Target: E4438C ▾]  ●Online/Offline   [Calculate] [Download] [Play ▮ Stop]  [Save] [Open]
├── Left rail: Project tree
│     Signal
│       ├─ Source: <personality>           (Multitone / Custom Mod / AWGN / Import IQ / CW)
│       ├─ Impairments: AWGN, CFR, Filter, IQ impairments
│       ├─ Markers
│       └─ Sequence (segments/steps)
│     Instrument settings
│     Notifications / History
├── Center: Signal-flow canvas  OR  active block's parameter panel (tabbed)
└── Right dock: Verification graphics (up to 3, each selectable view) + Results readout
```

- **Home = signal-flow canvas.** Blocks: `Baseband Source → [AWGN] → [CFR] → [Filter/Correction] → Output`. Click a block → its parameter panel opens in the center; canvas stays as breadcrumb.
- **Status bar (bottom):** online/offline + instrument model + memory used/available + last error.

---

## 4. The Source block — signal creation panels

A plug-in per personality (matches §7 of the companion doc). Each shares a common header: **Name**, **Sample rate** (auto/manual), **Length** (time | samples | symbols), **Oversampling**, and a **Calculate** affordance.

### 4.1 Custom Digital Modulation
- **Modulation picker**: ASK, FSK/GFSK, MSK, BPSK/QPSK/8PSK/π4-DQPSK, 16/32/64/128/256-QAM, plus custom constellation import. (Mirror WinIQSIM2 custom-mod + Signal Studio custom mod.)
- **Symbol rate**, **# symbols**, **filter** (RRC/RC/Gaussian/rect) with **roll-off α / BT**, **filter length**.
- **Payload/data source**: PN9/PN11/PN15/PN20/PN23, all-0/all-1, user bit pattern, **user file** (bit/binary), with MSB-first convention stated inline.
- **Burst/framing**: on/off, ramp shape, guard.
- Live **constellation** + **eye** preview.

### 4.2 Multitone
- **Tone table** (spreadsheet): per-tone **frequency offset, power (dB), phase, on/off**. Add/remove/duplicate rows.
- **Auto-spacing** generator: N tones, spacing, center → fills the table.
- **Phase strategy**: random, equal, **Newman** (low crest factor) — show resulting **PAPR/crest factor** live.
- Notch capability (turn off a tone or group) for distortion testing (mirrors Signal Studio multitone-distortion).

### 4.3 Multi-Carrier
- Combine multiple independent baseband signals at **frequency offsets** with per-carrier **power** and **delay/phase**. Table + small spectrum preview.

### 4.4 AWGN (as source or impairment)
- **Bandwidth**, **C/N or Eb/N0 or SNR** (selectable basis with live conversion), **carrier-to-noise readout**, crest-factor clipping. (Mirror WinIQSIM2 AWGN block result readout.)

### 4.5 Import I/Q
- **File formats**: CSV/TSV (float I,Q columns in [-1,+1]), WAV, MAT, raw int16 interleaved (the instrument-native format from the companion doc), Tektronix/RSA `.tiq/.iqt`-style and scope `.wfm` where feasible.
- **On import**: detect/confirm sample rate, I/Q column mapping, scaling, **resample** option, length trim/pad with granularity warning.
- Preview before commit.

### 4.6 CW / Single tone
- Trivial: frequency offset, amplitude, phase. Primary smoke-test path.

### 4.7 I/Q specifics that must be first-class everywhere
- Explicit **I and Q vs time** overlay/stacked view on every source.
- **I/Q impairments** panel (for receiver-stress testing): I/Q gain imbalance, quadrature skew, DC offset (I and Q), I/Q swap. (ESG supports these; expose them.)
- **Scaling/headroom** control surfaced (digital backoff + instrument `RSCaling`) with a live **DAC over-range predictor**.
- **Normalization** indicator showing peak/RMS and resulting int16 mapping.

---

## 5. Sequencing — table + script (the AWG paradigm)

### 5.1 Segment/Sequence table (primary)
Spreadsheet UI, one row per step. Columns (superset of Tektronix + Signal Studio AWU):

| Column | Values | Notes |
|---|---|---|
| # | step index | drag to reorder |
| Waveform | named segment | dropdown of created/imported segments |
| Repeat | 1…1,048,576 or ∞ | loop count |
| Power | dB | per-segment power (AWU) |
| Wait | Off / Trig A / Trig B / Internal | gate start on trigger |
| Event-Jump | Next / step N | on EVENT IN / Force Event |
| Go-To | Next / step N | unconditional after step |
| Markers | per-marker on/off | sample-aligned |
| Flags | A B C D | full-res step indicators |
| Idle/Burst | insert idle samples | create bursts (AWU "insert idle") |

- **Subsequences** (a step references another sequence).
- **Batch compile**: pick a parameter + low/high/step → emit a family of waveforms (RFXpress/Tek pattern). Great for receiver-margin sweeps.
- **Seamless-transition guard**: warn when segment boundaries would cause a phase/level discontinuity; offer auto-fix (integer-cycle length / zero-cross alignment).

### 5.2 Script view (power users)
- Same model rendered as editable script with `repeat`, `wait until`, `if/else`, `goto`, named-waveform references (niFgen/LabOne model). Round-trips with the table where possible; compile button + inline errors + keyboard shortcuts.

---

## 6. Markers & flags editor

- **Marker viewer/editor** (AWU pattern): per-marker, set **start/stop** points, **Range-On / Range-Off** spans, draw directly on the time axis, **save/import** marker files.
- Markers are 1 bit/sample embedded in the waveform; **flags** are separate, full-resolution step indicators (don't consume waveform bits). Make the distinction explicit in the UI.
- Common presets: marker at waveform start, marker every N samples, RF-blank marker.

---

## 7. Verification graphics (right dock — up to 3 concurrent)

Selectable per pane (R&S/Signal Studio superset), all live after Calculate, all with rubber-band **zoom** (legacy Signal Studio Tools→Zoom):
- **I & Q vs time** (stacked/overlay)
- **Magnitude & phase vs time**
- **Spectrum (FFT)**, with optional **CFR filter-mask overlay** (AWU)
- **Constellation**
- **Eye diagram**
- **CCDF** with **PAPR / crest-factor** numeric readout
- **Vector / I-Q polar**

Results readout strip: sample count, duration, sample rate, peak/RMS, PAPR, occupied BW, predicted DAC headroom.

---

## 8. The Calculate → Download → Play pipeline (UX)

Three visibly distinct, individually triggerable stages, each with state + progress + cancel:

1. **Calculate / Compile** — generate I/Q on the PC; populate plots; run validation. Off-UI-thread with progress. Surface all dependency-check results here *before* any hardware contact.
2. **Download** — show bytes, target memory (volatile WFM1 / non-volatile), and memory headroom; confirm against the target's option capacity; progress bar (large GPIB transfers are slow).
3. **Play / Stop** — arm ARB + RF; a play-state indicator with clear visual states (idle / playing / waiting-for-trigger / busy), echoing the AWG70000 play-button semantics (stopped, playing, waiting-for-trigger, inhibited).

A combined **"Calculate → Download → Play"** one-click for the common path, but always decomposable.

## 9. Validation & guidance (the dependency checker)

Live, inline, fixable (R&S parameter/dependency-checker + notifications/history):
- **DAC over-range** prediction (interpolation-aware) — suggest scaling / `RSCaling`.
- **Memory cap** vs connected option (8.3M / 33.5M / 67M samples) — block early.
- **Minimum 60 samples**, **even-count** preference, **granularity** multiples — warn/auto-fix.
- **Non-integer-cycle / wrap discontinuity** — warn + auto-fix.
- **License/option gating** (if modeling personalities): feature visible, checked at Calculate with a clear "requires Option NNN" message (Signal Studio pattern).
- A **Notifications / History** dock listing every warning/error with timestamp, severity, and jump-to-offending-field.

## 10. Project, files, offline mode

- **Single project artifact** (`*.ssproj`, JSON) capturing all blocks, sequence, markers, instrument settings, and (optionally) generated data. Save/Open/Reset-to-default; **Undo/Redo** global.
- **Export** to: instrument-native raw ARB (int16 interleaved big-endian), CSV float I/Q, and a SCPI command-script export (so a run can be replayed programmatically — WinIQSIM2 "export remote command list").
- **Offline / virtual instrument**: choose a target *capability profile* (frequency/memory/sample-rate caps) and author + validate against it with no hardware; recall and download later (SourceXpress pattern).
- **Standard presets / "Test Models"**: one-click configs that populate a whole signal (WinIQSIM2 pattern) — ship a few (multitone IMD test, single-carrier QAM, CW, AWGN-only).

## 11. Accessibility / interaction details

- Keyboard-first table editing (tab/enter/arrow nav, fill-down, copy/paste rows from Excel).
- Every numeric field accepts engineering suffixes (k/M/G, ms/µs/ns, dB/dBm) and shows resolved value.
- Context-sensitive help per panel (WinIQSIM2 convention).
- Consistent units policy; no silent unit coercion.
- Long ops never block UI; everything cancelable; nothing touches the instrument without an explicit Download/Play.

---

## 12. WinForms implementation notes (for Claude Code)

- **Signal-flow canvas**: a custom `Panel` with draggable block `UserControl`s and connector lines (GDI+); each block raises `BlockSelected` → host swaps the center parameter panel. Keep blocks as a bindable `ObservableCollection<SignalBlock>`.
- **Parameter panels**: one `UserControl` per personality implementing a shared `ISignalSourcePanel { object GetConfig(); void LoadConfig(); event Recalculate; }`. Reuse the companion doc's `IWaveformPersonality`.
- **Sequencer table**: `DataGridView` bound to `BindingList<SequenceStep>`; combo-box columns for Wait/Event-Jump/Go-To; checkbox columns for Flags/Markers; row drag-reorder; validation in `CellValidating`.
- **Plots**: `System.Windows.Forms.DataVisualization.Charting` or OxyPlot; one reusable `PlotPane` with a view-type dropdown (IQ/Spectrum/Constellation/Eye/CCDF) and rubber-band zoom. FFT/eye/CCDF math in Core (MathNet.Numerics).
- **Script view**: `RichTextBox`/Scintilla.NET with simple syntax coloring; parse → `BindingList<SequenceStep>` to keep table/script in sync.
- **Pipeline**: `BackgroundWorker`/`Task` for Calculate/Download with `IProgress<int>`; a `PlayStateIndicator` control with the four AWG states.
- **Validation**: a `List<ValidationResult{Severity,Message,FieldRef}>` produced by Core, rendered in the Notifications dock and as inline `ErrorProvider` glyphs.
- **Project I/O**: Newtonsoft.Json; capability profiles as embedded JSON resources per target model.

---

## 13. Phased build recommendation

- **P1 (MVP):** signal-flow canvas (Source→Output), CW + Multitone + Import-IQ sources, IQ-vs-time + Spectrum plots, Calculate→Download→Play pipeline, over-range/memory/min-sample validation, project save/open. (Pairs with companion doc P1.)
- **P2:** Custom Digital Modulation source, AWGN + CFR impairments, constellation/eye/CCDF plots, marker editor, I/Q impairments.
- **P3:** sequencer table + script view, subsequences, batch compile, offline/virtual instrument, SCPI-script export, Test-Model presets.

---

## 14. Reference links

**Keysight Signal Studio / PathWave / AWU**
- PathWave Signal Generation product page (tree navigation, unified app) — https://www.keysight.com/us/en/products/software/pathwave-test-software/signal-studio-software.html
- PathWave Signal Generation brochure (Desktop vs Embedded, same UX, licensing) — https://www.keysight.com/us/en/assets/7018-01538/brochures/5989-6448.pdf
- PathWave AWU technical overview (multi-segment, CFR + filter-mask plot, marker editor, calibration wizard, direct sample-rate) — https://www.keysight.com/content/dam/keysight/en/doc/ungate/technical-overviews/PathWave-Signal-Generation-PWSG-Advanced-Waveform-Utility-AWU.pdf
- N7608C Custom Modulation (data/idle segments, multi-segment, 5G candidate mods) — https://www.keysight.com/us/en/product/N7608APPC/pathwave-signal-generation-pro-custom-modulation-pc-application.html

**Rohde & Schwarz WinIQSIM2**
- WinIQSIM2 product page (block diagram signal flow; up to 3 graphics: IQ/mag-phase/FFT/constellation/eye/CCDF) — https://www.rohde-schwarz.com/us/products/test-and-measurement/signal-generator-software/rs-winiqsim2-simulation-software_63493-7614.html
- WinIQSIM2 **User Manual v10** (signal-flow blocks, custom digital mod, MCCW, multi-carrier, multi-segment, import IQ, AWGN, graphics, dependency checker, undo/redo, history, SCPI export) — https://scdn.rohde-schwarz.com/ur/pws/dl_downloads/pdm/cl_manuals/user_manual/1177_5533_01/WinIQSIM2_UserManual_en_10.pdf
- WinIQSIM2 data sheet (multicarrier/multisegment, seamless transitions, IQ import) — https://cdn-docs.av-iq.com/dataSheet/WinIQSIM2_Datasheet.pdf

**Tektronix RFXpress / SourceXpress / AWG**
- RFXpress data sheet (Auto mode sample-rate/length solving in time/samples/symbols; no wrap-around; markers; radar pulse builder) — https://www.tek.com/en/datasheet/rfxpress/rfx100-rfxpress
- RFXpress user manual (app screen anatomy, IQ/IF/RF, compile, import scope/RSA waveforms) — https://download.tek.com/manual/RFXpress-RFX100-User-Manual_0.pdf
- SourceXpress data sheet (offline/virtual instruments, same UI on/off-box, multi-instrument) — https://www.tek.com/en/datasheet/waveform-creation-and-instrument-control-environment-pc
- **Using the Sequencer on AWG70000A** (sequencer table: Wait, Repeat, Event-Jump, Go-To, Flags A-D, subsequences, batch compile) — https://download.tek.com/document/76W-30715-0%20Using%20the%20Sequencer%20on%20Tektronix%20AWG%2070000A%20series%20instruments.pdf
- AWG70000 printable help (play-state indicator semantics, sequence editor, precompensation, capture/playback IQ import) — https://download.tek.com/manual/AWG70000-Arbitrary-Waveform-Generator-Printable-Help-Document-077144600.pdf
- Pattern Generator plug-in (batch compile for margin sweeps; bit-pattern import; invert→differential) — https://www.tek.com/en/datasheet/applications-sourcexpress(r)-and-awg70000-5200-series-generators-0

**NI / Zurich / Pico (scripting + editor + import patterns)**
- NI: Advanced Waveform Sequencing & Triggering (repeat-until / wait-until / if-else / clear scripting model) — https://www.ni.com/en/support/documentation/supplemental/06/advanced-waveform-sequencing-and-triggering-on-arbitrary-wavefor.html
- Zurich LabOne AWG (C-like sequencer editor, compile, waveform math join/cut/scale, CSV float import, granularity/zero-pad guidance, markers vs flags) — https://docs.zhinst.com/hdawg_user_manual/tutorials/awg.html
- PicoScope AWG editor (built-in editor + CSV/TXT import, standard shapes, sweep/trigger modes) — https://www.picotech.com/library/knowledge-bases/oscilloscopes/arbitrary-waveform-generator-awg

# Signal Studio (Reborn) — E4406A Closed-Loop Verification Requirements

**Role of this device:** An **Agilent E4406A VSA Series Transmitter Tester** is connected to the RF output of the E4438C ESG. It becomes the application's **measurement reference / device-under-test verifier** — the thing that confirms the ESG actually produced what Signal Studio told it to produce.
**Implementation stack:** C# · Windows Forms · .NET Framework 4.7.2 · NI-VISA · SCPI.
**Companion docs:**
- *SignalStudio-Rebuild-Requirements.md* — ESG transport, ARB binary format, SCPI (the generation side).
- *SignalCreation-UX-Requirements.md* — signal-creation UI/UX.
- *SignalStudio-Claude-Integration-Requirements.md* — the Claude assistant / tool surface.

**Source references (uploaded, in `C:\Users\Tony\OneDrive\Documents\Manuals`):**
- *E4406A VSA Signal Analyzer Programmer's Guide* (E4406-90303, May 2007) — SCPI reference for MEASure/READ/FETCh/CONFigure, status, I/O. (`E4406A Programmers Guide.pdf`)
- *E4406A VSA Series Transmitter Tester User's Guide* — front-panel/measurement concepts. (`E4406A_Manual.pdf`)

**Document purpose:** Hand-off spec for Claude Code (VS Code) to add an E4406A measurement/transport layer and a **closed-loop generate→measure→compare** capability used both for app development/testing and as a verification tool the user (and the Claude assistant) can drive.

> Status: v1 draft assembled from the uploaded E4406A manuals. §10 lists items to confirm against the actual unit before coding the SCPI layer.

---

## 1. What the E4406A is, and why it matters here

The E4406A is a **vector signal analyzer / transmitter tester**: a measurement receiver covering ~7 MHz–4 GHz that digitizes an RF input down to baseband I/Q and runs transmitter measurements on it. In **Basic mode** it provides format-independent measurements — Channel Power, Adjacent Channel Power (ACP/ACPR), Power-Statistics CCDF, Spectrum (frequency domain), Waveform (time domain), Power-vs-Time — that need no communications-standard personality option. Standard-specific modes (GSM/EDGE, cdmaOne, cdma2000, W-CDMA, NADC, PDC) are option-gated personalities layered on top.

Until now the companion docs describe an **open-loop** system: Signal Studio computes an ARB waveform, downloads it to the ESG, and plays it — but nothing confirms the RF that comes out matches intent. With the E4406A on the ESG output, the system becomes **closed-loop**: the app can generate a signal, measure it on the analyzer, and compare measured-vs-expected. This is valuable in two distinct ways:

1. **As a development/test oracle.** During app development, the E4406A is the ground truth that proves the generation chain (ARB encoding, byte order, scaling, SCLock rate, instrument settings) is correct. A CW tone that lands at the wrong frequency, a multitone with the wrong PAPR, or an over-scaled waveform shows up immediately as a measurement discrepancy. This converts the companion doc's acceptance tests (rebuild §9) from "trust the math" into "verify on hardware."
2. **As a user-facing verification feature.** End users (and the Claude assistant) can ask "did it actually come out right?" and get a measured answer — channel power, occupied bandwidth, ACPR, PAPR — next to the expected value from the calculated waveform.

### The pairing, concretely
```
   Signal Studio (PC)
        | generate + download + play           | configure + measure + read
        v                                       v
  +-----------+   RF out -----coax-----> RF in  +-----------+
  |  E4438C   |                                 |  E4406A   |
  |   ESG     |   10 MHz ref (recommended) ---->|   VSA     |
  +-----------+                                 +-----------+
        ^                                              ^
        +------------ NI-VISA (GPIB / LAN) ------------+
                         from the PC
```

---

## 2. Goals & non-goals

**Goals**
- A second VISA transport + SCPI driver for the E4406A, peer to the existing `EsgInstrument`, behind a shared transport interface.
- A **measurement abstraction** exposing the Basic-mode measurements (Channel Power, ACP, CCDF, Spectrum, Waveform, Power-vs-Time) as typed calls returning structured results.
- A **closed-loop test harness**: generate a known signal on the ESG → measure on the E4406A → assert measured results against expected, with tolerances. Runnable headless for CI-style regression of the generation chain.
- A **Verification view** in the UI showing expected (from the calculated waveform) vs measured (from the E4406A) side by side.
- **Assistant tool surface** so Claude can drive measurements and report/compare results (extends the assistant doc's tool catalog).
- Safe defaults: the analyzer only ever **receives** RF; the app must never command the ESG to a power level that could damage the E4406A input (see §6 safety).

**Non-goals (v1)**
- No standard-personality (GSM/cdma2000/W-CDMA…) measurements unless the unit has those options; v1 targets **Basic mode** only.
- No replication of the analyzer's full front-panel feature set; only the measurements the verification loop needs.
- No demod/EVM constellation analysis in v1 (Basic mode doesn't provide modulation-quality metrics for arbitrary custom modulation; revisit if a matching personality option is present).
- No automated RF cabling/switching assumptions beyond a single direct ESG→E4406A connection.

---

## 3. Architecture (where the E4406A fits)

```
+--------------------------------------------------------------+
|  Core (class library)                                         |
|  +--------------------------+    +--------------------------+ |
|  | EsgInstrument (existing) |    | VsaInstrument (NEW)      | |
|  |  ESG generation transport|    |  E4406A measure transport| |
|  +--------------------------+    +--------------------------+ |
|            \ both implement IInstrumentTransport              |
|            v                                                  |
|  +--------------------------------------------------------+   |
|  | IVsaMeasurement abstraction (NEW)                       |   |
|  |  ChannelPower · Acp · Ccdf · Spectrum · Waveform · PvT  |   |
|  |  -> typed result objects                                |   |
|  +--------------------------------------------------------+   |
|  +--------------------------------------------------------+   |
|  | VerificationHarness (NEW)                               |   |
|  |  expected (from WaveformModel) vs measured (from VSA)   |   |
|  |  tolerance compare -> VerificationResult                |   |
|  +--------------------------------------------------------+   |
+--------------------------------------------------------------+
        | NI-VISA (GPIB / TCPIP) to TWO instruments
   +----v----+                       +----v----+
   |  E4438C | -- RF -->             |  E4406A |
   +---------+                       +---------+
```

`VsaInstrument` reuses the same `IInstrumentTransport` contract as `EsgInstrument` (rebuild doc §10 layout) so session management, SCPI write/query, error polling, and logging are shared. The verification harness is the new piece: it knows how to derive **expected** metrics from the `WaveformModel` the personality plug-in already produced (sample rate, PAPR from CCDF, occupied BW, set frequency/power) and compare them to **measured** metrics from the analyzer.

---

## 4. Transport & connection (NI-VISA)

- **Resource strings** (make configurable, store per-instrument in the project/profile):
  - GPIB: `GPIB0::17::INSTR` — **this E4406A is set to GPIB address 17** (factory default is 18; settable/queryable via `SYSTem:COMMunicate:GPIB:ADDRess`). Address 17 keeps it clear of the HP 8563E at 18.
  - LAN: `TCPIP0::<host-or-ip>::INSTR` (VXI-11/SICL) or raw socket `TCPIP0::<ip>::5025::SOCKET`. The E4406A supports SCPI over LAN sockets and a SICL/LAN server emulating 488.2 GPIB. IP is settable via `SYSTem:COMMunicate:LAN:IP`. (USB is available only on units with Option 111.)
- **Identify:** `*IDN?` → expect `Agilent Technologies, E4406A, <serial>, <fw>`. **`*OPT?`** to enumerate installed personality options (decides which measurement modes are legal — see §5.1).
- **Two simultaneous sessions:** the app now holds VISA sessions to **both** instruments at once. Ensure the session manager supports N instruments cleanly (the ESG and VSA are independent resources; don't assume a single global session).
- **Session hygiene:** `*CLS`; after each logical operation poll `SYSTem:ERRor?` and surface in the shared log; generous timeout for measurement reads (averaged measurements + trace transfers can be slow — make configurable, default ~10–30 s).
- **Binary trace transfers:** large trace data (spectrum/waveform/CCDF curves) should use binary format, not ASCII, for speed (`FORMat:DATA REAL,32` / `INT,16` per the guide's "improving measurement speed" advice); honor `FORMat:BORDer` byte order. Provide an ASCII fallback for debugging.

> **Bench addressing note (Tony's lab):** this E4406A is at GPIB **17**, which is clear of the HP 8563E spectrum analyzer at GPIB 18 and the other instruments on the bus (3325B@10, 5351A@14, 7090A@6, 8720C@16) — no collision, no readdressing needed. The app should still verify identity on connect: if `*IDN?` at GPIB 17 doesn't report an E4406A, refuse rather than drive the wrong instrument. The HP 37204A bus extender making all 30 addresses appear populated is a known gotcha here — confirm by `*IDN?`, not by resource enumeration.

---

## 5. The measurement model (most important section)

The E4406A's remote model is the standard Agilent **MEASure / CONFigure / FETCh / READ / INITiate** group. Get this mapping right and the rest is plumbing.

### 5.1 Measurement workflow (per the Programmer's Guide "Three Basic Steps")
1. **System setup:** `*RST` (sparingly), `FORMat:DATA`, status/error config, single-measurement mode (`INITiate:CONTinuous OFF`).
2. **Mode & mode setup:** `INSTrument:SELect BASIC` (v1 target), then frequency (`SENSe:FREQuency:CENTer`), input/attenuation (`INPut` / range), triggering (`TRIGger`). Standard-specific modes only if `*OPT?` shows the option.
3. **Measurement select & setup:** pick the measurement; adjust `SENSe:<meas>` (averaging, bandwidth, FFT/window) and `CALCulate:<meas>` (markers, limits) as needed; then read results.

### 5.2 The four command verbs (use the right one)
- **`MEASure:<meas>[n]?`** — one-shot using factory defaults: stops current measurement, configures, initiates, blocks until done, returns results. Use for a quick, default-settings measurement.
- **`CONFigure:<meas>`** — set up the measurement to defaults and go to single mode **without** initiating. Follow with `SENSe`/`CALCulate` tweaks, then `READ?`.
- **`READ:<meas>[n]?`** — initiate (INIT + FETCh) and return results, honoring any persistent `SENSe`/`CALCulate` settings. Use for repeated measurements with custom settings.
- **`FETCh:<meas>[n]?`** — return data from the **most recent** measurement without re-measuring (e.g. pull both scalar `[n]=1` and trace `[n]>1` results from one acquisition). Errors if a different measurement is active.

The `[n]` index selects scalar results (`[n]` omitted or `1`) vs specific trace-data result sets (`[n]>1`); the exact `[n]` meanings are per-measurement in the Language Reference and must be encoded per result type.

### 5.3 v1 measurements (Basic mode) and what the harness needs from each

| Measurement | SCPI root | Scalar result(s) we consume | Verifies (ESG side) |
|---|---|---|---|
| **Channel Power** | `…:CHPower` / `CPOWer` | total channel power (dBm), power spectral density | set RF power / amplitude correctness |
| **ACP / ACPR** | `…:ACP` | adjacent-channel power ratios (dBc) per offset | spectral regrowth / scaling / filter shape |
| **CCDF (Power Statistics)** | `…:PSTatistic` | PAPR / crest factor, % at power levels | multitone phasing (Newman vs random), AWGN crest |
| **Spectrum (freq domain)** | `…:SPECtrum` | trace + markers; occupied BW; tone offsets | tone placement (e.g. CW at Fc+1 MHz), occupied BW |
| **Waveform (time domain)** | `…:WAVeform` | time trace; RMS/peak power; burst timing | envelope, burst on/off, level |
| **Power vs Time** | (PvT) | power profile vs time, mask pass/fail | bursted/framed signals |

Each maps to a typed result object (`ChannelPowerResult`, `AcpResult { float[] OffsetsDbc }`, `CcdfResult { float PaprDb; … }`, `SpectrumResult { double[] FreqHz, PowerDb; … }`, etc.).

### 5.4 Marker-based reads (for spectrum verification)
For "is the tone where I asked?" checks, drive `CALCulate:SPECtrum:MARKer` peak-search + marker-X/Y queries (the guide's "Using Markers" example) to extract tone frequency and amplitude, rather than scraping the whole trace. (This mirrors the established 8563E `MKPK HI;MKCF` → re-sweep → `MKF?/MKA?` marker workflow Tony already uses — same idea, E4406A SCPI.)

### 5.5 Status & completion sync
Use `*OPC?`/single-mode blocking on `READ?`/`MEASure?` for completion (they block until done), and the Operation/Questionable status registers for over-range/under-range and unsettled conditions. Always check `SYSTem:ERRor?` after a measurement and surface the analyzer's view of input-range problems (e.g. input overload) — these are the early warning that the ESG output level is wrong or the input attenuation is mis-set.

---

## 6. Safety: protect the analyzer input (hard rule)

The E4406A RF input has a **maximum safe input level**; exceeding it can damage the front end. Because Signal Studio commands the ESG's output power, the app is in a position to overdrive the analyzer. Therefore:

- **Maintain a configurable input-damage threshold** (e.g. set conservatively below the E4406A's rated max input, confirm from the unit's specs — see §10) in the E4406A profile.
- **Gate ESG power commands against it** whenever the verification link is "armed": if a requested ESG output power (plus any known external gain/loss in the path) would exceed the analyzer's safe input, **block the command** with a clear message, regardless of who issued it (manual UI **or** Claude assistant).
- This check lives in Core/transport, not in the prompt or UI — same enforcement principle as the assistant doc's guardrails (assistant §6.3). The Claude assistant's `set_instrument_settings`/`play_rf` path must consult it.
- A **path-loss/attenuation field** in the verification profile so the user can declare an inline pad/attenuator; the damage check accounts for it. Default assumes a direct cable (0 dB) and warns that this is the conservative assumption.
- The E4406A's own input attenuation/range must be set appropriately before measuring; auto-range is convenient but the guide notes it costs speed — expose manual range with a sane default.

---

## 7. The closed-loop verification harness

### 7.1 What it does
Given a generated signal (the `WaveformModel` + instrument settings from the existing pipeline), the harness:
1. Computes **expected** metrics from the model itself: center frequency and power (from instrument settings), PAPR/crest factor (from the central CCDF computation already specified in the UX doc §7), occupied bandwidth (from the spectrum of the complex I/Q), tone offsets (for multitone/CW).
2. Drives the ESG through the normal **Calculate → Download → Play** pipeline.
3. Configures the E4406A for the matching measurement(s) at the right center frequency/span and **measures**.
4. **Compares** measured vs expected within per-metric tolerances — a `VerificationResult { Metric, Expected, Measured, Tolerance, Pass }` list.

### 7.2 Tolerances
- Per-metric, configurable, with sensible defaults (e.g. channel power ±0.5 dB accounting for path loss + analyzer accuracy; tone frequency within a small fraction of RBW; PAPR ±0.5 dB). Tolerances live in the verification profile so the harness is reusable across targets.
- Account for **known systematic offsets**: cable loss, analyzer absolute-amplitude accuracy, and the ESG's own level accuracy. Provide a one-time **path calibration** step (generate a known CW, measure, store the measured−commanded delta as a path-loss correction) — mirrors the AWU calibration-wizard idea from the UX doc.

### 7.3 Headless / regression use
The harness must be runnable without the GUI (a console/test entry point) so the generation chain can be regression-tested against real hardware:
- Seed a battery of known signals (CW at several offsets/powers; multitone with Newman vs random phasing; AWGN at a set crest factor) → measure → assert. This is the hardware-backed version of rebuild §9's acceptance tests and UX §13's P1 validation.
- Emit a machine-readable report (JSON) plus a human summary.

---

## 8. Assistant tool surface additions (Claude integration)

Extends the catalog in *SignalStudio-Claude-Integration-Requirements.md* §4.2. New tools, with the same effect-class guardrails:

Read / measure tools (`read` — analyzer only receives, never emits RF; safe to call):
- `get_vsa_state` — E4406A connection state, `*IDN?`, `*OPT?` options, current mode, input range, last error.
- `measure_channel_power` — center freq + span → channel power (dBm), PSD.
- `measure_acp` — offsets → ACPR per offset (dBc).
- `measure_ccdf` — → PAPR/crest factor and CCDF points.
- `measure_spectrum` — center/span → trace summary + peak markers (tone freq/amplitude).
- `measure_waveform` — → RMS/peak power, burst timing.
- `verify_signal` — the headline tool: run the closed-loop harness for the currently calculated/played signal and return the expected-vs-measured `VerificationResult` set with pass/fail.

Configure tools (`configure` — analyzer setup only, no RF emission):
- `connect_vsa` / `disconnect_vsa` (`hardware`-class for bus access, but **never emits RF**; still confirm bus connect per assistant policy).
- `set_vsa_mode_and_range` — `INSTrument:SELect`, center freq, input attenuation/range, averaging.

Guardrail note: any tool that ends up commanding **ESG power** (directly or because `verify_signal` triggers Play) is still subject to the §6 input-damage gate and the assistant doc's hardware-confirmation policy. The analyzer-only tools are read-class and run freely, which makes "did it come out right?" a cheap, safe question for the assistant to answer.

Example assistant flow: *"Generate the 4-tone Newman multitone, play it at 1 GHz −10 dBm, then check it on the analyzer."* → `configure_multitone` → `calculate_waveform` → (confirm) `set_instrument_settings`+`download_waveform`+`play_rf` → `verify_signal` → Claude reports "Measured channel power −10.3 dBm (expected −10.0, within ±0.5); PAPR 8.4 dB (expected 8.5); all four tones within 1 kHz of target. Pass."

---

## 9. UI requirements (WinForms)

- **Second connection panel** for the E4406A in the Connection Manager: VISA resource, `*IDN?`/`*OPT?` readout, mode indicator, input-range control, and a prominent **input-damage threshold + path-loss** field (§6).
- **Verification view** (new tab/dock): a side-by-side **Expected vs Measured** table (metric, expected, measured, Δ, tolerance, pass/fail) plus the relevant analyzer trace (spectrum / CCDF / waveform) overlaid where useful with the PC-computed expectation.
- **"Verify" action** in the action bar next to Calculate/Download/Play — one click runs the harness on the current signal.
- **Path-calibration wizard** (§7.2): guided CW generate→measure→store-offset.
- **Shared log integration:** every E4406A SCPI write/query + `SYSTem:ERRor?` lands in the existing timestamped log alongside ESG traffic, tagged by instrument, so a closed-loop run is fully auditable.
- **Two-instrument status bar:** show both ESG and VSA online/offline + model, and the **armed/safe** state of the RF link (is the damage-gate active?).

---

## 10. Open items to confirm on the actual E4406A before coding §5/§6

- **Max safe RF input level** and damage threshold from the unit's data sheet/specs (drives §6) — confirm for your serial/options.
- **Installed options (`*OPT?`)** — whether any standard personalities are present, or v1 is strictly Basic mode; and the frequency range option (≤4 GHz typical).
- **Exact SCPI roots** for each Basic-mode measurement (`CHPower` vs `CPOWer`, ACP node spelling, CCDF = `PSTatistic`) and the `[n]` trace-index meanings per measurement, from the Language Reference (Programmer's Guide ch.5).
- **GPIB address**: this unit is at **17** (clear of the 8563E at 18); confirm whether you'll run it on GPIB or LAN.
- **`FORMat:DATA` / `FORMat:BORDer`** defaults and the fastest binary trace format the unit accepts.
- **10 MHz reference**: decide whether to lock ESG and E4406A to a common reference (recommended for clean frequency comparisons); confirm `ROSCillator`/external-ref handling on the analyzer.
- **Settling/averaging**: realistic measurement times for the chosen averaging so timeouts and the assistant's "still working" affordance are tuned.

---

## 11. Suggested additions to the project layout

```
SignalStudio.Core/
 |- Visa/IInstrumentTransport.cs        (existing)
 |- Visa/EsgInstrument.cs               (existing - generation)
 |- Visa/VsaInstrument.cs               (NEW - E4406A SCPI: MEAS/READ/FETCh/CONF)
 |- Measure/IVsaMeasurement.cs          (NEW)
 |- Measure/Results/                    (NEW - ChannelPowerResult, AcpResult, CcdfResult, ...)
 |- Verify/VerificationHarness.cs       (NEW - expected vs measured compare)
 |- Verify/VerificationProfile.cs       (NEW - tolerances, damage threshold, path loss)
 |- Verify/PathCalibration.cs           (NEW)
SignalStudio.Ui/
 |- ConnectionManagerForm.cs            (extend: second instrument)
 |- Verify/VerificationView.cs          (NEW)
SignalStudio.Assistant/                 (from the Claude doc)
 |- Tools/Measure/                      (NEW - measure_*/verify_signal tools)
SignalStudio.Tests/
 |- ClosedLoop/                         (NEW - headless generate-measure-assert battery)
```

No new VISA dependency beyond what the ESG side already uses; `VsaInstrument` reuses `IInstrumentTransport`.

---

## 12. Phased build recommendation

- **P1:** `VsaInstrument` + `*IDN?`/`*OPT?` + Basic-mode **Channel Power** and **Spectrum (marker)** measurements; second connection panel; **input-damage gate (§6)**; manual "Verify" of a CW tone (frequency + power) against expected. Goal: prove the loop and prove the safety gate.
- **P2:** add **CCDF/PAPR**, **ACP**, **Waveform** measurements; the full Expected-vs-Measured Verification view; path-calibration wizard; headless regression harness (§7.3) wired into the existing acceptance tests.
- **P3:** assistant `measure_*`/`verify_signal` tools (§8); per-metric tolerance tuning; optional standard-personality measurements if `*OPT?` warrants; common-10-MHz-reference locking.

---

## 13. Reference links / sources

**Uploaded manuals (authoritative for SCPI), in `C:\Users\Tony\OneDrive\Documents\Manuals`:**
- *E4406A VSA Signal Analyzer Programmer's Guide* — E4406-90303 (May 2007). MEASure/CONFigure/FETCh/READ/INITiate group; SENSe/CALCulate subsystems; status registers; LAN/GPIB/socket I/O; markers and binary-trace examples. (`E4406A Programmers Guide.pdf`)
- *E4406A VSA Series Transmitter Tester User's Guide* — measurement concepts, modes, front-panel equivalents. (`E4406A_Manual.pdf`)
- Also present: `E4406A Test.pdf`, `E4406A Service.pdf`.

**Companion docs:**
- *SignalStudio-Rebuild-Requirements.md* (ESG generation, transport, acceptance tests).
- *SignalCreation-UX-Requirements.md* (CCDF/PAPR, occupied-BW, verification-graphics conventions reused here).
- *SignalStudio-Claude-Integration-Requirements.md* (tool surface + guardrail model extended in §8).

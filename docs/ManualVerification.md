# ESG-SignalCreator — Manual Verification Procedure

> 🌐 **English** (this page) · Dansk: [ManualVerification](da/ManualVerification.md)

A step-by-step **bench procedure** for manually verifying that an ESG-SignalCreator install — the
**E4438C** generator plus a **VSA** (Agilent **E4406A** or Keysight **N9010A/EXA**) — is wired,
configured and working end-to-end. Every step names the **exact UI control** to use, the **exact value**
to enter, and **what you should see on the analyzer**.

This is the manual companion to the one-click **Verify install…** self-test ([UserGuide §9.7](UserGuide.md#97-install-verification-self-test)):
it drives the *same* CW → AM → FM → I/Q battery and expects the *same* readings, but you do each step
yourself and read the analyzer directly. Use it to commission a new bench, to localise a failure the
automated self-test reports, or to learn what "good" looks like on the analyzer.

> **Safety first.** The analyzer only ever *receives* RF. Before driving any power, arm the input-damage
> gate (Step 2) and read [UserGuide §15 — Safety notes](UserGuide.md#15-safety-notes). All power values
> below assume **0 dB path loss**; if you have an inline pad/attenuator, subtract it from the expected
> analyzer readings (or, better, capture it with **Path cal…**, Step 3).

---

## Reference values used throughout

These are the defaults the automated **Verify install…** uses, so the manual run matches it exactly.

| Parameter | Value | Where it comes from |
|---|---|---|
| Carrier frequency | **1 GHz** (1 000 000 000 Hz) | ESG **Instrument settings → Frequency** |
| Commanded ESG power | **−10 dBm** | ESG **Instrument settings → Amplitude** |
| ARB sample clock | **10 MHz** | set automatically on **Play** |
| Analyzer span | **5 MHz** | VSA setup (Step 1) |
| Tone offset (CW/AM subcarrier) | **+1 MHz** → tone at **1.001 GHz** | built into the signals |
| Settle time before reading | **~3 s** | let the ALC re-level per waveform |
| Path loss | **0 dB** (adjust for your cabling) | RF-path safety / Path cal |

**Pass tolerances** (same as the automated self-test):

| Metric | Tolerance |
|---|---|
| Channel power | **± 3 dB** |
| PAPR (crest factor) | **± 2.5 dB** |
| Tone frequency | **± 50 kHz** |

---

## VSA settings checklist

Set these on the analyzer **before** you start (or confirm them if the app has already driven the
analyzer). The app sets the measurement mode and center/span automatically when it measures, but for
**watching the signal live** on the front panel — and for a manual read — configure the analyzer like
this:

| Setting | E4406A | N9010A / EXA | Notes |
|---|---|---|---|
| **Measurement mode** | **Basic** | **SA** (Spectrum Analyzer) for Channel Power / CCDF; **IQ Analyzer** for Spectrum/Waveform | The app selects the right mode per measurement; set Basic/SA for a manual read. |
| **Center frequency** | **1 GHz** | **1 GHz** | The carrier. |
| **Span** | **5 MHz** | **5 MHz** | Wide enough to see the carrier and the +1 MHz tone together. |
| **Reference level** | **≈ 0 dBm** (≥ 10 dB above −10 dBm) | **≈ 0 dBm** | Headroom above the −10 dBm signal; avoids input overload and clipping. |
| **Input attenuation** | **Auto** (or ≥ 10 dB) | **Auto** | Manual only if you know the level; never below the signal. |
| **Reference oscillator** | **Internal**, or **External 10 MHz** | **Internal**, or **External 10 MHz** | A **common 10 MHz** (house ref, or ESG 10 MHz OUT → analyzer) tightens frequency agreement. |
| **Sweep / trigger** | **Continuous** to watch, **Single** to read | **Continuous** / **Single** | The app uses single (`:INIT:CONT OFF`) for a settled read; continuous shows live. |
| **RBW / VBW** | **Auto** | **Auto** | Auto-couple is fine for these checks. |

> **Damage limits.** Seed the input-damage gate from the model: **E4406A** type-N input ≈ +35 dBm
> (gate defaults to +30 dBm); **N9010A** a conservative **+25 dBm** (confirm against its data sheet).
> At −10 dBm commanded you are far below either, but always arm the gate anyway (Step 2).

---

## Procedure

### Step 1 — Wire and set up the analyzer
1. Cable the **ESG RF OUTPUT** to the **analyzer RF INPUT** (through your pad/attenuator if any). The
   analyzer only ever receives.
2. On the analyzer, apply the **VSA settings checklist** above: mode, **center 1 GHz**, **span 5 MHz**,
   **ref level ≈ 0 dBm**, reference oscillator, continuous sweep.

### Step 2 — Connect both instruments and arm safety (in the app)
1. Click **Connect…** (top toolbar). Enter the ESG VISA resource (e.g. `TCPIP0::192.168.1.82::inst1::INSTR`
   or `GPIB0::19::INSTR`) and connect. The status strip shows **Online** and the model.
2. Set the **VSA model** toggle (next to **Connect VSA…**) to **E4406A** or **N9010A**.
3. Click **Connect VSA…**. Enter the analyzer's VISA resource (E4406A e.g. `GPIB0::17::INSTR`; N9010A
   e.g. `TCPIP0::<ip>::hislip0::INSTR`) and connect. The app refuses an instrument that doesn't match
   the selected model.
4. In the **RF-path safety** settings:
   - **Armed** → **on**.
   - **Analyzer max safe input (dBm)** → leave the model default (**+30** E4406A / **+25** N9010A).
   - **Path loss (dB)** → your inline loss, or **0** if directly cabled.

### Step 3 — (Recommended) Capture path loss
1. Click **Path cal…**. Let the wizard drive a clean carrier and measure it; it records
   *commanded − measured* as **path loss** and applies it to the safety gate and to Verify.
2. Finish the wizard (RF returns **off**). If you skip this, keep **path loss = 0** and mentally subtract
   your cable loss from every expected reading below.

### Step 4 — CW tone (the frequency/level reference)
1. Select **Source** (left tree) → **CW / Single tone**.
2. Set **Frequency offset** = **1 000 000 Hz** (1 MHz), **Amplitude** = **0 dBFS**, **Phase** = **0°**.
3. Click **Calculate**. In the **results readout**, confirm **PAPR ≈ 0 dB**.
4. Select **Instrument settings**; set **Frequency = 1 GHz**, **Amplitude = −10 dBm**. Confirm RF and
   modulation are enabled for ARB playback.
5. Click **Download**, then **Play**. The **play-state indicator** reaches **Playing**.
6. Wait **~3 s**, then read the analyzer.

**What you should see on the VSA:**
- **Spectrum:** a single sharp line at **1.001 GHz** (carrier + 1 MHz), no other significant tones.
- **Marker / Channel Power:** **≈ −10 dBm** (± 3 dB, minus your path loss).
- **Tone frequency:** **1.001 GHz** (± 50 kHz — tighter with a common 10 MHz reference).
- **CCDF / PAPR:** **≈ 0 dB** (± 2.5 dB) — a pure tone has essentially no crest.

### Step 5 — AM (the amplitude path)
1. The automated battery uses **50% AM at 100 kHz on a +1 MHz subcarrier**. To reproduce manually,
   either use **Verify install…** (which builds it for you) or set **Source → AM** with **depth 50%**,
   **rate 100 kHz** and add a **+1 MHz** offset. Keep **Amplitude −10 dBm**.
2. **Calculate → Download → Play**, wait ~3 s, read the analyzer.

**What you should see on the VSA:**
- **Spectrum:** a carrier at **1.001 GHz** with **AM sidebands at ± 100 kHz** around it.
- **Channel Power:** **≈ −13 dBm** (± 3 dB) — about **3 dB** below CW, because the peak-normalized ARB
  puts the AM crest below the commanded level.
- **CCDF / PAPR:** **≈ 3 dB** (± 2.5 dB) — the AM envelope's crest.

> If AM comes out **~60 dB low**, the baseband is being played as raw real-only AM (`I = 1 + m·sin`,
> `Q = 0`) with a large DC term the E4438C ARB won't reproduce at level — use the +1 MHz subcarrier form
> (which is what **Verify install…** does).

### Step 6 — FM (the frequency/phase path)
1. Use **Verify install…**'s FM (**500 kHz deviation at 100 kHz**), or **Source → FM** with those
   values. Keep **Amplitude −10 dBm**.
2. **Calculate → Download → Play**, wait ~3 s, read the analyzer.

**What you should see on the VSA:**
- **Spectrum:** a **wideband FM** spectrum roughly **± 500 kHz** around **1 GHz** (Bessel sidebands),
  constant envelope.
- **Channel Power:** **≈ −10 dBm** (± 3 dB) — same as CW; FM is constant-envelope.
- **CCDF / PAPR:** **≈ 0 dB** (± 2.5 dB) — constant envelope, no crest.

### Step 7 — I/Q multitone (the full complex path)
1. Use **Verify install…**'s multitone (**4-tone Newman, 1 MHz spacing**), or **Source → Multitone**
   with **4 tones**, **1 MHz spacing**, **Newman** phasing. Keep **Amplitude −10 dBm**.
2. **Calculate → Download → Play**, wait ~3 s, read the analyzer.

**What you should see on the VSA:**
- **Spectrum:** **four equally-spaced tones**, 1 MHz apart, centered on 1 GHz.
- **Channel Power:** **≈ −13 to −14 dBm** (± 3 dB) — below CW by the multitone crest.
- **CCDF / PAPR:** **≈ 3.5–4 dB** (± 2.5 dB) — Newman phasing keeps the crest low for 4 tones.

### Step 8 — Stop and record
1. Click **Stop** — the ARB disarms and **RF turns off**.
2. Record each metric against its expected value and tolerance. All four signals within tolerance = the
   install and configuration are verified end-to-end (unmodulated → amplitude → frequency → complex I/Q).

---

## Expected-reading summary

| Signal | Analyzer center | Channel power (±3 dB) | PAPR (±2.5 dB) | Notable spectrum |
|---|---|---|---|---|
| **CW** | 1 GHz | ≈ −10 dBm | ≈ 0 dB | one line at 1.001 GHz |
| **AM** | 1 GHz | ≈ −13 dBm | ≈ 3 dB | carrier + ±100 kHz sidebands |
| **FM** | 1 GHz | ≈ −10 dBm | ≈ 0 dB | ±500 kHz Bessel spectrum |
| **I/Q multitone** | 1 GHz | ≈ −13…−14 dBm | ≈ 3.5–4 dB | 4 tones, 1 MHz apart |

*(All channel-power figures assume 0 dB path loss and a −10 dBm commanded level; subtract your path loss.)*

---

## Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| **All levels ~path-loss low** | Uncaptured cable/pad loss | Run **Path cal…** (Step 3) or set path loss. |
| **Everything ~40–48 dB low, PAPR huge** | On an N9010A, CCDF returns a 5001-point trace, not scalars | Fixed in the app (PAPR from trace); update to the latest release. |
| **AM carrier ~60 dB low** | Raw real-only AM baseband with DC | Use the **+1 MHz subcarrier** AM (what **Verify install…** builds). |
| **Multitone channel power intermittently low** | Read before the ALC re-leveled | Increase settle to ~3 s and re-read. |
| **Tone frequency off by > 50 kHz** | Independent timebases | Lock a **common 10 MHz** reference (**Reference** button). |
| **Input overload warning** | Ref level / attenuation too low | Raise the analyzer reference level; keep attenuation ≥ signal. |
| **Power command refused** | Would exceed the analyzer's safe input | Lower the level or declare more path loss — the gate is protecting the front end. |

For the automated equivalent and a FAIL-time guidance dialog, see
[UserGuide §9.7](UserGuide.md#97-install-verification-self-test); for the hands-on tutorial, see
[Tutorials — Part F](Tutorials.md#part-f--vsa-verification-e4406a--n9010a).

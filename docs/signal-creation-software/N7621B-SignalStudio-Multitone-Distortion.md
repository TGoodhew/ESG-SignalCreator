# N7621B — Signal Studio for Multitone Distortion — Requirements

> Source category: **PC-based signal creation software**, from the Agilent E4438C ESG Vector Signal Generator Data Sheet (literature no. 5988-4039EN).
> Purpose: capture this product's capabilities as candidate requirements for the ESG-SignalCreator app (a modern reimplementation of Signal Studio for the E4438C).

> 🟢 **Implementation status (v2):** A **Multitone Distortion** personality ships in the app
> (`Core/Personalities/MultitoneDistortion/`). It generates 2–4097 equally-spaced tones with a
> configurable spacing/centre, per-tone phase preset (random / parabolic / constant), and an optional
> cleared **NPR notch** (width + offset) — covering **R-1**, **R-2**, **R-3**, **R-6** (notch geometry;
> the >60 dBc depth / ±0.5 dB flatness are hardware-verification targets), **R-8** (CCDF/spectrum plots),
> and **R-9** (shared ARB download).
> - **R-4/R-5** (✅ v2, #180): full **per-tone magnitude/phase tables** (cyclic), honoured by the shared
>   multitone engine's new `Manual` phase strategy — beyond the phase-preset PAPR control.
> - **R-7** (✅ v2, #180, in-band): **pre-distortion correction** — subtracts a measured per-tone
>   magnitude/phase error (the inverse channel response) from the base per-tone values. The measurement
>   itself is supplied by the signal analyzer (closed-loop Verify / external tool), not this personality.
>
> **Still deferred** (tracked in #180): out-of-band IMD-notch cancellation (injecting anti-phase
> correction tones inside the NPR notch) and the COM/.NET automation API (**R-10** — the Assistant
> `configure_multitone_distortion` tool provides scripted config in the interim). Hardware verification is
> tracked in the verification epic (#157).

## 1. Product identity
- **Model / option number:** N7621B (earlier revision: N7621A)
- **Product name:** Signal Studio for Multitone Distortion (now branded PathWave Signal Generation for Multitone Distortion)
- **Host instrument(s):** Agilent/Keysight **E4438C ESG**, E8267C/D PSG, and N5182A MXG vector signal generators.
- **Status:** Current product line (rebranded PathWave); the classic "Signal Studio" branding is legacy. Runs on the discontinued E4438C ESG as a supported host.

## 2. Overview
Signal Studio for Multitone Distortion is PC software that creates validated, performance-optimised multitone and noise-power-ratio (NPR) test stimulus signals for characterising the linearity of narrowband components, receivers, amplifiers, and converters. It builds distortion-free two-tone and multitone signals (used for intermodulation-distortion / IMD testing) and NPR notch stimuli (used for wideband/transponder linearity testing), then downloads the waveform to an ESG/PSG/MXG for playback. It also supports optional pre-distortion correction using a spectrum analyzer to further suppress residual IMD in the generated signal.

## 3. Standards & formats supported
(These are stimulus signal types, not comms standards.)
- Two-tone test signals (classic IMD test).
- Multitone test signals (up to thousands of equally spaced tones).
- Noise Power Ratio (NPR) test signals — a dense multitone "noise" band with a cleared notch.
- Downloadable ARB waveforms for the host signal generator's internal baseband/ARB.

## 4. Key capabilities / features
- Configure distortion-free two-tone and multitone test signals with **up to 4097 tones**, achieving **IMD suppression > 70 dBc**.
- Configure an NPR test stimulus with **notch depth > 60 dBc**, **± 0.5 dB noise flatness**, and wide noise bandwidth (see section 5).
- Per-tone control of **magnitude** and **phase**; phase presets of **random, parabolic, or constant** across tones to control the peak-to-average power ratio (PAPR) / CCDF (crest factor) of the composite signal.
- NPR notch definition: variable **notch width** and **notch offset** from the noise-band center; adjustable IMD/in-band correction levels within the notch.
- Advanced distortion correction — in-band and out-of-band pre-distortion, applied automatically using a spectrum analyzer, to improve IMD suppression / notch depth at the DUT.
- Calibrated measurements referenced to the DUT (can include test-fixture/loss elements).
- Waveform sequencing with smooth signal transitions.
- CCDF and spectrum graph visualisation of the configured signal.
- Parameterised and graphical signal configuration interface.
- Automation via COM and .NET API.

## 5. Configurable signal parameters
- **Tone count:** 2 (two-tone) up to **4097 tones**.
- **Tone spacing:** user-defined; for NPR, noise bandwidth is defined by *number of tones × tone spacing*.
- **Noise / signal bandwidth:** up to ~100 MHz on ESG/MXG-class hosts; PathWave/PSG documentation cites up to > 2 GHz noise BW on wideband hosts (host-dependent — the E4438C ESG is limited by its internal ARB bandwidth, nominally up to ~80 MHz RF modulation BW).
- **Per-tone magnitude:** individually settable.
- **Per-tone phase:** random, parabolic, or constant preset (drives PAPR/CCDF / crest factor).
- **NPR notch:** notch width and notch offset from noise center; in-band IMD correction level within the notch.
- **Notch depth:** target > 60 dBc.
- **Noise flatness:** ± 0.5 dB across the noise band.
- **IMD suppression:** > 70 dBc for multitone (with correction).
- **Pre-distortion:** optional spectrum-analyzer-assisted in-band / out-of-band correction.

## 6. Candidate requirements for ESG-SignalCreator
- **R-1:** The app SHALL generate a two-tone signal with user-configurable tone frequencies/spacing and per-tone amplitude.
- **R-2:** The app SHALL generate a multitone signal supporting from 2 up to at least 4097 equally spaced tones.
- **R-3:** The app SHALL let the user set tone spacing and derive the composite signal (noise) bandwidth from tone count × spacing.
- **R-4:** The app SHALL provide per-tone magnitude control and per-tone phase control with random, parabolic, and constant phase presets.
- **R-5:** The app SHALL report and let the user optimise the composite PAPR / CCDF (crest factor) via the phase preset.
- **R-6:** The app SHALL generate an NPR stimulus with a configurable notch (width and offset from band center) targeting notch depth > 60 dBc and ± 0.5 dB noise flatness.
- **R-7:** The app SHALL support optional pre-distortion correction (in-band and out-of-band) to improve IMD suppression / notch depth, using measured spectrum data.
- **R-8:** The app SHALL display CCDF and spectrum graphs of the configured signal before download.
- **R-9:** The app SHALL download the resulting ARB waveform to the E4438C internal ARB over LAN or GPIB.
- **R-10:** The app SHOULD expose a COM/.NET (or equivalent modern) automation API for the multitone/NPR configuration.

## 7. Dependencies, licensing & notes
- Requires a host signal generator with internal baseband generator + ARB waveform memory (on the E4438C: Option 601/602). Achievable notch depth / IMD suppression and maximum tone count are bounded by the host's ARB memory and modulation bandwidth — the > 2 GHz noise BW figure applies to wideband PSG-class hosts, not the E4438C ESG.
- Pre-distortion correction requires a compatible Keysight spectrum/signal analyzer for the measure-and-correct loop.
- Licensed per host instrument; modern N7621B ships with a KeysightCare software support subscription. The legacy Signal Studio license model (fixed/transportable) applied to the ESG-era product.
- No third-party comms-standard IP is involved (multitone/NPR are generic stimulus types).

## 8. References
- N7621B Signal Studio for Multitone Distortion — Technical Overview — literature no. 5991-3194EN — https://www.keysight.com/us/en/assets/7018-04111/technical-overviews/5991-3194.pdf
- N7621B PathWave Signal Generation for Multitone Distortion — Keysight product page — https://www.keysight.com/us/en/product/N7621B/pathwave-signal-generation-multitone-distortion.html
- N7621B Signal Studio Software for Multitone Distortion — software detail page — https://www.keysight.com/us/en/lib/software-detail/instrument-firmware-software/n7621b-signal-studio-software-for-multitone-distortion-2207788.html
- N7621A Signal Studio for Multitone Distortion — Technical Overview (legacy, N7621APPC_en) — https://www.altoo.dk/cosmoshop/default/artikelpdf/N7621APPC_en.pdf
- Agilent E4438C ESG Vector Signal Generator Data Sheet — literature no. 5988-4039EN — https://www.keysight.com/us/en/assets/7018-01039/data-sheets-archived/5988-4039.pdf
- **Not fully confirmed:** exact maximum tone spacing, minimum tone spacing, and the E4438C-specific maximum noise bandwidth were not read verbatim from a datasheet; the 100 MHz vs > 2 GHz noise BW figures are host-dependent and should be validated against the E4438C ARB modulation-bandwidth spec before being treated as hard limits.

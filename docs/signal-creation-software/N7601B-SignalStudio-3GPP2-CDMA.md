# N7601B — Signal Studio for 3GPP2 CDMA (cdma2000/1xEV-DO) — Requirements

> Source category: **PC-based signal creation software**, from the Agilent E4438C ESG Vector Signal Generator Data Sheet (literature no. 5988-4039EN).
> Purpose: capture this product's capabilities as candidate requirements for the ESG-SignalCreator app (a modern reimplementation of Signal Studio for the E4438C).

> 🟡 **Implementation status (v1 core):** A **3GPP2 CDMA (cdma2000)** personality now ships in the app
> (`Core/Personalities/Cdma2000/`, on the shared `Dsp/DsssEngine`). It generates a single-code forward-link-style
> signal — QPSK data spread by a Walsh code, PN-scrambled, pulse-shaped at **1.2288 Mcps** — for
> chip-rate/occupied-bandwidth checks. **Simplified v1, not standards-compliant.** Deferred: pilot/sync/paging
> channels, radio configurations (RC1–5), 1xEV-DO Rev 0/A slot structure, frame/PCG timing, and the exact
> cdma2000 baseband filter (an RRC approximation is used). Hardware verification is tracked in the epic.

## 1. Product identity
- **Model / option number:** N7601B (later branded "Signal Studio for cdma2000/1xEV-DO"; supersedes the original N7601A ESG personality)
- **Product name:** Keysight (formerly Agilent) Signal Studio for 3GPP2 cdma2000 / 1xEV-DO
- **Host instrument(s):** Agilent/Keysight E4438C ESG Vector Signal Generator; the N7601B/C generation also supports X-Series MXG/EXG, PSG, M9381A PXIe VSG, E6640A EXM, M8190/95A AWG, and M9420/21A PXIe VXT.
- **Status:** Discontinued. Keysight directs users to the successor embedded/PathWave products (e.g. N7601EMBC). The E4438C ESG is itself a legacy instrument.

## 2. Overview
Signal Studio for cdma2000/1xEV-DO creates performance-optimized 3GPP2 reference signals for characterizing and verifying UE and BTS components, transmitters, and receivers. It offers both waveform-playback (ARB) generation of statistically correct, partially coded signals and a real-time mode with transport-channel coding for receiver conformance and closed-loop/interactive testing, spanning IS-95A, cdma2000, and 1xEV-DO radio formats.

## 3. Standards & formats supported
- IS-95A.
- cdma2000 (1x), including Radio Configurations 1–5 (forward) and reverse-link configurations.
- 1xEV-DO Revision 0 and Revision A.
- Mixed-format configurations across the supported radio formats.

## 4. Key capabilities / features
- UE and BTS component, transmitter, and receiver testing.
- UE and BTS receiver conformance testing with predefined configurations in all supported radio formats and mixed-format setups.
- Receiver testing with transport-channel coding.
- Waveform-playback mode: partially coded, statistically correct signals for component/transmitter test.
- Real-time mode: nonrepeating, dynamically changing signals with closed-loop or interactive control.
- Multicarrier waveform generation (up to 25 carriers).
- Parameterized and graphical signal configuration with tree-style navigation.

## 5. Configurable signal parameters
- **IS-95A channels:** Forward — Pilot, 9-Ch, 32-Ch, 64-Ch; Reverse — Traffic.
- **cdma2000 channels:** Forward — Pilot with 9/12/15-channel options and Radio Configuration 1–5 forward; Reverse — Pilot with 5-channel reverse options.
- **1xEV-DO Rev. 0:** Forward — MAC, Traffic (16QAM / 8PSK / QPSK, without coding); Reverse — Basic, Traffic (BPSK, without coding).
- **1xEV-DO Rev. A:** Forward — MAC, Traffic (16QAM with coding options); Reverse — Basic, Traffic (with Q2 / E4E2 options).
- **Multicarrier (per-carrier parameters):**
  - Oversampling ratio: Auto, 1, 2, 4, 8, 16, 32, 64.
  - Frequency offset: −37.5 to +37.5 MHz.
  - Power: −40 to 0 dB.
  - Timing offset: 0–149 chips / 0–512 chips (carrier-type dependent).
  - Frame repetition: 0 to 30.
  - Maximum 25 carriers per waveform.
- **Modulation (traffic):** QPSK, 8PSK, 16QAM, BPSK (format dependent).

## 6. Candidate requirements for ESG-SignalCreator
- **R-1:** The personality shall generate 3GPP2 signals for IS-95A, cdma2000 (RC 1–5), and 1xEV-DO Rev. 0 and Rev. A.
- **R-2:** The personality shall provide forward-link and reverse-link channel configurations per radio format as enumerated in Section 5.
- **R-3:** The personality shall support mixed-format multicarrier configurations combining different radio formats within a single waveform.
- **R-4:** The personality shall support up to 25 carriers, each with configurable oversampling ratio, frequency offset (−37.5 to +37.5 MHz), power (−40 to 0 dB), timing offset, and frame repetition.
- **R-5:** The personality shall support traffic-channel modulation selection (QPSK, 8PSK, 16QAM, BPSK) per the active radio format.
- **R-6:** The personality shall provide a waveform-playback mode producing partially coded, statistically correct signals for component/transmitter test.
- **R-7:** The personality shall provide predefined receiver-conformance configurations for UE and BTS, with transport-channel coding where required.
- **R-8:** Where instrument capability allows, the personality shall provide a real-time mode for nonrepeating, dynamically changing signals with closed-loop/interactive control.
- **R-9:** The personality shall present a tree-style, parameterized graphical configuration UI.

## 7. Dependencies, licensing & notes
- Requires a compatible vector signal generator with the internal baseband generator / ARB option; real-time mode requires the appropriate real-time baseband hardware option on the host.
- Node-locked / licensed PC software model. The N7601B trial license is no longer valid.
- Real-time mode and closed-loop receiver test depend on host hardware that the legacy E4438C ESG may only partially provide; the ESG-SignalCreator reimplementation should capability-gate real-time features versus ARB playback.
- Multicarrier configurations (up to 25 carriers, ±37.5 MHz offset) may exceed the RF/ARB bandwidth of the legacy E4438C and should be treated as capability-gated.

## 8. References
- Keysight, N7601B Signal Studio online documentation — "Technical Overview" — https://helpfiles.keysight.com/csg/n7601b/Content/Common/technical_overview.htm
- Keysight, N7601B Signal Studio online documentation — "Carrier (N)" (channel types, radio configurations, multicarrier parameters) — https://helpfiles.keysight.com/csg/n7601b/Content/Main/Carrier__N_.htm
- Keysight, "Signal Studio for cdma2000/1xEV-DO" (N7601C successor) — Technical Overview, publication no. **5992-2778EN** — https://www.keysight.com/us/en/assets/7018-06041/technical-overviews/5992-2778.pdf
- Keysight, "N7601B Signal Studio Software for cdma2000/1xEV-DO" (software detail / status page) — https://www.keysight.com/us/en/lib/software-detail/instrument-firmware-software/n7601b-signal-studio-software-for-cdma20001xevdo-2207883.html
- Agilent E4438C ESG Vector Signal Generator Data Sheet — literature no. 5988-4039EN (source category reference; not re-verified online in this pass).
- Note: A standalone N7601B PDF datasheet is indexed on alldatasheet.com but was not fetched; the channel/parameter detail above comes from the N7601B online help and the N7601C technical overview. The original E4438C-era personality was N7601A (not separately verified in this pass).

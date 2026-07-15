# N7612B — Signal Studio for TD-SCDMA — Requirements

> Source category: **PC-based signal creation software**, from the Agilent E4438C ESG Vector Signal Generator Data Sheet (literature no. 5988-4039EN).
> Purpose: capture this product's capabilities as candidate requirements for the ESG-SignalCreator app (a modern reimplementation of Signal Studio for the E4438C).

## 1. Product identity
- **Model / option number:** N7612B (branded "Signal Studio for TD-SCDMA/HSPA" / "TD-SCDMA/HSDPA")
- **Product name:** Keysight (formerly Agilent) Signal Studio for TD-SCDMA
- **Host instrument(s):** Agilent/Keysight E4438C ESG Vector Signal Generator; the N7612B generation also supports X-Series MXG/EXG, PSG, M8190A / M9381A, and the PXB baseband generator/channel emulator.
- **Status:** Discontinued. Keysight directs users to the successor embedded/PathWave products (e.g. N7612EMBC → N7612C). The E4438C ESG is itself a legacy instrument.

## 2. Overview
Signal Studio for TD-SCDMA configures partially or fully coded arbitrary baseband I/Q waveforms for testing TD-SCDMA components (amplifiers, filters), transmitters, receivers, and chipsets. It generates spectrally correct signals for ACLR, channel-power, spectral-mask, and spurious testing, and supports multicarrier waveforms for both LCR TDD and HSDPA/HSPA operation.

## 3. Standards & formats supported
- 3GPP 1.28 Mcps TDD (Low Chip Rate TDD).
- TD-SCDMA / HSPA (HSDPA, HSUPA) physical and transport channels.
- Chinese TD-SCDMA (CWTS) standards.
- Predefined Reference Measurement Channels (RMC) and Fixed Reference Channels (FRC) for UE and BTS receiver conformance testing.

## 4. Key capabilities / features
- Support for all 3GPP physical and transport channels for TD-SCDMA/HSPA.
- Predefined RMC and FRC for UE and BTS receiver conformance testing.
- Multicarrier waveforms (documented up to 12 carriers) for LCR and HSDPA.
- Spectrally correct signal creation for ACLR, channel power, spectral mask, and spurious measurements.
- Simultaneous uplink/downlink timeslot control.
- Slot-length-based waveforms for efficient testing.
- CCDF, spectrum, and time-domain visualization.

## 5. Configurable signal parameters
- **Framing:** switching point (uplink/downlink boundary), per-timeslot configuration, simultaneous UL/DL timeslot control.
- **Channels:** all TD-SCDMA/HSPA physical and transport channels; RMC and FRC presets.
- **Code domain:** code domain power settings; cell ID configuration.
- **Modulation:** QPSK, 16QAM, 64QAM (data-channel dependent).
- **Multicarrier (per-carrier):** configurable modulation type, frequency offset, timing offset, power, and baseband filter settings; up to 12 carriers.
- **Power / conditioning:** channel power; spectrally correct output for ACLR/mask/spurious.
- **Analysis aids:** CCDF, spectrum, and time-domain plots.

## 6. Candidate requirements for ESG-SignalCreator
- **R-1:** The personality shall generate 3GPP 1.28 Mcps TDD (TD-SCDMA) signals compliant with 3GPP LCR TDD and CWTS standards.
- **R-2:** The personality shall support TD-SCDMA/HSPA physical and transport channels, including HSDPA.
- **R-3:** The personality shall allow configuration of the switching point and per-timeslot settings with simultaneous uplink/downlink timeslot control.
- **R-4:** The personality shall allow configuration of code domain power and cell ID.
- **R-5:** The personality shall support data-channel modulation selection among QPSK, 16QAM, and 64QAM where the channel permits.
- **R-6:** The personality shall provide predefined RMC and FRC configurations for UE and BTS receiver conformance testing.
- **R-7:** The personality shall support multicarrier composition (up to 12 carriers) with per-carrier modulation, frequency offset, timing offset, power, and baseband filter settings.
- **R-8:** The personality shall produce spectrally correct waveforms suitable for ACLR, channel-power, spectral-mask, and spurious testing.
- **R-9:** The personality shall support partially or fully coded ARB waveform generation.
- **R-10:** The personality shall display CCDF, spectrum, and time-domain views of the generated waveform.

## 7. Dependencies, licensing & notes
- Requires a compatible vector signal generator with the internal baseband generator / ARB option; on the E4438C ESG this is the internal baseband generator with adequate ARB memory.
- Node-locked / licensed PC software model. The N7612B trial license is no longer valid.
- Multicarrier configurations (up to 12 carriers) may exceed the RF/ARB bandwidth of the legacy E4438C and should be treated as capability-gated in the reimplementation.
- 64QAM and certain HSPA features belong to later releases of the TD-SCDMA software and may not have been present in the earliest E4438C-era personality; treat higher-order modulation as capability-gated.

## 8. References
- Keysight, "Signal Studio for TD-SCDMA/HSDPA" (N7612B) — Technical Overview, publication no. **5990-9099EN** — https://www.keysight.com/ca/en/assets/7018-03138/technical-overviews/5990-9099.pdf
- Keysight/Agilent, "Signal Studio for TD-SCDMA/HSPA — N7612B" (datasheet PDF) — https://assets-us-01.kc-usercontent.com/ecb176a6-5a2e-0000-8943-84491e5fc8d1/806cf1d0-b168-49e7-ab77-c86c73995edd/N7612B.pdf
- Keysight, "N7612B Signal Studio Software for TD-SCDMA/HSPA" (software detail / status page) — https://www.keysight.com/us/en/lib/software-detail/instrument-firmware-software/n7612b-signal-studio-software-for-tdscdmahspa-2207803.html
- Keysight, "Signal Studio for TD-SCDMA/HSPA" (N7612C successor) — Technical Overview, publication no. **5992-2784EN** — https://www.keysight.com/us/en/assets/7018-06047/technical-overviews/5992-2784.pdf
- Agilent E4438C ESG Vector Signal Generator Data Sheet — literature no. 5988-4039EN (source category reference; not re-verified online in this pass).
- Note: The "up to 12 carriers" figure and QPSK/16QAM/64QAM modulation set are drawn from the N7612B technical overview and search-indexed datasheet summary; the version boundary for 64QAM/HSPA vs. the original E4438C-era personality was not independently verified in this pass.

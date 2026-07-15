# N7600B — Signal Studio for 3GPP W-CDMA FDD — Requirements

> Source category: **PC-based signal creation software**, from the Agilent E4438C ESG Vector Signal Generator Data Sheet (literature no. 5988-4039EN).
> Purpose: capture this product's capabilities as candidate requirements for the ESG-SignalCreator app (a modern reimplementation of Signal Studio for the E4438C).

> 🟡 **Implementation status (v1 core):** A **3GPP W-CDMA FDD** personality now ships in the app
> (`Core/Personalities/Wcdma/`, on the shared `Dsp/DsssEngine`). It generates a single-code
> downlink-style signal — QPSK data spread by an **OVSF** code, complex-scrambled, RRC-shaped (β 0.22)
> at **3.84 Mcps** — a representative W-CDMA carrier for chip-rate/occupied-bandwidth/spectral-mask
> checks. **This is a simplified v1, not a standards-compliant multi-code downlink.** Deferred:
> multi-code composite (CPICH/P-CCPCH/P-SCH/S-SCH), slot/frame (15-slot, 10 ms) structure, TFCI,
> transmit diversity, cell scrambling-code sets, and HSPA channels (see E4438C-419). Hardware
> verification is tracked in the epic.

## 1. Product identity
- **Model / option number:** N7600B (later branded "Signal Studio for W-CDMA/HSPA+"; supersedes the original N7600A ESG personality)
- **Product name:** Keysight (formerly Agilent) Signal Studio for 3GPP W-CDMA FDD / HSPA / HSPA+
- **Host instrument(s):** Agilent/Keysight E4438C ESG Vector Signal Generator; the N7600B generation also supports X-Series MXG/EXG, PSG, M9381A PXIe VSG, M8190A AWG, and SystemVue.
- **Status:** Discontinued. Keysight directs users to the successor embedded/PathWave products (e.g. N7600EMBC). The E4438C ESG is itself a legacy instrument.

## 2. Overview
Signal Studio for W-CDMA FDD creates validated, performance-optimized physical-layer W-CDMA / HSPA / HSPA+ reference signals for testing base-station (BTS) and user-equipment (UE) components, transmitters, and receivers. It produces standards-compliant downlink and uplink signals with a broad set of predefined 3GPP physical-layer test models and sub-tests, enabling power, modulation-quality, and receiver measurements on a device under test.

## 3. Standards & formats supported
- 3GPP W-CDMA (UTRA FDD), HSPA, and HSPA+ — compliance stated up to 3GPP Release 11 in the N7600B generation.
- Uplink sub-tests referenced for 3GPP Release 6–8.
- Predefined downlink physical-layer 3GPP test models.
- Reference Measurement Channels (RMC) and Fixed Reference Channels (FRC).
- HSDPA physical-layer H-Set definitions (H-Sets 1–11).

> Note: The original E4438C-era N7600A personality covered W-CDMA with HSDPA/HSUPA; the higher release numbers and HSPA+ features above belong to the later N7600B software generation. See References for the version boundary.

## 4. Key capabilities / features
- Predefined 3GPP downlink physical-layer test models for transmitter/PA characterization.
- RMC and FRC definitions, including HSDPA H-Sets 1–11, for receiver conformance testing.
- Uplink real-time mode supporting PRACH and related channels.
- UL HS-DPCCH with 4C+MIMO feedback for BTS receiver test; DL DC+MIMO signals for UE receiver test.
- Multicarrier signal generation (up to 128 carriers) with per-carrier timing, phase offset, and clipping.
- Automatic calculation of cubic metric and k-value.
- CCDF graphs for waveform power-statistics insight.
- Slot-length-based waveforms for efficient PA testing.

## 5. Configurable signal parameters
- **Channels:** HS-DPDCH, S-CCPCH, E-DPDCH (and standard DPCH/DPCCH/DPDCH, PRACH in UL real-time).
- **Modulation:** BPSK, QPSK, 4PAM, 16QAM, 64QAM (data-channel dependent).
- **Physical-layer controls:** channel power, scrambling code, TFCI field, transmit diversity.
- **Framing / spreading:** spreading factor / OVSF code allocation via test-model and channel setup.
- **Multicarrier:** up to 128 carriers with per-carrier timing, phase offset, and clipping.
- **Impairments / conditioning:** clipping; cubic-metric and k-value auto-calculation.
- **Analysis aids:** CCDF power-statistics graphs.

## 6. Candidate requirements for ESG-SignalCreator
- **R-1:** The personality shall generate 3GPP W-CDMA FDD downlink signals conforming to the predefined 3GPP physical-layer test models.
- **R-2:** The personality shall provide selectable downlink physical channels including HS-DPDCH and S-CCPCH, with configurable channel power per channel.
- **R-3:** The personality shall support data-channel modulation selection among BPSK, QPSK, 4PAM, 16QAM, and 64QAM where the channel type permits.
- **R-4:** The personality shall provide HSDPA Fixed Reference Channels covering H-Sets 1–11 for receiver conformance workflows.
- **R-5:** The personality shall provide Reference Measurement Channels (RMC) for UE/BTS conformance testing.
- **R-6:** The personality shall allow configuration of scrambling code, TFCI field, and transmit-diversity mode.
- **R-7:** The personality shall automatically compute and display the cubic metric and k-value for the configured waveform.
- **R-8:** The personality shall support multicarrier composition with per-carrier timing offset, phase offset, and clipping.
- **R-9:** The personality shall support waveform-playback (ARB) generation of slot-length-based waveforms and, where feasible, an uplink real-time mode including PRACH.
- **R-10:** The personality shall display CCDF power statistics for the generated waveform.

## 7. Dependencies, licensing & notes
- Requires a compatible vector signal generator with the ARB/baseband generator option; on the E4438C ESG this is the internal baseband generator with adequate ARB memory.
- Node-locked / licensed PC software model (per-instrument license typical of the Signal Studio line). The N7600B trial license is no longer valid.
- Downlink DC+MIMO and multicarrier configurations may exceed the RF bandwidth/ARB capabilities of the legacy E4438C; the ESG-SignalCreator reimplementation should treat wideband multicarrier and MIMO as capability-gated features.
- Certain HSPA+ / higher-release features were introduced in the N7600B software generation and may not have existed in the original E4438C-era N7600A personality.

## 8. References
- Keysight, "N7600B Signal Studio for W-CDMA/HSPA+" — Technical Overview, publication no. **5990-8735EN** — https://www.keysight.com/us/en/assets/7018-03083/technical-overviews/5990-8735.pdf
- Keysight, "N7600B Signal Studio Software for W-CDMA/HSPA+" (software detail / status page) — https://www.keysight.com/us/en/lib/software-detail/instrument-firmware-software/n7600b-signal-studio-software-for-wcdmahspa-2207885.html
- Keysight, "N7600A Signal Studio for 3GPP W-CDMA with HSDPA/HSUPA [Obsolete]" (identifies the original E4438C-era personality) — https://www.keysight.com/us/en/product/N7600A/signal-studio-for-3gpp-wcdma-with-hsdpahsupa.html
- Agilent E4438C ESG Vector Signal Generator Data Sheet — literature no. 5988-4039EN (source category reference; not re-verified online in this pass).
- Note: A dedicated E4438C-specific N7600A datasheet was not located in this pass; release/feature details above are drawn from the later N7600B technical overview and are flagged where the version boundary matters.

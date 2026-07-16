# N7606B — Signal Studio for Bluetooth — Requirements

> Source category: **PC-based signal creation software**, from the Agilent E4438C ESG Vector Signal Generator Data Sheet (literature no. 5988-4039EN).
> Purpose: capture this product's capabilities as candidate requirements for the ESG-SignalCreator app (a modern reimplementation of Signal Studio for the E4438C).

> 🟡 **Implementation status (v2):** A **Bluetooth** personality ships in the app
> (`Core/Personalities/Bluetooth/`) with two modulations:
> - **GFSK** (v1 core) — Basic Rate / LE Gaussian-filtered FSK (BT 0.5, configurable modulation index),
>   constant envelope.
> - **EDR** (✅ v2, #190): differential **π/4-DQPSK** (2 Mbps) / **8-DPSK** (3 Mbps), RRC-shaped (β 0.4),
>   non-constant envelope, at the 1 Msym/s symbol rate — partial **R-1/R-2**.
>
> Still representative, not standards-compliant. **Still deferred** (#190): the **LE coded PHY** (R-1/R-3/
> R-10), BR/EDR **packet types** (R-4), packet framing + LE data-length extension (R-5), fully channel-coded
> packet generation with access-code/sync words (R-7), and channel hopping. Hardware verification is
> tracked in the verification epic (#157).

## 1. Product identity
- **Model / option number:** N7606B (listed in the E4438C data sheet ordering list as "N7606B Signal Studio for Bluetooth")
- **Product name:** Signal Studio for Bluetooth (Bluetooth BR/EDR and Bluetooth Low Energy)
- **Host instrument(s):** Agilent/Keysight E4438C ESG (explicitly named in the N7606B technical overview supported-instrument list, and N7606B is listed as compatible signal-creation software in the E4438C data sheet). Also supports X-Series N5182A/B MXG and N5172B EXG, E8267D PSG, M9381A PXIe VSG, EXM and EXT wireless communication test sets, plus SystemVue and Waveform Creator software.
- **Status:** Discontinued. The N7606B trial license is no longer valid; Keysight directs users to N7606C software and the N7606EMBC free trial. The E4438C ESG is a legacy/discontinued instrument.

## 2. Overview
N7606B is PC-based signal-creation software that simplifies building standards-based Bluetooth waveforms for component, transmitter and receiver test. It covers Bluetooth Basic Rate (BR), Enhanced Data Rate (EDR) and Bluetooth Low Energy (LE) technologies, generating fully coded packets and modulated data streams. Waveform files are downloaded to a signal generator such as the E4438C ESG for ARB playback, and the tool can build "dirty transmitter" signals for receiver-sensitivity testing. The interface is parameterized and graphical.

## 3. Standards & formats supported
- **Bluetooth BR** (Basic Rate).
- **Bluetooth EDR** (Enhanced Data Rate) — including v2.1 + EDR generation.
- **Bluetooth Low Energy (LE):**
  - **LE 4.0** (original LE).
  - **LE 4.2** (longer packet length / data-length extension up to 255 bytes).
  - **LE 5.0** (2 Msym/s high data rate; channel coding for long range).
- **Modulation formats:** **GFSK** (BR and LE), **π/4-DQPSK** (EDR 2 Mbps), **8DPSK** (EDR 3 Mbps).
- **Symbol rates:** **1 Msym/s** and **2 Msym/s**.
- **LE PHY types:** LE 1M, LE 2M, LE Coded (long range).

## 4. Key capabilities / features
- Creation of standard-based BR/EDR and LE waveforms.
- Fully channel-coded Bluetooth packets and modulated data streams.
- Waveform-playback mode for component and transmitter characterization.
- Fully channel-coded signals supporting receiver BER / BLER / PER / FER analysis.
- "Dirty transmitter" test-setup signals (with carrier-frequency-offset and other impairments) for receiver-sensitivity tests.
- Graphical visualization: CCDF, spectrum, and time-domain views.
- Parameterized and graphical signal configuration.
- Performance-optimized, Keysight-validated reference signals; encrypted/portable waveform sharing (as per the Signal Studio family).

## 5. Configurable signal parameters
- **Technology / PHY:** BR, EDR, LE (LE 1M, LE 2M, LE Coded / long range).
- **Modulation:** GFSK, π/4-DQPSK, 8DPSK; symbol rate 1 or 2 Msym/s.
- **Packet types:** DH1 / DH3 / DH5, 2-DHx, 3-DHx, 2-EVx, 3-EVx (i.e. the DHx, 2-DHx, 2-EVx, 3-DHx, 3-EVx families named in the overview).
- **Payload:** configurable payload and PN data patterns; LE 4.2 data-length extension up to 255-byte payloads.
- **Impairments (dirty transmitter):** carrier-frequency offset and related dirty-transmitter impairments for receiver-sensitivity testing.
- **Coding:** fully channel-coded packet generation vs. uncoded modulated data streams; LE 5.0 long-range channel coding.

## 6. Candidate requirements for ESG-SignalCreator
- **R-1:** The app SHALL generate Bluetooth BR, EDR (incl. v2.1 + EDR) and Bluetooth LE (4.0, 4.2, 5.0) waveforms.
- **R-2:** The app SHALL support modulation formats GFSK, π/4-DQPSK and 8DPSK at symbol rates of 1 Msym/s and 2 Msym/s.
- **R-3:** The app SHALL support LE PHY selection among LE 1M, LE 2M and LE Coded (long range).
- **R-4:** The app SHALL support configuration of BR/EDR packet types including DH1/DH3/DH5, 2-DHx, 3-DHx, 2-EVx and 3-EVx.
- **R-5:** The app SHALL allow configurable payload data (including PN patterns) and LE data-length extension up to 255-byte payloads (LE 4.2).
- **R-6:** The app SHALL support "dirty transmitter" impairment configuration, including carrier-frequency offset, for receiver-sensitivity test signals.
- **R-7:** The app SHALL support both fully channel-coded packet generation and uncoded modulated data streams.
- **R-8:** The app SHALL provide CCDF, spectrum and time-domain graphical views of the configured waveform.
- **R-9:** The app SHALL download the generated waveform to the E4438C ESG for ARB playback, and SHOULD support export to a portable (optionally encrypted) waveform file.
- **R-10:** The app SHOULD support LE 5.0 long-range channel coding for coded-PHY test signals.

## 7. Dependencies, licensing & notes
- **Instrument hardware:** ARB playback on the E4438C requires an internal baseband generator option (Option 601 or 602; the E4438C data sheet marks Signal Studio software items as requiring Option 601/602). ARB memory capacity limits waveform length. The E4438C data sheet also references a legacy ESG-native "E4438C-406 Signal Studio for Bluetooth" option that predates N7606B.
- **Licensing:** N7606B required a license (right-to-use). It is discontinued; the trial license is no longer valid and Keysight points users to N7606C / N7606EMBC.
- **External IP / trademarks:** "Bluetooth" and the Bluetooth logos are trademarks owned by Bluetooth SIG, Inc. and licensed to Agilent/Keysight (noted in the E4438C data sheet). Bluetooth core-spec conformance details derive from Bluetooth SIG specifications.
- **Successor:** N7606C is the current-generation replacement (see references), adding platforms beyond the ESG.

## 8. References
- *N7606B Signal Studio for Bluetooth — Technical Overview* — literature no. **5990-9097EN** — https://www.keysight.com/us/en/assets/7018-03136/technical-overviews/5990-9097.pdf. Primary source for BR/EDR/LE 4.0/4.2/5.0 support, modulation formats (GFSK, π/4-DQPSK, 8DPSK), 1/2 Msym/s symbol rates, packet types, dirty-transmitter impairments, and supported instruments (E4438C ESG named). (Exact publication month not separately captured beyond the 5990-9097EN number.)
- *Agilent E4438C ESG Vector Signal Generator — Data Sheet* — literature no. **5988-4039EN** — https://www.keysight.com/us/en/assets/7018-01039/data-sheets-archived/5988-4039.pdf. Confirms N7606B is listed as compatible PC signal-creation software; source for Option 601/602 baseband-generator dependency and the Bluetooth SIG trademark notice; references legacy option E4438C-406.
- *N7606B Signal Studio Software for Bluetooth* (product/download page) — https://www.keysight.com/us/en/lib/software-detail/instrument-firmware-software/n7606b-signal-studio-software-for-bluetooth-2207809.html. Confirms discontinued status and N7606C/N7606EMBC successor.
- *N7606C Signal Studio for Bluetooth — Technical Overview* — literature no. **5992-2739EN** — https://www.keysight.com/us/en/assets/7018-06015/technical-overviews/5992-2739.pdf. Successor product, consulted for continuity.
- **Not located / not confirmed:** A per-packet-type coding/CRC and hopping-sequence detail table specific to N7606B, and exact ARB memory/waveform-length limits, were not retrieved from the N7606B software help; the LE 4.2 255-byte and LE 5.0 2 Msym/s figures come from the technical overview's LE version descriptions. Whether N7606B exposes full adaptive-frequency-hopping sequences (vs. single-channel packets) could not be confirmed from the available literature.

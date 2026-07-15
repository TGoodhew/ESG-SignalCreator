# N7617B — Signal Studio for 802.11 WLAN — Requirements

> Source category: **PC-based signal creation software**, from the Agilent E4438C ESG Vector Signal Generator Data Sheet (literature no. 5988-4039EN).
> Purpose: capture this product's capabilities as candidate requirements for the ESG-SignalCreator app (a modern reimplementation of Signal Studio for the E4438C).

## 1. Product identity
- **Model / option number:** N7617B (listed in the E4438C data sheet ordering list as "N7617B Signal Studio for 802.11 WLAN")
- **Product name:** Signal Studio for WLAN 802.11a/b/g/j/p/n/ac/ah/ax
- **Host instrument(s):** Agilent/Keysight E4438C ESG (N7617B is listed as compatible signal-creation software in the E4438C data sheet; the ESG-native predecessor was option E4438C-417). N7617B also supports N5182A MXG, N5162A MXG ATE, E8267D PSG, N5106A/N5182B and the N5106A PXB baseband generator, and (in later software versions) the E6640A EXM wireless test set, EXF wireless test, and M9420A VXT PXIe vector transceiver.
- **Status:** Discontinued. The N7617B trial license is no longer valid; Keysight directs users to the N7617EMBC free trial / current-generation software. The E4438C ESG is a legacy/discontinued instrument.

## 2. Overview
N7617B is PC-based signal-creation software that creates standards-based baseband and RF reference signals for testing IEEE 802.11 WLAN components and receivers. It covers 802.11a/b/g/j/p/n/ac/ah/ax, from legacy DSSS/CCK and OFDM through high-throughput MIMO and 802.11ax (HE) OFDMA/MU-MIMO. It provides full channel coding, flexible MAC-header configuration, spatial-stream mapping, and multi-frame waveforms for PER testing. Waveform files are downloaded to a signal generator such as the E4438C ESG for ARB playback.

## 3. Standards & formats supported
- **IEEE 802.11 variants:** 802.11a, 802.11b, 802.11g, 802.11j, 802.11p, 802.11n, 802.11ac, 802.11ah, 802.11ax.
- **Modulation / PHY:**
  - 802.11b: DSSS / CCK (DBPSK, DQPSK, CCK).
  - 802.11a/g/j/p: OFDM with BPSK, QPSK, 16QAM, 64QAM.
  - 802.11n / 802.11ac: OFDM MIMO with MCS-based modulation up to **256QAM** (256QAM referenced for 11n/ac).
  - 802.11ax (HE): OFDMA and MU-MIMO; HE SU PPDU, HE MU PPDU, HE extended-range SU PPDU, and HE NDP PPDU formats.
- **Channel bandwidths:** 20 / 40 / 80 / 80+80 / 160 MHz (80+80 and 160 MHz for 802.11ac/ax).
- **Coding:** BCC (binary convolutional coding, standard) and **LDPC** encoding for 802.11n/ac/ah/ax.

## 4. Key capabilities / features
- Create and customize WLAN 802.11a/b/g/j/p/n/ac/ah/ax waveforms to characterize transmitter/receiver-component power and modulation performance.
- Full channel coding, flexible MAC-header configuration, and spatial-stream mapping.
- **A-MPDU (Aggregation MPDU) mode** for 802.11n/ac/ah/ax.
- **LDPC encoding** for 802.11n/ac/ah/ax (in addition to BCC).
- **MIMO** testing: up to 4 antennas/streams for 802.11n and 802.11ah; up to 8 streams/antennas for 802.11ac and 802.11ax (hardware dependent).
- Space-time streams with direct mapping or a configurable spatial-expansion matrix; beamforming-matrix support for 802.11ac.
- 802.11n / 802.11ac channel models (**A through F**).
- 802.11ax OFDMA with configurable Resource Units (RUs).
- Multi-frame waveforms with incrementing Sequence Control field for PER testing.
- Beacon-frame support; frequency-selective I/Q impairments; CCDF result API querying; time-length information (added in later N7617B versions).
- Guard-interval configuration.

## 5. Configurable signal parameters
- **Standard / PHY variant:** 802.11a/b/g/j/p/n/ac/ah/ax; PPDU format (incl. HE SU / HE MU / HE ER-SU / HE NDP for 11ax).
- **Modulation & coding scheme (MCS):** per-standard MCS selection (BPSK/QPSK/16QAM/64QAM/256QAM per applicable variant); DSSS/CCK for 11b.
- **Channel bandwidth:** 20 / 40 / 80 / 80+80 / 160 MHz.
- **Spatial configuration:** number of spatial/space-time streams and antennas (up to 4 for 11n/11ah; up to 8 for 11ac/11ax); direct mapping or spatial-expansion matrix; beamforming matrix (11ac).
- **Coding:** BCC or LDPC.
- **Guard interval:** short/long (and 11ax GI options) configurable.
- **Aggregation:** A-MPDU mode (11n/ac/ah/ax).
- **MAC:** MAC-header configuration; Beacon frame; multi-frame sequences with incrementing Sequence Control for PER.
- **OFDMA (11ax):** Resource Unit allocation and MU-MIMO.
- **Impairments:** frequency-selective I/Q impairments; 802.11n/ac channel models A–F.
- **Payload/PSDU:** configurable frame payload (PSDU) content.

## 6. Candidate requirements for ESG-SignalCreator
- **R-1:** The app SHALL generate IEEE 802.11a/b/g/j/p/n/ac/ah/ax waveforms.
- **R-2:** The app SHALL support 802.11b DSSS/CCK and 802.11a/g OFDM (BPSK, QPSK, 16QAM, 64QAM) modulation.
- **R-3:** The app SHALL support MCS-based modulation for 802.11n/ac up to 256QAM, and 802.11ax HE PPDU formats (HE SU, HE MU, HE ER-SU, HE NDP).
- **R-4:** The app SHALL support channel bandwidths of 20, 40, 80, 80+80 and 160 MHz (per applicable standard).
- **R-5:** The app SHALL support both BCC and LDPC channel coding (LDPC for 802.11n/ac/ah/ax).
- **R-6:** The app SHALL support A-MPDU aggregation mode for 802.11n/ac/ah/ax.
- **R-7:** The app SHALL support MIMO configuration with up to 4 streams (802.11n/ah) and up to 8 streams (802.11ac/ax), including direct mapping, spatial-expansion matrix, and 802.11ac beamforming matrix.
- **R-8:** The app SHALL support 802.11ax OFDMA with configurable Resource Units and MU-MIMO.
- **R-9:** The app SHALL support configurable MAC headers, Beacon frames, and multi-frame waveforms with an incrementing Sequence Control field for PER testing.
- **R-10:** The app SHALL support guard-interval selection and 802.11n/ac channel models A–F.
- **R-11:** The app SHALL support frequency-selective I/Q impairment configuration.
- **R-12:** The app SHALL download the generated waveform to the E4438C ESG for ARB playback, and SHOULD support export to a portable (optionally encrypted) waveform file.

## 7. Dependencies, licensing & notes
- **Instrument hardware:** ARB playback on the E4438C requires an internal baseband generator option (Option 601 or 602; the E4438C data sheet marks Signal Studio software items as requiring Option 601/602). ARB memory capacity limits waveform length. High-bandwidth 802.11ac/ax channel bandwidths (80/160 MHz) exceed the E4438C ESG's RF/baseband capability — on the ESG, practical use is limited to the legacy variants (802.11a/b/g and narrower configurations) that the ESG-era E4438C-417 option supported; the full 11n/ac/ah/ax feature set targets newer platforms (MXG/PSG/PXB/VXT). This is an important scoping note for an ESG reimplementation.
- **ESG-native predecessor:** E4438C-417 "Signal Studio for 802.11 WLAN" (ESG option; latest ESG-native revision A.02.01, released 2004-02-19) covered the 802.11a/b/g generation; N7617B is the later PC-software generation that adds 11j/p/n/ac/ah/ax.
- **MIMO note:** Multi-antenna/MIMO signals require multiple synchronized generators or a MIMO-capable platform; a single E4438C provides one RF path, so MIMO stream counts above 1 are platform-dependent.
- **Licensing:** N7617B required a license (right-to-use) with free-trial and flexible RTU options. It is discontinued; the trial license is no longer valid and Keysight points users to N7617EMBC / current software.
- **External IP / trademarks:** IEEE 802.11 are IEEE standards; "Wi-Fi" is a Wi-Fi Alliance trademark. Standards conformance detail derives from the IEEE 802.11 specifications.

## 8. References
- *N7617B Signal Studio for WLAN 802.11a/b/g/j/p/n/ac/ah/ax — Technical Overview* — literature no. **5990-9008EN** — https://www.keysight.com/us/en/assets/7018-03119/technical-overviews/5990-9008.pdf. Source for supported standards, MCS/256QAM, channel bandwidths, MIMO up to 8 streams, A-MPDU + LDPC, channel models A–F, beamforming, and 11ax OFDMA/RU. (The fetched copy did not render full per-rate modulation tables verbatim; DSSS/CCK and OFDM BPSK/QPSK/16QAM/64QAM mappings are standard IEEE 802.11 assignments, stated here as such.)
- *N7617B Signal Studio Software for WLAN 802.11* (product/download page, with version history) — https://www.keysight.com/us/en/lib/software-detail/instrument-firmware-software/n7617b-signal-studio-software-for-wlan-80211-2207794.html. Source for 11ax D1.3 / 64-bit support, HE SU/MU/ER-SU/NDP PPDU, OFDMA/MU-MIMO, Beacon frame, frequency-selective I/Q impairments (version notes), discontinued status, and supported platforms (later versions added E6640A EXM, EXF, M9420A VXT).
- *E4438C-417 Signal Studio for 802.11 WLAN* (ESG option software page) — https://www.keysight.com/us/en/lib/software-detail/computer-software/e4438c417-signal-studio-for-80211-wlan-95305.html. Confirms the ESG-native predecessor option (rev A.02.01, 2004-02-19) covering the 802.11a/b/g generation.
- *Agilent E4438C ESG Vector Signal Generator — Data Sheet* — literature no. **5988-4039EN** — https://www.keysight.com/us/en/assets/7018-01039/data-sheets-archived/5988-4039.pdf. Confirms N7617B is listed as compatible PC signal-creation software; source for Option 601/602 baseband-generator dependency.
- **Not located / not confirmed:** A verbatim N7617B specification table mapping each MCS index to exact data rate, and an explicit statement of the maximum 802.11 bandwidth playable on the E4438C ESG specifically, were not retrieved. The 802.11ac/ax high-bandwidth-vs-ESG scoping note above is an engineering inference from the E4438C's RF/baseband limits, not a direct quote. The N7617B overview does not explicitly list the E4438C ESG among its own supported platforms; ESG compatibility is asserted from the E4438C data sheet ordering list and the E4438C-417 predecessor.

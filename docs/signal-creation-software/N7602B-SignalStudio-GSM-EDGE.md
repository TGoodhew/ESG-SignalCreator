# N7602B — Signal Studio for GSM/EDGE — Requirements

> Source category: **PC-based signal creation software**, from the Agilent E4438C ESG Vector Signal Generator Data Sheet (literature no. 5988-4039EN).
> Purpose: capture this product's capabilities as candidate requirements for the ESG-SignalCreator app (a modern reimplementation of Signal Studio for the E4438C).

> 🟡 **Implementation status (v2):** A **GSM/EDGE** personality ships in the app
> (`Core/Personalities/GsmEdge/`) with two modulations:
> - **GMSK** (v1 core) — the GSM/GPRS continuous-phase modulation (BT = 0.3, index 0.5) at 270.833 ksym/s.
> - **EDGE 8-PSK** (✅ v2, #186): **3π/8-continuously-rotated 8-PSK** (3 bits/symbol), pulse-shaped and
>   peak-normalized (non-constant envelope) — partial **R-2**. The pulse is a representative RRC stand-in
>   for the linearised GMSK pulse.
>
> Still representative, not a standards-compliant GSM/EDGE signal. **Still deferred** (#186): EDGE
> Evolution / EGPRS2 higher-order formats (R-1/R-2), HSR (R-3), normal/access **burst** structures + TSC
> (R-4), channel coding CS/MCS (R-5), 13/26/51/52-multiframe framing (R-6), control channels (R-7),
> multi-carrier (R-8), and per-timeslot power (R-11). Hardware verification is tracked in the verification
> epic (#157).

## 1. Product identity
- **Model / option number:** N7602B (product name in the E4438C data sheet ordering list: "N7602B Signal Studio for GSM/EDGE"). Full product name on the current technical overview: *Signal Studio for GSM/EDGE/Evo*.
- **Product name:** Signal Studio for GSM/EDGE/Evo (GSM, EDGE and EDGE Evolution / EGPRS2)
- **Host instrument(s):** Agilent/Keysight E4438C ESG (listed as a compatible platform — "ESG" is named in the N7602B technical overview supported-platform list, and N7602B is listed as compatible signal-creation software in the E4438C data sheet). Also supports X-Series MXG/EXG, PSG, first-generation MXG, M9381A PXIe VSG, M9420A PXIe vector transceiver, E6630A wireless test set, PXB baseband generator/channel emulator, M8190A AWG, plus SystemVue and Waveform Creator software.
- **Status:** Discontinued. Keysight directs users to the successor N7602C software (the N7602B trial license is no longer valid; the free trial is offered via N7602EMBC, which requires N7602C). The E4438C ESG itself is a legacy/discontinued instrument.

## 2. Overview
N7602B is PC-based signal-creation software that builds standards-compliant GSM, EDGE and EDGE Evolution (EGPRS2) reference signals for testing base-station (BTS) and mobile-station (MS/UE) components, transmitters and receivers. Signals are compliant to 3GPP Release 7. It operates in a waveform-playback (ARB) mode for component/transmitter test with partially coded, statistically correct signals, and in advanced/real-time modes with full transport-channel coding for receiver BER/BLER/PER/FER analysis. The user interface is parameterized and graphical with tree-style navigation, and waveforms are downloaded to the signal generator for playback.

## 3. Standards & formats supported
- 3GPP GSM / GPRS.
- EDGE / EGPRS.
- EDGE Evolution / EGPRS2 (EGPRS2-A and EGPRS2-B), compliant to **3GPP Release 7**.
- Modulation formats: **GMSK, 8-PSK, QPSK, 16QAM, 32QAM** (QPSK/16QAM/32QAM are the higher-order formats introduced with EDGE Evolution/EGPRS2), at both normal and high symbol rates.
- Symbol rates: normal symbol rate plus a **High Symbol Rate (HSR)** format at **325 ksps** (introduced with EDGE Evolution).
- Referenced 3GPP standards (from the overview's standards table): TS 24.008, TS 44.004, TS 44.018, TS 44.060, TS 45.001, TS 45.002, TS 45.003, TS 45.004, TS 45.010, and control-channel spec V6.15.0 (2006-12); EDGE Evolution entries at TS 45.001/45.002/45.003/45.004 Release 7 versions.

## 4. Key capabilities / features
- Waveform-playback (basic) mode for component and transmitter test using partially coded, statistically correct signals.
- Advanced waveform-playback and real-time modes producing fully channel-coded signals for receiver BER/BLER/PER/FER test.
- Transport-channel coding for UE and BTS receiver conformance testing (uplink and downlink).
- Pre-defined signal configurations: single-slot, all-slots, uniform, and mixed-burst waveforms for GSM, EDGE and EDGE Evolution, covering all modulation formats and symbol rates.
- Pre-defined receiver configurations: Advanced GSM/EDGE/EGPRS2-A, Advanced half-rate GSM/EDGE/EGPRS2-A, Advanced EGPRS2-B, and Control channels.
- Mixed-frame configuration mixing GSM, EDGE and EDGE Evolution timeslots in one frame.
- Multi-carrier generation up to **25 carriers**, with adjustable data-bit offset for low cross-carrier correlation and configurable multi-carrier timing, phase offsets and clipping.
- Spectrally correct signals for ORFS, power and spurious testing.
- Long multi-frame waveforms for receiver BER testing (continuous PN9 sequence data).
- MS factory-test-mode signals containing BCH content.
- Graphical analysis views: CCDF, spectrum, time-domain graphs, and a timeslot view for verifying varying power-level slots within a frame.
- Export of encrypted waveform files for sharing between engineers.
- Option R81 (with a 16800/16900/N5343A logic analyzer) enables creation of symbol bits for GSM/EDGE DigRF communication.

## 5. Configurable signal parameters
- **Modulation type per data channel:** GMSK, 8-PSK, QPSK, 16QAM, 32QAM.
- **Symbol rate:** normal and high (HSR 325 ksps).
- **Channel power** per carrier / per timeslot; alternate-amplitude / power-level control bits; adjustable timing-advance command bits.
- **Coded channel types (BTS/receiver test):** TCH/FS, TCH/HS, CS1, CS4, MCS1/5/9 (uplink), MCS1 and MCS4–9 (downlink), MCS 6–8 (UL and DL), E-TCH/F43.2k, TCH/F (9.6k, 4.8k), TCH/WFS (12.65k, 8.85k, 6.60k), TCH/AFS (12.2k, 10.2k, 7.95k, 7.4k, 6.7k, 5.9k, 5.15k, 4.75k), FACCH/F, UAS-11, UBS-6/8/11.
- **Framing / multiframe:** 13, 26, and 52 multiframe structures for BER testing; 51-multiframe support for BCH content synchronization; configuration of 51- or 52-multiframes.
- **Control channels:** FCCH, SCH, BCH, CCCH; or FCCH, SCH, BCCH, CCCH, SDCCH, SACCH. SACCH frame with coded TCH.
- **Carrier configurations:** control-channel bursts with dummy and GSM/EDGE bursts; choice of pre-defined carriers with BCH, TCH and/or packet traffic; user-configurable signal structure.
- **Payload data:** continuous PN9 sequence data for BER testing (PN9/PN15 used in the underlying GSM/EDGE frame coding per the E4438C platform).
- **Impairments/effects observable:** power ramps, power changes, clipping (viewable via CCDF/spectrum/time-domain graphs).
- **Optional AWGN:** calibrated AWGN available (requires an instrument option).

## 6. Candidate requirements for ESG-SignalCreator
- **R-1:** The app SHALL generate GSM, EDGE and EDGE Evolution (EGPRS2-A and EGPRS2-B) reference signals compliant to 3GPP Release 7.
- **R-2:** The app SHALL support per-data-channel modulation selection among GMSK, 8-PSK, QPSK, 16QAM and 32QAM.
- **R-3:** The app SHALL support both normal symbol rate and the High Symbol Rate (HSR) 325 ksps format.
- **R-4:** The app SHALL provide pre-defined configurations for single-slot, all-slots, uniform and mixed-burst waveforms, and for mixed-frame layouts combining GSM, EDGE and EDGE Evolution timeslots.
- **R-5:** The app SHALL support transport-channel coding for the following channel types: TCH/FS, TCH/HS, CS1, CS4, MCS1/5/9 (UL), MCS1 and MCS4–9 (DL), E-TCH/F43.2k, TCH/F, TCH/WFS, TCH/AFS, FACCH/F, UAS-11, UBS-6/8/11.
- **R-6:** The app SHALL support 13-, 26-, 51- and 52-multiframe structures, including 51-multiframe BCH content synchronization for MS factory-test-mode signals.
- **R-7:** The app SHALL support control-channel content configuration (FCCH, SCH, BCH/BCCH, CCCH, SDCCH, SACCH) and SACCH-with-coded-TCH frames.
- **R-8:** The app SHALL support multi-carrier generation of up to 25 carriers with adjustable per-carrier data-bit offset, timing, phase offset and clipping.
- **R-9:** The app SHALL support continuous PN9 payload data for receiver BER testing over long multi-frame waveforms.
- **R-10:** The app SHALL provide CCDF, spectrum, time-domain and timeslot graphical views of the configured waveform.
- **R-11:** The app SHALL allow per-timeslot / per-carrier channel power, alternate-amplitude / power-level control bits, and timing-advance command bits.
- **R-12:** The app SHALL download the generated waveform to the E4438C ESG for ARB playback, and SHOULD support export of the waveform to a portable (optionally encrypted) file.
- **R-13:** The app SHOULD provide a real-time / advanced mode producing fully channel-coded signals for receiver BER/BLER/PER/FER analysis (dependent on instrument capability).

## 7. Dependencies, licensing & notes
- **Instrument hardware:** ARB/waveform playback on the E4438C requires an internal baseband generator option (Option 601 or 602; the E4438C data sheet marks Signal Studio software items as requiring Option 601/602, and some functions require Option 001/002/601/602). ARB memory capacity limits waveform length.
- **Related ESG-native option:** The E4438C also offers Option 402 "TDMA (GSM, GPRS, EDGE, EGPRS, DADC, PCD, PHS, TETRA, DECT)", an internal real-time TDMA personality. Its data-sheet detail describes the underlying GSM/EDGE domain used by these signals: full-rate speech TCH/FS with CS-1..CS-4 coding, PN9/PN15 data, 26-frame multiframe (ETSI GSM 05.01), and EDGE/EGPRS coding of MCS-1/MCS-5/MCS-9 and E-TCH/F43.2 over a 52-frame multiframe (max 4 timeslots with coded EDGE/EGPRS data). Bit-error-rate analysis requires Option UN7 (BER analyzer).
- **Licensing:** N7602B required a license (right-to-use). It is discontinued; the trial license is no longer valid and Keysight points users to N7602C / N7602EMBC. For a modern reimplementation, licensing is a policy decision, not a technical constraint.
- **External IP / trademarks:** GSM/EDGE/EGPRS standards are 3GPP/ETSI specifications. Waveform files could be exported in encrypted form in the original product.
- **DigRF option:** Option R81 (plus a supported logic analyzer) was needed for GSM/EDGE DigRF symbol-bit creation — likely out of scope for an ESG RF-focused reimplementation.

## 8. References
- *Signal Studio for GSM/EDGE/Evo N7602B — Technical Overview* — literature no. **5990-8737EN** — https://www.keysight.com/us/en/assets/7018-03085/technical-overviews/5990-8737.pdf (also mirrored at https://assets-us-01.kc-usercontent.com/ecb176a6-5a2e-0000-8943-84491e5fc8d1/d472294e-d65b-4caa-bb6f-f1a9f3cf4ba9/5990-8737EN.pdf). Primary source for modulation formats, coded channel types, multiframe/control-channel config, 25-carrier support, standards table, and supported platforms. (Publication/date code not separately printed beyond the 5990-8737EN number; standards table entries run to 2010-04.)
- *Agilent E4438C ESG Vector Signal Generator — Data Sheet* — literature no. **5988-4039EN** — https://www.keysight.com/us/en/assets/7018-01039/data-sheets-archived/5988-4039.pdf. Confirms N7602B is listed as compatible PC signal-creation software; source for Option 402 TDMA real-time GSM/EDGE framing/coding detail (CS-1..4, MCS-1/5/9, 26/52 multiframe, PN9/PN15), Option 601/602 baseband-generator dependency, and Option UN7 BER analyzer.
- *N7602B Signal Studio Software for GSM/EDGE/Evo* (product/download page) — https://www.keysight.com/us/en/lib/software-detail/instrument-firmware-software/n7602b-signal-studio-software-for-gsmedgeevo-2207881.html. Confirms discontinued status and N7602C/N7602EMBC successor.
- *Signal Studio for GSM/EDGE/Evo N7602C — Technical Overview* — literature no. **5992-2779EN** — https://www.keysight.com/us/en/assets/7018-06042/technical-overviews/5992-2779.pdf. Successor product; consulted for continuity (note: the N7602C overview does not list the E4438C ESG among its supported platforms).
- **Not located / not confirmed:** A GSM/EDGE-specific N7602B user guide with exact ARB memory/waveform-length limits and full per-standard symbol-rate/filter tables was not retrieved; those platform limits are inferred from the E4438C data sheet rather than the N7602B software help.

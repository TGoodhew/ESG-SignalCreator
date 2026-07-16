# N7624B — Signal Studio for 3GPP LTE (FDD) — Requirements

> Source category: **PC-based signal creation software**, from the Agilent E4438C ESG Vector Signal Generator Data Sheet (literature no. 5988-4039EN).
> Purpose: capture this product's capabilities as candidate requirements for the ESG-SignalCreator app (a modern reimplementation of Signal Studio for the E4438C).

> 🟡 **Implementation status (v2):** A **3GPP LTE FDD** personality ships in the app
> (`Core/Personalities/Lte/`, on the shared `Dsp/OfdmEngine` + `Fft.Inverse`) with two modes:
> - **Generic** (v1 core) — a downlink OFDM signal with LTE numerology (**15 kHz** spacing, standard
>   FFT/occupied-subcarrier count per **channel bandwidth** 1.4–20 MHz, QPSK/16/64/256QAM) for
>   occupied-bandwidth / PAPR / spectral checks.
> - **Frame-structured** (✅ v2, #188, `LteFrame`) — a proper **E-UTRA downlink radio-frame**: 10 ms
>   frame / 0.5 ms slots / **per-symbol CP** with **normal & extended CP** (**R-2**), and correctly-positioned
>   **PSS** (Zadoff-Chu), **SSS** (interleaved m-sequences), and **CRS** (antenna port 0) plus a **PDSCH**
>   data fill — driven by the physical cell ID (**R-6 core**). PSS/SSS/CRS sequences follow 3GPP TS 36.211.
>
> Still a representative frame, not fully conformant: single antenna port, no PBCH/PDCCH/PCFICH/PHICH
> payloads or channel coding, no PDSCH scrambling, symmetric DC-nulled layout. **Still deferred** (#188):
> E-TM/FRC wizards (R-4/R-5), uplink physical channels (R-7), MIMO (R-8/R-9), HARQ/fully-coded frames
> (R-10/R-11), real-time (R-12), carrier aggregation (R-13), and impairments (R-14). Hardware verification
> is tracked in the verification epic (#157).

## 1. Product identity
- **Model / option number:** N7624B; E4438C ESG connectivity provided by option **N7624B-1FP** ("Connect to E4438C ESG signal generator").
- **Product name:** Signal Studio for LTE / LTE-Advanced / LTE-Advanced Pro **FDD** (originally "Signal Studio for 3GPP LTE FDD").
- **Host instrument(s):** Agilent/Keysight E4438C ESG (legacy host, via -1FP); later platforms: MXG/EXG X-Series, first-gen MXG, M9381A PXIe VSG, EXT/E6607 test set, N5106A PXB, M9420A VXT, M8190A AWG, SystemVue.
- **Status:** **Obsolete** on Keysight's site (succeeded by PathWave Signal Generation). Listed in the E4438C data sheet (5988-4039EN).

## 2. Overview
Signal Studio for LTE FDD is a Windows PC application that creates 3GPP LTE / LTE-Advanced / LTE-Advanced Pro **FDD** downlink and uplink waveforms and downloads them to a signal generator. Its tree-style, parameterized interface builds Keysight-validated reference signals for component, transmitter, and receiver test. It offers basic waveform-playback modes (partially coded, spectrally correct signals), advanced fully-coded playback (up to 1024 frames) for BER/BLER/PER/FER receiver test, and a real-time mode for closed-loop HARQ and timing-adjustment testing. Note: the full LTE-Advanced/Pro feature set (256QAM, 8×8 MIMO, carrier aggregation) targets newer platforms; the E4438C ESG is the legacy host with narrower practical bandwidth/ARB.

## 3. Standards & formats supported
- 3GPP LTE / LTE-Advanced / LTE-Advanced Pro **FDD** duplex.
- 3GPP releases: **Rel-9, Rel-10, Rel-11, Rel-12** (feature summary tables in 5990-6086EN cite these), with LTE-Advanced Pro features (incl. NB-IoT / eMTC) added for Rel-13 in the product line.
- 3GPP specs referenced: TS 36.141, 36.211, 36.212, 36.213, 36.306, 36.321, 36.331, 36.521-1, 36.423; multi-standard radio per TS 37.141 Rel-9.
- E-UTRA test models (E-TM) for downlink; fixed reference channels (FRC) / reference measurement channels (RMC) for uplink.
- Multi-standard radio (MSR): import W-CDMA/HSPA, GSM/EDGE, cdma2000/1xEV-DO, TD-SCDMA, LTE TDD/FDD, WLAN waveforms from other Signal Studio products.

## 4. Key capabilities / features
- **Basic (playback) mode:** spectrally correct signals for ACLR, channel power, spectral mask, spurious, occupied bandwidth, CCDF, EVM; configurable transmission bandwidth, cyclic prefix, and modulation type.
- **Advanced (playback) mode:** fully channel-coded signals, up to **1024-frame** waveform length, for BER/BLER/PER/FER receiver test; transport-channel coding for eNB and UE verification.
- **Real-time mode:** non-repeating/dynamic signals with direct instrument connection; **closed-loop HARQ** and **timing-adjustment (TA)** feedback.
- Predefined **E-TM** wizard (1.1/1.2/2/2a/3.1/3.1a/3.2/3.3) and **FRC** wizards (downlink and uplink).
- **Multi-carrier / multi-cell / multi-user** waveform configuration; multi-standard radio (MSR) waveform import (waveform library manager, Option JFP).
- **Multi-UE simulation** (Option LFP): single ARB waveform simulating up to 100 UEs (RNTI, modulation, RB allocation per UE) for eNB capacity test; Excel import/export.
- Calibrated AWGN; static multipath fading; LUT-based digital pre-distortion (DPD); envelope tracking (Option KFP).
- Code-domain, CCDF (with peak-power position), spectrum, time-domain, and power-envelope graphs.
- Short-length (slot-based) waveforms for fast PA test; VoLTE (TTI bundling with enhanced HARQ, FRC A11-1).

## 5. Configurable signal parameters
- **Channel bandwidths:** 1.4, 3, 5, 10, 15, 20 MHz (E-UTRA transmission bandwidths; performance tables characterize 5/10/20 MHz).
- **Subcarrier spacing:** 15 kHz (LTE); **cyclic prefix** normal and extended.
- **Modulation:** BPSK, QPSK, 16QAM, 64QAM, and **256QAM** (PDSCH/PMCH 256QAM in later releases).
- **Downlink physical channels & signals:** PDSCH, PDCCH, PBCH, PHICH, PCFICH, reference signals (CRS, CSI-RS, DM-RS), PRS (positioning), MCH/PMCH with MBSFN RS; DCI formats 1/1A/1B/1C/2/2A/2B/2C/2D/3/3A/4; DL FRC wizard; HARQ processing for DL-SCH.
- **Downlink MIMO / transmission modes:** transmission modes 1–10; transmit diversity and spatial multiplexing; **up to 8×8 MIMO**; antenna/beam configuration; CDD.
- **Uplink physical channels:** PUSCH, PUCCH (formats 0/1/1a/1b/2/2a/2b/3, and 1b with channel selection), PRACH, sounding reference signals (SRS, incl. frequency hopping), clustered SC-FDMA, simultaneous PUSCH/PUCCH, UCI multiplexing on PUSCH.
- **Uplink MIMO:** up to **4×4** uplink MIMO; **2×2 uplink MIMO with closed-loop HARQ** in real-time.
- **Carrier aggregation:** up to 5 component carriers; inter-band CA with cross-carrier scheduling; **FDD-TDD carrier aggregation** for downlink (Option VFP).
- **HARQ:** up to 15 simulated retransmissions with user-definable RV index sequence; closed-loop HARQ + TA in real-time.
- **Waveform / ARB:** waveform-playback (basic/advanced) and real-time modes; up to 1024 frames; short/slot-length waveforms; virtual cell ID for UL CoMP.
- **Impairments:** calibrated AWGN, static multipath fading, LUT-based DPD, envelope tracking.

## 6. Candidate requirements for ESG-SignalCreator
- **R-1:** The app shall generate 3GPP LTE **FDD** downlink and uplink waveforms and download them to the E4438C ESG.
- **R-2:** The app shall support channel bandwidths 1.4, 3, 5, 10, 15, and 20 MHz with 15 kHz subcarrier spacing and normal/extended cyclic prefix.
- **R-3:** The app shall support data-channel modulation of QPSK, 16QAM, 64QAM (and 256QAM where the target release/host allows).
- **R-4:** The app shall provide predefined E-UTRA test models (E-TM 1.1/1.2/2/2a/3.1/3.1a/3.2/3.3) via a wizard.
- **R-5:** The app shall provide predefined uplink fixed reference channels (FRC) and downlink FRC via wizards.
- **R-6:** The app shall build downlink physical channels/signals (PDSCH, PDCCH, PBCH, PCFICH, PHICH, CRS/CSI-RS/DM-RS) with configurable DCI formats.
- **R-7:** The app shall build uplink physical channels (PUSCH, PUCCH formats 0–3, PRACH, SRS) including UCI multiplexing on PUSCH.
- **R-8:** The app shall support downlink transmission modes 1–10 including transmit diversity and spatial multiplexing up to 8×8 MIMO (subject to host capability).
- **R-9:** The app shall support up to 4×4 uplink MIMO in playback and 2×2 uplink MIMO with closed-loop HARQ in real-time.
- **R-10:** The app shall support HARQ with up to 15 retransmissions and a user-definable RV index sequence.
- **R-11:** The app shall support a fully-coded advanced playback mode of up to 1024 frames for BER/BLER/PER/FER receiver test.
- **R-12:** The app shall support a real-time mode with closed-loop HARQ and timing-adjustment feedback.
- **R-13:** The app shall support carrier aggregation (up to 5 CC, inter-band, cross-carrier scheduling) subject to host bandwidth limits.
- **R-14:** The app shall support calibrated AWGN, static multipath fading, LUT-based DPD, and multi-carrier configuration.
- **R-15:** The app shall provide code-domain, CCDF, spectrum, time-domain, and power-envelope graphs.

## 7. Dependencies, licensing & notes
- **Host hardware:** E4438C requires a baseband generator/ARB option (Option 001/002/601/602). The ESG is the legacy host (via N7624B-1FP); many LTE-Advanced/Pro features (wide CA, 256QAM, 8×8 MIMO, envelope tracking) are practically oriented to newer X-Series / M9381A hardware.
- **Key option structure (FDD):** -1FP connect to E4438C ESG; -HFP Basic LTE FDD; -JFP waveform library manager; -SFP Advanced LTE FDD; -TFP Advanced LTE-Advanced FDD; -WFP real-time R9/R10 UL; -KFP envelope tracking; -LFP multi-UE simulation; -VFP FDD-TDD carrier aggregation. Real-time modes require -WFP; MXG/EXG needs an external fader (or PXB) for channel simulation.
- **Licensing:** flexible right-to-use — fixed (perpetual), transportable/floating, and waveform (up to 545 user-configured waveforms) licenses; 30-day free trial historically; -MEU minor-enhancement-update license for feature updates.
- **Companion tools:** N7649B Test Case Manager for TS 36.141 conformance test setup; N5106A PXB for fading/receiver test.
- **External IP:** Implements 3GPP-specified channel coding and physical-layer procedures; no third-party runtime dependency beyond the application and instrument.

## 8. References
- Signal Studio for LTE/LTE-Advanced FDD/TDD N7624B/N7625B — Technical Overview — literature no. **5990-6086EN** (Feb 2016; detailed feature summary, standards/release tables, MIMO limits, options, licensing) — https://www.keysight.com/us/en/assets/7018-02606/technical-overviews/5990-6086.pdf
- N7624B Signal Studio for LTE / LTE-Advanced / LTE-Advanced Pro FDD [Obsolete] — Keysight product page (releases, 256QAM, MIMO, NB-IoT/eMTC, dynamic TDD/eIMTA, FDD-TDD CA, MSR) — https://www.keysight.com/us/en/product/N7624B/signal-studio-for-lte-lte-advanced-lte-advanced-pro-fdd.html
- Agilent N7624B Signal Studio for 3GPP LTE FDD — earlier Technical Overview (Agilent-era) — https://www.keysight.com/us/en/assets/7018-02967/technical-overviews-archived/5990-7916.pdf
- Agilent E4438C ESG Data Sheet — literature no. 5988-4039EN (lists "N7624B Signal Studio for 3GPP LTE") — https://www.keysight.com/us/en/assets/7018-01039/data-sheets-archived/5988-4039.pdf
- Related literature cited in 5990-6086EN: Move Forward to What's Possible in LTE (brochure 5989-7817EN); Keysight 3GPP LTE (app note 5989-8139EN); Signal Studio Software (brochure 5989-6448EN); 2G/3G to 3.9G/4G BS Receiver Conformance Test (app note 5991-0280EN).
- **Note:** The detailed parameter tables above (bandwidths, MIMO limits, DCI formats, options) are read directly from 5990-6086EN, which is a later "B"-revision covering the whole product line; specific advanced features may exceed the practical playback capability of the legacy E4438C ESG host.

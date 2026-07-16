# N7625B — Signal Studio for 3GPP LTE TDD — Requirements

> Source category: **PC-based signal creation software**, from the Agilent E4438C ESG Vector Signal Generator Data Sheet (literature no. 5988-4039EN).
> Purpose: capture this product's capabilities as candidate requirements for the ESG-SignalCreator app (a modern reimplementation of Signal Studio for the E4438C).

> 🟡 **Implementation status (v2):** A **3GPP LTE TDD** personality ships in the app
> (`Core/Personalities/Lte/LteTddPersonality.cs`), sharing the LTE OFDM numerology and the `LteFrame`
> builder with the FDD personality. Two modes:
> - **Generic** (v1 core) — a downlink OFDM signal at 15 kHz spacing per channel bandwidth (1.4–20 MHz),
>   QPSK/16/64/256QAM, for occupied-bandwidth / PAPR / spectral checks.
> - **Frame-structured** (✅ v2, #189) — a proper **E-UTRA TDD downlink frame** (frame structure type 2):
>   the **D/S/U subframe pattern** of the **uplink-downlink configuration** (0–6), the **special subframe**
>   split into DwPTS / GP / UpPTS by the **special-subframe configuration** (0–9), **TDD-positioned PSS**
>   (DwPTS of subframes 1 & 6) and **SSS** (last symbol of subframes 0 & 5), **CRS**, and a **PDSCH** fill;
>   uplink subframes and the GP/UpPTS are silent. Normal & extended CP (**R-2**). Sequences per 3GPP TS 36.211.
>
> Still a representative frame, not fully conformant (single antenna port, no channel coding/scrambling).
> **Still deferred** (#189): dynamic TDD/eIMTA (R-3), E-TM/FRC wizards (R-6), the full DL physical-channel
> payloads (R-7), uplink physical channels (R-8), MIMO (R-9), HARQ/coded frames (R-10/R-11), carrier
> aggregation (R-12), and impairments (R-13). Hardware verification is tracked in the verification epic (#157).

## 1. Product identity
- **Model / option number:** N7625B; E4438C ESG connectivity provided by option **N7625B-1FP** ("Connect to E4438C ESG signal generator").
- **Product name:** Signal Studio for LTE / LTE-Advanced **TDD** (originally "Signal Studio for 3GPP LTE TDD").
- **Host instrument(s):** Agilent/Keysight E4438C ESG (legacy host, via -1FP); later platforms: MXG/EXG X-Series, first-gen MXG, M9381A PXIe VSG, EXT/E6607 test set, N5106A PXB, M9420A VXT, M8190A AWG, SystemVue.
- **Status:** **Obsolete** / discontinued (trial license no longer valid; succeeded by PathWave Signal Generation). Listed in the E4438C data sheet (5988-4039EN).

## 2. Overview
Signal Studio for LTE TDD is a Windows PC application that creates 3GPP LTE / LTE-Advanced **TDD** downlink and uplink waveforms and downloads them to a signal generator. It shares the N7624B (FDD) architecture and technical overview (5990-6086EN), adding TDD-specific frame configuration (uplink-downlink configurations and special-subframe settings) and dynamic TDD (eIMTA). It supports basic (partially coded) playback, advanced fully-coded playback (up to 1024 frames) for BER/BLER/PER/FER, and real-time mode for closed-loop HARQ and timing-adjustment test. As with N7624B, the full LTE-Advanced feature set targets newer platforms; the E4438C ESG is the legacy host.

## 3. Standards & formats supported
- 3GPP LTE / LTE-Advanced **TDD** duplex; compliant with 3GPP Rel-9 (June/Dec 2010) and Rel-10, and further Rel-11/Rel-12 features per 5990-6086EN.
- 3GPP specs referenced: TS 36.141, 36.211, 36.212, 36.213, 36.306, 36.321, 36.331, 36.521-1, 36.423; MSR per TS 37.141 Rel-9 (incl. BC3 CS3 test models, TS 37.141 Annex A — TDD only).
- E-UTRA test models (E-TM) for downlink; fixed reference channels (FRC) for uplink.
- Multi-standard radio (MSR) waveform import (W-CDMA/HSPA, GSM/EDGE, cdma2000/1xEV-DO, TD-SCDMA, LTE TDD/FDD, WLAN).

## 4. Key capabilities / features
- **Basic (playback) mode:** spectrally correct signals for EVM, ACLR, channel power, spectral mask, CCDF, occupied bandwidth; configurable bandwidth, cyclic prefix, and modulation.
- **Advanced (playback) mode:** fully channel-coded signals up to **1024 frames** for BER/BLER/PER/FER receiver test.
- **Real-time mode:** closed-loop HARQ and timing-adjustment (TA) feedback; dynamic signals with direct instrument connection.
- **TDD-specific:** uplink-downlink subframe configurations; **special-subframe configuration 9 (normal CP) / 7 (extended CP)**; **Dynamic TDD (eIMTA)**; **FDD-TDD carrier aggregation** (Option VFP).
- Predefined **E-TM** wizard (incl. 2a/3.1a 256QAM and BC3 CS3 TDD-only models) and **FRC** wizards.
- Multi-carrier/multi-cell/multi-user; MSR waveform import; **Multi-UE simulation** (up to 100 UEs, Option LFP).
- Calibrated AWGN; static multipath fading; LUT-based DPD; envelope tracking (Option KFP).
- Code-domain, CCDF (with peak-power position), spectrum, time-domain, and power-envelope graphs; short/slot-length waveforms for fast PA test.

## 5. Configurable signal parameters
- **Channel bandwidths:** 1.4, 3, 5, 10, 15, 20 MHz (performance tables characterize 5/10/20 MHz).
- **Subcarrier spacing:** 15 kHz; **cyclic prefix** normal and extended.
- **Modulation:** BPSK, QPSK, 16QAM, 64QAM, and **256QAM** (PDSCH/PMCH 256QAM in later releases).
- **TDD frame:** configurable uplink-downlink configuration; special-subframe configuration (9 for normal CP, 7 for extended CP); dynamic TDD (eIMTA).
- **Downlink physical channels & signals:** PDSCH, PDCCH, PBCH, PCFICH, PHICH, CRS/CSI-RS/DM-RS, PRS, MCH/PMCH with MBSFN RS; DCI formats 1/1A/1B/1C/2/2A/2B/2C/2D/3/3A/4; almost-blank subframe (ABS) for eICIC; DL FRC wizard.
- **Downlink MIMO / transmission modes:** transmission modes 1–10; transmit diversity and spatial multiplexing; **up to 8×8 MIMO**; antenna/beam configuration.
- **Uplink physical channels:** PUSCH, PUCCH (formats 0/1/1a/1b/2/2a/2b/3, and 1b with channel selection), PRACH, SRS (incl. frequency hopping), clustered SC-FDMA, simultaneous PUSCH/PUCCH, UCI multiplexing on PUSCH.
- **Uplink MIMO:** up to **4×4** uplink MIMO; **2×2 uplink MIMO with closed-loop HARQ** in real-time.
- **Carrier aggregation:** up to 5 component carriers; inter-band CA with cross-carrier scheduling; FDD-TDD CA (Option VFP).
- **HARQ:** up to 15 simulated retransmissions with user-definable RV index sequence; closed-loop HARQ + TA in real-time.
- **Waveform / ARB:** basic/advanced playback and real-time modes; up to 1024 frames; short/slot-length waveforms; virtual cell ID for UL CoMP.
- **Impairments:** calibrated AWGN, static multipath fading, LUT-based DPD, envelope tracking.

## 6. Candidate requirements for ESG-SignalCreator
- **R-1:** The app shall generate 3GPP LTE **TDD** downlink and uplink waveforms and download them to the E4438C ESG.
- **R-2:** The app shall support all TDD uplink-downlink subframe configurations and special-subframe configurations (9 normal CP / 7 extended CP).
- **R-3:** The app shall support dynamic TDD (eIMTA).
- **R-4:** The app shall support channel bandwidths 1.4, 3, 5, 10, 15, 20 MHz with 15 kHz subcarrier spacing and normal/extended CP.
- **R-5:** The app shall support QPSK, 16QAM, 64QAM (and 256QAM where the target release/host allows).
- **R-6:** The app shall provide predefined E-TM (incl. TDD-only BC3 CS3 models) and FRC wizards.
- **R-7:** The app shall build downlink physical channels/signals (PDSCH, PDCCH, PBCH, PCFICH, PHICH, CRS/CSI-RS/DM-RS, PRS, MCH/PMCH) with configurable DCI formats and ABS for eICIC.
- **R-8:** The app shall build uplink physical channels (PUSCH, PUCCH formats 0–3, PRACH, SRS) including UCI multiplexing on PUSCH.
- **R-9:** The app shall support downlink transmission modes 1–10 up to 8×8 MIMO (subject to host capability) and up to 4×4 uplink MIMO.
- **R-10:** The app shall support 2×2 uplink MIMO with closed-loop HARQ in real-time and HARQ with up to 15 retransmissions with user-definable RV sequence.
- **R-11:** The app shall support advanced fully-coded playback up to 1024 frames for BER/BLER/PER/FER receiver test.
- **R-12:** The app shall support carrier aggregation (up to 5 CC, inter-band, cross-carrier scheduling) and FDD-TDD CA subject to host limits.
- **R-13:** The app shall support calibrated AWGN, static multipath fading, LUT-based DPD, and multi-carrier configuration.
- **R-14:** The app shall provide code-domain, CCDF, spectrum, time-domain, and power-envelope graphs.

## 7. Dependencies, licensing & notes
- **Host hardware:** E4438C requires a baseband generator/ARB option (Option 001/002/601/602). The ESG is the legacy host (via N7625B-1FP); advanced LTE-Advanced features (wide CA, 256QAM, 8×8 MIMO, envelope tracking) are practically oriented to newer X-Series / M9381A hardware.
- **Key option structure (TDD):** -1FP connect to E4438C ESG; -EFP Basic LTE TDD; -JFP waveform library manager; -QFP Advanced LTE TDD; -TFP Advanced LTE-Advanced TDD; -WFP real-time R9/R10 UL; -KFP envelope tracking; -LFP multi-UE simulation; -VFP FDD-TDD carrier aggregation (only one VFP needed if both N7624B and N7625B are owned). Real-time modes require -WFP.
- **Licensing:** flexible right-to-use — fixed (perpetual), transportable/floating, and waveform (up to 545 user-configured waveforms) licenses; 30-day free trial historically; -MEU minor-enhancement-update license.
- **Companion tools:** N7649B Test Case Manager for TS 36.141 conformance setup; N5106A PXB for fading/receiver test (PXB requires as many upconverter signal generators as eNB receiver antennas for RF output).
- **External IP:** Implements 3GPP-specified channel coding and physical-layer procedures; no third-party runtime dependency beyond the application and instrument.

## 8. References
- Signal Studio for LTE/LTE-Advanced FDD/TDD N7624B/N7625B — Technical Overview — literature no. **5990-6086EN** (Feb 2016; TDD special-subframe configs, eIMTA, BC3 CS3 TDD models, options, licensing) — https://www.keysight.com/us/en/assets/7018-02606/technical-overviews/5990-6086.pdf
- N7625B Signal Studio for LTE / LTE-Advanced TDD [Obsolete] — Keysight product page — https://www.keysight.com/us/en/product/N7625B/signal-studio-for-lte-lte-advanced-tdd.html
- N7625B Signal Studio Software for LTE / LTE-Advanced TDD — Keysight software-detail page (Rel-9/Rel-10 compliance; EVM/ACLR/CCDF; HARQ/BLER; up to 8×8 DL / 4×4 UL MIMO) — https://www.keysight.com/us/en/lib/software-detail/instrument-firmware-software/n7625b-signal-studio-software-for-lte--lteadvanced-tdd-2204105.html
- Agilent Signal Studio for LTE/LTE-Advanced TDD N7625B — Technical Overview — literature no. 5990-6087EN — https://www.keysight.com.cn/cn/zh/assets/7018-07663/technical-overviews/5990-6087.pdf
- Agilent E4438C ESG Data Sheet — literature no. 5988-4039EN (lists "N7625B Signal Studio for 3GPP LTE TDD") — https://www.keysight.com/us/en/assets/7018-01039/data-sheets-archived/5988-4039.pdf
- **Note:** The detailed parameter tables above are read directly from 5990-6086EN (the combined FDD/TDD "B"-revision overview). A dedicated TDD-only overview exists as 5990-6087EN (linked above, not fully extracted). As with N7624B, some advanced features may exceed the practical playback capability of the legacy E4438C ESG host.

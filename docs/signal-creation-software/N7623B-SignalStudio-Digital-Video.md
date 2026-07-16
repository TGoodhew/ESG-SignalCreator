# N7623B — Signal Studio for Digital Video — Requirements

> Source category: **PC-based signal creation software**, from the Agilent E4438C ESG Vector Signal Generator Data Sheet (literature no. 5988-4039EN).
> Purpose: capture this product's capabilities as candidate requirements for the ESG-SignalCreator app (a modern reimplementation of Signal Studio for the E4438C).

> 🟡 **Implementation status (v2):** A **Digital Video (DVB-T COFDM)** personality ships in the app
> (`Core/Personalities/DigitalVideo/`) with two modes:
> - **Generic** (v1 core, `Dsp/OfdmEngine`) — the DVB-T COFDM PHY for an 8 MHz channel (elementary rate
>   64/7 MHz, 2K/8K FFT, selectable guard interval, QPSK…64QAM).
> - **Frame-structured** (✅ v2, #196, `DigitalVideoFrame`) — inserts the standard DVB-T **scattered
>   pilots**: on each symbol the carriers where `k mod 12 == 3·(l mod 4)` carry boosted (4/3) BPSK pilots
>   (the pattern shifts by 3 each symbol), values from the DVB-T reference PRBS (X¹¹+X²+1) — partial
>   **R-1/R-3**. Follows ETSI EN 300 744.
>
> Still representative. **Still deferred** (#196): the continual/TPS carriers, PRBS energy dispersal,
> RS/convolutional coding (R-4), MPEG-TS payload (R-5), BER frames (R-6), impairments (R-7), and the
> **many other digital-video standards** (ISDB-T/Tb, ATSC + ATSC-M/H 8VSB, DVB-C/S/S2 QAM, DTMB/CTTB,
> CMMB, DVB-H/T2 — **R-1**). Hardware verification is tracked in the verification epic (#157).

## 1. Product identity
- **Model / option number:** N7623B
- **Product name:** Signal Studio for Digital Video
- **Host instrument(s):** Agilent/Keysight E4438C ESG; MXG, EXG, PSG vector signal generators; M9381A PXIe VSG; M819xA arbitrary waveform generator; PXB baseband generator/channel emulator; SystemVue
- **Status:** Discontinued / obsolete. Replacement lineage: N7623C, then N7623EMBC PathWave Signal Generation for Digital Video (waveform playback).

## 2. Overview
N7623B Signal Studio for Digital Video creates Keysight-validated, performance-optimized reference waveforms for a broad set of terrestrial, cable, and satellite digital-TV standards. Its parameterized/graphical interface exposes channel-coding and modulation parameters and lets the user drive payload from PN sequences, fixed patterns, user-defined data, or seamless continuous MPEG transport-stream (TS) video files for component and receiver test. With appropriate hardware it adds real-time fading, single-frequency-network (SFN) and MISO simulation, AWGN, and interferers for conformance testing.

## 3. Standards & formats supported
- **DVB (terrestrial/handheld):** DVB-T, DVB-H, DVB-T2
- **DVB (cable/satellite):** DVB-C, DVB-S, DVB-S2
- **ISDB:** ISDB-T, ISDB-Tb, ISDB-Tsb, ISDB-Tmm
- **ATSC:** ATSC, ATSC-M/H
- **China:** DTMB (a.k.a. CTTB), CMMB
- **Cable / ITU-T:** J.83 Annex A/B/C
- **Cable data:** DOCSIS 1.x / 2.0 / 3.0 / 3.1

## 4. Key capabilities / features
- Create validated, performance-optimized reference waveforms compliant with all supported standards.
- Perform BER tests with PN sequence, all-1s, all-0s, or user-defined data patterns.
- Drive payload from a seamless continuous MPEG transport-stream (TS) video file.
- Real-time fading, SFN simulation, MISO simulation, AWGN, and interferers for conformance testing (hardware-dependent).
- Parameterized and graphical signal configuration.
- Easy manipulation of high-level signal parameters (transmission bandwidth, cyclic prefix/guard interval, modulation type) to simplify signal creation.

## 5. Configurable signal parameters
Per-standard parameter families exposed by the software include:
- **OFDM structure (DVB-T/H/T2, ISDB-T family, DTMB, CMMB, DVB-C2 where applicable):** transmission bandwidth; FFT/carrier mode; cyclic prefix / guard interval; carrier configuration.
- **Modulation:** modulation type (e.g. BPSK, QPSK, 16QAM, 64QAM; higher orders where a standard defines them).
- **Channel coding / FEC:** code rate and coding parameters per standard.
- **Payload / transport:** PN sequence, all-1s, all-0s, user-defined pattern, or continuous TS video file.
- **RF / conditioning:** channel power, frequency offsets and power levels, baseband filter options, data-channel settings.
- **Impairments (hardware-dependent):** real-time fading, SFN, MISO, AWGN, and interferers.

Note: exact parameter ranges and enumerations differ per standard; the N7623B online documentation (.chm / help files) provides per-standard pages (e.g. "Carrier for ISDB-T") and should be consulted for authoritative per-standard value sets.

## 6. Candidate requirements for ESG-SignalCreator
- **R-1:** The app SHALL let the user select a digital-video standard from at least: DVB-T, DVB-H, DVB-T2, DVB-C, DVB-S, DVB-S2, ISDB-T, ISDB-Tb, ISDB-Tsb, ISDB-Tmm, ATSC, ATSC-M/H, DTMB (CTTB), CMMB, J.83 Annex A/B/C, and DOCSIS 1.x/2.0/3.0/3.1.
- **R-2:** For OFDM-based standards, the app SHALL allow configuration of transmission bandwidth, FFT/carrier mode, and cyclic prefix / guard interval.
- **R-3:** The app SHALL allow selection of modulation type appropriate to each standard (e.g. QPSK, 16QAM, 64QAM).
- **R-4:** The app SHALL allow selection of FEC/code-rate and channel-coding parameters per standard.
- **R-5:** The app SHALL accept payload from PN sequence, all-1s, all-0s, user-defined pattern, or a seamless continuous MPEG transport-stream (TS) video file.
- **R-6:** The app SHALL support BER test signal generation.
- **R-7:** Where supported by hardware, the app SHALL allow real-time fading, SFN simulation, MISO simulation, AWGN, and interferers.
- **R-8:** The app SHALL expose RF/conditioning controls including channel power, frequency offset, power level, and baseband filter selection.
- **R-9:** The app SHALL present a parameterized, graphical configuration UI with per-standard parameter pages.
- **R-10:** The app SHALL download generated waveforms to the connected E4438C ESG (and, optionally, other supported generators) for playback.

## 7. Dependencies, licensing & notes
- Requires a licensed E4438C ESG (or other supported generator) with a baseband generator and adequate waveform/arb memory to play back the (often long) TS-driven waveforms.
- Real-time impairments (fading, SFN, MISO, AWGN, interferers) require appropriate hardware such as the PXB.
- Node-locked/licensed software; free trial historically offered (now expired for N7623B).
- Many standards involve standard-body/third-party IP (DVB, ISDB/ARIB, ATSC, DTMB, CMMB, DOCSIS); conformance to each standard is the reimplementation's responsibility.

## 8. References
- "N7623B Signal Studio for Digital Video — Technical Overview" — literature no. 5990-9101EN — https://www.keysight.com/us/en/assets/7018-03140/technical-overviews/5990-9101.pdf
- Mirror of the same technical overview (5990-9101EN) — https://docs.ampnuts.ru/eevblog.docs/HP_Agilent_Keysight/5990-9101EN%20N7623B%20Signal%20Studio%20for%20Digital%20Video%20-%20Technical%20Overview%20c20140722%20[12].pdf
- "N7623B Signal Studio Software for Digital Video" (product/software page) — https://www.keysight.com/us/en/lib/software-detail/instrument-firmware-software/n7623b-signal-studio-software-for-digital-video-2207780.html
- "N7623B Signal Studio for Digital Video Online Documentation (.chm)" — https://www.keysight.com/us/en/lib/resources/help-files/n7623b-signal-studio-for-digital-video-online-documentation-chm-file-1242926.html
- "Carrier for ISDB-T" (per-standard help page example) — https://helpfiles.keysight.com/csg/n7623b/Content/Main/carrier_for_isdb-t.htm
- Note: The per-standard parameter enumerations in Section 5 are summarized from the technical overview and general OFDM/standard knowledge; authoritative per-standard value sets should be taken from the N7623B .chm/help files before implementation.

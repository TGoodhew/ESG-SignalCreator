# E4438C-419 — Signal Studio for 3GPP W-CDMA HSPA — Requirements

> Source category: **PC-based signal creation software**, from the Agilent E4438C ESG Vector Signal Generator Data Sheet (literature no. 5988-4039EN).
> Purpose: capture this product's capabilities as candidate requirements for the ESG-SignalCreator app (a modern reimplementation of Signal Studio for the E4438C).

> 🟡 **Implementation status (v1 core):** A **3GPP W-CDMA HSPA** personality now ships in the app
> (`Core/Personalities/Hspa/`, on the shared `Dsp/DsssEngine`). It generates a single-code HS-PDSCH-style
> signal defaulting to **16QAM** on an **SF-16** OVSF code, complex-scrambled and RRC-shaped at
> 3.84 Mcps — capturing the defining HSPA feature (higher-order modulation on the high-speed shared
> channel). **Simplified v1, not a standards-compliant HSDPA/HSUPA link.** Deferred: HS-SCCH/HS-DPCCH
> signalling, E-DCH (HSUPA) channels, H-ARQ, TTI/frame structure, CQI and rate control, 64QAM release
> features. Hardware verification is tracked in the epic.

## 1. Product identity
- **Model / option number:** E4438C-419 (license-key option; upgrade order number E4438CK-419)
- **Product name:** Signal Studio for 3GPP W-CDMA HSPA (HSDPA/HSUPA)
- **Host instrument(s):** Agilent E4438C ESG Vector Signal Generator. Requires ESG firmware later than C.04.60. The successor software for later platforms is N7615B (Signal Studio for 3GPP W-CDMA HSPA) / N7600-series.
- **Status:** Discontinued/legacy (Agilent, now Keysight). The final released Signal Studio build for this option is version 3.3.0.0 (released 1 September 2007); the installer/license remained available for download on keysight.com as of this research.

## 2. Overview
The E4438C-419 is a PC-based Signal Studio application that configures High-Speed Packet Access (HSPA) signals and drives the E4438C ESG in real time over LAN or GPIB. It generates real-time, W-CDMA–based HSDPA and HSUPA physical- and transport-layer coded signals for evaluating base-station and UE receiver functionality against the 3GPP standard. It is aimed primarily at component-level test but can also be used for system-level testing of mobile and base-station equipment. HSPA layers on top of the base 3GPP W-CDMA FDD capability (option 403 covers the base W-CDMA FDD personality).

## 3. Standards & formats supported
- 3GPP W-CDMA FDD (Release 99 baseline framing/spreading), extended for:
- HSDPA (High-Speed Downlink Packet Access) — 3GPP Release 5.
- HSUPA (High-Speed Uplink Packet Access, a.k.a. Enhanced Uplink / E-DCH) — 3GPP Release 6, targeting uplink data rates up to 5.76 Mbit/s with reduced latency.
- Real-time physical-layer and transport-layer channel coding per the 3GPP specifications current at the software's release.

## 4. Key capabilities / features
- Real-time generation of coded HSDPA and HSUPA signals (not merely arb-file playback), enabling closed-loop / continuous receiver test scenarios.
- PC-hosted graphical configuration with remote control of the ESG in real time via LAN or GPIB.
- HSDPA downlink physical channels including HS-SCCH (shared control channel) and HS-PDSCH (high-speed physical downlink shared channel).
- HSUPA (E-DCH) downlink control channels: E-AGCH (absolute grant), E-RGCH (relative grant), and E-HICH (HARQ indicator).
- HSUPA uplink channels: E-DPCCH (control) and E-DPDCH (data), plus HS-DPCCH (uplink control feedback for HSDPA).
- Transport-layer coding chain (CRC attachment, turbo/convolutional coding, rate matching, interleaving, HARQ handling) applied in real time.
- Supports adding the base W-CDMA FDD dedicated/common channels (e.g. DPCH, P-CCPCH, PICH) as the carrier framework for the HSPA channels.

## 5. Configurable signal parameters
- HSDPA modulation scheme: QPSK and 16QAM on HS-PDSCH.
- HSUPA / uplink modulation: BPSK/QPSK-based DPCCH/DPDCH and E-DPCCH/E-DPDCH per 3GPP.
- Channelization / spreading (OVSF) codes and spreading factors for the HS-PDSCH and E-DPDCH channels.
- Number of HS-PDSCH codes (multicode) and code allocation.
- Transport block size / coding rate and HARQ (H-ARQ) process configuration for HS-DSCH / E-DCH.
- HS-DPCCH feedback fields (CQI, ACK/NACK) and timing.
- E-AGCH / E-RGCH / E-HICH grant and HARQ-indicator settings.
- Channel power levels / relative power for each physical channel.
- Scrambling code selection, frame/slot timing and TTI (2 ms / 10 ms) settings.
- RF carrier frequency and output power (via the ESG), plus filtering (root-raised-cosine) per W-CDMA.

## 6. Candidate requirements for ESG-SignalCreator
- **R-1:** The app SHALL provide a 3GPP W-CDMA HSPA signal-configuration mode covering both HSDPA (downlink) and HSUPA (uplink, E-DCH).
- **R-2:** The app SHALL support HSDPA downlink physical channels HS-SCCH and HS-PDSCH, with configurable number of HS-PDSCH multicodes.
- **R-3:** The app SHALL support selectable HS-PDSCH modulation of QPSK and 16QAM.
- **R-4:** The app SHALL support the HSUPA E-DCH channel set: uplink E-DPCCH and E-DPDCH, uplink feedback HS-DPCCH, and downlink control channels E-AGCH, E-RGCH, and E-HICH.
- **R-5:** The app SHALL apply 3GPP transport-layer coding (CRC, channel coding, rate matching, interleaving, HARQ) to HS-DSCH and E-DCH so generated signals are physical- and transport-layer conformant.
- **R-6:** The app SHALL let the user configure per-channel power, OVSF/channelization codes, spreading factors, scrambling code, and frame/slot/TTI timing.
- **R-7:** The app SHALL configure HS-DPCCH feedback (CQI, ACK/NACK) and HSUPA grant/HARQ-indicator parameters.
- **R-8:** The app SHALL layer HSPA channels onto a configurable base W-CDMA FDD carrier (dedicated and common channels such as DPCH, P-CCPCH, PICH).
- **R-9:** The app SHALL target the 3GPP releases relevant to HSPA (Release 5 for HSDPA, Release 6 for HSUPA) and clearly indicate the release/specification version applied.
- **R-10:** The app SHALL support real-time generation and control of the signal source, equivalent to the original real-time LAN/GPIB control model.
- **R-11:** Where the original required baseband generator hardware (option 601/602), the reimplementation SHALL document the equivalent baseband-memory / real-time-coding capability it depends on.

## 7. Dependencies, licensing & notes
- **Prerequisite hardware option:** Requires E4438C internal baseband generator option **601** (8 MSa memory) **or 602** (64 MSa memory), each with digital-bus capability. Option 419 does not function without one of these baseband generators.
- **Base personality:** HSPA builds on W-CDMA FDD; the base 3GPP W-CDMA FDD personality is option **403** (E4438C-403). Confirm whether 403 is required as a co-requisite in the reimplementation's requirement set.
- **Firmware:** ESG firmware must be later than C.04.60.
- **Licensing:** Delivered as a license-key option (E4438CK-419 for field upgrade); requires activation license installed on the E4438C.
- **Control interfaces:** LAN or GPIB, real-time.
- **External IP / standards note:** Signals conform to 3GPP TS specifications (W-CDMA/HSDPA/HSUPA). Any reimplementation must reference the applicable 3GPP release documents for exact coding/framing; 3GPP specifications are the authoritative source.

## 8. References
- E4438C-419 Signal Studio for 3GPP W-CDMA HSPA — Keysight software-detail page (version 3.3.0.0, firmware/host and download info) — https://www.keysight.com/us/en/lib/software-detail/computer-software/e4438c419-signal-studio-for-3gpp-wcdma-hspa-2216830.html
- Agilent E4428C and E4438C ESG Signal Generators Configuration Guide (confirms option E4438C-419 "Signal Studio for 3GPP W-CDMA HSPA", license key, requires 601 or 602) — https://assets.testequity.com/te1/Documents/pdf/E4428C-E4438C-config.pdf
- Agilent E4438C ESG Vector Signal Generator Data Sheet — literature no. 5988-4039EN — https://www.keysight.com/us/en/assets/7018-01039/data-sheets-archived/5988-4039.pdf (option list confirms; note: this PDF did not extract cleanly as text during research, so per-channel HSPA specifics below were sourced from the pages above rather than from this data sheet directly)
- Channel-set details (HSDPA: HS-SCCH, HS-PDSCH; HSUPA/E-DCH downlink: E-AGCH, E-RGCH, E-HICH; uplink: HS-DPCCH, E-DPCCH, E-DPDCH; Release 6 HSUPA 5.76 Mbit/s) sourced from the Keysight product page and corroborated against the related Agilent N7600A "Signal Studio for 3GPP W-CDMA with HSDPA/HSUPA" datasheet (literature no. 5989-3802EN).
- **Not retrieved:** The N7600A datasheet PDF (5989-3802EN, rfmw.em.keysight.com/wireless/helpfiles/n7600a/5989-3802en.pdf) and the option-419 release-notes help page (rfmw.em.keysight.com/wireless/helpfiles/opt419/release_notes_june.htm) both returned HTTP 403 during research and could not be read directly; the closest verifiable specifics came from search-result summaries of those pages. Exact modulation-per-channel tables and full test-model lists could not be independently confirmed from primary sources and should be validated against the 3GPP TS documents and Keysight documentation before implementation.

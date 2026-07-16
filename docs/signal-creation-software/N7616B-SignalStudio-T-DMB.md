# N7616B — Signal Studio for T-DMB — Requirements

> Source category: **PC-based signal creation software**, from the Agilent E4438C ESG Vector Signal Generator Data Sheet (literature no. 5988-4039EN).
> Purpose: capture this product's capabilities as candidate requirements for the ESG-SignalCreator app (a modern reimplementation of Signal Studio for the E4438C).

> 🟡 **Implementation status (v2):** A **T-DMB (DAB COFDM)** personality ships in the app
> (`Core/Personalities/Tdmb/`) with two modes:
> - **Generic** (v1 core, `Dsp/OfdmEngine`) — the DAB COFDM signal underlying T-DMB (2.048 MHz across all
>   four transmission **modes** I/II/III/IV), DQPSK approximated by plain QPSK.
> - **Frame-structured** (✅ v2, #195, `TdmbFrame`) — a DAB transmission frame: the **synchronisation
>   channel** (a **null symbol** + a **phase-reference symbol**) — partial **R-3** — followed by
>   **differentially-encoded DQPSK** data symbols — partial **R-1**. Follows the DAB frame structure
>   (ETSI EN 300 401).
>
> Still representative. **Still deferred** (#195): the exact phase-reference table, SI/FIC/TII content
> (R-3), the FIC/MSC multiplex configuration (R-4), payload sources (R-5), long BER frames (R-6), the
> emission-suppression filter (R-7), impairments (R-9), and convolutional coding. Hardware verification
> is tracked in the verification epic (#157).

## 1. Product identity
- **Model / option number:** N7616B (successor to N7616A)
- **Product name:** Signal Studio for T-DMB (Terrestrial Digital Multimedia Broadcasting)
- **Host instrument(s):** Agilent/Keysight E4438C ESG; N5182A MXG; N5162A MXG vector signal generators
- **Status:** Discontinued / obsolete. Replacement lineage: N7611EMBC PathWave Signal Generation for Broadcast Radio (broadcast-radio family that now covers T-DMB).

## 2. Overview
N7616B Signal Studio for T-DMB creates standards-based T-DMB waveforms through an intuitive graphical interface that exposes transmission, channel-coding, and signal parameters for T-DMB video, audio, and packet-data services. Together with a vector signal generator it provides a cost-effective, reliable way to generate test signals for designing and verifying T-DMB receivers and components. It produces fully coded, spectrum-compliant signals and supports frame-based generation for BER testing.

## 3. Standards & formats supported
- T-DMB (Terrestrial Digital Multimedia Broadcasting), which is built on the Eureka-147 DAB physical layer.
- All four DAB/T-DMB transmission modes: Mode I, II, III, and IV.
- Service types: video, audio, and packet-data services.

## 4. Key capabilities / features
- Generates fully coded, spectrum-compliant T-DMB signals with a filter to suppress out-of-channel emission.
- Full implementation of SI (Service Information), FIC (Fast Information Channel), and TII (Transmitter Identification Information) for video, audio, and packet-data services.
- Supports all four transmission modes (I–IV).
- Flexible multiplex-configuration parameter setup.
- Multiple data-source inputs: PN sequence, fixed pattern, or user files.
- Up to 64 MSa of frame generation for BER test.
- Real-time AWGN; IQ impairments; real-time fading via Baseband Studio.
- Frequency, amplitude, and ALC control; waveform scaling, triggers, and markers.
- Display graphs: I(t), Q(t), I(t)+Q(t), P(t), spectrum, CCDF, and frame-structure views.
- SCPI automation over LAN and GPIB.

## 5. Configurable signal parameters
- **Transmission:** transmission mode (I / II / III / IV); frequency; amplitude; ALC on/off.
- **Multiplex / framing:** multiplex configuration information; SI, FIC, and TII content; service and service-component definitions for video/audio/packet-data.
- **Channel coding:** T-DMB channel-coding parameters exposed per service.
- **Payload / data source:** PN sequence, fixed pattern, or user file; up to 64 MSa frame length for BER.
- **Signal conditioning:** out-of-channel emission suppression filter; waveform scaling; markers/triggers.
- **Impairments:** real-time AWGN; IQ impairments; real-time fading (via Baseband Studio hardware).

## 6. Candidate requirements for ESG-SignalCreator
- **R-1:** The app SHALL generate fully coded, spectrum-compliant T-DMB waveforms for video, audio, and packet-data services.
- **R-2:** The app SHALL support all four T-DMB/DAB transmission modes (I, II, III, IV).
- **R-3:** The app SHALL allow configuration of SI, FIC, and TII content.
- **R-4:** The app SHALL allow flexible multiplex-configuration parameter setup with definable services and service components.
- **R-5:** The app SHALL accept payload data from a PN sequence, fixed pattern, or user file.
- **R-6:** The app SHALL support frame generation up to at least 64 MSa for BER testing.
- **R-7:** The app SHALL apply an out-of-channel emission suppression filter to the generated signal.
- **R-8:** The app SHALL expose instrument controls for frequency, amplitude, ALC, waveform scaling, triggers, and markers.
- **R-9:** Where supported by hardware, the app SHALL allow addition of real-time AWGN, IQ impairments, and real-time fading.
- **R-10:** The app SHALL provide signal-inspection displays including I(t), Q(t), I(t)+Q(t), P(t), spectrum, CCDF, and frame structure.
- **R-11:** The app SHALL be automatable via SCPI over LAN and GPIB.

## 7. Dependencies, licensing & notes
- Requires a licensed E4438C ESG (or N5182A/N5162A MXG) with a baseband generator to play back waveforms.
- Real-time fading requires Baseband Studio hardware; real-time AWGN requires appropriate host support.
- Node-locked/licensed software.
- T-DMB reuses the Eureka-147 DAB transport and adds MPEG-4/H.264 video framing; standard-body IP applies and conformance is the reimplementation's responsibility.

## 8. References
- "N7616B Signal Studio for T-DMB [Obsolete]" (product page) — https://www.keysight.com/us/en/product/N7616B/signal-studio-for-tdmb.html
- "N7616B Signal Studio for T-DMB — Technical Overview" (online help) — https://rfmw.em.keysight.com/wireless/helpfiles/n7616b/technical_overview.htm  — Note: this URL returned HTTP 403 on direct fetch; details above were taken from the Keysight product page and from Keysight search-result summaries of the technical overview.
- "N7616A Signal Studio for T-DMB [Obsolete]" (predecessor product page) — https://www.keysight.com/us/en/product/N7616A/signal-studio-for-tdmb.html
- Note: A dedicated printable technical-overview PDF with a literature number for N7616B was not located during research; the online technical-overview help page is the primary technical source.

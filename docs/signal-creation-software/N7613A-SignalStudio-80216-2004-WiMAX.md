# N7613A — Signal Studio for 802.16-2004 (Fixed WiMAX) — Requirements

> Source category: **PC-based signal creation software**, from the Agilent E4438C ESG Vector Signal Generator Data Sheet (literature no. 5988-4039EN).
> Purpose: capture this product's capabilities as candidate requirements for the ESG-SignalCreator app (a modern reimplementation of Signal Studio for the E4438C).

## 1. Product identity
- **Model / option number:** N7613A (E4438C host connectivity provided via the N7613A ESG option / `-1FP`-style fixed license)
- **Product name:** Signal Studio for 802.16-2004 (WiMAX) — "Fixed WiMAX"
- **Host instrument(s):** Agilent/Keysight E4438C ESG vector signal generator; also supported on PSG and (first-generation) MXG vector signal generators.
- **Status:** Discontinued / **Obsolete** (Keysight lists the product page as Obsolete; no direct replacement). Historically listed in the E4438C data sheet (5988-4039EN).

## 2. Overview
Signal Studio for 802.16-2004 is a Windows PC application that creates IEEE 802.16-2004 ("Fixed WiMAX") single-carrier **256-point OFDM** waveforms and downloads them to an ESG for playback. It produces spectrally correct reference signals for component and transmitter test, and fully channel-coded MAC-layer signals (with automatic FCH/DL-MAP/UL-MAP generation) for receiver test. A graphical, parameterized interface lets the user configure uplink and downlink physical-layer parameters, then calculate and download the resulting ARB waveform.

## 3. Standards & formats supported
- IEEE Std 802.16-2004 (Fixed WiMAX), **OFDM PHY** (WirelessMAN-OFDM), single carrier.
- 256-point FFT OFDM air interface (the mandatory Fixed WiMAX / WiBro-era OFDM mode).
- Both **downlink (DL)** and **uplink (UL)** signal generation.
- Raw (uncoded, user-supplied) data and fully channel-coded (standard-compliant) data.

## 4. Key capabilities / features
- Graphical, tree/parameter-based signal configuration with waveform calculation and download over LAN or GPIB.
- Spectrally correct reference signals for component test (ACLR, channel power, spectral mask, CCDF, EVM).
- Fully coded MAC/PHY signals for receiver test (BER/PER-type measurements).
- Automatic generation of the Frame Control Header (FCH) and broadcast messages: **DL-MAP, UL-MAP, DCD, UCD**.
- MAC PDU creation with headers and CRC.
- Configurable channel coding chain: randomization, Reed-Solomon + convolutional coding (RS-CC), and interleaving.
- Impairments: I/Q impairments; real-time additive white Gaussian noise (AWGN); real-time channel fading when paired with the N5106A PXB baseband generator / channel emulator.
- Visualization graphs: I(t), Q(t), I(t)+Q(t), P(t) power envelope, spectrum, CCDF, and frame-structure view.
- SCPI command automation for programmatic control.

## 5. Configurable signal parameters
- **OFDM structure:** 256-point FFT; 200 used subcarriers = 192 data + 8 pilots; 56 null/guard subcarriers (including DC). Pilots BPSK-modulated.
- **Channel bandwidth:** configurable, per 802.16-2004 (nominal channel bandwidth together with sampling factor `n` determines sample rate). Bandwidth setting exposed in the UI.
- **Cyclic prefix ratio (G):** configurable guard interval (802.16-2004 defines G = 1/4, 1/8, 1/16, 1/32).
- **Sampling factor (n):** configurable oversampling/sampling factor used to derive sample rate from bandwidth.
- **Modulation (data bursts):** BPSK, QPSK, 16-QAM, 64-QAM (adaptive per-burst modulation and coding).
- **FEC / coding:** randomization → Reed-Solomon + convolutional coding → interleaving; selectable coding rate per burst.
- **Frame structure:** frame length; DL preamble (long, 2-symbol) and UL preamble (short, 1-symbol); FCH (1 symbol, BPSK); data bursts; DL and UL sub-frame layout.
- **Data content:** raw data payload or fully coded data; user-defined MAC PDUs (headers + CRC).
- **Impairments:** I/Q impairments, real-time AWGN, real-time fading (with N5106A PXB).

## 6. Candidate requirements for ESG-SignalCreator
- **R-1:** The app shall generate IEEE 802.16-2004 (Fixed WiMAX) 256-point OFDM waveforms for both downlink and uplink.
- **R-2:** The app shall implement the 256-FFT subcarrier map (192 data, 8 pilots, 56 null/guard) with BPSK pilots.
- **R-3:** The app shall let the user select data-burst modulation from BPSK, QPSK, 16-QAM, and 64-QAM.
- **R-4:** The app shall implement the standard FEC chain (randomization, Reed-Solomon + convolutional coding, interleaving) with selectable coding rate per burst.
- **R-5:** The app shall support a configurable cyclic-prefix ratio G ∈ {1/4, 1/8, 1/16, 1/32}.
- **R-6:** The app shall expose configurable channel bandwidth and sampling factor `n`, and derive the resulting sample rate.
- **R-7:** The app shall build a full frame: DL/UL preamble, FCH, and data bursts, with configurable frame length.
- **R-8:** The app shall automatically generate FCH and broadcast messages (DL-MAP, UL-MAP, DCD, UCD) for coded signals.
- **R-9:** The app shall support MAC PDU creation with headers and CRC, and both raw and fully coded data payloads.
- **R-10:** The app shall support I/Q impairments and (host permitting) real-time AWGN.
- **R-11:** The app shall provide waveform visualization: I(t), Q(t), P(t), spectrum, CCDF, and frame structure.
- **R-12:** The app shall calculate an ARB waveform and download it to the E4438C over LAN or GPIB, with SCPI automation.

## 7. Dependencies, licensing & notes
- **Host hardware:** E4438C requires a baseband generator/ARB option (Option 001/002/601/602) to play back downloaded waveforms; the internal baseband generator provides the ARB and real-time I/Q capability.
- **Real-time fading** requires the external N5106A PXB baseband generator and channel emulator.
- **Licensing:** flexible right-to-use licensing (fixed/transportable options historically); ESG connectivity supplied by the ESG-targeted license.
- **External IP:** "WiMAX" is a trademark of the WiMAX Forum; standard is IEEE 802.16-2004. No third-party runtime dependency beyond the Signal Studio application and the instrument.
- **Note:** N7615B (Mobile WiMAX / 802.16e OFDMA) is a **separate** product — see its own file. N7613A is fixed/OFDM only.

## 8. References
- N7613A Signal Studio for Fixed WiMAX [Obsolete] — Keysight product page — https://www.keysight.com/us/en/product/N7613A/signal-studio-for-fixed-wimax.html
- N7613A additional information / documentation index — Keysight — https://helpfiles.keysight.com/csg/N7613A/additional_information.htm
- 802.16 OFDM Overview (256-FFT: 200 used = 192 data + 8 pilots; BPSK/QPSK/16QAM/64QAM; long/short preamble; 1-symbol FCH) — Keysight 89600 VSA help — https://helpfiles.keysight.com/csg/89600B/Webhelp/Subsystems/80216ofdm/content/wimax_overview.htm
- Agilent E4438C ESG Vector Signal Generator Data Sheet — literature no. 5988-4039EN (lists "N7613A Signal Studio for 802.16-2004 (WiMAX)") — https://www.keysight.com/us/en/assets/7018-01039/data-sheets-archived/5988-4039.pdf
- **Not located:** A standalone N7613A technical overview/datasheet with a distinct literature number was not found; the specific enumerated channel-bandwidth list and per-rate FEC tables above are grounded in the 802.16-2004 standard and Keysight OFDM help rather than a dedicated N7613A spec sheet. Cyclic-prefix set {1/4,1/8,1/16,1/32} is from the IEEE 802.16-2004 OFDM PHY, not directly quoted from an N7613A datasheet.

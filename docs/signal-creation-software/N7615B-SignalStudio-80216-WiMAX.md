# N7615B — Signal Studio for 802.16 WiMAX (Mobile WiMAX / 802.16e) — Requirements

> Source category: **PC-based signal creation software**, from the Agilent E4438C ESG Vector Signal Generator Data Sheet (literature no. 5988-4039EN).
> Purpose: capture this product's capabilities as candidate requirements for the ESG-SignalCreator app (a modern reimplementation of Signal Studio for the E4438C).

> 🟡 **Implementation status (v1 core):** An **802.16e Mobile WiMAX (OFDMA)** personality now ships in the
> app (`Core/Personalities/WimaxMobile/`, on the shared `Dsp/OfdmEngine`). It generates a
> scalable-OFDMA-numerology signal (FFT 128/512/1024/2048 at the fixed 10.9375 kHz spacing, selectable CP
> ratio, QPSK…64QAM). **Modelled as plain OFDM — not a standards-compliant 802.16e frame.** Deferred:
> OFDMA subchannel permutation zones (PUSC/FUSC/AMC), preamble, FCH/DL-MAP/UL-MAP, pilots, MIMO
> (Matrix A/B), and CTC/CC coding. Hardware verification is tracked in the epic.

## 1. Product identity
- **Model / option number:** N7615B (E4438C host connectivity provided via the ESG-targeted fixed license)
- **Product name:** Signal Studio for 802.16 WiMAX (Mobile WiMAX / WiBro / 802.16e OFDMA)
- **Host instrument(s):** Agilent/Keysight E4438C ESG (supported from N7615B v1.8.2.0 or later, downloading via GPIB or LAN/TCP-IP); also PSG and MXG X-Series vector signal generators.
- **Status:** **Discontinued**; the trial license is no longer valid. Keysight directs users to N7615EMBC / newer PathWave Signal Generation (N7615C) software. Last N7615B release: **v3.6.0.1 (2014-09-10)**. Historically listed in the E4438C data sheet (5988-4039EN).

## 2. Overview
Signal Studio for 802.16 WiMAX is a Windows PC application that creates **Mobile WiMAX / WiBro (IEEE 802.16e OFDMA)** waveforms and downloads them to an ESG for playback. It builds standards-based downlink and uplink OFDMA frame structures with flexible permutation-zone and burst configuration, produces fully channel-coded signals for receiver design test, and supports MIMO/STC and WiMAX Wave 2 features. It is a distinct product from N7613A (Fixed WiMAX, 256-FFT OFDM).

## 3. Standards & formats supported
- IEEE 802.16e-2005 (Mobile WiMAX, **OFDMA PHY** — WirelessMAN-OFDMA / scalable OFDMA).
- IEEE 802.16 Rev2 features (802.16-2009).
- WiBro (Korean mobile broadband profile) and WiMAX Forum Mobile system profiles.
- WiMAX **Wave 1** and **Wave 2** feature sets (Wave 2 adds MIMO/STC).
- Downlink and uplink OFDMA frame generation; fully coded and (partially coded / statistically correct) signals.

## 4. Key capabilities / features
- Graphical, parameterized OFDMA frame builder with flexible **zone** and **burst** configuration.
- Fully channel-coded downlink and uplink signals for receiver design/verification; partially coded signals for component/transmitter test.
- **MIMO / STC** (WiMAX Wave 2): Matrix A (space-time coding / transmit diversity) and Matrix B (spatial multiplexing); **collaborative spatial multiplexing** for the uplink PUSC zone.
- Supported permutation/subchannelization **zones:** DL-PUSC, DL-FUSC, UL-PUSC, UL-OPUSC, and DL/UL **AMC 2×3** zones.
- Automatic construction of frame control elements (preamble, FCH, DL-MAP/UL-MAP, ranging) for coded frames.
- Impairments and (host-dependent) real-time AWGN; MIMO fading via companion baseband/channel-emulator hardware (e.g. N5106A PXB / N5106A-WMX Mobile WiMAX MIMO bundle).
- Waveform calculation and download to the signal generator over GPIB or LAN.

## 5. Configurable signal parameters
- **OFDMA structure:** scalable FFT (128 / 512 / 1024 / 2048 points) per the 802.16e scalable-OFDMA PHY.
- **Channel bandwidth:** scalable OFDMA bandwidths per 802.16e / WiBro profiles (commonly 5, 7, 8.75, and 10 MHz; the exact selectable set is defined by the loaded system profile). See References note.
- **Permutation zones:** DL-PUSC, DL-FUSC, UL-PUSC, UL-OPUSC, DL/UL AMC 2×3 — with per-zone burst allocation.
- **Modulation (per burst):** QPSK, 16-QAM, 64-QAM (adaptive modulation and coding).
- **Channel coding:** convolutional coding (CC) and convolutional turbo coding (CTC) with selectable rates (standard MCS set).
- **Frame structure:** DL and UL subframes; preamble; FCH; DL-MAP / UL-MAP; ranging region; user-configurable bursts within zones.
- **MIMO / STC:** Matrix A (2×1/2×2 STC), Matrix B (spatial multiplexing), collaborative SM (UL PUSC).
- **Payload / data:** fully coded MAC-layer content or user data payloads for the configured bursts.
- **Impairments:** I/Q impairments; real-time AWGN (host permitting); MIMO channel fading via external hardware.

## 6. Candidate requirements for ESG-SignalCreator
- **R-1:** The app shall generate IEEE 802.16e (Mobile WiMAX / WiBro) OFDMA downlink and uplink waveforms.
- **R-2:** The app shall support scalable OFDMA FFT sizes of 128, 512, 1024, and 2048 points.
- **R-3:** The app shall let the user select channel bandwidth from the values defined by the loaded Mobile WiMAX / WiBro system profile.
- **R-4:** The app shall support configurable permutation zones: DL-PUSC, DL-FUSC, UL-PUSC, UL-OPUSC, and DL/UL AMC 2×3.
- **R-5:** The app shall support per-burst modulation of QPSK, 16-QAM, and 64-QAM with selectable coding (CC and CTC).
- **R-6:** The app shall build a complete OFDMA frame: preamble, FCH, DL-MAP/UL-MAP, ranging region, and zone/burst allocations.
- **R-7:** The app shall support MIMO/STC per WiMAX Wave 2: Matrix A (STC/transmit diversity) and Matrix B (spatial multiplexing).
- **R-8:** The app shall support collaborative spatial multiplexing for the uplink PUSC zone.
- **R-9:** The app shall produce fully channel-coded frames for receiver test and partially coded signals for transmitter/component test.
- **R-10:** The app shall support I/Q impairments and (host permitting) real-time AWGN, and integrate with external hardware for MIMO fading.
- **R-11:** The app shall calculate an ARB waveform and download it to the E4438C over GPIB or LAN.

## 7. Dependencies, licensing & notes
- **Host hardware:** E4438C requires a baseband generator/ARB option (Option 001/002/601/602) for playback; N7615B v1.8.2.0 or later is required for E4438C connectivity.
- **MIMO test** typically requires a second signal generator and/or the N5106A PXB (e.g. N5106A-WMX Mobile WiMAX MIMO application bundle) for channel emulation.
- **Licensing:** flexible right-to-use licensing (fixed/transportable). Trial licenses for N7615B are no longer valid; Keysight recommends N7615EMBC / N7615C for waveform playback going forward.
- **External IP:** WiMAX is a WiMAX Forum trademark; standards are IEEE 802.16e-2005 / 802.16-2009. No third-party runtime dependency beyond the application and instrument.
- **Note:** Keep separate from N7613A (Fixed WiMAX, 802.16-2004 OFDM). This product is OFDMA/mobile.

## 8. References
- N7615B Signal Studio Software for Mobile WiMAX — Keysight software-detail page (status discontinued; v3.6.0.1, 2014-09-10) — https://www.keysight.com/us/en/lib/software-detail/instrument-firmware-software/n7615b-signal-studio-software-for-mobile-wimax-2207799.html
- N7615B Signal Studio for Mobile WiMAX — Online documentation (.chm) index; references "Signal Studio for 802.16 WiMAX N7615B — Technical Overview, 5990-9100EN" (zone types DL-PUSC/DL-FUSC/UL-PUSC/UL-OPUSC, AMC 2×3, Matrix A/B, collaborative SM) — https://www.keysight.com/us/en/lib/resources/help-files/n7615b-signal-studio-for-mobile-wimax-online-documentation-chm-file-856466.html
- Signal Studio for 802.16 WiMAX N7615B — Technical Overview — literature no. 5990-9100EN (asset page; underlying PDF was behind a gate and could not be fully extracted) — https://www.keysight.com/us/en/assets/7018-03139/technical-overviews/5990-9100.pdf
- Agilent Releases Software for 802.16e Mobile WiMAX — EDN — https://www.edn.com/agilent-releases-software-for-802-16e-mobile-wimax/
- Mobile WiMAX instruments add MIMO and Wave 2 test support — EE Times — https://www.eetimes.com/mobile-wimax-instruments-add-mimo-and-wave-2-test-support/
- N5106A-WMX Mobile WiMAX MIMO Software Application Bundle — Keysight — https://www.keysight.com/en/pd-1455268-pn-N5106A/mobile-wimax-mimo-software-application-bundle
- Agilent E4438C ESG Data Sheet — literature no. 5988-4039EN (lists "N7615B Signal Studio for 802.16 WiMAX") — https://www.keysight.com/us/en/assets/7018-01039/data-sheets-archived/5988-4039.pdf
- **Not fully confirmed:** The exact enumerated channel-bandwidth set (5/7/8.75/10 MHz) is the standard Mobile WiMAX/WiBro profile set and was not read directly from the gated N7615B technical overview PDF (5990-9100EN); FFT range 128–2048 is confirmed from search results and the 802.16e scalable-OFDMA PHY. CTC availability is per the 802.16e MCS set; the N7615B datasheet PDF text could not be extracted to confirm the precise coding-rate table.

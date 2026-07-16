# N7611B — Signal Studio for Broadcast Radio — Requirements

> Source category: **PC-based signal creation software**, from the Agilent E4438C ESG Vector Signal Generator Data Sheet (literature no. 5988-4039EN).
> Purpose: capture this product's capabilities as candidate requirements for the ESG-SignalCreator app (a modern reimplementation of Signal Studio for the E4438C).

> 🟡 **Implementation status (v2):** A **Broadcast Radio (FM)** personality ships in the app
> (`Core/Personalities/BroadcastRadio/`). It generates an analog FM broadcast signal — an audio test tone
> (mono), optionally with a 19 kHz stereo pilot + 38 kHz DSB-SC subcarrier, FM-modulated at 75 kHz peak
> deviation.
> - **RDS** (✅ v2, #194): an optional **57 kHz** data subcarrier — a **1187.5 bps** biphase (Manchester)
>   data stream (differentially-encoded PRBS), DSB-SC on 57 kHz (3× the 19 kHz pilot), with configurable
>   RDS deviation — partial **R-2/R-3**.
>
> Still representative. **Still deferred** (#194): real **RDS group content** (EON/TP/TA/PTY/PS/AF/CT/RT,
> R-3), DAB/DAB+/XM in this personality (R-1/R-4 — DAB/DAB+ via the T-DMB personality), multi-carrier (R-5),
> channel-coded generation (R-6), impairments (R-7), pre-emphasis, and SCA subcarriers. Hardware
> verification is tracked in the verification epic (#157).

## 1. Product identity
- **Model / option number:** N7611B
- **Product name:** Signal Studio for Broadcast Radio
- **Host instrument(s):** Agilent/Keysight E4438C ESG; also X-Series MXG/EXG vector signal generators, PXIe M9381A VSG, PXB baseband generator/channel emulator, and SystemVue
- **Status:** Discontinued / obsolete. Replacement lineage is N7611C, then N7611EMBC PathWave Signal Generation for Broadcast Radio.

## 2. Overview
N7611B Signal Studio for Broadcast Radio is a PC application that creates Keysight-validated, performance-optimized reference signals for broadcast radio receiver and component test. It provides a parameterized, tree-style graphical interface for configuring FM stereo/RDS and digital broadcast-radio waveforms, then downloads them to a vector signal generator for playback or fully coded signal generation. It supports both waveform-playback mode (for transmitter/component test) and fully channel-coded signals (for receiver BER/BLER/PER/FER analysis).

## 3. Standards & formats supported
- FM Stereo with RDS (Radio Data System)
- DAB (Digital Audio Broadcasting)
- DAB+
- T-DMB (Terrestrial Digital Multimedia Broadcasting)
- XM (XM Satellite Radio)

## 4. Key capabilities / features
- Create validated, performance-optimized reference signals for each supported standard.
- Independently configure multi-carrier / multi-channel signals for up to 12 carriers.
- Add real-time fading, AWGN, and interferers (with appropriate hardware, e.g. PXB) for conformance/stress testing.
- Parameterized and graphical signal configuration with tree-style navigation.
- Waveform-playback mode for component/transmitter test and fully channel-coded generation for receiver BER/BLER/PER/FER analysis.
- Demonstrated use for Sirius XM radio receiver test (per Keysight demo material).

## 5. Configurable signal parameters
- **FM Stereo / RDS:** FM deviation, pilot deviation, and RDS deviation; RDS information content. RDS/RBDS group functions cited in Keysight material include EON, TP, TA, PTY, PS, AF, CT, and RT.
- **DAB / DAB+ / T-DMB:** transmission mode; service and service-component settings; FIG (Fast Information Group) parameters.
- **XM:** one terrestrial carrier plus two satellite carriers in ensemble A and ensemble B respectively.
- **Multi-carrier:** up to 12 independently configured carriers/channels with per-carrier frequency/power offsets.
- **Impairments (hardware-dependent):** real-time fading, AWGN, interferers.

## 6. Candidate requirements for ESG-SignalCreator
- **R-1:** The app SHALL let the user select a broadcast-radio standard from at least: FM Stereo/RDS, DAB, DAB+, T-DMB, and XM.
- **R-2:** For FM Stereo/RDS, the app SHALL allow independent setting of FM deviation, pilot deviation, and RDS deviation.
- **R-3:** For RDS, the app SHALL allow configuration of RDS group content including EON, TP, TA, PTY, PS, AF, CT, and RT fields.
- **R-4:** For DAB/DAB+/T-DMB, the app SHALL allow selection of transmission mode and configuration of services, service components, and FIG parameters.
- **R-5:** The app SHALL support multi-carrier configuration of up to 12 independently configured carriers/channels.
- **R-6:** The app SHALL support both waveform-playback output and fully channel-coded signal generation suitable for receiver BER/BLER/PER/FER measurement.
- **R-7:** Where supported by target hardware, the app SHALL allow addition of AWGN, fading, and interferers.
- **R-8:** The app SHALL present a parameterized, tree-style navigation UI for signal configuration.
- **R-9:** The app SHALL download the generated waveform to the connected E4438C ESG (and, optionally, other supported generators) for playback.

## 7. Dependencies, licensing & notes
- Requires a licensed E4438C ESG with a baseband generator (arb/real-time) to play back waveforms; feature availability (e.g. fading, AWGN, interferers) depends on host hardware such as the PXB.
- Node-locked/licensed software; free trial license historically offered (now expired for N7611B).
- Multi-standard support (DAB/DAB+/T-DMB, XM) may involve third-party/standard-body IP; conformance to each broadcast standard is the responsibility of the reimplementation.

## 8. References
- "N7611B Signal Studio for Broadcast Radio — Technical Overview" — literature no. 5990-9098EN — https://www.keysight.com/us/en/assets/7018-03137/technical-overviews/5990-9098.pdf
- Mirror of the same technical overview (5990-9098EN) — https://docs.ampnuts.ru/eevblog.docs/HP_Agilent_Keysight/Signal%20Studio%20for%20Broadcast%20Radio%20N7611B%20-%20Technical%20Overview%205990-9098EN%20c20140729%20%5B9%5D.pdf
- "N7611B Signal Studio Software for Broadcast Radio" (product/software page) — https://www.keysight.com/us/en/lib/software-detail/instrument-firmware-software/n7611b-signal-studio-software-for-broadcast-radio-2207805.html
- "Using N7611B Signal Studio Software for Sirius XM Radio" (demo) — https://www.keysight.com/us/en/library/demos/demo/using-n7611b-signal-studio-software-for-sirius-xm-radio.html
- Note: The specific RDS function list (EON/TP/TA/PTY/PS/AF/CT/RT) is drawn from Keysight search-result summaries of the N7611B material; it should be re-verified against the full technical overview PDF before being treated as an exhaustive spec.

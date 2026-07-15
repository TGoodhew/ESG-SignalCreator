# N7622A — Signal Studio Toolkit — Requirements

> Source category: **PC-based signal creation software**, from the Agilent E4438C ESG Vector Signal Generator Data Sheet (literature no. 5988-4039EN).
> Purpose: capture this product's capabilities as candidate requirements for the ESG-SignalCreator app (a modern reimplementation of Signal Studio for the E4438C).

## 1. Product identity
- **Model / option number:** N7622A
- **Product name:** Signal Studio Toolkit (custom I/Q waveform download utility)
- **Host instrument(s):** Agilent/Keysight ESG (E4438C), PSG, and MXG (N5182A) vector signal generators; also PXB baseband generator / channel emulator and EXT wireless communications test set.
- **Status:** Discontinued / obsolete (free utility). Superseded by **N7618APPC PathWave Signal Generation Advanced Waveform Utility (AWU)**.

## 2. Overview
The Signal Studio Toolkit is a **free** PC utility that simplifies downloading and playing back custom I/Q waveforms created in external environments such as MATLAB and C++. It provides a graphical interface that manages the whole download-and-playback process, including automatic translation of the user's I/Q data into the correct file format for the target baseband generator, plus control of the instrument's RF/baseband settings and I/Q adjustments. Unlike the standards-based Signal Studio personalities, the Toolkit does not synthesise a specific signal format — it is the bridge for getting arbitrary user-generated I/Q into the instrument.

## 3. Standards & formats supported
(These are I/Q container formats, not comms standards.)
- MATLAB "MAT File 5" (`.mat`) I/Q data.
- ASCII / text I/Q data.
- Keysight/Agilent 16-bit binary I/Q format.
- Keysight/Agilent 14-bit binary I/Q format.
- (Six import file formats total per Keysight literature; the four above are the named ones.)
- Automatic translation of imported I/Q into the target baseband generator's native waveform format.

## 4. Key capabilities / features
- Import custom I/Q waveforms from common development environments (MATLAB, C++).
- Automatic conversion/translation of I/Q data to the proper file format for the selected target instrument.
- Graphical user interface driving the full waveform download and playback workflow.
- Download waveforms to the instrument's ARB and initiate/manage playback.
- Control instrument settings: RF frequency, amplitude, and ALC settings.
- Apply I/Q impairments and adjustments to the played-back waveform.
- Marker/trigger handling as part of the download (waveform markers carried with the ARB segment). *(See note in section 8 — marker/trigger detail not fully verified.)*
- Automation via COM object, .NET API, and SCPI command set (API help system included).
- Connectivity over LAN and GPIB.

## 5. Configurable signal parameters
- **Source I/Q file format:** MAT File 5, ASCII, Agilent 16-bit, Agilent 14-bit (+ two additional formats).
- **Target instrument:** ESG (E4438C) / PSG / MXG / PXB / EXT.
- **RF frequency** of the host instrument.
- **Amplitude / power level** and **ALC** settings.
- **I/Q impairments / adjustments** (e.g. I/Q gain, offset, quadrature — applied to the downloaded waveform).
- **Sample rate / clock** of the ARB playback (as required to correctly reproduce the imported I/Q).
- **Waveform markers / triggers** associated with the downloaded segment.

## 6. Candidate requirements for ESG-SignalCreator
- **R-1:** The app SHALL import custom I/Q waveform data from at least MATLAB `.mat` (MAT File 5) and ASCII formats.
- **R-2:** The app SHALL import Keysight/Agilent 16-bit and 14-bit binary I/Q formats.
- **R-3:** The app SHALL automatically translate imported I/Q data into the E4438C's native ARB waveform format prior to download.
- **R-4:** The app SHALL download an arbitrary I/Q waveform to the E4438C internal ARB and start playback over LAN or GPIB.
- **R-5:** The app SHALL let the user set host RF frequency, amplitude, and ALC state when playing a downloaded waveform.
- **R-6:** The app SHALL let the user apply I/Q impairments/adjustments (I/Q gain/offset/quadrature) to the downloaded waveform.
- **R-7:** The app SHALL allow the user to define/attach waveform markers and triggers to a downloaded ARB segment.
- **R-8:** The app SHALL let the user specify the playback sample rate/clock for imported I/Q data.
- **R-9:** The app SHOULD provide a graphical, wizard-style workflow covering import → translate → download → play.
- **R-10:** The app SHOULD expose an automation API (COM/.NET or a modern equivalent) mirroring the interactive functions.

## 7. Dependencies, licensing & notes
- **Free** utility (no license charge), but requires a host instrument with an internal baseband generator + ARB (on the E4438C: Option 601/602 dual ARB) to play the downloaded waveform.
- Waveform length/segment count is bounded by the host instrument's ARB memory.
- The user is responsible for generating valid I/Q data externally; the Toolkit does not create signal content itself — this makes it the natural model for a generic "import arbitrary I/Q" feature in ESG-SignalCreator rather than a standards personality.
- Superseded by N7618APPC Advanced Waveform Utility (AWU) — worth reviewing for the modern feature set if extending beyond the classic Toolkit scope.

## 8. References
- N7622A Signal Studio Toolkit [Obsolete] — Keysight product page — https://www.keysight.com/us/en/product/N7622A/signal-studio-toolkit.html
- N7622A Signal Studio Toolkit — System Requirements (online help) — http://rfmw.em.keysight.com/wireless/helpfiles/n7622a/requirements.htm
- Agilent E4438C ESG Vector Signal Generator Data Sheet — literature no. 5988-4039EN — https://www.keysight.com/us/en/assets/7018-01039/data-sheets-archived/5988-4039.pdf
- **Not fully confirmed:** the identity of the two remaining (of six) supported I/Q file formats, and the exact marker/trigger configuration capabilities of the Toolkit, were not read verbatim from Keysight literature and are inferred from the product page description; verify against the N7622A online help before treating as hard specifications. No standalone N7622A datasheet/literature number was located (the utility was distributed as free software rather than a sold option).

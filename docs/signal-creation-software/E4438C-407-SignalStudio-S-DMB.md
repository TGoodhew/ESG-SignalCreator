# E4438C-407 — Signal Studio for S-DMB — Requirements

> Source category: **PC-based signal creation software**, from the Agilent E4438C ESG Vector Signal Generator Data Sheet (literature no. 5988-4039EN).
> Purpose: capture this product's capabilities as candidate requirements for the ESG-SignalCreator app (a modern reimplementation of Signal Studio for the E4438C).

> 🟠 **Implementation status (v1, approximate — air interface still UNVERIFIED):** An **S-DMB (CDM,
> approx.)** personality ships in the app (`Core/Personalities/Sdmb/`, on the shared `Dsp/DsssEngine`).
> It generates a *representative* CDM signal (QPSK spread by an OVSF code, scrambled, RRC-shaped) —
> **explicitly NOT a verified S-DMB waveform.** Chip rate, spreading, FEC, and framing are placeholders.
>
> **v2 confirmation attempt (#197, 2026-07-16):** a web-research pass *confirmed the standard designation*
> — S-DMB uses **ITU-R BO.1130-4 "System E"**, a CDM/CDMA satellite-broadcasting air interface (Korean
> TU Media / Japanese MBCo), S-band downlink ~2170–2200 MHz, ~15–25 MHz bandwidth. But the **exact
> baseband physical-layer parameters (chip rate in Mcps, spreading factor/codes, modulation, channel
> coding, interleaving, frame structure) are held in the paid ITU-R BO.1130-4 and ARIB STD-B41 standards
> and could NOT be recovered from accessible secondary sources.** So the personality is **still not
> implementable as a *verified* S-DMB waveform** — the v1 remains a flagged CDM placeholder, and #197 is
> blocked on obtaining a primary S-DMB specification (a standards procurement, not a coding task).
> Hardware verification is tracked in the epic.

## 1. Product identity
- **Model / option number:** E4438C-407 (instrument option, not an N-series standalone product)
- **Product name:** Signal Studio for S-DMB (Satellite Digital Multimedia Broadcasting)
- **Host instrument(s):** Agilent E4438C ESG Vector Signal Generator
- **Status:** Discontinued / legacy. Last software version 1.0.3.0, released 30 August 2004.

## 2. Overview
E4438C-407 Signal Studio for S-DMB is a PC-based user interface that, working with an E4438C ESG, configures and generates Satellite DMB test signals. S-DMB was a hybrid satellite/terrestrial mobile broadcast system deployed in South Korea (TU Media, a SK Telecom subsidiary, launched May 2005) and Japan (Mobile Broadcasting Corp. "MobaHo!", 2004–2009) using a shared high-power geostationary satellite plus terrestrial gap-filler repeaters. The Keysight material for this option references pilot-channel and CRC-encoder configuration, consistent with a CDMA/code-division-multiplex physical layer.

## 3. Standards & formats supported
- S-DMB (Satellite Digital Multimedia Broadcasting), the CDM-based satellite mobile broadcast system used by TU Media (Korea) / MobaHo! (Japan).
- Operates in the IMT-2000 S band (2170–2200 MHz); the system delivered roughly 18 channels at 128 kbit/s within a ~15 MHz allocation.

Note: Keysight's public E4438C-407 pages do not restate the underlying S-DMB air-interface standard designation. The physical layer is widely documented as a code-division-multiplex (CDMA-family) satellite system; specific standard-body references could not be confirmed from the located Keysight literature — see References.

## 4. Key capabilities / features
- PC-based Signal Studio user interface that downloads/configures S-DMB signals on the E4438C ESG.
- Generates S-DMB test signals for receiver and component design/verification.
- Configurable CRC-encoder behavior (v1.0.3.0 added the ability to invert CRC-encoder output bits for compatibility with different vendor chipsets).
- Configurable pilot-channel data length (v1.0.3.0 extended capacity from 221,184 to 479,232 bits).

## 5. Configurable signal parameters
Confirmed from Keysight release-note material:
- **CRC encoder:** output-bit inversion option (for cross-vendor chipset compatibility).
- **Pilot channel:** pilot-channel data length (up to 479,232 bits in v1.0.3.0).

Inferred/expected for a CDM S-DMB signal (NOT confirmed in located Keysight literature — verify before implementing):
- Code-division channelization (pilot / traffic / control channels), spreading, chip rate, and QPSK-family modulation.
- Frame/interleaving and FEC parameters per the S-DMB physical layer.
- RF frequency/amplitude and standard ESG playback controls.

## 6. Candidate requirements for ESG-SignalCreator
- **R-1:** The app SHALL generate S-DMB (Satellite DMB) test signals suitable for receiver and component test.
- **R-2:** The app SHALL allow configuration of the CRC encoder, including an option to invert CRC-encoder output bits for cross-vendor chipset compatibility.
- **R-3:** The app SHALL allow configuration of pilot-channel data length, supporting at least 479,232 bits.
- **R-4:** The app SHALL expose the S-DMB channel structure (pilot / traffic / control channels) and code-division channelization parameters. *(Requires confirmation of the exact S-DMB air-interface spec.)*
- **R-5:** The app SHALL expose standard ESG playback controls (frequency, amplitude, waveform download).
- **R-6:** The app SHOULD target the IMT-2000 S band (2170–2200 MHz) operating context for default frequency setup.

## 7. Dependencies, licensing & notes
- Delivered as an E4438C instrument option (407); requires a licensed E4438C ESG with a baseband generator (option 601/602) to play back the arbitrary/real-time waveform.
- Requires an E4438C license key installation; node-locked to the instrument.
- Legacy PC host requirements: Windows XP / 2000 / NT.
- S-DMB is an obsolete commercial system (both Korean and Japanese services have been discontinued); underlying air-interface IP may involve third parties and could not be fully characterized from public Keysight literature.

## 8. References
- "E4438C-407 Signal Studio for S-DMB Software" (software-detail page) — literature/asset no. 9018-33255 — https://www.keysight.com/us/en/lib/software-detail/computer-software/e4438c407-signal-studio-for-sdmb-software-2225135.html
- "E4438C Signal Studio for S-DMB Release Notes" — asset no. 9018-19601 — https://www.keysight.com/zz/en/assets/9018-19601/release-notes/9018-19601.htm (page returned navigation-only content on fetch; version/CRC/pilot details above came from the software-detail page summary)
- "Software Installation Guide (Baseband Studio and Signal Studio)" — asset no. 9018-01549
- "Agilent E4438C ESG Vector Signal Generator Data Sheet" — literature no. 5988-4039EN — https://www.keysight.com/us/en/assets/7018-01039/data-sheets-archived/5988-4039.pdf (option 407 listed among Signal Studio options; the fetched PDF was not text-extractable in this session, so option-level detail was not read directly)
- "S-DMB" (Wikipedia) — https://en.wikipedia.org/wiki/S-DMB (S-band allocation 2170–2200 MHz, ~18 channels @128 kbit/s, TU Media / MobaHo! deployment history)
- Note: The detailed S-DMB physical-layer parameters (chip rate, spreading, FEC, modulation, air-interface standard designation) could NOT be confirmed from located Keysight sources and are flagged as unverified in Sections 3 and 5. Do not treat the inferred parameters as authoritative without a primary S-DMB specification.

### v2 air-interface research (#197, 2026-07-16)
A dedicated web-research pass to confirm the air interface before implementing established the **standard designation** but not the **baseband parameters**:
- **Confirmed:** S-DMB is **ITU-R BO.1130-4 "System E"** — a CDM/CDMA satellite sound/multimedia broadcasting air interface; Korean domestic S-DMB (TU Media / SK Telecom, with Japan's MBCo), S-band downlink ~2170–2200 MHz (IMT-2000), ~15–25 MHz bandwidth; the related Japanese standard is **ARIB STD-B41** ("Transmission System for Digital Satellite Sound Broadcasting").
- **NOT confirmable from accessible sources:** the exact chip rate (Mcps), spreading factor / code type, modulation, channel-coding scheme (convolutional vs turbo) and rate, interleaving, and frame structure. These are held in the **paid** ITU-R BO.1130-4 recommendation and ARIB STD-B41 standard; the ITU PDF and academic PDFs located were paywalled, 404, or not text-extractable.
- **Conclusion:** #197 stays **blocked on obtaining a primary S-DMB spec** (ITU-R BO.1130-4 / ARIB STD-B41). Until then the v1 remains a flagged, unverified CDM approximation — no corrected physical layer should be authored, to avoid shipping fabricated parameters.
- Sources: ITU-R BO.1130 recommendation page — https://www.itu.int/rec/R-REC-BO.1130/en ; ARIB STD-B41 overview — https://www.arib.or.jp/english/std_tr/broadcasting/desc/std-b41.html ; S-DMB (Wikipedia) — https://en.wikipedia.org/wiki/S-DMB ; EURASIP IST'05 S-DMB paper — https://www.eurasip.org/Proceedings/Ext/IST05/papers/307.pdf

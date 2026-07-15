# N7620A — Signal Studio for Pulse Building — Requirements

> Source category: **PC-based signal creation software**, from the Agilent E4438C ESG Vector Signal Generator Data Sheet (literature no. 5988-4039EN).
> Purpose: capture this product's capabilities as candidate requirements for the ESG-SignalCreator app (a modern reimplementation of Signal Studio for the E4438C).

## 1. Product identity
- **Model / option number:** N7620A (advanced capability add-ons: Option 205, Option 206)
- **Product name:** Signal Studio for Pulse Building
- **Host instrument(s):** Agilent/Keysight E4438C ESG and E8267D PSG vector signal generators; also drives standalone Arbitrary Waveform Generators (N8240A / N6030A / M9330A, later N824xA / N603xA / M933xA). Waveforms are downloaded to and replayed from the instrument's internal ARB.
- **Status:** Discontinued / obsolete. Superseded by **N7620B PathWave Signal Generation for Pulse Building**.

## 2. Overview
Signal Studio for Pulse Building is PC software for constructing complex, wideband radar-style pulse patterns and downloading them to an ESG/PSG (or wideband AWG) for playback. It targets radar and EW receiver test, letting the user build custom pulse envelopes, apply intra-pulse (on-pulse) modulation, and assemble single-emitter scenarios with per-pulse control of timing, frequency, phase, and power. Pulse patterns can be stored, recalled, and nested inside larger patterns to maximise instrument memory for long scenarios.

## 3. Standards & formats supported
(These are radar/EW signal types, not comms standards.)
- Custom user-defined pulse envelopes (shape/rise/fall control).
- Intra-pulse (on-pulse) modulation formats:
  - Linear FM chirp and non-linear FM chirp
  - FM step (stepped-frequency)
  - AM step (stepped-amplitude)
  - BPSK and QPSK
  - Barker codes
  - Frank codes
  - Polyphase codes
- Pulse trains / pulse patterns with per-pulse parameter variation.
- Pattern nesting (a completed pulse pattern can be nested inside another pattern for playback).
- CSV (spreadsheet) file import/export of pulse/PRI definitions (Option 205/206).

## 4. Key capabilities / features
- Build custom pulse envelopes and import them into the signal generator.
- Apply any of the standard intra-pulse modulation formats (see section 3) or a custom modulation on each pulse.
- Set, on a **pulse-by-pulse** basis: pulse repetition interval, number of repetitions, and frequency, phase, and power offsets.
- Create, store, and recall complex pulse patterns; nest patterns for long scenario playback that maximises ARB memory.
- Generate staggered / jittered PRI: PRI patterns containing jitter components or periodic (staggered) functionality *(Option 205/206)*.
- Antenna scan patterning and antenna radiation (main-beam/scan) patterns applied across the pulse train *(Option 205/206)*.
- Signal impairments applied to the scenario *(Option 205/206)*.
- 16-bit ARB waveform resolution; over 65 dBc spurious-free dynamic range.
- Automation via COM API and SCPI; LAN and GPIB connectivity.

## 5. Configurable signal parameters
- **Carrier frequency:** up to 44 GHz (dependent on host instrument/AWG + upconverter; E4438C ESG covers 250 kHz–6 GHz).
- **Modulation bandwidth:** up to 1 GHz (with wideband AWG; ESG internal ARB is lower).
- **ARB resolution:** 16-bit.
- **Per-pulse parameters:** pulse width, repetition interval (PRI), number of repetitions, frequency offset, phase offset, power offset.
- **Pulse envelope:** custom shape (with rise/fall); standard or imported envelope.
- **Intra-pulse modulation:** chirp (linear/non-linear FM), FM step, AM step, BPSK, QPSK, Barker, Frank, polyphase; chirp deviation and code selection.
- **PRI patterning:** fixed, staggered/periodic, or jittered PRI (Option 205/206).
- **Antenna scan:** scan pattern and antenna radiation pattern parameters (Option 205/206).
- **Import/export:** pulse and PRI tables via CSV (Option 205/206).

## 6. Candidate requirements for ESG-SignalCreator
- **R-1:** The app SHALL let the user define a single pulse with configurable pulse width, rise time, fall time, and envelope shape.
- **R-2:** The app SHALL support assigning an intra-pulse modulation to each pulse from at least: linear FM chirp, non-linear FM chirp, FM step, AM step, BPSK, QPSK, Barker code, Frank code, and polyphase code.
- **R-3:** The app SHALL allow the user to build a pulse train and set, per pulse, the pulse repetition interval, number of repetitions, and frequency, phase, and power offsets.
- **R-4:** The app SHALL support PRI patterning including fixed, staggered/periodic, and jittered PRI.
- **R-5:** The app SHALL support antenna scan / antenna radiation patterning applied across a pulse train.
- **R-6:** The app SHALL allow a completed pulse pattern to be stored, recalled, and nested inside another pattern.
- **R-7:** The app SHALL import and export pulse/PRI definitions via CSV.
- **R-8:** The app SHALL generate 16-bit ARB waveform data suitable for download to the E4438C internal ARB (Option 602/601) over LAN or GPIB.
- **R-9:** The app SHALL expose signal impairments that can be applied to a pulse scenario.
- **R-10:** The app SHOULD provide a graphical preview of the pulse train (timing, envelope) before download.

## 7. Dependencies, licensing & notes
- Requires a host signal generator with internal ARB / baseband generator. On the E4438C this means the internal baseband generator + ARB waveform memory options (e.g. Option 601/602 with dual ARB).
- Base N7620A provides core pulse building; **Option 205 and Option 206** are advanced add-ons that enable PRI patterns, signal impairments, antenna radiation, additional modulation types, scan patterning, and CSV import/export. (The exact split of features between Option 205 and Option 206 was not confirmed in the located literature — see References.)
- Licensed per host instrument (Signal Studio fixed/transportable license model typical of the family); full-band carrier/bandwidth figures (up to 44 GHz / 1 GHz) require a PSG or wideband external AWG, not the ESG alone.
- Radar/EW pulse code definitions (Barker, Frank, polyphase) are standard published sequences — no third-party IP licensing expected for the code definitions themselves.

## 8. References
- Agilent N7620A Signal Studio for Pulse Building — Technical Overview — literature no. 5990-8920EN — https://www.keysight.com/us/en/assets/7018-07469/technical-overviews-archived/5990-8920.pdf *(PDF located and referenced by Keysight search; automated text extraction of this specific PDF failed, so parameter details below were corroborated from the Keysight product pages rather than read verbatim from this PDF.)*
- N7620A Signal Studio for Pulse Building [Obsolete] — Keysight product page — https://www.keysight.com/us/en/product/N7620A/signal-studio-for-pulse-building.html
- N7620A Signal Studio for Pulse Building [Obsolete] — legacy product page — https://www.keysight.com/en/pd-761770-pn-N7620A/signal-studio-for-pulse-building?cc=US&lc=eng
- Agilent E4438C ESG Vector Signal Generator Data Sheet — literature no. 5988-4039EN — https://www.keysight.com/us/en/assets/7018-01039/data-sheets-archived/5988-4039.pdf
- N7620B (successor) Signal Studio for Pulse Building — Technical Overview — literature no. 5991-0779EN — https://www.keysight.com/us/en/assets/7018-03568/technical-overviews/5991-0779.pdf
- **Not located / not confirmed:** exact numeric ranges for pulse width, PRI, chirp deviation, and rise/fall time; and the precise feature allocation between Option 205 and Option 206. These should be verified against the N7620A user guide / online help before being treated as hard specifications.

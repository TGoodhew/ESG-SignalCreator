# E4438C-250…259 — Waveform License 50-Packs — Requirements

> Source category: **PC-based signal creation software**, from the Agilent E4438C ESG Vector Signal Generator Data Sheet (literature no. 5988-4039EN).
> Purpose: capture this item's role as candidate requirements for the ESG-SignalCreator app (a modern reimplementation of Signal Studio for the E4438C).

## 1. Product identity
- **Model / option number:** E4438C-250, -251, -252, -253, -254, -255, -256, -257, -258, -259 (ten distinct option numbers; the equivalent N5182A/N5162A MXG options carry the same 250–259 numbering).
- **Product name:** Waveform License 50-Pack (Signal Studio waveform licensing, 50-pack family).
- **Host instrument(s):** Agilent/Keysight E4438C ESG Vector Signal Generator (also offered on the MXG N5182A/N5162A generation).
- **Status:** Discontinued. The E4438C and its 50-pack waveform-license options are legacy.

## 2. Overview
A "waveform license" is a permanent, per-instrument right-to-use that authorizes one Signal Studio ARB waveform to be played back on a specific signal generator without buying the full Signal Studio software package for that format. The 50-pack options (250–259) each add **fifty** waveform slots to the instrument — the high-volume counterpart to the 5-pack family. Ordering all ten 50-pack options provides up to **500** waveform slots on a single E4438C. Each licensed waveform is locked into a slot and, once committed, is permanent (non-revocable, non-transferable).

## 3. Standards & formats supported
- Format-agnostic at the licensing layer: a 50-pack slot can license a waveform produced by any Arb-based Signal Studio waveform-creation product (the N76xxB / later N76xxC Signal Studio generations), across the air-interface formats those products create (e.g. W-CDMA/HSPA, LTE, WLAN, GSM/EDGE, custom digital modulation, multitone/noise, etc.).
- The license carries no waveform format itself; it unlocks whichever Signal Studio-generated ARB waveform the user assigns to a slot.
- Each candidate waveform is offered under a **48-hour trial** (requires E4438C firmware C.05.23 or later, Feb 2009) during which the waveform can be played back, cleared, or replaced in the slot before permanent commit. On firmware without trial support, assignment is immediately permanent.

## 4. Key capabilities / features
- Adds fifty permanent waveform slots per option ordered (250–259).
- Per-waveform licensing decoupled from per-format Signal Studio software ownership — economical for users licensing many waveforms.
- Permanent, node-locked (per-instrument) license once a slot is committed.
- 48-hour evaluation/trial window per waveform on supported firmware, allowing play/clear/replace before commit.
- Ten independent option numbers so multiple 50-packs can be stacked on one instrument (up to 500 slots), provided each option number is used only once per instrument.
- Uses the instrument's existing ARB baseband generator for playback; the license unlocks playback rights rather than adding hardware.

## 5. Configurable signal parameters
- **License count / granularity:** 50 waveforms per option; 500 maximum across all ten 50-pack options.
- **Binding:** per-instrument (node-locked). A license is tied to one signal generator's host/serial identity and cannot be moved to another instrument (not portable/transportable).
- **Consumption model:** each slot holds exactly one waveform. During the trial window the slot is reusable (play/clear/replace); on commit the slot is consumed permanently and cannot be revoked, exchanged, or reassigned.
- **Ordering constraint:** the same option number cannot be licensed to a given signal generator more than once — stacking requires distinct option numbers (250 through 259).
- **Fixed vs. portable:** these are **fixed** (permanent, per-instrument) licenses; they are not floating, not transportable, and not time-limited (beyond the initial trial window).
- **5-pack vs. 50-pack difference:** identical mechanism; the only difference is granularity — 5 slots per option (221–229) versus 50 slots per option (250–259), and the per-instrument maximum (45 vs. 500).

## 6. Candidate requirements for ESG-SignalCreator
- **R-1:** The app SHALL, as its baseline, generate and play plain (unlicensed) ARB waveforms; reproducing Agilent's per-instrument waveform-license enforcement is not required for a functional reimplementation, since licensing was a commercial gate rather than a signal-generation feature.
- **R-2:** If the app targets real E4438C hardware with committed 50-pack slots, it SHOULD query and display used/available waveform-license slot counts (per option 250–259) so users understand remaining capacity, without creating or forging licenses.
- **R-3:** The app SHALL NOT attempt to bypass, forge, or replicate Agilent/Keysight license keys; licensing-aware behavior is limited to read-only reporting of existing instrument state.
- **R-4:** If the app maintains its own waveform library, it SHOULD model a reusable "slot" abstraction for workflow parity with the ESG, but SHOULD treat slots as freely reusable (no permanent commit), reflecting that the reimplementation carries no external IP licensing obligation.
- **R-5:** The app SHOULD treat 5-pack and 50-pack licensing uniformly in any capacity-reporting UI (a single "waveform license slots used / available" model), since the two families differ only in per-option granularity.

## 7. Dependencies, licensing & notes
- **Hardware/firmware:** requires an E4438C with the internal baseband generator / ARB memory to play the waveforms; the 48-hour trial capability requires firmware C.05.23 or later.
- **Licensing model:** permanent, node-locked, per-instrument, non-revocable, non-transferable. Not floating and not portable between instruments.
- **External IP:** the waveforms are Keysight validated/performance-optimized ARB files produced by Signal Studio (N76xxB/C). A clean-room reimplementation that generates its own ARB data does not inherit these licenses and should not embed or redistribute Keysight-licensed waveform content.
- **Relationship to 5-packs:** same scheme at 50-waveform granularity; see the 5-pack family (options 221–229) for the lower-volume variant.

## 8. References
- Keysight, "Waveform Licensing 5-Pack and 50-Pack" (CSG Signal Studio help) — https://helpfiles.keysight.com/csg/n7601/Content/Common/Licensing/waveform_licensing.htm
- Keysight, "Waveform Licensing" (CSG help, N7616B/N7610 variants — same scheme) — https://helpfiles.keysight.com/csg/n7610/Content/Common/Licensing/waveform_licensing.htm
- Keysight, "N5182A-221 Waveform License 5-Pack [Discontinued]" (product/status page confirming the 5-pack/50-pack family and discontinued status) — https://www.keysight.com/en/pd-1247244/waveform-license-5-pack?cc=US&lc=eng
- Agilent, "User's Guide — E4428C/38C ESG Signal Generators" (Waveform Licensing sections) — https://www.keysight.com/co/en/assets/9018-01484/user-manuals/9018-01484.pdf
- Agilent E4438C ESG Vector Signal Generator Data Sheet — literature no. 5988-4039EN — https://www.keysight.com/us/en/assets/7018-01039/data-sheets-archived/5988-4039.pdf
- Note: an explicit standalone Keysight page enumerating each 50-pack option number 250–259 was not individually located; the 250–259 range, 50-waveforms-per-option granularity, and 500-waveform maximum are confirmed from the CSG waveform-licensing help file and the E4428C/38C user guide. The "Creating and Downloading Waveform Files" PDF (Keysight S3 host) returned HTTP 403 and is therefore not cited with a literature number.

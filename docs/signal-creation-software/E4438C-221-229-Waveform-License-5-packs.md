# E4438C-221…229 — Waveform License 5-Packs — Requirements

> Source category: **PC-based signal creation software**, from the Agilent E4438C ESG Vector Signal Generator Data Sheet (literature no. 5988-4039EN).
> Purpose: capture this item's role as candidate requirements for the ESG-SignalCreator app (a modern reimplementation of Signal Studio for the E4438C).

## 1. Product identity
- **Model / option number:** E4438C-221, -222, -223, -224, -225, -226, -227, -228, -229 (nine distinct option numbers; the equivalent N5182A/N5162A MXG options carry the same 221–229 numbering).
- **Product name:** Waveform License 5-Pack (Signal Studio waveform licensing, 5-pack family).
- **Host instrument(s):** Agilent/Keysight E4438C ESG Vector Signal Generator (also offered on the MXG N5182A/N5162A generation).
- **Status:** Discontinued. The E4438C and its 5-pack waveform-license options are legacy; Keysight lists the corresponding MXG 5-pack option as discontinued.

## 2. Overview
A "waveform license" is a permanent right-to-use, locked to one specific signal generator, that authorizes a single Signal Studio ARB waveform to be played back on that instrument without owning the full Signal Studio software package for that format. The 5-pack options (221–229) each add **five** waveform slots to the instrument; a licensed waveform is bound into one slot and, once locked, is permanent (non-revocable, non-transferable). Ordering all nine 5-pack options provides 45 waveform slots. This lets a user pay per waveform rather than per Signal Studio format license.

## 3. Standards & formats supported
- Format-agnostic at the licensing layer: a 5-pack slot can license a waveform produced by any Arb-based Signal Studio waveform-creation product (the N76xxB / later N76xxC Signal Studio generations), covering the same air-interface formats those products create (e.g. W-CDMA/HSPA, LTE, WLAN, GSM/EDGE, custom digital modulation, tone/noise, etc.).
- The license does not itself carry a waveform format; it unlocks whichever Signal Studio-generated ARB waveform the user assigns to the slot.
- Each candidate waveform is offered under a **48-hour trial** (requires E4438C firmware C.05.23 or later, Feb 2009) during which the waveform can be played back, cleared, or replaced in the slot before the license is permanently committed. On firmware without trial support, assignment is immediately permanent.

## 4. Key capabilities / features
- Adds five permanent waveform slots per option ordered (221–229).
- Per-waveform licensing decoupled from per-format Signal Studio software ownership.
- Permanent, node-locked (per-instrument) license once a slot is committed.
- 48-hour evaluation/trial window per waveform on supported firmware, allowing play/clear/replace before commit.
- Nine independent option numbers so multiple 5-packs can be stacked on one instrument (up to 45 slots), provided each option number is used only once per instrument.
- Complements — does not replace — the instrument's ARB baseband generator; licensed waveforms still play through the E4438C internal baseband/ARB hardware.

## 5. Configurable signal parameters
- **License count / granularity:** 5 waveforms per option; 45 maximum across all nine 5-pack options.
- **Binding:** per-instrument (node-locked). A license is tied to one signal generator's host/serial identity and cannot be moved to another instrument (not portable/transportable).
- **Consumption model:** each slot holds exactly one waveform. During the trial window the slot is reusable (play/clear/replace); on commit the slot is consumed permanently and cannot be revoked, exchanged, or reassigned.
- **Ordering constraint:** the same option number cannot be licensed to a given signal generator more than once — stacking requires distinct option numbers (221 through 229).
- **Fixed vs. portable:** these are **fixed** (permanent, per-instrument) licenses; they are not floating, not transportable, and not time-limited (beyond the initial trial window).

## 6. Candidate requirements for ESG-SignalCreator
- **R-1:** The app SHALL, as its baseline, generate and play plain (unlicensed) ARB waveforms; it does not need to reproduce Agilent's per-instrument waveform-license enforcement to be useful, because the licensing scheme was a commercial gating mechanism, not a signal-generation feature.
- **R-2:** If the app targets real E4438C hardware that has committed waveform-license slots, it SHOULD be able to query and display the instrument's used/available waveform-license slot counts (per option 221–229) so a user understands remaining capacity, without attempting to create or forge licenses.
- **R-3:** The app SHALL NOT attempt to bypass, forge, or replicate Agilent/Keysight license keys; any licensing-aware behavior is limited to read-only reporting of existing instrument state.
- **R-4:** Where the app maintains its own waveform library, it SHOULD model a "slot" abstraction (named waveform assigned to a play position) for parity with the ESG workflow, but SHOULD treat slots as freely reusable (no permanent commit), reflecting that the reimplementation carries no external IP licensing obligation.
- **R-5:** The app MAY surface an informational note when a user selects a legacy Signal Studio-origin waveform, explaining that on original hardware such a waveform would have consumed a permanent 5-pack license slot.

## 7. Dependencies, licensing & notes
- **Hardware/firmware:** requires an E4438C with the internal baseband generator / ARB memory to play the waveforms; the 48-hour trial capability requires firmware C.05.23 or later.
- **Licensing model:** permanent, node-locked, per-instrument, non-revocable, non-transferable. Not floating and not portable between instruments.
- **External IP:** the waveforms themselves are Keysight validated/performance-optimized ARB files produced by Signal Studio (N76xxB/C). A clean-room reimplementation that generates its own ARB data does not inherit these licenses and should not embed or redistribute Keysight-licensed waveform content.
- **Relationship to 50-packs:** functionally identical scheme at 5-waveform granularity; see the 50-pack family (options 250–259) for the 50-waveform-per-option variant intended for higher-volume users.

## 8. References
- Keysight, "Waveform Licensing" (CSG Signal Studio help — 5-pack and 50-pack) — https://helpfiles.keysight.com/csg/n7601/Content/Common/Licensing/waveform_licensing.htm
- Keysight, "Waveform Licensing 5-Pack and 50-Pack" (CSG help mirror) — https://helpfiles.keysight.com/csg/n7601/Content/Common/Licensing/waveform_licensing.htm
- Keysight, "N5182A-221 Waveform License 5-Pack [Discontinued]" (product/status page; MXG equivalent of the E4438C-221 option) — https://www.keysight.com/en/pd-1247244/waveform-license-5-pack?cc=US&lc=eng
- Agilent, "User's Guide — E4428C/38C ESG Signal Generators" (section: Using Waveform 5-Pack Licensing, Options 221–229) — https://www.keysight.com/co/en/assets/9018-01484/user-manuals/9018-01484.pdf
- Agilent E4438C ESG Vector Signal Generator Data Sheet — literature no. 5988-4039EN — https://www.keysight.com/us/en/assets/7018-01039/data-sheets-archived/5988-4039.pdf
- Note: the ManualsLib copy of the E4428C User Guide page 137 ("Using Waveform 5-Pack Licensing") returned HTTP 410 during this pass; the equivalent content was verified from the Keysight-hosted user guide and the CSG waveform-licensing help file above. Literature/part number for the standalone "Creating and Downloading Waveform Files" guide could not be re-fetched (host returned 403) and is therefore not cited.

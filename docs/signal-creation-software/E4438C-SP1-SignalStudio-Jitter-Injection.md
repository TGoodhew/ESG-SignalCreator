# E4438C-SP1 — Signal Studio for Jitter Injection — Requirements

> Source category: **PC-based signal creation software**, from the Agilent E4438C ESG Vector Signal Generator Data Sheet (literature no. 5988-4039EN).
> Purpose: capture this product's capabilities as candidate requirements for the ESG-SignalCreator app (a modern reimplementation of Signal Studio for the E4438C).

> 🟢 **Implementation status (v1):** A **Jitter Injection** personality now ships in the app
> (`Core/Personalities/Jitter/`). It generates a jittered clock/tone via timing (phase) modulation:
> periodic jitter (**Sinusoidal / Square / Triangle / SawTooth / Exponential**) at a configurable rate
> and UI peak-to-peak amplitude, optional Gaussian **random** jitter (UI RMS + seed), and their
> **composite** — covering **R-1**, **R-2** (custom shape deferred), **R-3**, **R-4**, **R-5**, and
> **R-7** (digital repeatability via the seed). **Deferred:** predefined standards masks (ITU-T G.8251
> OC-48/192/768, **R-6**), automated SJ frequency sweeps (**R-8**), and the achievable-range enforcement
> for ESG hardware (**R-10**, which the doc flags as unverified). Waveform save (**R-9**) is via the
> existing project save. Hardware verification is tracked in the epic.

## 1. Product identity
- **Model / option number:** E4438C-SP1 (license-key option; upgrade order number E4438CK-SP1)
- **Product name:** Signal Studio for Jitter Injection
- **Host instrument(s):** Agilent E4438C ESG Vector Signal Generator. The same Signal Studio for Jitter Injection product also shipped for the Agilent E8267C PSG (option E8267C-SP1); the ESG variant is E4438C-SP1.
- **Status:** Discontinued/legacy. The corresponding E8267C-SP1 variant is marked "Obsolete — no replacement" on keysight.com; the ESG E4438C-SP1 is likewise a legacy option.

## 2. Overview
Signal Studio for Jitter Injection is a PC-hosted application that creates standards-based or custom jitter impairments — periodic jitter, random jitter, or a composite of both — and plays them out through the E4438C ESG as a jittered clock or serial signal. Its purpose is jitter-tolerance and jitter-compliance testing of high-speed digital receivers, clock-recovery circuits, and serial links, where the device under test must be exercised with controlled, repeatable amounts of injected jitter. It generates the jittered signal via the ESG's internal baseband generator and RF/vector output.

## 3. Standards & formats supported
- Standards-based jitter profiles for serial-data compliance testing. Documented predefined masks for the Signal Studio for Jitter Injection family include ITU-T G.8251 (OC-48, OC-192, OC-768) SDH/SONET jitter templates.
- Applicable to serial-interface tolerance testing such as (per the product family) CEI, XFP/XFI, Fibre Channel, Gigabit Ethernet, PCI Express, and Serial ATA.
- Custom / user-defined jitter profiles for non-standard or research use.
- Note: several published numeric limits and the specific standards list above are documented for the E8267C PSG variant of the same product; the E4438C ESG variant is constrained by the ESG's narrower baseband/RF modulation bandwidth (see §7).

## 4. Key capabilities / features
- Injects **periodic jitter** with selectable waveform shapes: sinusoidal, square, triangle, saw-tooth, exponential, and custom.
- Injects **random jitter** with user-configurable standard deviation (RMS) and noise seed for repeatable pseudo-random sequences.
- Combines random and periodic jitter to build **composite** jittered clock/serial signals.
- Produces digitally repeatable jitter so tolerance measurements can be reproduced exactly (same seed → same sequence).
- Stores generated waveforms as files for automated/ATE test flows.
- Remote control and file transfer over LAN and GPIB.
- Sinusoidal jitter (SJ) sweeps for building jitter-tolerance masks vs. jitter frequency.

## 5. Configurable signal parameters
- **Jitter type:** periodic, random, or composite (periodic + random).
- **Periodic-jitter shape:** sinusoidal / square / triangle / saw-tooth / exponential / custom.
- **Periodic-jitter frequency (jitter rate):** user-set (family spec: custom rates/deviation up to 20 MHz at 0.15 UI peak-to-peak on the PSG variant).
- **Jitter amplitude / deviation:** in unit intervals (UI) peak-to-peak (periodic) and RMS (random), i.e. jitter magnitude expressed relative to the bit/clock period.
- **Random-jitter standard deviation and noise seed:** configurable for Gaussian random jitter with repeatable seeds.
- **Underlying carrier / clock rate:** the data or clock rate of the signal being jittered (constrained by the ESG baseband/RF capability).
- **Predefined standard mask selection** (e.g. ITU-T G.8251 OC-48/OC-192/OC-768) where applicable.
- **Output level / RF frequency** via the ESG.

## 6. Candidate requirements for ESG-SignalCreator
- **R-1:** The app SHALL provide a Jitter Injection mode that produces a jittered clock or serial-data signal for receiver jitter-tolerance testing.
- **R-2:** The app SHALL support periodic jitter with selectable shapes: sinusoidal, square, triangle, saw-tooth, exponential, and custom/user-defined.
- **R-3:** The app SHALL support random jitter with configurable RMS standard deviation and a settable noise seed for repeatable pseudo-random sequences.
- **R-4:** The app SHALL support composite jitter (periodic + random combined) in a single output.
- **R-5:** The app SHALL express jitter amplitude in unit intervals (UI) — peak-to-peak for periodic and RMS for random — and let the user set periodic jitter frequency (jitter rate).
- **R-6:** The app SHALL provide predefined standard jitter profiles/masks (at minimum ITU-T G.8251 OC-48/OC-192/OC-768) and allow user-defined custom profiles.
- **R-7:** The app SHALL guarantee digital repeatability: identical settings and seed produce an identical jitter sequence.
- **R-8:** The app SHALL support a sinusoidal-jitter frequency sweep to construct jitter-tolerance masks.
- **R-9:** The app SHALL allow saving generated jittered waveforms to file for automated test reuse.
- **R-10:** The app SHOULD document and enforce the achievable jitter range (max deviation, max jitter rate, max carrier/clock rate) for the target hardware, since these are bounded by the baseband generator and RF modulation bandwidth.

## 7. Dependencies, licensing & notes
- **Prerequisite hardware option:** Requires E4438C internal baseband generator option **601** (8 MSa memory) **or 602** (64 MSa memory), each with digital-bus capability. Jitter injection is generated through this baseband hardware.
- **Licensing:** Delivered as a license-key option (E4438CK-SP1 for field upgrade); requires an activation license installed on the E4438C.
- **Control interfaces:** LAN (10BaseT) and GPIB.
- **Platform-bandwidth caveat:** Published high-end numbers for this product — e.g. clock rates to 20 Gb/s, 80 MHz instantaneous bandwidth, random jitter to 1×10⁻¹² via source mixing — belong to the **E8267C PSG** implementation of Signal Studio for Jitter Injection. The **E4438C ESG** has substantially narrower modulation bandwidth, so the ESG variant's maximum clock/data rate and jitter deviation are lower. The exact ESG limits were not found in a primary datasheet during research and MUST be verified before being used as firm requirements.
- **External IP / standards note:** Standard jitter masks reference ITU-T (G.8251) and various serial-interface compliance specs (PCIe, SATA, Fibre Channel, etc.); authoritative parameters live in those standards documents.

## 8. References
- E8267C-SP1 Signal Studio for Jitter Injection (Obsolete) — Keysight product page (jitter types: sinusoidal/square/triangle/saw-tooth/exponential/custom periodic; random with std-dev and seed; composite; ITU-T G.8251 OC-48/192/768; serial-standard use cases) — https://www.keysight.com/us/en/product/E8267C-SP1/signal-studio-for-jitter-injection.html  (Note: this is the E8267C PSG variant; used as the closest primary description of the same product family, since a dedicated E4438C-SP1 page was not located.)
- Agilent E4428C and E4438C ESG Signal Generators Configuration Guide (confirms option E4438C-SP1 "Signal Studio for jitter injection", license key, requires 601 or 602) — https://assets.testequity.com/te1/Documents/pdf/E4428C-E4438C-config.pdf
- Agilent E4438C ESG Vector Signal Generator Data Sheet — literature no. 5988-4039EN — https://www.keysight.com/us/en/assets/7018-01039/data-sheets-archived/5988-4039.pdf (source-category reference; option-level confirmation)
- **Not located / could not confirm:** A dedicated E4438C-SP1 product or datasheet page on keysight.com was not found during research. ESG-specific jitter limits (max clock/data rate, max deviation, bandwidth) were not confirmed from a primary Agilent/Keysight source — the numeric specifications cited in §3–§5 and flagged in §7 come from the E8267C PSG variant page and general Signal Studio jitter-injection descriptions, not from E4438C-specific literature. These MUST be validated before implementation.

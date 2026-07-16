# PC-based signal creation software — per-item requirements

This folder captures **candidate requirements** for each product listed under the
**"PC-based signal creation software"** heading in the *Agilent E4438C ESG Vector Signal
Generator Data Sheet* (literature no. **5988-4039EN**, ordering-information page). Each file
distils a single Signal Studio product (or licensing item) from public Keysight/Agilent
datasheets, technical overviews, user guides, and online help into a consistent 8-section
template, ending with numbered `R-x` requirements framed for the **ESG-SignalCreator** app.

> ⚠️ **These are research-derived drafts, not verified specs.** Each file's *References*
> section explicitly flags what could and could not be confirmed against primary literature.
> Several of these products are the later "B/C" software generation whose newest features
> (wide carrier aggregation, 8×8 MIMO, 256QAM, multi-GHz bandwidths) practically target
> newer hardware and **exceed the legacy E4438C's RF/ARB limits** — treat those as
> capability-gated when triaging into the app. See the tracking issue for the review workflow.

> ✅ **Implementation status (2026-07-16).** These docs are no longer just a backlog: **20 of the 21
> products have shipped** as app personalities across #158–#177 (each file's line-6 banner records its
> status and v2 increment). The lone exception is **E4438C-407 (S-DMB)**, which is **not implemented**
> — its unverified placeholder was removed (PR #222) because the physical layer needs a paid primary
> spec; that file documents the block. The two waveform-license items (E4438C-221-229 / -250-259) are
> deliberate "no app modelling" decisions. Bench verification of the shipped set is tracked in epic
> **#157** (with HIL-battery and tutorial-capture follow-ups #229 / #230).

## Index

### Cellular / wireless standards
| Model | Product | File |
|---|---|---|
| N7600B | Signal Studio for 3GPP W-CDMA FDD | [N7600B](N7600B-SignalStudio-3GPP-WCDMA-FDD.md) |
| E4438C-419 | Signal Studio for 3GPP W-CDMA HSPA | [E4438C-419](E4438C-419-SignalStudio-3GPP-WCDMA-HSPA.md) |
| N7601B | Signal Studio for 3GPP2 CDMA (cdma2000 / 1xEV-DO) | [N7601B](N7601B-SignalStudio-3GPP2-CDMA.md) |
| N7602B | Signal Studio for GSM/EDGE | [N7602B](N7602B-SignalStudio-GSM-EDGE.md) |
| N7612B | Signal Studio for TD-SCDMA | [N7612B](N7612B-SignalStudio-TD-SCDMA.md) |
| N7624B | Signal Studio for 3GPP LTE (FDD) | [N7624B](N7624B-SignalStudio-3GPP-LTE.md) |
| N7625B | Signal Studio for 3GPP LTE TDD | [N7625B](N7625B-SignalStudio-3GPP-LTE-TDD.md) |

### Short-range / WLAN / WiMAX
| Model | Product | File |
|---|---|---|
| N7606B | Signal Studio for Bluetooth | [N7606B](N7606B-SignalStudio-Bluetooth.md) |
| N7617B | Signal Studio for 802.11 WLAN | [N7617B](N7617B-SignalStudio-80211-WLAN.md) |
| N7613A | Signal Studio for 802.16-2004 (fixed WiMAX) | [N7613A](N7613A-SignalStudio-80216-2004-WiMAX.md) |
| N7615B | Signal Studio for 802.16 WiMAX (mobile) | [N7615B](N7615B-SignalStudio-80216-WiMAX.md) |

### Broadcast / video / DMB
| Model | Product | File |
|---|---|---|
| N7611B | Signal Studio for Broadcast Radio | [N7611B](N7611B-SignalStudio-Broadcast-Radio.md) |
| N7616B | Signal Studio for T-DMB | [N7616B](N7616B-SignalStudio-T-DMB.md) |
| N7623B | Signal Studio for Digital Video | [N7623B](N7623B-SignalStudio-Digital-Video.md) |
| E4438C-407 | Signal Studio for S-DMB | [E4438C-407](E4438C-407-SignalStudio-S-DMB.md) |

### General-purpose signal / test tools
| Model | Product | File |
|---|---|---|
| N7620A | Signal Studio for Pulse Building | [N7620A](N7620A-SignalStudio-Pulse-Building.md) |
| N7621B | Signal Studio for Multitone Distortion | [N7621B](N7621B-SignalStudio-Multitone-Distortion.md) |
| N7622A | Signal Studio Toolkit (custom I/Q download) | [N7622A](N7622A-SignalStudio-Toolkit.md) |
| E4438C-SP1 | Signal Studio for Jitter Injection | [E4438C-SP1](E4438C-SP1-SignalStudio-Jitter-Injection.md) |

### Waveform licensing (not signal generators)
| Model | Item | File |
|---|---|---|
| E4438C-221…229 | Waveform license 5-packs | [E4438C-221–229](E4438C-221-229-Waveform-License-5-packs.md) |
| E4438C-250…259 | Waveform license 50-packs | [E4438C-250–259](E4438C-250-259-Waveform-License-50-packs.md) |

## How these were produced

Each file follows the same template: product identity, overview, standards/formats,
key capabilities, configurable parameters, candidate `R-x` requirements for the app,
dependencies/licensing/notes, and references. Sources were gathered from public Keysight
literature; where a primary datasheet could not be retrieved, the file says so and marks
the affected details as unverified. Nothing here should be treated as a hard specification
until validated against the original user guide / online help.

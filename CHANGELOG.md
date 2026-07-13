# Changelog

All notable changes to this project are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and the project aims to follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- **Keysight N9010A (EXA) analyzer support — in progress** (epic #105). Delivered so far:
  - Analyzer **model abstraction**: `VsaModel` resolved from `*IDN?`, with an `IVsaDialect`
    SCPI-dialect strategy (E4406A and N9010A) so measurement code stays instrument-agnostic (#106).
  - Model-aware **control plane**: mode entry selects the right `:INSTrument:SELect` per measurement
    (E4406A `BASIC`; N9010A `BASIC` for Spectrum/Waveform, `SA` for Channel Power/ACP/CCDF), and
    `:INSTrument:CATalog?` parsing handles both the E4406A and X-Series response dialects (#107).
  - **VSA model toggle** in the UI: choose whether the app targets the E4406A or the N9010A; the
    connect dialog's title, interface defaults (GPIB@17 vs LAN/USB), resource hint, and identity
    guard follow the selection. The choice is persisted between sessions (#108).
  - **Per-model input-damage safety limit**: the RF-path safety gate is seeded with a max-safe-input
    default chosen from the selected model — E4406A +30 dBm; N9010A a conservative +25 dBm backstop
    (unconfirmed by the supplied manuals, flagged in the UI to confirm against the data sheet) (#109).

### Notes
- Still in progress for full N9010A verification: the SA-mode Channel Power / ACP / CCDF and IQ
  Analyzer Spectrum / Waveform measurement mappings (#110, #111), safety limits/addressing (#109),
  the headless harness/tests/assistant surface (#112), and the full docs/tutorials sweep (#113).

## [1.0.3] - 2026-07-13

### Fixed
- Release installer no longer falsely blocks with "requires .NET Framework 4.7.2" on machines where
  4.7.2 is already installed. The MSI launch condition now uses the WiX netfx extension's
  `WIX_IS_NETFRAMEWORK_472_OR_LATER_INSTALLED` property instead of a raw registry-DWORD comparison (#103).

## [1.0.2] - 2026-06-27

- Earlier packaged release (installer/bootstrapper, docs, HIL harness). See the Git history and the
  GitHub Releases page for details prior to the introduction of this changelog.

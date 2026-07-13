# Changelog

All notable changes to this project are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and the project aims to follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- **Install verification self-test** (#125): a **Verify install…** action runs a guided
  CW → AM → FM → I/Q multitone battery — synthesized as ARB I/Q, played through the ESG and measured on
  the connected analyzer (E4406A or N9010A) — showing expected-vs-measured per step with an overall
  PASS/FAIL in the Verification view. The input-damage safety gate is enforced before any RF. The
  reusable `InstallVerification` orchestrator lives in Core (to be shared by the automated harness, #126).
- **Headless install-verification** (#126): the HIL harness gains an `--install-verify` flag that runs the
  same CW/AM/FM/I/Q battery (via the shared `InstallVerification` orchestrator) on the **one** analyzer
  selected by `--vsa-model` (`e4406a` | `n9010a`), emitting a JSON report and a non-zero exit on failure.
  It targets a single analyzer per run — cover both by running it twice.

### Changed
- **N9010A auto-alignment handling** (#129): N9010A measurement reads now wait for completion via an SRQ
  (Status-Byte MAV) notification — re-arming short waits up to a long overall deadline — instead of a
  fixed read timeout, so a periodic auto-alignment that coincides with a measurement no longer fails it
  spuriously. Added an optional `ISupportsServiceRequest` transport capability (implemented by the VISA
  transport); the E4406A path is unchanged. Model-selected via the dialect.
- **Live capability binding** (core of #120): on ESG connect, the capability profile is now reconciled
  with the connected unit's `*IDN?` model, `*OPT?` installed options, and queried frequency range,
  instead of a static `E4438C` profile. The memory cap reflects only the **installed** baseband option
  (e.g. a 001/601 unit is 8.3M samples, not the model-max 67M), Calculate-time validation checks the
  **intended carrier** from the settings panel (not a range midpoint), and **ARB download is gated on
  the baseband-generator option** — an incapable unit fails fast with a clear message instead of a
  silent instrument-side rejection. Offline mode still uses the static profile.

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
  - **N9010A Spectrum & Waveform measurements**: the Spectrum marker workflow (span + peak-search
    markers) is identical SCPI across both analyzers and now works on the N9010A via the mode routing;
    the Waveform measurement reads peak/mean/peak-to-mean from model-specific scalar positions (the
    N9010A's peak is the Maximum at index 5, not index 0), driven by the dialect (#110).
  - **N9010A Channel Power / ACP / CCDF measurements** (SA mode, from 9018-06099): Channel Power is
    cross-model (same root/config/order); CCDF reads its scalars at a per-model result index (E4406A
    n=1, N9010A n=2 — PAPR still at [8]); ACP uses a per-model layout (E4406A 5 offsets with adjacent
    dBc in the header; N9010A `ACPower` with 6 offsets A–F and adjacent dBc from offset A). With these,
    the closed-loop **Verify** (channel power + PAPR + spectrum tone) works on the N9010A (#111).
  - **Headless harness + assistant** target either analyzer: the HIL harness takes a `--vsa-model`
    flag (`e4406a` | `n9010a`) that drives the identity guard, the per-model input-damage default, and
    the sweep ceiling; the assistant's measure_*/verify_signal tool text and system prompt are now
    model-neutral, and `get_vsa_state` reports the connected analyzer's model (#112).
  - **Documentation** generalized to dual-analyzer: README, UserGuide (§9), Tutorials (Part F), and the
    Danish `docs/da/` mirrors now cover E4406A **or** N9010A with the model toggle, per-model addressing
    and safety, and the harness `--vsa-model` flag (#113).

### Notes
- N9010A support (epic #105, issues #106–#113) is code-complete and unit-tested against the Keysight
  X-Series manuals, but is **not yet bench-validated** — confirm the ACP result layout and the max
  safe input against a physical N9010A. The E4406A path remains hardware-validated.

## [1.0.3] - 2026-07-13

### Fixed
- Release installer no longer falsely blocks with "requires .NET Framework 4.7.2" on machines where
  4.7.2 is already installed. The MSI launch condition now uses the WiX netfx extension's
  `WIX_IS_NETFRAMEWORK_472_OR_LATER_INSTALLED` property instead of a raw registry-DWORD comparison (#103).

## [1.0.2] - 2026-06-27

- Earlier packaged release (installer/bootstrapper, docs, HIL harness). See the Git history and the
  GitHub Releases page for details prior to the introduction of this changelog.

using System.Runtime.Serialization;

namespace EsgSignalCreator.Personalities.MultitoneDistortion
{
    /// <summary>
    /// Per-tone phase preset for a multitone-distortion / NPR stimulus. The preset trades composite
    /// peak-to-average power ratio (PAPR / crest factor) against simplicity, matching the N7621B
    /// "random / parabolic / constant" presets.
    /// </summary>
    public enum MultitonePhasePreset
    {
        /// <summary>Deterministic pseudo-random phases (low PAPR, seeded for reproducibility).</summary>
        Random,

        /// <summary>Parabolic (Newman/Schroeder quadratic) phasing — low, deterministic PAPR.</summary>
        Parabolic,

        /// <summary>All tones aligned at 0° (worst-case, highest PAPR).</summary>
        Constant
    }

    /// <summary>
    /// Serializable settings for <see cref="MultitoneDistortionPersonality"/> — a dense, equally-spaced
    /// multitone stimulus for intermodulation-distortion (IMD) and noise-power-ratio (NPR) testing,
    /// with an optional cleared notch.
    /// </summary>
    /// <remarks>
    /// Models Signal Studio for Multitone Distortion (N7621B): the equally-spaced comb + phase preset +
    /// NPR notch, plus (v2, #180) per-tone magnitude/phase tables and in-band pre-distortion correction.
    /// The pre-distortion here <em>applies</em> a measured per-tone error (the inverse channel response) —
    /// the measurement itself comes from a signal analyzer (the closed-loop Verify path / an external
    /// tool), not from this personality. Out-of-band IMD-cancellation (correction tones inside the NPR
    /// notch) remains a follow-up on #180.
    /// </remarks>
    [DataContract]
    public sealed class MultitoneDistortionConfig
    {
        /// <summary>I/Q sample (playback clock) rate, in hertz.</summary>
        [DataMember] public double SampleRateHz { get; set; } = 40e6;

        /// <summary>Number of complex samples to generate.</summary>
        [DataMember] public int Length { get; set; } = 32768;

        /// <summary>Number of equally-spaced tones in the comb (2 up to 4097).</summary>
        [DataMember] public int ToneCount { get; set; } = 64;

        /// <summary>Spacing between adjacent tones, in hertz. Noise bandwidth ≈ ToneCount × spacing.</summary>
        [DataMember] public double ToneSpacingHz { get; set; } = 100e3;

        /// <summary>Frequency offset of the comb centre from baseband centre, in hertz.</summary>
        [DataMember] public double CenterOffsetHz { get; set; } = 0.0;

        /// <summary>Relative power assigned to every tone, in dB (the uniform default when no
        /// <see cref="PerToneMagnitudeDb"/> table is supplied).</summary>
        [DataMember] public double PowerDbPerTone { get; set; } = 0.0;

        /// <summary>Optional per-tone magnitude table, in dB, applied cyclically across the comb
        /// (tone <c>k</c> uses element <c>k mod length</c>) and overriding <see cref="PowerDbPerTone"/>.
        /// Null/empty = uniform power. (N7621B R-4.)</summary>
        [DataMember] public double[] PerToneMagnitudeDb { get; set; }

        /// <summary>Optional per-tone phase table, in degrees, applied cyclically across the comb.
        /// When non-empty the phase preset is overridden with these explicit phases. Null/empty = use
        /// <see cref="Phase"/>. (N7621B R-4.)</summary>
        [DataMember] public double[] PerTonePhaseDeg { get; set; }

        /// <summary>Per-tone phase preset (drives composite PAPR / crest factor) — used when no
        /// <see cref="PerTonePhaseDeg"/> table is supplied.</summary>
        [DataMember] public MultitonePhasePreset Phase { get; set; } = MultitonePhasePreset.Parabolic;

        /// <summary>Seed for <see cref="MultitonePhasePreset.Random"/> so results are reproducible.</summary>
        [DataMember] public int RandomSeed { get; set; } = 12345;

        /// <summary>When true, clear a notch of tones to create an NPR stimulus.</summary>
        [DataMember] public bool NotchEnabled { get; set; } = false;

        /// <summary>NPR notch width, in hertz (tones whose frequency falls inside are removed).</summary>
        [DataMember] public double NotchWidthHz { get; set; } = 1e6;

        /// <summary>NPR notch centre offset from the comb centre, in hertz.</summary>
        [DataMember] public double NotchOffsetHz { get; set; } = 0.0;

        /// <summary>When true, apply in-band pre-distortion: subtract the measured per-tone magnitude/phase
        /// error (see below) from the base per-tone values, pre-inverting the measured channel response so
        /// the emitted comb lands flat (or on target). (N7621B R-7.)</summary>
        [DataMember] public bool PredistortionEnabled { get; set; } = false;

        /// <summary>Measured per-tone magnitude error, in dB (analyzer reading minus target), applied
        /// cyclically. When <see cref="PredistortionEnabled"/>, this is subtracted from each tone's
        /// magnitude. Null/empty = no magnitude correction.</summary>
        [DataMember] public double[] MeasuredToneMagnitudeErrorDb { get; set; }

        /// <summary>Measured per-tone phase error, in degrees, applied cyclically. When
        /// <see cref="PredistortionEnabled"/>, this is subtracted from each tone's phase (forcing the
        /// manual per-tone phase path). Null/empty = no phase correction.</summary>
        [DataMember] public double[] MeasuredTonePhaseErrorDeg { get; set; }
    }
}

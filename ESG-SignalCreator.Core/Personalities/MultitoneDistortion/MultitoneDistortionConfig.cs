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
    /// Models the core of Signal Studio for Multitone Distortion (N7621B). Spectrum-analyzer-assisted
    /// pre-distortion correction (N7621B R-7) is out of scope for this v1; it needs a measure-and-correct
    /// loop against a signal analyzer and is tracked as a follow-up.
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

        /// <summary>Relative power assigned to every tone, in dB.</summary>
        [DataMember] public double PowerDbPerTone { get; set; } = 0.0;

        /// <summary>Per-tone phase preset (drives composite PAPR / crest factor).</summary>
        [DataMember] public MultitonePhasePreset Phase { get; set; } = MultitonePhasePreset.Parabolic;

        /// <summary>Seed for <see cref="MultitonePhasePreset.Random"/> so results are reproducible.</summary>
        [DataMember] public int RandomSeed { get; set; } = 12345;

        /// <summary>When true, clear a notch of tones to create an NPR stimulus.</summary>
        [DataMember] public bool NotchEnabled { get; set; } = false;

        /// <summary>NPR notch width, in hertz (tones whose frequency falls inside are removed).</summary>
        [DataMember] public double NotchWidthHz { get; set; } = 1e6;

        /// <summary>NPR notch centre offset from the comb centre, in hertz.</summary>
        [DataMember] public double NotchOffsetHz { get; set; } = 0.0;
    }
}
